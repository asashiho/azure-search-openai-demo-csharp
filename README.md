# ChatGPT + Enterprise data with Azure OpenAI and Cognitive Search

![GitHub Workflow Status](https://img.shields.io/github/actions/workflow/status/Azure-Samples/azure-search-openai-demo-csharp/dotnet-build.yml?label=BUILD%20%26%20TEST&logo=github&style=for-the-badge)
[![Open in GitHub - Codespaces](https://img.shields.io/static/v1?style=for-the-badge&label=GitHub+Codespaces&message=Open&color=brightgreen&logo=github)](https://github.com/codespaces/new?hide_repo_select=true&ref=main&repo=624102171&machine=standardLinux32gb&devcontainer_path=.devcontainer%2Fdevcontainer.json&location=WestUs2)
[![Open in Remote - Containers](https://img.shields.io/static/v1?style=for-the-badge&label=Remote%20-%20Containers&message=Open&color=blue&logo=visualstudiocode)](https://vscode.dev/redirect?url=vscode://ms-vscode-remote.remote-containers/cloneInVolume?url=https://github.com/azure-samples/azure-search-openai-demo-csharp)

このサンプルでは、Retrieval Augmented Generation パターンを使用して、独自のデータで ChatGPT のような生成AIを活用したアプリケーションを開発します。ChatGPT モデル (`gpt-35-turbo`) へのアクセスには Azure OpenAI を使用し、データのインデックス化と検索には Azure Cognitive Search を使用します。

このリポジトリにはサンプルデータが含まれているので、エンドツーエンドで試すことができます。このサンプルアプリケーションでは、鉄道技術に関する架空の論文をデータとして使い、水素ハイブリット電車に関する技術的な質問に答えることができます。

![RAG Architecture](docs/appcomponents-version-3.png)

このアプリケーションの開発の詳細については、こちらの記事をご参照ください:

- [Transform your business with smart .NET apps powered by Azure and ChatGPT blog post](https://aka.ms/build-dotnet-ai-blog)
- [Build Intelligent Apps with .NET and Azure - Build Session](https://build.microsoft.com/sessions/f8f953f3-2e58-4535-92ae-5cb30ef2b9b0)


## サンプルアプリケーションの機能

* ボイスチャット/文字チャット/Q&Aインターフェース
* 引用やソースコンテンツの追跡など、ユーザーが回答の信頼性を評価するためのさまざまなオプション
* データ準備、プロンプト構築、モデル（ChatGPT）と検索（Cognitive Search）間のオーケストレーション

![Chat screen](docs/chatscreen.png)

## サンプルアプリケーションの実行手順

> **💡注意💡**<br>
>このサンプルをデプロイして実行するには、**Azure OpenAIサービスへのアクセスを有効にしたAzureサブスクリプション**が必要です。 [申請](https://aka.ms/oaiapply)はこちらです。また、Azureのサブスクリプション自体をお持ちでない方は[こちら](https://azure.microsoft.com/free/cognitive-search/)にアクセスして、Azureのトライアルを申請できます。

> **🚩警告🚩**<br>
>デフォルトでは、このサンプルは、月額費用が発生する Azure App Service、Azure Static Web App、Azure Cognitive Search リソースと、ドキュメントページごとに費用が発生する Form Recognizer リソースを作成します。このコストを回避したい場合は、`infra` フォルダ下のパラメータファイルを変更することで、それぞれのリソースを無料版に切り替えることができます (ただし、考慮すべき制限もあります。たとえば、無料の Cognitive Search リソースは 1 サブスクリプションにつき 1 つまでで、無料の Form Recognizer リソースは各ドキュメントの最初の 2 ページのみしか分析できません)

### 前提条件

#### ローカルで実行する場合

- [Azure Developer CLI](https://aka.ms/azure-dev/install)
- [.NET 7](https://dotnet.microsoft.com/download)
- [Git](https://git-scm.com/downloads)
- [Powershell 7+ (pwsh)](https://github.com/powershell/powershell) - For Windows ユーザのみ
   - **重要**: PowerShell コマンドから `pwsh.exe` を実行できることを確認します。失敗した場合は、PowerShellをアップグレードする必要があります。
- [Docker](https://www.docker.com/products/docker-desktop/)
  - **重要**: `azd` のプロビジョニング/デプロイコマンドを実行する前に、Docker が起動していることを確認してください。


> **💡注意💡**<br>
> Azure アカウントには、[User Access Administrator](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles#user-access-administrator) または [Owner](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles#owner) などの `Microsoft.Authorization/roleAssignments/write` 権限が必要です。


#### GitHubコードスペースまたはVS Codeリモートコンテナで実行する場合

GitHub Codespaces または Visual Studio Code Dev Container を利用できます。以下のボタンのいずれかをクリックして、このリポジトリを開いてください。

[![Open in GitHub - Codespaces](https://img.shields.io/static/v1?style=for-the-badge&label=GitHub+Codespaces&message=Open&color=brightgreen&logo=github)](https://github.com/codespaces/new?hide_repo_select=true&ref=main&repo=624102171&machine=standardLinux32gb&devcontainer_path=.devcontainer%2Fdevcontainer.json&location=WestUs2)
[![Open in Remote - Containers](https://img.shields.io/static/v1?style=for-the-badge&label=Remote%20-%20Containers&message=Open&color=blue&logo=visualstudiocode)](https://vscode.dev/redirect?url=vscode://ms-vscode-remote.remote-containers/cloneInVolume?url=https://github.com/azure-samples/azure-search-openai-demo-csharp)


### インストール

#### プロジェクトの初期化

1. 新しいフォルダを作成し、ターミナルでそのフォルダに移動
1. `azd auth login` 実行
1. `azd init -t azure-search-openai-demo-csharp` 実行
    * このサンプルで使用されているモデルを現在サポートしている地域は、**米国東部**または**米国南中部**です。最新の地域とモデルのリストについては、 [こちら](https://learn.microsoft.com/azure/cognitive-services/openai/concepts/models)をチェックしてください。

#### スクラッチから開始する場合

既存のAzureサービスがなく、新しいデプロイから始めたい場合は、以下のコマンドを実行します。

1. `azd up` を実行 - Azure リソースをプロビジョニングし、このサンプルをそれらのリソースにデプロイします。

> **💡注意💡**<br>
> このアプリケーションは `text-davinci-003` と `gpt-35-turbo` のモデルを使用しています。どのリージョンにデプロイするかを選択する際には、そのリージョンで利用可能であることを確認してください（例: EastUS） 詳細については [Azure OpenAI Service documentation](https://learn.microsoft.com/en-us/azure/cognitive-services/openai/concepts/models#gpt-3-models-1)を参照してください。


1. アプリケーションが正常にデプロイされると、コンソールにURLが表示されます。 そのURLをクリックして、ブラウザでアプリケーションを開きます。

!['Output from running azd up'](assets/endpoint.png)

> **💡注意💡**<br>
> アプリケーションが完全にデプロイされるまで数分かかる場合があります。


#### 既存のリソースを利用する場合

1. `azd env set AZURE_OPENAI_SERVICE {既存のOpenAIのサービス名}` を実行
1. `azd env set AZURE_OPENAI_RESOURCE_GROUP {OpenAIサービスがプロビジョニングされる既存のリソースグループ名}` を実行
1. `azd env set AZURE_OPENAI_CHATGPT_DEPLOYMENT {既存のChatGPTデプロイメントの名前}` を実行。※この手順はChatGPT デプロイメントがデフォルトの 'chat' でない場合にのみ必要
1. `azd env set AZURE_OPENAI_GPT_DEPLOYMENT {既存の GPT デプロイメントの名前}` を実行します。※この手順はChatGPT デプロイメントがデフォルトの `davinci` でない場合のみ必要
1. `azd up` を実行

> **📝メモ📝**<br>
> 既存の Search Account や Storage Account を利用することもできます。 既存のリソースを設定するために `azd env set` に渡す環境変数のリストについては `./infra/main.parameters.json` を参照してください。

#### リポジトリのクローンのデプロイまたは再デプロイの場合

* `azd up` の実行

#### 4. App Spacesを使ってレポをデプロイする場合

> **📝メモ📝**<br>
> リポジトリにazdがサポートするbicepファイルがあることを確認し、手動（初回デプロイ用）またはコード変更時（最新の変更で自動的に再デプロイ）にトリガーできる初期GitHub Actions Workflowファイルを追加します。
> リポジトリをApp Spacesと互換性を持たせるには、AZDが適切なタグを持つ既存のリソースグループにデプロイできるように、メインのバイセップとメインのパラメーターファイルを変更する必要があります。

1. メインパラメータファイルにAZURE_RESOURCE_GROUPを追加し、App SpacesがGitHub Actionsワークフローファイルに設定した環境変数の値を読み込むようにします。
   ```json
   "resourceGroupName": {
      "value": "${AZURE_RESOURCE_GROUP}"
    }
2. メインパラメータファイルにAZURE_TAGSを追加し、App SpacesがGitHub Actionsワークフローファイルに設定した環境変数から値を読み込むようにする。
   ```json
   "tags": {
      "value": "${AZURE_TAGS}"
    }
3. App Spacesによって設定されている値を読み取るために、メインのbicepファイルにリソースグループとタグのサポートを追加します。
   ```bicep
   param resourceGroupName string = ''
   param tags string = ''
4. `azd`によって設定されたデフォルトのタグと、App Spacesによって設定されたタグを組み合わせる。メインのbicepファイルの*tags initialization*を以下のように置き換えます
   ```bicep
   var baseTags = { 'azd-env-name': environmentName }
   var updatedTags = union(empty(tags) ? {} : base64ToJson(tags), baseTags)
   Make sure to use "updatedTags" when assigning "tags" to resource group created in your bicep file and update the other resources to use "baseTags" instead of "tags". For example - 
   ```json
   resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
     name: !empty(resourceGroupName) ? resourceGroupName : '${abbrs.resourcesResourceGroups}${environmentName}'
     location: location
     tags: updatedTags
   }

#### ローカルでの実行の場合

1. `azd auth login` の実行
1. アプリケーションがデプロイされたら、環境変数 `AZURE_KEY_VAULT_ENDPOINT` を設定します。この値は *.azure/YOUR-ENVIRONMENT-NAME/.env* ファイルまたは Azure ポータルで確認
1. 次のコマンドを実行して、ASP.NET Core Minimal API サーバー（クライアントホスト）を起動
    ```dotnetcli
    dotnet run --project ./app/backend/MinimalApi.csproj --urls=https://localhost:7181/
    ```

ブラウザで<https://localhost:7181>に移動し、アプリを試してみてください。

#### 環境の共有

デプロイされた既存の環境へのアクセス権を他の人に与えたい場合は、以下を実行してください。

1. [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)のインストール
1. `azd init -t azure-search-openai-demo-csharp` の実行
1. `azd env refresh -e {環境名}` を実行 
- このコマンドを実行するには `azd` 環境名、サブスクリプション ID、リージョンが必要であることに注意してください。 これで、`azd`環境の`.env`ファイルに、アプリをローカルで実行するために必要なすべての設定が入力されます。
4. `pwsh ./scripts/roles.ps1`を実行 
- これは必要なロールをすべてユーザに割り当て、ローカルでアプリを実行できるようにします。 ユーザがサブスクリプションでロールを作成するのに必要な権限を持っていない場合は、このスクリプトを実行する必要があるかもしれません。`azd.env`ファイルまたはシェルで、環境変数`AZURE_PRINCIPAL_ID`を自分の`Azure ID`に設定してください。

#### リソースのクリーンアップ

1. `azd down`の実行

### クイックスタート

* Azure の場合:
 `azd` によってデプロイされた Azure Static Web App に移動する。URL は `azd` が完了したときに出力される`Endpoint`か、Azure ポータルで確認できます。

* ローカルで実行する場合:
クライアント・アプリは<https://localhost:7181>に、Open APIサーバーは<https://localhost:7181/swagger>にアクセスしてください。

#### サンプルアプリの設定方法

* **音声チャット**ページで、音声設定ダイアログを選択し、音声合成設定を行います。
  * **[Blazor Clippy]** と対話するためにメッセージを入力するか、**[Speak]** トグルボタンを選択して音声テキストを入力として使用することができます。
* **[Chat]** または **[Ask]** のコンテキストでさまざまなトピックを試してみてください。チャットの場合は、フォローアップの質問、明確な説明、答えを簡単にしたり詳しく説明したりすることなどを試してみてください。

* 引用と出典の設定
  * **[設定]** アイコンをクリックすると、さまざまなオプションを試したり、プロンプトを微調整したりできます。

## 参考情報

* [Revolutionize your Enterprise Data with ChatGPT: Next-gen Apps w/ Azure OpenAI and Cognitive Search](https://aka.ms/entgptsearchblog)
* [Azure Cognitive Search](https://learn.microsoft.com/azure/search/search-what-is-azure-search)
* [Azure OpenAI Service](https://learn.microsoft.com/azure/cognitive-services/openai/overview)
* [`Azure.AI.OpenAI` NuGet package](https://www.nuget.org/packages/Azure.AI.OpenAI)
* [Original Blazor App](https://github.com/IEvangelist/blazor-azure-openai)

> **Note**<br>
> The PDF documents used in this demo contain information generated using a language model (Azure OpenAI Service). The information contained in these documents is only for demonstration purposes and does not reflect the opinions or beliefs of Microsoft. Microsoft makes no representations or warranties of any kind, express or implied, about the completeness, accuracy, reliability, suitability or availability with respect to the information contained in this document. All rights reserved to Microsoft.


### よくある質問

***質問***: 
Azure Cognitive Searchは大きな文書の検索をサポートしているのに、なぜPDFをチャンクに分割する必要があるのでしょうか？

***回答***: 
チャンクによって、トークンの制限のためにOpenAIに送信する情報量を制限できます。コンテンツを分割することで、OpenAIに注入できる潜在的なテキストのチャンクを見つけることができます。私たちが使っているチャンクの方法は、あるチャンクが終わると次のチャンクが始まるように、テキストのスライディングウィンドウを活用します。これにより、テキストの文脈が失われる可能性を減らすことが可能です。
