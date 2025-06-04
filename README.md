<!--
---
name: Durable Functions C# Fan-Out/Fan-In using Azure Developer CLI
description: This repository contains a Durable functions quickstart written in C# demonstrating the fan-out/fan-in pattern. It's deployed to Azure Functions Flex Consumption plan using the Azure Developer CLI (azd). The sample uses managed identity and a virtual network to make sure deployment is secure by default.
page_type: sample
products:
- azure-functions
- azure
- entra-id
urlFragment: starter-durable-fan-out-fan-in-csharp
languages:
- csharp
- bicep
- azdeveloper
---
-->

# Durable Functions Fan-Out/Fan-In using Azure Developer CLI

This template repository contains a Durable Functions sample demonstrating the fan-out/fan-in pattern in C# (isolated process model). The sample can be easily deployed to Azure using the Azure Developer CLI (`azd`). It uses managed identity and a virtual network to make sure deployment is secure by default. You can opt out of a VNet being used in the sample by setting VNET_ENABLED to false in the parameters.

[Durable Functions](https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-overview) is part of Azure Functions offering. It helps orchestrate stateful logic that's long-running or multi-step by providing *durable execution*. An execution is durable when it can continue in another process or machine from the point of failure in the face of interruptions or infrastructure failures. Durable Functions handles automatic retries and state persistence as your orchestrations run to ensure durable execution. 

Durable Functions needs a [backend provider](https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-storage-providers) to persist application states. This sample uses the Azure Storage backend. 

This project is designed to run on your local computer. You can also use GitHub Codespaces:

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://github.com/codespaces/new?hide_repo_select=true&ref=main&repo=836901178)

This codespace is already configured with the required tools to complete this tutorial using either `azd` or Visual Studio Code. If you're working a codespace, skip down to [Prepare your local environment](#prepare-your-local-environment).

## Prerequisites

