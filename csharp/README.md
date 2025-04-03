# Multi-Agent AI application using Semantic Kernel Agents

## Prerequisites

- Laptop or workstation with **administrator rights** (Alternatively you can run this workshop virtually in [GitHub Codespaces](https://github.com/features/codespaces))
- Azure subscription with **owner rights**
- Subscription access to Azure OpenAI service. Start here to [Request Access to Azure OpenAI Service](https://aka.ms/oaiapply). If you have access, see below for ensuring enough quota to deploy.

  ### Checking Azure OpenAI quota limits

  For this sample to deploy successfully, there needs to be enough Azure OpenAI quota for the models used by this sample within your subscription. This sample deploys a new Azure OpenAI account with two models, **gpt-4o with 30K tokens** per minute and **text-3-embedding-small with 5k tokens** per minute. For more information on how to check your model quota and change it, see [Manage Azure OpenAI Service Quota](https://learn.microsoft.com/azure/ai-services/openai/how-to/quota)

  ### Azure Subscription Permission Requirements

  This solution deploys a [user-assigned managed identity](https://learn.microsoft.com/entra/identity/managed-identities-azure-resources/overview) and defines then applies Azure Cosmos DB and Azure OpenAI RBAC permissions to this as well as your own Service Principal Id. You will need the following Azure RBAC roles assigned to your identity in your Azure subscription or [Subscription Owner](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/privileged#owner) access which will give you both of the following.

  - [Manged Identity Contributor](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/identity#managed-identity-contributor)
  - [Cosmos DB Operator](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/databases#cosmos-db-operator)
  - [Cognitive Services OpenAI User](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/ai-machine-learning#cognitive-services-openai-user)

## Get Started

You can choose from the following options to get started with the workshop.

### GitHub Codespaces

You can run this sample using GitHub Codespaces (requires a GitHub account). The button will open a web-based VS Code instance in your browser:

1. Open the template (this may take several minutes):

  [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/AzureCosmosDB/banking-multi-agent-workshop?branch=main&devcontainer_path=.devcontainer%2Fcsharp%2Fdevcontainer.json)

1. Move on to the [Deployment](readme.md#deployment) section.

### Local Environment using VS Code Dev Containers

1. Install [Docker Desktop](https://docs.docker.com/desktop/), and [VS Code](https://code.visualstudio.com/Download) along with the [Dev Containers extension](https://code.visualstudio.com/docs/devcontainers/tutorial#_install-the-extension) extension.

2. Clone the repository:

   ```bash
   git clone https://github.com/AzureCosmosDB/banking-multi-agent-workshop/
   cd banking-multi-agent-workshop
   ```

3. Open the repository in VS Code and select **Reopen in Container** when prompted. When asked to **Select a devcontainer.json file**, select the **C# Development Container**.

4. Wait for the container to build and start. This is a one time operation and may take a few minutes.

5. Move on to the [Deployment](readme.md#deployment) section.

#### Local Environment without VS Code Dev Containers

1. To run the workshop locally on your machine, install the following:

   - [Docker Desktop](https://docs.docker.com/desktop/)
   - [Git](https://git-scm.com/downloads)
   - [Azure Developer CLI (azd)](https://aka.ms/install-azd)
   - [.NET 8](https://dotnet.microsoft.com/downloads/)
   - [Node.js](https://nodejs.org/en/download/)
   - [Angular CLI](https://angular.dev/installation#install-angular-cli)
   - [Visual Studio](https://visualstudio.microsoft.com/downloads/) or [VS Code](https://code.visualstudio.com/Download) with [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)

2. Clone the repository:

   ```bash
   git clone https://github.com/AzureCosmosDB/banking-multi-agent-workshop/
   cd banking-multi-agent-workshop
   ```

3. Move on to the [Deployment](readme.md#deployment) section.

### Deployment

1. Navigate to the infra folder:

   ```bash
   cd csharp/infra
   ```

1. Log in to Azure using AZD. Follow the prompts to complete authentication.

   ```bash
   azd auth login
   ```

1. Provision the Azure services and deploy the application.

   ```bash
   azd up
   ```

This step will take approximately 10-15 minutes.

> [!IMPORTANT]
> If you encounter any errors during the deployment, rerun `azd up` to continue the deployment from where it left off. This will not create duplicate resources, and tends to resolve most issues.

1. When the resources are finally deployed, you will see a message in the terminal like below:

```bash
Deploying services (azd deploy)

  (✓) Done: Deploying service ChatServiceWebApi
  - Endpoint: https://ca-webapi-6xbkqp3ybtbuw.whitemoss-86b36485.eastus2.azurecontainerapps.io/

Do you want to add some dummy data for testing? (yes/no): y
```

1. Press `y` to load the data for the workshop.

1. After the data is loaded, you will see a message in the terminal like below:

```bash
PUT offerdata Request Successful: True
PUT offerdata Request Successful: True
PUT offerdata Request Successful: True

Do you want to deploy the frontend app? (yes/no): 
```

1. Press `y` to deploy the frontend application.

### Setting up local debugging

When you deploy this solution it automatically injects endpoints and configuration values into the .env file stored in the .azure directory in a folder with the name of your resource group.

1. Navigate to `.azure\[your-resource-group-name]\.env`
1. Open the .env file using any text editor.
1. Navigate to `csharp\src\MultiAgentCopilot.sln` and open the solution.
1. Within your IDE, navigate to `ChatAPI` project and open `appsettings.json`
1. Update `"CosmosDBSettings:CosmosUri": "https://[accountname].documents.azure.com:443/"` with the AZURE_COSMOSDB_ENDPOINT value from the .env file.
1. Update `"SemanticKernelServiceSettings:AzureOpenAISettings:Endpoint": "https://[accountname].openai.azure.com/"` with the AZURE_OPENAI_ENDPOINT value from the .env file.
1. Update `"ApplicationInsights:ConnectionString": "[connectionstring]"` with the APP_INSIGHTS_CONNECTION_STRING value from the .env file.

### Running the ChatAPI and Frontend App

#### 1. Start the ChatAPI

##### If running on Codespaces

1. Navigate to `src/ChatAPI`.
2. Run the following command to trust the development certificate:

   ```sh
   dotnet dev-certs https --trust
   ```

3. Start the application:

   ```sh
   dotnet run
   ```

4. In the **Ports** tab, right-click and select the **Port Visibility** option to set port **63280** as **Public**.
5. Copy the URL for **63280** port.

   ![Ports Dialog for CodeSpaces](./media/ports-dialog.png)

##### If running locally on Visual Studio or VS Code

1. Navigate to `src\ChatAPI`.
2. Press **F5** or select **Run** to start the application.
3. Copy the URL from the browser window that opens.

#### 2. Run the Frontend App

1. Open a new terminal. Navigate to the `frontend` folder.
1. Copy and run the following:

   ```sh
   npm install
   npm start
   ```

##### If running locally

1. Open your browser and navigate to <http://localhost:4200/>.

##### If running on Codespaces

1. From the **PORTS** tab, search for the port with the label **Frontend app**. Hover over the address and choose **Open in Browser** (second icon) to access the frontend application.

#### 3. Start a Conversation

1. Open the frontend app.
1. Start a new conversation.
1. Send the sequence of user prompts below to see the agents in action:

    ```text
    Who can help me here?
    Transfer $50 to my friend. (When prompted, give it an account number ranging from Acc001 to Acc009 and any email address)
    Looking for a Savings account with high interest rate.
    File a complaint about theft from my account.
    How much did I spend on groceries? (If prompted, say over the last 6 months)
    Provide me a statement of my account. (If prompted, give it an account number ranging from Acc001 to Acc009)
   ```

#### 4. Stop the Application

- In the frontend terminal, press **Ctrl + C** to stop the application.
- In your IDE press **Shift + F5** or stop the debugger.
- If you are in CodeSpaces, go to each terminal and press **Ctrl + C**.

## Clean up

1. Open a terminal and navigate to the /infra directory in this solution.

1. Type azd down

   ```bash
   azd down --force --purge
   ```
