# Multi-Agent AI application using LangGraph

## Description

This is a sample that exposes turn-by-turn conversation with a multi-agent banking application using the Azure OpenAI gpt-4o model, via REST API delivered through FastAPI that stores chat history and message state in Azure Cosmos DB. The agents are implemented using LangGraph. Each agent is a state machine that can handle a few banking-related tasks. A simple CLI tool is provided that consumes the REST API and allows the user to interact with the agents. This sample also comes with an Angular Frontend that can be optionally deployed with this sample.

## CLI User Experience

![Demo](./media/demo.gif)

### How it works

1. `banking_agents_api.py`: This is a FastAPI server that exposes a REST API to interact with the banking multi-agent program build using LangGraph. It will:
   - Save chat memory in Azure CosmosDB using the native LangGraph [checkpoint implementation for Azure Cosmos DB](https://pypi.org/project/langgraph-checkpoint-cosmosdb/).
   - Provide operations including `/tenant/{tenantId}/user/{userId}/sessions` to create a sessionId (used as [thread_id](https://langchain-ai.github.io/langgraph/concepts/persistence/#threads) in langgraph) in UserData for each user.
   - Create user data in Cosmos DB with [hierarchical partitioning](https://learn.microsoft.com/azure/cosmos-db/hierarchical-partition-keys) so that multitenancy for users and sessions can be supported.
   - Return user prompt and agent responses in the last "turn" of the conversation to the client.
1. `banking_agents.py`: This defines the agents and tools in the graph with routing logic. It will:
   - Always route to coordinator agent first, which hands over the conversation to the appropriate sub-agent.
   - Each sub-agent either:
     - Routes to another agent.
     - Calls a tool (functionality). Tools are implemented in a unified [MCP](https://en.wikipedia.org/wiki/Model_Context_Protocol) server that supports two deployment modes:
       - **Direct Mode**: MCP server runs embedded within the application (see `/app/tools/mcp_server.py`)
       - **HTTP Mode**: MCP server runs as a separate HTTP microservice with enterprise security (see `/mcpserver/` folder with JWT authentication, RBAC, rate limiting, audit logging, and input validation)
       
1. `banking_agents_api_cli.py`: This is a simple CLI tool that consumes the FastAPI server endpoint. It will:
   - Creates a sessionId using `POST /tenant/{tenantId}/user/{userId}/sessions` endpoint
   - Sends messages to the agent using `POST /tenant/{tenantId}/user/{userId}/sessions/{sessionId}/completion` endpoint and prints the responses to the console
   - Deletes session using `DELETE /tenant/{tenantId}/user/{userId}/sessions/{sessionId}` endpoint when user types "exit".
1. `azure_open_ai.py`: This is a utility class that defines Azure OpenAI credentials and initialises Azure OpenAI API client
1. `azure_cosmos_db.py`: This is a utility class that defines Azure Cosmos DB credentials, Database and Container name for storing conversation memory

## Getting Started

Complete the following tasks in order to prepare your environment for this sample.

### Prerequisites

- Laptop or workstation with **administrator rights** (Alternatively you can run this sample virtually in [GitHub Codespaces](https://github.com/features/codespaces))
- Azure subscription with **owner rights**
- Subscription access to Azure OpenAI service. Start here to [Request Access to Azure OpenAI Service](https://aka.ms/oaiapply). If you have access, see below for ensuring enough quota to deploy.

  #### Checking Azure OpenAI quota limits

  For this sample to deploy successfully, there needs to be enough Azure OpenAI quota for the models used by this sample within your subscription. This sample deploys a new Azure OpenAI account with two models, **gpt-4o with 30K tokens** per minute and **text-3-embedding-small with 5k tokens** per minute. For more information on how to check your model quota and change it, see [Manage Azure OpenAI Service Quota](https://learn.microsoft.com/azure/ai-services/openai/how-to/quota)

  #### Azure Subscription Permission Requirements

  This solution deploys a [user-assigned managed identity](https://learn.microsoft.com/entra/identity/managed-identities-azure-resources/overview) and defines then applies Azure Cosmos DB and Azure OpenAI RBAC permissions to this as well as your own Service Principal Id. You will need the following Azure RBAC roles assigned to your identity in your Azure subscription or [Subscription Owner](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/privileged#owner) access which will give you both of the following.

  - [Manged Identity Contributor](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/identity#managed-identity-contributor)
  - [Cosmos DB Operator](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/databases#cosmos-db-operator)
  - [Cognitive Services OpenAI User](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles/ai-machine-learning#cognitive-services-openai-user)

### GitHub Codespaces

You can run this sample app using GitHub Codespaces (requires a GitHub account). The button will open a web-based VS Code instance in your browser:

1. Open the template (this may take several minutes):

   [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/AzureCosmosDB/banking-multi-agent-workshop?branch=main&devcontainer_path=.devcontainer%2Fpython%2Fdevcontainer.json)

2. Move on to the [Deployment](readme.md#deployment) section.

#### Local Environment using VS Code Dev Containers

1. Install [Docker Desktop](https://docs.docker.com/desktop/), and [VS Code](https://code.visualstudio.com/Download) along with the [Dev Containers extension](https://code.visualstudio.com/docs/devcontainers/tutorial#_install-the-extension) extension.

2. Clone the repository:

   ```bash
   git clone https://github.com/AzureCosmosDB/banking-multi-agent-workshop/
   cd banking-multi-agent-workshop
   ```

3. Open the repository in VS Code and select **Reopen in Container** when prompted. When asked to **Select a devcontainer.json file**, select the **Python Development Container**.

4. Wait for the container to build and start. This is a one time operation and may take a few minutes.

5. Move on to the [Deployment](readme.md#deployment) section.

#### Local Environment without VS Code Dev Containers

1. To run the workshop locally on your machine, install the following:

   - [Docker Desktop](https://docs.docker.com/desktop/)
   - [Git](https://git-scm.com/downloads)
   - [Azure Developer CLI (azd)](https://aka.ms/install-azd)
   - [Node.js](https://nodejs.org/en/download/)
   - [Angular CLI](https://angular.dev/installation#install-angular-cli)
   - [Python 3.12+](https://www.python.org/downloads/)
   - Your Python IDE or [VS Code](https://code.visualstudio.com/Download) with [Python Extension](https://marketplace.visualstudio.com/items?itemName=ms-python.python)

2. Clone the repository and navigate to the folder:

   ```bash
   git clone https://github.com/AzureCosmosDB/banking-multi-agent-workshop/
   cd banking-multi-agent-workshop
   ```

3. Move on to the [Deployment](readme.md#deployment) section.

### Deployment

1. Navigate to the Python folder:

   ```bash
   cd /path/to/banking-multi-agent-workshop/02_completed/python
   ```

2. Log in to Azure using AZD. Follow the prompts to complete authentication:

   ```bash
   azd auth login
   ```

3. Provision the Azure services and deploy the application:

   ```bash
   azd up
   ```

   This step will take approximately 10-15 minutes and will:
   - Create all required Azure resources (Cosmos DB, Azure OpenAI, Container Apps, etc.)
   - Deploy both the Banking API and MCP Server as Container Apps
   - Configure authentication and networking between services
   - Set up environment variables automatically

   > [!IMPORTANT]
   > If you encounter any errors during the deployment, rerun `azd up` to continue the deployment from where it left off. This will not create duplicate resources.

4. When the deployment completes successfully, you will see endpoint URLs for both services:

   ```bash
   SUCCESS: Your application was deployed to Azure in X minutes.
   
   Banking API: https://ca-webapi-xxxxx.azurecontainerapps.io/
   MCP Server: https://ca-mcpserver-xxxxx.azurecontainerapps.io/
   ```

5. The deployment will automatically:
   - Load sample banking data (accounts, transactions, offers)
   - Configure MCP client-server communication
   - Set up authentication tokens
   - Update environment variables

### Setting up local development

When you deploy this solution, it automatically configures `.env` files with the required Azure endpoints and authentication tokens for both the main application and MCP server.

To run the solution locally after deployment:

1. Navigate to the python folder and install dependencies:

   ```bash
   cd /path/to/banking-multi-agent-workshop/02_completed/python
   ```

2. Create and activate a virtual environment:

   **Linux/Mac:**
   ```shell
   python -m venv .venv
   source .venv/bin/activate
   ```

   **Windows:**
   ```shell
   python -m venv .venv
   .venv\Scripts\Activate.ps1
   ```

3. Install the main application dependencies:

   ```shell
   pip install -r src/app/requirements.txt
   ```

4. Install MCP server dependencies:

   ```shell
   pip install -r ../mcpserver/requirements.txt
   ```

### Running the solution locally

This solution uses **Model Context Protocol (MCP)** architecture with the MCP server running as a separate HTTP service.

#### Terminal 1 - Start the MCP Server:
```bash
# Navigate to mcpserver folder and ensure virtual environment is activated
cd /path/to/banking-multi-agent-workshop/02_completed/mcpserver
source .venv/bin/activate  # On Windows: .venv\Scripts\activate

# Start the MCP server (run from mcpserver root)
PYTHONPATH=src python3 -m uvicorn src.mcp_http_server:app --host 0.0.0.0 --port 8080
```

#### Terminal 2 - Start the Banking API:
```bash
# Navigate to python folder and ensure virtual environment is activated
cd /path/to/banking-multi-agent-workshop/02_completed/python
source .venv/bin/activate  # On Windows: .venv\Scripts\activate

# Start the main banking API
uvicorn src.app.banking_agents_api:app --reload --host 0.0.0.0 --port 8000
```

#### Terminal 3 - Start the Frontend (optional):
```bash
# Navigate to frontend folder
cd /path/to/banking-multi-agent-workshop/02_completed/frontend

# Install and start frontend
npm install
npm start
```

**Access the applications:**
- Banking API: <http://localhost:8000/docs>
- MCP Server: <http://localhost:8080/docs>
- Frontend (if started): <http://localhost:4200>

#### Verify the setup:
```bash
# Test MCP server health
curl http://localhost:8080/health

# Test MCP server authentication (simple token mode)
curl http://localhost:8080/auth/token

# Test banking API
curl http://localhost:8000/health
```

#### Run the Frontend App locally

1. Update the `apiUrl` values in `frontend/src/environments/environment.ts` file with the API endpoint <http://localhost:8000/>
1. Open a new terminal, navigate to the `frontend` folder and run the following to start the application:

   ```sh
   npm install
   npm start
   ```

1. Open your browser and navigate to <http://localhost:8000/>.

#### Run the Frontend App on Codespaces

1. Open a new terminal, navigate to the `frontend` folder and run the following to start the application:

   ```sh
   npm install
   npm start
   ```

1. From the **Ports** tab:
   1. Right-click and select the **Port Visibility** option to set port **ChatAPI (8000)** as **Public**.
   1. For the port with the label **Frontend app**. Hover over the address and choose **Open in Browser** (second icon) to access the frontend application.

### Start a Conversation

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

### Stop the Application

- In the frontend terminal, press **Ctrl + C** to stop the application.
- In your IDE press **Shift + F5** or stop the debugger.
- If you are in CodeSpaces, go to each terminal and press **Ctrl + C**.
- **For HTTP MCP Mode:** Stop both terminals (MCP server and main application).

### Troubleshooting

#### HTTP MCP Mode Issues

**MCP Server won't start:**
```shell
# Navigate to mcpserver directory first
cd /path/to/banking-multi-agent-workshop/mcpserver

# Check if virtual environment is activated
source .venv/bin/activate

# Verify dependencies are installed
pip install -r requirements.txt

# Check for port conflicts
lsof -i :8080

# Check for Python/import errors with correct PYTHONPATH
PYTHONPATH=src python -c "import src.mcp_http_server; print('Server imports successfully')"

# Start with proper PYTHONPATH
PYTHONPATH=src python3 -m uvicorn src.mcp_http_server:app --host 0.0.0.0 --port 8080
```

**Main application can't connect to MCP server:**
```shell
# Verify MCP server is running
curl http://localhost:8080/health

# Check environment variables are set
echo $USE_REMOTE_MCP_SERVER
echo $MCP_SERVER_ENDPOINT

# Test authentication endpoint
curl -X POST http://localhost:8080/auth/token

# Check application logs for connection errors
```

**Security and Authentication errors:**
```shell
# Verify JWT secret key is properly configured
echo $JWT_SECRET

# Check if authentication is working
curl -X POST http://localhost:8080/auth/token

# Run security validation tests
cd /path/to/banking-multi-agent-workshop/mcpserver
python test_security.py

# Check server audit logs for security events
```

**Rate limiting errors (429 status):**
```shell
# Check current rate limit settings
echo $RATE_LIMIT_REQUESTS
echo $RATE_LIMIT_WINDOW_SECONDS

# Wait for rate limit window to reset or adjust limits
export RATE_LIMIT_REQUESTS=200
```

#### Common Issues

**Module not found errors:**
```shell
# Ensure virtual environment is activated
source .venv/bin/activate

# Reinstall dependencies
pip install -r requirements.txt
```

**Azure connection errors:**
- Verify your `.env` file contains valid Azure endpoints
- Check Azure service authentication and permissions
- Ensure Azure resources are deployed and accessible

## Clean up

1. Open a terminal and navigate to the /infra directory in this solution.

1. Type azd down

   ```bash
   azd down --force --purge
   ```
