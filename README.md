# banking-multi-agent-workshop
A multi-agent sample and workshop for a retail banking scenario. Implemented in both C# using Semantic Kernel Agents and Python using LangGraph. 

# Build a Multi-Agent AI application using Semantic Kernel Agents or LangGraph

Csharp project - [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/AzureCosmosDB/banking-multi-agent-workshop?devcontainer_path=.devcontainer%2Fcsharp%2Fdevcontainer.json)

Python project - [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/AzureCosmosDB/banking-multi-agent-workshop?devcontainer_path=.devcontainer%2Fpython%2Fdevcontainer.json)

[![Open in Dev Containers](https://img.shields.io/static/v1?style=for-the-badge&label=Dev%20Containers&message=Open&color=blue&logo=visualstudiocode)](https://vscode.dev/redirect?url=vscode://ms-vscode-remote.remote-containers/cloneInVolume?url=https://github.com/AzureCosmosDB/banking-multi-agent-workshop)

This sample application and full-day workshop shows how to build a multi-tenant, multi-agent, banking application with containerized applications built in two different versions

- Using Semantic Kernel Agents in C# or 
- LangGraph written in Python

Both are hosted on Azure Container Apps, with Azure Cosmos DB for NoSQL as the transactional database and vector store and Azure OpenAI Service for embeddings and completions. This is sample and full-day workshop provides practical guidance on many concepts you will need to design and build these types of applications.

## Important Security Notice

This template, the application code and configuration it contains, has been built to showcase Microsoft Azure specific services and tools. We strongly advise our customers not to make this code part of their production environments without implementing or enabling additional security features.

## Workshop Learning Exercises

This application demonstrates the following concepts and how to implement them:

View the Exercises for the [LangGraph Python Workshop](./python/workshop/Exercise-00.md)
View the Exercises for the [Semantic Kernel Csharp Workshop](./csharp/workshop/Exercise-00.md)


### Architecture Diagram


![Architecture Diagram](./media/construction.jpg)

### User Experience

![Multi-Agent user interface](./media/construction.jpg)

## Getting Started

### Prerequisites

- Azure subscription.
- Subscription access to Azure OpenAI service. Start here to [Request Access to Azure OpenAI Service](https://aka.ms/oaiapply). If you have access, see below for ensuring enough quota to deploy.


  #### Checking Azure OpenAI quota limits

  For this sample to deploy successfully, there needs to be enough Azure OpenAI quota for the models used by this sample within your subscription. This sample deploys a new Azure OpenAI account with two models, **gpt-4o with 10K tokens** per minute and **text-3-embedding-small with 5k tokens** per minute. For more information on how to check your model quota and change it, see [Manage Azure OpenAI Service Quota](https://learn.microsoft.com/azure/ai-services/openai/how-to/quota)

  #### Azure Subscription Permission Requirements

  This solution deploys a [user-assigned managed identity](https://learn.microsoft.com/entra/identity/managed-identities-azure-resources/overview) and defines then applies Azure Cosmos DB and Azure OpenAI RBAC permissions to this as well as your own Service Principal Id. You will need the following Azure RBAC roles assigned to your identity in your Azure subscription or [Subscription Owner](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/privileged#owner) access which will give you both of the following.

  - [Manged Identity Contributor](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/identity#managed-identity-contributor)
  - [Cosmos DB Operator](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/databases#cosmos-db-operator)
  - [Cognitive Services OpenAI User](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/ai-machine-learning#cognitive-services-openai-user)

### GitHub Codespaces

You can run this sample app and workshop virtually by using GitHub Codespaces. The button will open a web-based VS Code instance in your browser:

1. Open the template (this may take several minutes):

  [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/AzureCosmosDB/banking-multi-agent-workshop)

1. Open a terminal window and navigate to the csharp or python folder.

1. Continue with the [Deployment](#deployment) below

### Local Environment

1. If you're CodeSpaces for opening the project, install the following prerequisites

  * [Docker Desktop](https://docs.docker.com/desktop/)
  * [.NET 9](https://dotnet.microsoft.com/downloads/)
  * [Python 3.12+](https://www.python.org/downloads/)
  * [Git](https://git-scm.com/downloads)
  * [Azure Developer CLI (azd)](https://aka.ms/install-azd)
  * Any IDE or [VS Code](https://code.visualstudio.com/Download) or [Visual Studio](https://visualstudio.microsoft.com/downloads/)
    * If using VS Code and using C#, install the [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)

1. Download the project code:

  ```shell
  azd init -t AzureCosmosDB/banking-multi-agent-workshop
  ```

### Deployment

1. From the terminal, naviate to the csharp or python folder.

1. Navigate to the /infra folder.

1. Log in to AZD.

   ```bash
   azd auth login
   ```

1. Provision the Azure services, build your local solution container, and deploy the application.

   ```bash
   azd up
   ```

### Setting up local debugging

When you deploy this solution it automatically injects endpoints and configuration values into the secrets.json file used by .NET applications.

To modify values for the Quickstarts, locate the value of `UserSecretsId` in the csproj file in the /src folder of this sample and save the value.

```xml
<PropertyGroup>
  <UserSecretsId>your-guid-here</UserSecretsId>
</PropertyGroup>
```

Locate the secrets.json file and open with a text editor.

- Windows: `C:\Users\<YourUserName>\AppData\Roaming\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`
- macOS/Linux: `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`


## Clean up

1. Open a terminal and navigate to the /infra directory in this solution.

1. Type azd down

   ```bash
   azd down
   ```

## Guidance

### Region Availability

This template uses gpt-4o and text-embedding-3-small models which may not be available in all Azure regions. Check for [up-to-date region availability](https://learn.microsoft.com/azure/ai-services/openai/concepts/models#standard-deployment-model-availability) and select a region during deployment accordingly
  * We recommend using `eastus2', 'eastus', 'japaneast', 'uksouth', 'northeurope', or 'westus3'

### Costs

You can estimate the cost of this project's architecture with [Azure's pricing calculator](https://azure.microsoft.com/pricing/calculator/)

As an example in US dollars, here's how the sample is currently built:

Average Daily Cost is $1.50
* Azure Cosmos DB Serverless ($0.25 USD per 1M RU/s): <$0.01
* Azure Container Apps Service - 2 containers (1 CPU, 2 Gi memory): $0.54
* Azure Container Registry Service (Standard Plan): $0.52
* Azure OpenAI (GPT-4o): $0.10 (Sample uses 10K tokens)
* Azure OpenAI (text-3-embedding-small): < $0.01 (Sample uses 5K tokens)

## Resources

To learn more about the services and features demonstrated in this sample, see the following:

- [Azure Cosmos DB for NoSQL Vector Search announcement](https://aka.ms/CosmosDBDiskANNBlog/)
- [Azure OpenAI Service documentation](https://learn.microsoft.com/azure/cognitive-services/openai/)
- [Semantic Kernel](https://learn.microsoft.com/semantic-kernel/overview)
- [Azure App Service documentation](https://learn.microsoft.com/azure/app-service/)
- [ASP.NET Core Blazor documentation](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
