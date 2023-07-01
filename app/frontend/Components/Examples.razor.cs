// Copyright (c) Microsoft. All rights reserved.

namespace ClientApp.Components;

public sealed partial class Examples
{
    [Parameter, EditorRequired] public required string Message { get; set; }
    [Parameter, EditorRequired] public EventCallback<string> OnExampleClicked { get; set; }

    private string WhatIsIncluded { get; } = "水素ハイブリット電車とは何ですか?";
    private string WhatIsPerfReview { get; } = "燃料電池スタックについて詳しく教えてください";
    private string WhatDoesPmDo { get; } = "水素ハイブリッド電車の静音性について説明してください";

    private async Task OnClickedAsync(string exampleText)
    {
        if (OnExampleClicked.HasDelegate)
        {
            await OnExampleClicked.InvokeAsync(exampleText);
        }
    }
}
