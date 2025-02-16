# Banking Agents API

## Description

This is a sample that exposes turn-by-turn conversation with a multi-agent banking application using the Azure OpenAI gpt-4o model, via REST API delivered through FastAPI that stores chat history and message state in Azure Cosmos DB. The agents are implemented using LangGraph. Each agent is a state machine that can handle a few banking-related tasks. A simple CLI tool is provided that consumes the REST API and allows the user to interact with the agents. 

### How it works
1. `banking_agents_native_api.py`: This is a FastAPI server that exposes a REST API to interact with the banking multi-agent program build using LangGraph. It will:
   - Save chat memory in Azure CosmosDB using the native LangGraph [checkpoint implementation for Azure Cosmos DB](https://pypi.org/project/langgraph-checkpoint-cosmosdb/).
   - Provide operations including `/tenant/{tenantId}/user/{userId}/sessions` to create a sessionId (used as [thread_id](https://langchain-ai.github.io/langgraph/concepts/persistence/#threads) in langgraph) in UserData for each user.
   - Create user data in Cosmos DB with [hierarchical partitioning](https://learn.microsoft.com/azure/cosmos-db/hierarchical-partition-keys) so that multitenancy for users and sessions can be supported.
   - Maintain an "active agent" in the UserData for each session for more deterministic agent routing after collecting user input/prompt.
   - Return user prompt and agent responses in the last "turn" of the conversation to the client.
2. `banking_agents_native.py`: This defines the agents and tools in the graph with routing logic. It will:
   - Always route to supervisor agent first, which either: 
     - Handles the question and hands of to another agent, or...
     - Routes directly to another agent if that agent was the last "active agent" in the session.
   - Each sub-agent either:
     - Routes to another agent.
     - Calls a tool (functionality).
3. `banking_agents_test_cli.py`: This is a simple CLI tool that consumes the FastAPI server endpoint. It will:
   - Creates a sessionId using `POST /tenant/{tenantId}/user/{userId}/sessions` endpoint
   - Sends messages to the agent using `POST /tenant/{tenantId}/user/{userId}/sessions/{sessionId}/completion` endpoint and prints the responses to the console
   - Deletes session using `DELETE /tenant/{tenantId}/user/{userId}/sessions/{sessionId}` endpoint when user types "exit".
5. `azure_open_ai.py`: This is a utility class that defines Azure OpenAI credentials and initialises Azure OpenAI API client
6. `azure_cosmos_db.py`: This is a utility class that defines Azure Cosmos DB credentials, Database and Container name for storing conversation memory


## How to run the project

1. Clone the repository.

2. Have the right system environment variables set up: 

    ```bash
    export AZURE_OPENAI_API_KEY=
    export AZURE_OPENAI_ENDPOINT=
    export AZURE_OPENAI_CHATDEPLOYMENTID=
    export COSMOSDB_ENDPOINT=
    export COSMOSDB_KEY=
    ```
3. install the dependencies:
    ```bash
    pip install fastapi
    pip install "fastapi[standard]"
    pip install azure-cosmos
    pip install openai
    pip install langgraph
    pip install langgraph-checkpoint-cosmosdb
    pip install tiktoken
    pip install langchain-openai
    pip install uvicorn
    ```
4. Start the agent api server
    ```bash
    uvicorn src.app.banking_agents_native_api:app --reload --host 0.0.0.0 --port 8000
    ```
   View and test out the swagger UI at http://localhost:8000/docs

5. Run cli test tool that interacts with agent api server
    ```bash
    python src/app/banking_agents_test_cli.py
    ```
![Demo](./media/demo.gif)
