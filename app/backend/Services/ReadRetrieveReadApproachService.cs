// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Planning.Planners;

namespace MinimalApi.Services;

internal sealed class ReadRetrieveReadApproachService : IApproachBasedService
{
    private readonly SearchClient _searchClient;
    private readonly AzureOpenAITextCompletionService _completionService;
    private readonly ILogger<ReadRetrieveReadApproachService> _logger;
    private readonly IConfiguration _configuration;

    private const string PlanPrompt = """
        次のステップを行います:
         - 質問に対する情報を検索し、結果を $knowledge に保存します
         - 持っている知識に基づいて $question に答え、結果を$answerに保存します
        """;

    private const string Prefix = """
        あなたは鉄道技術に関する質問をサポートする教師アシスタントです。
        質問者が「私」で質問しても、「あなた」を使って質問者を指すようにしてください。
        次の質問に、以下の出典で提供されたデータのみを使用して答えてください。
        表形式の情報については、HTMLテーブルとして返してください。マークダウン形式は返さないでください。
        各出典元には、名前の後にコロンが続き、実際の情報が記載されています。回答で使用する情報には、必ず出典元名を記載してください。
        以下の情報源で答えられない場合は、「わからない」と答えてください。

        ###
        Question: '水素ハイブリット電車とはなんですか？'

        Knowledge:
        info1.txt: 水素をエネルギー源とする燃料電池は、高いエネルギー変換効率と環境負荷の少なさが特徴
        info2.txt: 燃料電池自動車やバスの技術を鉄道車両の技術と融合・応用することにより、水素ハイブリッド電車を開発し、実証試験を始めた。

        Answer:
        水素を燃料とする燃料電池は、高いエネルギー変換効率と環境負荷の少なさが特徴[info1.txt]です。この技術を鉄道車両に応用し、水素ハイブリッド電車を開発し、実証試験を始めました。[info2.txt] 

        ###
        Question:
        {{$question}}

        Knowledge:
        {{$knowledge}}

        Answer:
        """;

    public Approach Approach => Approach.ReadRetrieveRead;

    public ReadRetrieveReadApproachService(
        SearchClient searchClient,
        AzureOpenAITextCompletionService service,
        ILogger<ReadRetrieveReadApproachService> logger,
        IConfiguration configuration)
    {
        _searchClient = searchClient;
        _completionService = service;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<ApproachResponse> ReplyAsync(
        string question,
        RequestOverrides? overrides,
        CancellationToken cancellationToken = default)
    {
        var kernel = Kernel.Builder.Build();
        kernel.Config.AddTextCompletionService("openai", _ => _completionService);
        kernel.ImportSkill(new RetrieveRelatedDocumentSkill(_searchClient, overrides));
        kernel.CreateSemanticFunction(ReadRetrieveReadApproachService.Prefix, functionName: "Answer", description: "answer question",
            maxTokens: 1_024, temperature: overrides?.Temperature ?? 0.7);
        var planner = new SequentialPlanner(kernel, new PlannerConfig
        {
            RelevancyThreshold = 0.7,
        });
        var sb = new StringBuilder();
        var plan = await planner.CreatePlanAsync(ReadRetrieveReadApproachService.PlanPrompt);
        var step = 1;
        plan.State["question"] = question;
        plan.State["knowledge"] = string.Empty;
        _logger.LogInformation("{Plan}", PlanToString(plan));

        do
        {
            plan = await kernel.StepAsync(plan, cancellationToken: cancellationToken);
            sb.AppendLine($"Step {step++} - Execution results:\n");
            sb.AppendLine(plan.State + "\n");
        } while (plan.HasNextStep);

        return new ApproachResponse(
            DataPoints: plan.State["knowledge"].ToString().Split('\r'),
            Answer: plan.State["Answer"],
            Thoughts: sb.ToString().Replace("\n", "<br>"),
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
