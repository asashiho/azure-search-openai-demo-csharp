// Copyright (c) Microsoft. All rights reserved.

namespace MinimalApi.Services;

public class ReadRetrieveReadChatService
{
    private readonly SearchClient _searchClient;
    private readonly AzureOpenAIChatCompletionService _completionService;
    private readonly IKernel _kernel;
    private readonly IConfiguration _configuration;

    private const string FollowUpQuestionsPrompt = """
        質問に答えた後、ユーザーが次に尋ねそうな3つの簡単なフォローアップ質問も作成します。
        質問を参照するには、二重の角括弧を使用します（例：<<水素式燃料電池駆動電車とはなにですか?>>）
        すでに聞かれた質問を繰り返さないようにしましょう。
        質問のみを生成し、「次の質問」のような質問の前後にテキストを生成しないでください。
        """;

    private const string AnswerPromptTemplate = """
        <|im_start|>system
        あなたは鉄道技術に関する質問をサポートする教師アシスタントで、水素ハイブリット電車の技術に関する質問をサポートしています。回答は簡潔にしてください。
        以下の出典リストに記載されている事実のみを答えます。以下の情報が十分でない場合は、わからないと答えましょう。以下の出典を使用しない解答は作成しないでください。
        {{$follow_up_questions_prompt}}
        表形式の情報については、HTMLテーブルとして返してください。マークダウン形式は返さないでください。
        各出典元には、名前の後にコロンと実際の情報が続きます。回答で使用する事実については、「必ず」出典元を参照してください。出典を参照するには、四角いブラケットを使用します。なお各出典は別々に記載してください。
        {{$injected_prompt}}

        例:
        ### 例1 (出典を含む) ###
        リンゴは果物である[reference1.pdf]。
        ### 例2 (複数の出典を含む) ###
        リンゴは果物である[reference1.pdf][reference2.pdf]。
        ### 例3 (出典を記載し、二重角括弧を使用して質問を参照している) ###
        マイクロソフトはソフトウエア企業である[reference1.pdf].  <<followup question 1>> <<followup question 2>> <<followup question 3>>
        ### END ###
        Sources:
        {{$sources}}

        Chat history:
        {{$chat_history}}
        <|im_end|>
        <|im_start|>user
        {{$question}}
        <|im_end|>
        <|im_start|>assistant
        """;

    public ReadRetrieveReadChatService(
        SearchClient searchClient,
        AzureOpenAIChatCompletionService completionService,
        IConfiguration configuration)
    {
        _searchClient = searchClient;
        _completionService = completionService;
        var deployedModelName = configuration["AzureOpenAiChatGptDeployment"];
        var kernel = Kernel.Builder.Build();
        kernel.Config.AddTextCompletionService(deployedModelName!, _ => completionService);
        _kernel = kernel;
        _configuration = configuration;
    }

    public async Task<ApproachResponse> ReplyAsync(
        ChatTurn[] history,
        RequestOverrides? overrides,
        CancellationToken cancellationToken = default)
    {
        var top = overrides?.Top ?? 3;
        var useSemanticCaptions = overrides?.SemanticCaptions ?? false;
        var useSemanticRanker = overrides?.SemanticRanker ?? false;
        var excludeCategory = overrides?.ExcludeCategory ?? null;
        var filter = excludeCategory is null ? null : $"category ne '{excludeCategory}'";

        // step 1
        // use llm to get query
        var queryFunction = CreateQueryPromptFunction(history);
        var context = new ContextVariables();
        var historyText = history.GetChatHistoryAsText(includeLastTurn: true);
        context["chat_history"] = historyText;
        if (history.LastOrDefault()?.User is { } userQuestion)
        {
            context["question"] = userQuestion;
        }
        else
        {
            throw new InvalidOperationException("Use question is null");
        }

        var query = await _kernel.RunAsync(context, cancellationToken, queryFunction);
        // step 2
        // use query to search related docs
        var documentContents = await _searchClient.QueryDocumentsAsync(query.Result, overrides, cancellationToken);

        // step 3
        // use llm to get answer
        var answerContext = new ContextVariables();
        ISKFunction answerFunction;
        string prompt;
        answerContext["chat_history"] = history.GetChatHistoryAsText();
        answerContext["sources"] = documentContents;
        if (overrides?.SuggestFollowupQuestions is true)
        {
            answerContext["follow_up_questions_prompt"] = ReadRetrieveReadChatService.FollowUpQuestionsPrompt;
        }
        else
        {
            answerContext["follow_up_questions_prompt"] = string.Empty;
        }

        if (overrides is null or { PromptTemplate: null })
        {
            answerContext["$injected_prompt"] = string.Empty;
            answerFunction = CreateAnswerPromptFunction(ReadRetrieveReadChatService.AnswerPromptTemplate, overrides);
            prompt = ReadRetrieveReadChatService.AnswerPromptTemplate;
        }
        else if (overrides is not null && overrides.PromptTemplate.StartsWith(">>>"))
        {
            answerContext["$injected_prompt"] = overrides.PromptTemplate[3..];
            answerFunction = CreateAnswerPromptFunction(ReadRetrieveReadChatService.AnswerPromptTemplate, overrides);
            prompt = ReadRetrieveReadChatService.AnswerPromptTemplate;
        }
        else if (overrides?.PromptTemplate is string promptTemplate)
        {
            answerFunction = CreateAnswerPromptFunction(promptTemplate, overrides);
            prompt = promptTemplate;
        }
        else
        {
            throw new InvalidOperationException("Failed to get search result");
        }

        var ans = await _kernel.RunAsync(answerContext, cancellationToken, answerFunction);
        prompt = await _kernel.PromptTemplateEngine.RenderAsync(prompt, ans);
        return new ApproachResponse(
            DataPoints: documentContents.Split('\r'),
            Answer: ans.Result,
            Thoughts: $"Searched for:<br>{query}<br><br>Prompt:<br>{prompt.Replace("\n", "<br>")}",
            CitationBaseUrl: _configuration.ToCitationBaseUrl());
    }

    private ISKFunction CreateQueryPromptFunction(ChatTurn[] history)
    {
        var queryPromptTemplate = """
            <|im_start|>system
            チャット履歴:
            {{$chat_history}}
            
            良い検索クエリの例:
            ### 良い例 その1 ###
            水素ハイブリット電車 AND 燃料電池
            ### 良い例 その2 ###
            水素供給ステーション AND 輸送 AND 貯蔵
            ###

            <|im_end|>
            <|im_start|>system
            フォローアップ質問の検索クエリを生成します。文脈情報のためにチャット履歴を参照することができます。検索クエリを返すだけで、他の情報は含まれません。
            {{$question}}
            <|im_end|>
            <|im_start|>assistant
            """;

        return _kernel.CreateSemanticFunction(queryPromptTemplate,
            temperature: 0,
            maxTokens: 32,
            stopSequences: new[] { "<|im_end|>" });
    }

    private ISKFunction CreateAnswerPromptFunction(string answerTemplate, RequestOverrides? overrides) =>
        _kernel.CreateSemanticFunction(answerTemplate,
            temperature: overrides?.Temperature ?? 0.7,
            maxTokens: 1024,
            stopSequences: new[] { "<|im_end|>" });
}
