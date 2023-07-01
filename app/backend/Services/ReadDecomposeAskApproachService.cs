// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Planning.Planners;

namespace MinimalApi.Services;

internal sealed class ReadDecomposeAskApproachService : IApproachBasedService
{
    private readonly SearchClient _searchClient;
    private readonly ILogger<ReadDecomposeAskApproachService> _logger;
    private readonly IConfiguration _configuration;
    private readonly AzureOpenAITextCompletionService _completionService;

    private const string AnswerPromptPrefix = """
        与えられたナレッジのみを使って質問に答えてください。表形式の情報については、HTMLテーブルとして返してください。マークダウン形式で返さないでください。各ナレッジにはソース名の後にコロンと実際の情報が続きます。
        ただしナレッジリストにない情報を引用しないこととします。
        ナレッジリストだけでは答えられない場合は、「わからない」と答えてください。

        ### 例
        Question: '水素ハイブリッド電車とはなんですか?'

        Knowledge:
        info1.txt: 水素をエネルギー源とする燃料電池は、高いエネルギー変換効率と環境負荷の少なさが特徴
        info2.txt: 燃料電池自動車やバスの技術を鉄道車両の技術と融合・応用することにより、水素ハイブリッド電車を開発し、実証試験を始めた。

        Answer:
        水素を燃料とする燃料電池は、高いエネルギー変換効率と環境負荷の少なさが特徴[info1.txt]です。この技術を鉄道車両に応用し、水素ハイブリッド電車を開発し、実証試験を始めました。[info2.txt] 

        Question: 'Azureの特徴を教えてください'

        Knowledge:

        Answer:
        分かりません
        ###
        Knowledge:
        {{$knowledge}}

        Question:
        {{$question}}

        Answer:
        """;

    private const string CheckAnswerAvailablePrefix = """
        答えが不明な場合は0を返し、そうでない場合は1を返します。

        Answer:
        {{$answer}}

        ### 例
        Answer: 分かりません
        Your reply:
        0

        Answer: 回答が分かりません
        Your reply:
        0

        Answer: 水素を燃料とする燃料電池は、高いエネルギー変換効率と環境負荷の少なさが特徴[info1.txt]です。この技術を鉄道車両に応用し、水素ハイブリッド電車を開発し、実証試験を始めました。[info2.txt] 
        Your reply:
        1
        ###

        Your reply:
        """;

    private const string ExplainPrefix = """
        質問に答えるために必要なナレッジをまとめてください。回答には既存のナレッジを含めないでください。

        ### 例:

        Knowledge: 'エネルギー制御システムは、燃料電池、バッテリー、電気モーターなどの各コンポーネントのエネルギー供給と消費を調整します'
        Question: 'エネルギー制御システムとはなにか教えてください'
        Explain: 水素ハイブリット電車におけるエネルギー制御システムの役割が知りたい
        ###
        Knowledge:
        {{$knowledge}}

        Question:
        {{$question}}

        Explain:
        """;

    private const string GenerateKeywordsPrompt = """
        説明からキーワードを生成します。複数のキーワードはカンマで区切ります。

        Explain: 水素ハイブリッド電車の普及に伴い、水素供給基盤の整備と安全性の向上が進んでいます。
        キーワード: 水素ハイブリッド電車, 水素供給基盤
        ###

        Explanation:
        {{$explanation}}
        Keywords:
        """;

    private const string ThoughtProcessPrompt = """
        与えられた質問、説明、キーワード、情報、回答を用いて、質問に答えるための思考プロセスを記述します。

        ### 例:
        Question: 'マイクロソフトの従業員数は？'

        Explanation: マイクロソフトの情報と社員数を知りたい。

        Keywords: Microsoft, 社員数

        Information: [google.pdf]: マイクロソフトは2019年現在、全世界で14万4000人以上の従業員を抱えています。

        Answer: マイクロソフトが現在何人の従業員を抱えているかは知りませんが、2019年のマイクロソフトの従業員数は全世界で14万4000人を超えています。

        Summary:
        マイクロソフトの従業員数は何人ですか？
        質問に答えるには、マイクロソフトの情報と従業員数を知る必要があります。
        マイクロソフト、従業員数というキーワードで情報を検索してみると、以下のような情報が見つかりました：
         - [google.pdf] マイクロソフトは2019年現在、全世界で14万4000人以上の従業員を抱えている。 この情報を使って、私は答えを次のように定式化しました。
         - マイクロソフトが現在何人の従業員を抱えているかは知りませんが、2019年のマイクロソフトの従業員数は全世界で14万4000人を超えています。
        ###

        question:
        {{$question}}

        explanation:
        {{$explanation}}

        keywords:
        {{$keywords}}

        information:
        {{$knowledge}}

        answer:
        {{$answer}}

        summary:
        """;

