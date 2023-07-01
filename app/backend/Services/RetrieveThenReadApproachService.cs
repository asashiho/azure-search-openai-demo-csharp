// Copyright (c) Microsoft. All rights reserved.

namespace MinimalApi.Services;

internal sealed class RetrieveThenReadApproachService : IApproachBasedService
{
    private readonly SearchClient _searchClient;

    private const string SemanticFunction = """
          あなたは鉄道技術に関する質問をサポートする教師アシスタントです。
          質問者が「私」で質問しても、「あなた」を使って質問者を指すようにしてください。
          次の質問に、以下の出典で提供されたデータのみを使用して答えてください。
          表形式の情報については、HTMLテーブルとして返してください。マークダウン形式は返さないでください。
          各出典元には、名前の後にコロンが続き、実際の情報が記載されています。回答で使用する情報には、必ず出典元名を記載してください。
          以下の情報源で答えられない場合は、「わからない」と答えてください。
          
          ###
          Question: '水素ハイブリット電車とはなんですか？'
          
          Sources:
          info1.txt: 水素をエネルギー源とする燃料電池は、高いエネルギー変換効率と環境負荷の少なさが特徴
          info2.txt: 燃料電池自動車やバスの技術を鉄道車両の技術と融合・応用することにより、水素ハイブリッド電車を開発し、実証試験を始めた。
          
          Answer:
          水素を燃料とする燃料電池は、高いエネルギー変換効率と環境負荷の少なさが特徴[info1.txt]です。この技術を鉄道車両に応用し、水素ハイブリッド電車を開発し、実証試験を始めました。[info2.txt] 
          
          ###
          Question: {{$question}}?
          
          Sources:
          {{$retrieve}}
          
          Answer:
          """;

    private readonly IKernel _kernel;
    private readonly IConfiguration _configuration;
    private readonly ISKFunction _function;

    public Approach Approach => Approach.RetrieveThenRead;

    public RetrieveThenReadApproachService(SearchClient searchClient, IKernel kernel, IConfiguration configuration)
    {
        _searchClient = searchClient;
        _kernel = kernel;
        _configuration = configuration;
        _function = kernel.CreateSemanticFunction(
            SemanticFunction, maxTokens: 200, temperature: 0.7, topP: 0.5);
    }

    public async Task<ApproachResponse> ReplyAsync(
        string question,
        RequestOverrides? overrides = null,
        CancellationToken cancellationToken = default)
    {
        var text = await _searchClient.QueryDocumentsAsync(question, cancellationToken: cancellationToken);
        var context = _kernel.CreateNewContext();
        context["retrieve"] = text;
        context["question"] = question;

        var answer = await _kernel.RunAsync(context.Variables, cancellationToken, _function);

        return new ApproachResponse(
            DataPoints: text.Split('\r'),
            Answer: answer.ToString(),
            Thoughts: $"Question: {question} \r Prompt: {context.Variables}",
            CitationBaseUrl: _configuration.ToCitationBaseUrl());
    }
}
