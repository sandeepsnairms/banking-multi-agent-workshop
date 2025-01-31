# Banking Agents API

## Description

This is a sample that exposes turn-by-turn conversation with a multi-agent banking application using the Azure OpenAI gpt-4o, via a hand-cranked REST API delivered through FastAPI that stores chat history and message state in Azure Cosmos DB. The agents are implemented using LangGraph. Each agent is a state machine that can handle a few banking-related tasks. A simple CLI tool is provided that consumes the REST API and allows the user to interact with the agents. 

### How it works
1. `banking_agents_api.py`: This is a FastAPI server that exposes a REST API to interact with the banking multi-agent program build using LangGraph. It will:
   - retrieve the conversation history from CosmosDB and reconstruct the message state for the graph
   - add the incoming message to the graph state and store it in CosmosDB
   - create a conversation id and return it to the client if this is the first message in the conversation
   - uses utility functions in `chat_history.py` to retrieve the conversation history from CosmosDB and store the incoming message.
   - set an `active_agent` and store it in CosmosDB to be used for routing the message to the correct agent.
   - iterate over the graph stream defined in the `banking_agents.py` and get the response from the agent.
2. `banking_agents.py`: This defines the agents, and routes to last active agent based on retrieved active_agent from CosmosDB and if/else conditions.
3. `banking_agents_test_cli.py`: This is a simple CLI tool that consumes the FastAPI server endpoint. It will:
   - send a message to the agent
   - retrieve the response from the agent and store conversation id for subsequent messages
   - print the responses to the console
4. `azure_cosmosdb.py`: This is a utility class that defines CosmosDB credentias and initialises CosmosClient
5. `azure_open_ai.py`: This is a utility class that defines Azure OpenAI credentials and initialises OpenAI API client


The banking agent is a simple state machine that can handle a few banking-related tasks. The agent is implemented as a FastAPI server that exposes a REST API. The API is used by a simple CLI tool that allows the user to interact with the agent.

## How to run the project

1. Clone the repository.

2. Have the right system environment variables set up: 

    ```bash
    export AZURE_OPENAI_API_KEY=
    export AZURE_OPENAI_ENDPOINT=
    export AZURE_OPENAI_CHATDEPLOYMENTID=
    export COSMOSDB_AI_ENDPOINT=
    export COSMOSDB_AI_KEY=
    export COSMOSDB_CONTAINER_NAME="agents"
    ```
3. install the dependencies:
    ```bash
    pip install fastapi
    pip install "fastapi[standard]"
    pip install azure-cosmos
    pip install langgraph
    pip install langchain-openai
    ```
4. Start the agent api server
    ```bash
    fastapi dev src/app/banking_agents_api.py
    ```

5. Run cli test tool that interacts with agent api server
    ```bash
    python src/app/banking_agents_test_cli.py
    ```
![Demo](./media/demo.gif)