    private const string PlannerPrefix = """
        次のステップを行います:
         - $question に答えるために必要なことを説明します
         - 説明から $keywords を生成します
         - $keywords を使用して情報を検索します
         - 得られた情報で $knowledge を更新します
         - プロセス全体を要約し、$summary を更新します
         - あなたが持っている知識に基づいて答えてください
        """;

    public Approach Approach => Approach.ReadDecomposeAsk;

    public ReadDecomposeAskApproachService(
        SearchClient searchClient,
        AzureOpenAITextCompletionService completionService,
        ILogger<ReadDecomposeAskApproachService> logger,
        IConfiguration configuration)
    {
        _searchClient = searchClient;
        _completionService = completionService;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<ApproachResponse> ReplyAsync(
        string question,
        RequestOverrides? overrides,
        CancellationToken cancellationToken = default)
    {
        var kernel = Kernel.Builder.Build();
        kernel.Config.AddTextCompletionService("openai", (kernel) => _completionService);
        kernel.ImportSkill(new RetrieveRelatedDocumentSkill(_searchClient, overrides));
        kernel.ImportSkill(new LookupSkill(_searchClient, overrides));
        kernel.CreateSemanticFunction(ReadDecomposeAskApproachService.AnswerPromptPrefix, functionName: "Answer", description: "answer question with given knowledge",
            maxTokens: 1024, temperature: overrides?.Temperature ?? 0.7);
        kernel.CreateSemanticFunction(ReadDecomposeAskApproachService.ExplainPrefix, functionName: "Explain", description: "explain", temperature: 0.5,
            presencePenalty: 0.5, frequencyPenalty: 0.5);
        kernel.CreateSemanticFunction(ReadDecomposeAskApproachService.GenerateKeywordsPrompt, functionName: "GenerateKeywords", description: "Generate keywords for lookup or search from given explanation", temperature: 0,
            presencePenalty: 0.5, frequencyPenalty: 0.5);
        kernel.CreateSemanticFunction(ReadDecomposeAskApproachService.ThoughtProcessPrompt, functionName: "Summarize", description: "Summarize the entire process of getting answer.", temperature: overrides?.Temperature ?? 0.7,
            presencePenalty: 0.5, frequencyPenalty: 0.5, maxTokens: 2048);

        var planner = new SequentialPlanner(kernel, new PlannerConfig
        {
            RelevancyThreshold = 0.7,
        });
        var planInstruction = $"{ReadDecomposeAskApproachService.PlannerPrefix}";
        var plan = await planner.CreatePlanAsync(planInstruction);
        plan.State["question"] = question;
        _logger.LogInformation("{Plan}", PlanToString(plan));

        do
        {
            plan = await kernel.StepAsync(question, plan, cancellationToken: cancellationToken);
        } while (plan.HasNextStep);

        return new ApproachResponse(
            DataPoints: plan.State["knowledge"].ToString().Split('\r'),
            Answer: plan.State["Answer"],
            Thoughts: plan.State["SUMMARY"].Replace("\n", "<br>"),
            CitationBaseUrl: _configuration.ToCitationBaseUrl());
    }

    private static string PlanToString(Plan originalPlan)
    {
        return $"Goal: {originalPlan.Description}\n\nSteps:\n" + string.Join("\n", originalPlan.Steps.Select(
            s =>
                $"- {s.SkillName}.{s.Name} {string.Join(" ", s.NamedParameters.Select(p => $"{p.Key}='{p.Value}'"))}{" => " + string.Join(" ", s.NamedOutputs.Where(s => s.Key.ToUpper(System.Globalization.CultureInfo.CurrentCulture) != "INPUT").Select(p => $"{p.Key}"))}"
        ));
    }
}
