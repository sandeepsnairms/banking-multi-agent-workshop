# Module 00 - Prerequisites - Deployment and Setup

**[Home](Home.md)** - [Creating Your First Agent >](./Module-01.md)


## Introduction

In this Module, you'll deploy the Azure Services needed to run this workshop and get your local environment configured and ready. You will also learn about the structure of this workshop and get an overview of Multi-Agent Systems.

## Learning Objectives and Activities

- Begin the deployment of the Azure Services
- Learn the structure and get an overview of this workshop
- Explore the core principals of multi-agent systems
- Complete the configuration of your local environment
- Compile and run the starter solution locally


## Module Exercises

1. [Activity 1: Configure Workshop Environment](#activity-1-configure-workshop-environment)
1. [Activity 2: Deploy Azure Services](#activity-2-deploy-azure-services)
1. [Activity 3: Workshop Structure and Overview Session](#activity-3-workshop-structure-and-overview)
1. [Activity 4: Configure Environment Variables](#activity-4-configure-environment-variables)
1. [Activity 5: Compile and Run](#activity-5-compile-and-run)


## Activity 1 Configure Workshop Environment

Complete the following tasks in order to prepare your environment for this workshop.

**Note:** These pre-requisites are a hard requirement for successful completion of this workshop. If you do not have all three you will not be able to successfully complete this workshop.

### Prerequisites

- Laptop or workstation with **administrator rights** (Alternatively you can run this workshop virtually in [GitHub Codespaces](https://github.com/features/codespaces))
- Azure subscription with **owner rights**
- Subscription access to Azure OpenAI service. Start here to [Request Access to Azure OpenAI Service](https://aka.ms/oaiapply). If you have access, see below for ensuring enough quota to deploy.


  #### Checking Azure OpenAI quota limits

  For this sample to deploy successfully, there needs to be enough Azure OpenAI quota for the models used by this sample within your subscription. This sample deploys a new Azure OpenAI account with two models, **gpt-4o-mini with 10K tokens** per minute and **text-3-large with 5k tokens** per minute. For more information on how to check your model quota and change it, see [Manage Azure OpenAI Service Quota](https://learn.microsoft.com/azure/ai-services/openai/how-to/quota)

  #### Azure Subscription Permission Requirements

  This solution deploys a [user-assigned managed identity](https://learn.microsoft.com/entra/identity/managed-identities-azure-resources/overview) and defines then applies Azure Cosmos DB and Azure OpenAI RBAC permissions to this as well as your own Service Principal Id. You will need the following Azure RBAC roles assigned to your identity in your Azure subscription or [Subscription Owner](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/privileged#owner) access which will give you both of the following.

  - [Manged Identity Contributor](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/identity#managed-identity-contributor)
  - [Cosmos DB Operator](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/databases#cosmos-db-operator)
  - [Cognitive Services OpenAI User](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/ai-machine-learning#cognitive-services-openai-user)


### Get Started

#### Local Environment

1. To run the workshop locally on your machine, install the following prerequisites:

    - Common pre-requisites:
      - [Docker Desktop](https://docs.docker.com/desktop/)
      - [Git](https://git-scm.com/downloads)
      - [Azure Developer CLI (azd)](https://aka.ms/install-azd)
  
    Then either of the following depending on which sample you want to explore

    - LangGraph Sample
      - [Python 3.12+](https://www.python.org/downloads/)
      - Your Python IDE or [VS Code](https://code.visualstudio.com/Download) with [Python Extension](https://marketplace.visualstudio.com/items?itemName=ms-python.python)
    - Semantic Kernel Agent Sample
      - [.NET 9](https://dotnet.microsoft.com/downloads/)
      - [Visual Studio](https://visualstudio.microsoft.com/downloads/) or [VS Code](https://code.visualstudio.com/Download) with [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)
  
1. Open a terminal session and navigate to the folder you want to copy the source code to.

1. Download the project source code:

  ```shell
  azd init -t AzureCosmosDB/banking-multi-agent-workshop/tree/start
  ```

#### GitHub Codespaces

You can run this sample app and workshop virtually by using GitHub Codespaces. The button will open a web-based VS Code instance in your browser:

1. Open the template (this may take several minutes):

  [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/AzureCosmosDB/banking-multi-agent-workshop/tree/start)

1. Continue with the [Deployment](#deployment) below


## Activity 2: Deploy Azure Services

### Deployment

1. From the terminal, navigate to the `csharp` or `python` folder.

1. Navigate to the /infra folder.

1. Log in to Azure using AZD.

   ```bash
   azd auth login
   ```

1. Provision the Azure services and deploy the application.

   ```bash
   azd up
   ```

This step will take approximately 10-15 minutes. If you encounter an error during step, first rerun `azd up`. This tends to correct most errors.

> [!IMPORTANT]
> If you encounter any errors during the deployment, rerun `azd up` to continue the deployment from where it left off. This will not create duplicate resources, and tends to resolve most issues.

## Activity 3: Workshop Structure and Overview Session

While the Azure Services are deploying we will have a presentation to cover on the structure for this workshop for today as well as provide an introduction and overview of multi-agent sytems.



## Activity 4: Configure Environment Variables

### Setting up local debugging

When you deploy this solution it automatically injects endpoints and configuration values into the secrets.json file used by .NET applications and exports these to environment variables for Python.


### Update src\ChatAPI\appsettings.json

1. Update "CosmosUri": "https://[accountname].documents.azure.com:443/" by replacing account name with the Cosmos DB Account deployed via azd.
2. Update "Endpoint": "https://[accountname].openai.azure.com/" by replacing account name with the Azure Open AI Account deployed via azd.

## Activity 5: Compile and Run

Here’s a refined version of your steps in Markdown:  

```md
### Running the ChatAPI and Frontend App

#### 1. Start the ChatAPI

##### If running on Codespaces:
1. Navigate to `src\ChatAPI`.
2. Run the following command to trust the development certificate:
   ```sh
   dotnet dev-certs https --trust
   ```
3. Start the application:
   ```sh
   dotnet run
   ```
4. Copy the URL from the **Ports** tab.

##### If running locally on Visual Studio or VS Code:
1. Navigate to `src\ChatAPI`.
2. Press **F5** or select **Run** to start the application.
3. Copy the URL from the browser window that opens.

#### 2. Run the Frontend App
Follow the [README instructions](../../README.md) to start the frontend application.  
Use the URL copied in the previous step as the API endpoint.

#### 3. Start a Chat Session
1. Open the frontend app.
2. Start a new chat session.
3. Send the message:  
   ```
   Hello, how are you?
   ```
4. Expected response: The message is echoed back to you.

#### 4. Stop the Application
Press **Ctrl + C** to stop the debugger.
```



### Deployment Validation

Use the steps below to validate that the solution was deployed successfully.

- [ ] All Azure resources are deployed successfully
- [ ] You can compile the solution in CodeSpaces or locally
- [ ] You can start the project and it runs without errors
- [ ] You are able to launch the Chat Frontend app , create a new chat session, and get a reply when you send a message.

### Common Issues and Troubleshooting

1. Errors during azd deployment:
  - Service principal "not found" error.
  - Rerun `azd up`
1. Azure OpenAI deployment issues:
  - Ensure your subscription has access to Azure OpenAI
  - Check regional availability
1. Python environment issues:
  - Ensure correct Python version
  - Verify all dependencies are installed


## Success Criteria

To complete this Module successfully, you should be able to:

- Verify that all services have been deployed successfully.
- Have an open IDE or CodeSpaces session with the source code and environment variables loaded.
- Be able to compile and run the application with no warnings or errors.

## Next Steps

Proceed to [Creating Your First Agent](./Module-01.md)

## Resources

- [azd Command Reference](https://learn.microsoft.com/azure/developer/azure-developer-cli/reference)
- [Semantic Kernel Agent Framework](https://learn.microsoft.com/semantic-kernel/frameworks/agent)
- [LangGraph](https://langchain-ai.github.io/langgraph/concepts/)
- [Azure OpenAI Service documentation](https://learn.microsoft.com/azure/cognitive-services/openai/)
- [Azure Cosmos DB Vector Database](https://learn.microsoft.com/azure/cosmos-db/vector-database)


<details>
  <summary>If you prefer to run this locally on your machine, open this section and install these additional tools.</summary>

<br>

</details>