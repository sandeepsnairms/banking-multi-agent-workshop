# Banking Agents API

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