+ [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
+ [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local?pivots=programming-language-csharp#install-the-azure-functions-core-tools)
+ To use Visual Studio to run and debug locally:
  + [Visual Studio 2022](https://visualstudio.microsoft.com/vs/).
  + Make sure to select the **Azure development** workload during installation.
+ To use Visual Studio Code to run and debug locally:
  + [Visual Studio Code](https://code.visualstudio.com/)
  + [Azure Functions extension](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-azurefunctions)

## Initialize the local project

You can initialize a project from this `azd` template in one of these ways:

+ Use this `azd init` command from an empty local (root) folder:

    ```shell
    azd init --template durable-functions-quickstart-dotnet-azd
    ```

    Supply an environment name, such as `dfquickstart` when prompted. In `azd`, the environment is used to maintain a unique deployment context for your app.

+ Clone the GitHub template repository locally using the `git clone` command:

    ```shell
    git clone https://github.com/Azure-Samples/durable-functions-quickstart-dotnet-azd.git
    cd FanOutFanIn
    ```

    You can also clone the repository from your own fork in GitHub.

## Prepare your local environment

Navigate to the `FanOutFanIn` app folder and create a file in that folder named _local.settings.json_ that contains this JSON data:

```json
{
    "IsEncrypted": false,
    "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
    }
}
```

## Run your app from the terminal

1. From the `FanOutFanIn` folder, run this command to start the Functions host locally:

    ```shell
    func start
    ```

1. From your HTTP test tool in a new terminal (or from your browser), call the HTTP trigger endpoint: <http://localhost:7071/api/ConcurrentFetchOrchestration_HttpStart> to start a new orchestration instance. This orchestration then fans out to several activities to fetch the titles of Microsoft Learn articles in parallel. When the activities finish, the orchestration fans back in and returns the titles as a formatted string. 

1. When you're done, press Ctrl+C in the terminal window to stop the `func.exe` host process.

## Run your app using Visual Studio Code

1. Open the `FanOutFanIn` app folder in a new terminal.
1. Run the `code .` code command to open the project in Visual Studio Code.
1. In the command palette (F1), type `Azurite: Start`, which enables debugging without warnings.
1. Press **Run/Debug (F5)** to run in the debugger. Select **Debug anyway** if prompted about local emulator not running.
1. From your HTTP test tool in a new terminal (or from your browser), call the HTTP trigger endpoint: <http://localhost:7071/api/ConcurrentFetchOrchestration_HttpStart> to start a new orchestration instance.

## Run your app using Visual Studio

1. Open the `FanOutFanIn.sln` solution file in Visual Studio.
1. Press **Run/F5** to run in the debugger. Make a note of the `localhost` URL endpoints, including the port, which might not be `7071`.
1. From your HTTP test tool in a new terminal (or from your browser), call the HTTP trigger endpoint: <http://localhost:7071/api/ConcurrentFetchOrchestration_HttpStart> to start a new orchestration instance.

## Source Code
Fanning out is easy to do with regular functions, simply send multiple messages to a queue. However, fanning in is more challenging, because you need to track when all the functions are completed and store the outputs.

Durable Functions makes implementing fan-out/fan-in easy for you. This sample uses a simple scenario of fetching article titles in parallel to demonstrate how you can implement the pattern with Durable Functions. In `ConcurrentFetchOrchestration`, the title fetching activities are tracked using a dynamic task list. The line `await Task.WhenAll(parallelTasks);` waits for all the called activities, which are run concurrently, to complete. When done, all outputs are aggregated as a formatted string. More sophisticated aggregation logic is probably required in real-world scenarios, such as uploading the result to storage or sending it downstream, which you can do by calling another activity function.

```csharp
[Function(nameof(ConcurrentFetchOrchestration))]
public static async Task<string> RunOrchestrator(
    [OrchestrationTrigger] TaskOrchestrationContext context)
{
    ILogger logger = context.CreateReplaySafeLogger(nameof(ConcurrentFetchOrchestration));
    logger.LogInformation("Fetching data.");
    var parallelTasks = new List<Task<string>>();
    
    // List of URLs to fetch titles from
    var urls = new List<string>
    {
        "https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-overview",
        "https://learn.microsoft.com/azure/azure-functions/durable/durable-task-scheduler/durable-task-scheduler",
        "https://learn.microsoft.com/azure/azure-functions/functions-scenarios",
        "https://learn.microsoft.com/azure/azure-functions/functions-create-ai-enabled-apps",
    };

    // Run fetching tasks in parallel
    foreach (var url in urls)
    {
        Task<string> task = context.CallActivityAsync<string>(nameof(FetchTitleAsync), url);
        parallelTasks.Add(task);
    }
    
    // Wait for all the parallel tasks to complete before continuing
    await Task.WhenAll(parallelTasks);
    
    // Return fetched titles as a formatted string
    return string.Join(", ", parallelTasks.Select(t => t.Result));
}
```


## Deploy to Azure

Run this command to provision the function app, with any required Azure resources, and deploy your code:

```shell
azd up
```

By default, this sample prompts to enable a virtual network for enhanced security. If you want to deploy without a virtual network without prompting, you can configure `VNET_ENABLED` to `false` before running `azd up`:

```bash
azd env set VNET_ENABLED false
azd up
```

You're prompted to supply these required deployment parameters:

| Parameter | Description |
| ---- | ---- |
| _Environment name_ | An environment that's used to maintain a unique deployment context for your app. You won't be prompted if you created the local project using `azd init`.|
| _Azure subscription_ | Subscription in which your resources are created.|
| _Azure location_ | Azure region in which to create the resource group that contains the new Azure resources. Only regions that currently support the Flex Consumption plan are shown.|

After publish completes successfully, `azd` provides you with the URL endpoints of your new functions, but without the function key values required to access the endpoints. To learn how to obtain these same endpoints along with the required function keys, see [Invoke the function on Azure](https://learn.microsoft.com/azure/azure-functions/create-first-function-azure-developer-cli?pivots=programming-language-dotnet#invoke-the-function-on-azure).

## Redeploy your code

You can run the `azd up` command as many times as you need to both provision your Azure resources and deploy code updates to your function app.

>[!NOTE]
>Deployed code files are always overwritten by the latest deployment package.

## Clean up resources

When you're done working with your function app and related resources, you can use this command to delete the function app and its related resources from Azure and avoid incurring any further costs:

```shell
azd down
```
