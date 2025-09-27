# Module 06 (NEW) - Converting to Model Context Protocol (MCP)

[< Lessons Learned, Agent Futures, Q&A](./Module-05.md) - **[Home](Home.md)**

## Overview

In this module, you'll learn how to convert your multi-agent banking application to use **Model Context Protocol (MCP)**. MCP provides a standardized way for AI applications to integrate with external tools and data sources, offering better modularity and reusability.

## Why MCP?

MCP addresses several challenges in AI tool integration:

- **Standardization**: Common protocol for tool integration across different AI systems
- **Loose Coupling**: AI models can interact with tools without tight dependencies
- **Reusability**: Tools can be shared across multiple AI applications
- **Team Independence**: Different teams can develop AI logic and business tools separately

## Learning Objectives

By the end of this module, you will:
- Understand MCP's benefits for multi-agent applications
- Convert LangChain tools to MCP tools using `@mcp.tool()` decorators
- Configure MCP client-server architecture
- Test the complete MCP-enabled banking system

## Module Exercises

1. [Activity 1: Understanding the MCP Architecture](#activity-1-understanding-the-mcp-architecture)
2. [Activity 2: Update Banking Agents for MCP](#activity-2-update-banking-agents-for-mcp)
3. [Activity 3: Start the MCP Server](#activity-3-start-the-mcp-server)
4. [Activity 4: Test the Complete System](#activity-4-test-the-complete-system)

## Activity 1: Understanding the MCP Architecture

### Current Architecture (LangChain Tools)
Your current banking application uses LangChain tools directly:

```python
# Direct tool imports
from src.app.tools.bank_balance import bank_balance
from src.app.tools.bank_transfer import bank_transfer

# Tools used directly by agents
transactions_tools = [bank_balance, bank_transfer, ...]
transactions_agent = create_react_agent(model, transactions_tools, ...)
```

### MCP Architecture
With MCP, tools are provided by a separate server:

```python
# MCP client connects to server
from langchain_mcp_adapters.client import MultiServerMCPClient
from langchain_mcp_adapters.tools import load_mcp_tools

# Tools loaded from MCP server
client = MultiServerMCPClient(config)
session = client.session("banking_tools")
all_tools = await load_mcp_tools(session)
```

### Key Benefits
- **Separation of Concerns**: Business logic (tools) separated from AI orchestration
- **Protocol Standardization**: Uses JSON-RPC for reliable communication
- **Development Independence**: Tool updates don't require AI agent redeployment

## Activity 2: Update Banking Agents for MCP

### MCP Server Tools
The MCP server (in `/mcpserver/`) provides these banking tools using `@mcp.tool()` decorators:

```python
# Example from mcp_http_server.py
@mcp.tool()
def get_offer_information(user_prompt: str, accountType: str) -> list[dict[str, Any]]:
    """Provide information about a product based on the user prompt."""
    vectors = generate_embedding(user_prompt)
    search_results = vector_search(vectors, accountType)
    return search_results

@mcp.tool()
def create_account(account_holder: str, balance: float, config: RunnableConfig) -> str:
    """Create a new bank account for a user."""
    # Implementation details...
    return f"Account created successfully with number: {new_account_number}"
```

### Update banking_agents.py

Replace your `src/app/banking_agents.py` with this MCP-enabled version:

<details>
<summary><strong>Complete banking_agents.py Implementation (click to expand)</strong></summary>

```python
import logging
import os
import sys
import uuid
import asyncio
import json
from langchain_core.messages import ToolMessage, SystemMessage
from langchain.schema import AIMessage
from langchain_mcp_adapters.client import MultiServerMCPClient
from langchain_mcp_adapters.tools import load_mcp_tools
from typing import Literal
from langgraph.graph import StateGraph, START, MessagesState
from langgraph.prebuilt import create_react_agent
from langgraph.types import Command, interrupt
from langgraph_checkpoint_cosmosdb import CosmosDBSaver
from langgraph.checkpoint.memory import MemorySaver
from langsmith import traceable
from src.app.services.azure_open_ai import model
#from src.app.services.local_model import model  # Use local model for testing
from src.app.services.azure_cosmos_db import DATABASE_NAME, checkpoint_container, chat_container, \
    update_chat_container, patch_active_agent

# Uncomment these if you want to use custom OAuth configuration
# try:
#     from fastmcp.client.auth import OAuth
# except ImportError:
#     print("fastmcp not available, OAuth configuration will not be available")

local_interactive_mode = False

logging.basicConfig(level=logging.DEBUG)

PROMPT_DIR = os.path.join(os.path.dirname(__file__), 'prompts')


def load_prompt(agent_name):
    file_path = os.path.join(PROMPT_DIR, f"{agent_name}.prompty")
    print(f"Loading prompt for {agent_name} from {file_path}")
    try:
        with open(file_path, "r", encoding="utf-8") as file:
            return file.read().strip()
    except FileNotFoundError:
        print(f"Prompt file not found for {agent_name}, using default placeholder.")
        return "You are an AI banking assistant."


# Tool filtering utility
def filter_tools_by_prefix(tools, prefixes):
    return [tool for tool in tools if any(tool.name.startswith(prefix) for prefix in prefixes)]

# Global variables for persistent session management
_mcp_client = None
_session_context = None
_persistent_session = None

async def setup_agents():
    global coordinator_agent, customer_support_agent, transactions_agent, sales_agent
    global _mcp_client, _session_context, _persistent_session

    print("🚀 Starting unified Banking Tools MCP client...")
    
    # Load authentication configuration
    try:
        from dotenv import load_dotenv
        load_dotenv(override=False)
        simple_token = os.getenv("MCP_AUTH_TOKEN")
        github_client_id = os.getenv("GITHUB_CLIENT_ID")
        github_client_secret = os.getenv("GITHUB_CLIENT_SECRET")
        
        print("🔐 Client Authentication Configuration:")
        print(f"   Simple Token: {'SET' if simple_token else 'NOT SET'}")
        print(f"   GitHub OAuth: {'SET' if github_client_id and github_client_secret else 'NOT SET'}")
        
        # Determine authentication mode (same logic as server)
        if github_client_id and github_client_secret:
            auth_mode = "github_oauth"
            print("   Mode: GitHub OAuth (Production)")
        elif simple_token:
            auth_mode = "simple_token" 
            print(f"   Mode: Simple Token (Development)")
            print(f"   Token: {simple_token[:8]}...")
        else:
            auth_mode = "none"
            print("   Mode: No Authentication")
            
    except ImportError:
        auth_mode = "none"
        simple_token = None
        print("🔐 Client Authentication: Dependencies unavailable - no auth")
    
    print("   - Transport: streamable_http")
    print("   - Server URL: "+os.getenv("MCP_SERVER_BASE_URL", "http://localhost:8080")+"/mcp/")
    print(f"   - Authentication: {auth_mode.upper()}")
    print("   - Status: Ready to connect\\n")
    
    # MCP Client configuration based on authentication mode
    client_config = {
        "banking_tools": {
            "transport": "streamable_http",
            "url": os.getenv("MCP_SERVER_BASE_URL", "http://localhost:8080")+"/mcp/",
        }
    }
    
    # Add authentication if configured
    if auth_mode == "simple_token" and simple_token:
        # Add bearer token header for simple token auth
        client_config["banking_tools"]["headers"] = {
            "Authorization": f"Bearer {simple_token}"
        }
        print("🔐 Added Bearer token authentication to client")
    elif auth_mode == "github_oauth":
        # Enable OAuth for GitHub authentication
        client_config["banking_tools"]["auth"] = "oauth"
        print("🔐 Enabled OAuth authentication for client")
    
    _mcp_client = MultiServerMCPClient(client_config)
    print("✅ MCP Client initialized successfully")

    # Create a persistent session that stays alive for the application lifetime
    _session_context = _mcp_client.session("banking_tools")
    _persistent_session = await _session_context.__aenter__()
    
    # Load tools using the persistent session
    all_tools = await load_mcp_tools(_persistent_session)

    print("[DEBUG] All tools registered from unified MCP server:")
    for tool in all_tools:
        print("  -", tool.name)

    # Assign tools to agents based on tool name prefix
    coordinator_tools = filter_tools_by_prefix(all_tools, ["transfer_to_"])
    support_tools = filter_tools_by_prefix(all_tools, ["service_request", "get_branch_location", "transfer_to_sales_agent", "transfer_to_transactions_agent"])
    sales_tools = filter_tools_by_prefix(all_tools, ["get_offer_information", "create_account", "calculate_monthly_payment", "transfer_to_customer_support_agent", "transfer_to_transactions_agent"])
    transactions_tools = filter_tools_by_prefix(all_tools, ["bank_transfer", "get_transaction_history", "bank_balance", "transfer_to_customer_support_agent"])

    # Create agents with their respective tools
    coordinator_agent = create_react_agent(model, coordinator_tools, state_modifier=load_prompt("coordinator_agent"))
    customer_support_agent = create_react_agent(model, support_tools, state_modifier=load_prompt("customer_support_agent"))
    sales_agent = create_react_agent(model, sales_tools, state_modifier=load_prompt("sales_agent"))
    transactions_agent = create_react_agent(model, transactions_tools, state_modifier=load_prompt("transactions_agent"))

async def cleanup_persistent_session():
    """Clean up the persistent MCP session when the application shuts down"""
    global _session_context, _persistent_session
    
    if _session_context is not None and _persistent_session is not None:
        try:
            # Properly exit the async context manager
            await _session_context.__aexit__(None, None, None)
            print("MCP persistent session cleaned up successfully")
        except Exception as e:
            print(f"Error cleaning up MCP session: {e}")
        finally:
            _session_context = None
            _persistent_session = None

@traceable(run_type="llm")
async def call_coordinator_agent(state: MessagesState, config) -> Command[Literal["coordinator_agent", "human"]]:
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    userId = config["configurable"].get("userId", "UNKNOWN_USER_ID")
    tenantId = config["configurable"].get("tenantId", "UNKNOWN_TENANT_ID")

    print(f"Calling coordinator agent with Thread ID: {thread_id}")

    try:
        activeAgent = chat_container.read_item(item=thread_id, partition_key=[tenantId, userId, thread_id]).get(
            'activeAgent', 'unknown')
    except Exception as e:
        logging.debug(f"No active agent found: {e}")
        activeAgent = None

    if activeAgent is None:
        if local_interactive_mode:
            update_chat_container({
                "id": thread_id,
                "tenantId": "cli-test",
                "userId": "cli-test",
                "sessionId": thread_id,
                "name": "cli-test",
                "age": "cli-test",
                "address": "cli-test",
                "activeAgent": "unknown",
                "ChatName": "cli-test",
                "messages": []
            })

    print(f"Active agent from point lookup: {activeAgent}")

    if activeAgent not in [None, "unknown", "coordinator_agent"]:
        print(f"Routing straight to last active agent: {activeAgent}")
        return Command(update=state, goto=activeAgent)
    else:
        response = await coordinator_agent.ainvoke(state)
        return Command(update=response, goto="human")


@traceable(run_type="llm")
async def call_customer_support_agent(state: MessagesState, config) -> Command[Literal["customer_support_agent", "human"]]:
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    if local_interactive_mode:
        patch_active_agent("cli-test", "cli-test", thread_id, "customer_support_agent")
    response = await customer_support_agent.ainvoke(state)
    return Command(update=response, goto="human")


@traceable(run_type="llm")
async def call_sales_agent(state: MessagesState, config) -> Command[Literal["sales_agent", "human"]]:
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    if local_interactive_mode:
        patch_active_agent("cli-test", "cli-test", thread_id, "sales_agent")
    response = await sales_agent.ainvoke(state, config)
    return Command(update=response, goto="human")


@traceable(run_type="llm")
async def call_transactions_agent(state: MessagesState, config) -> Command[Literal["transactions_agent", "human"]]:
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    userId = config["configurable"].get("userId", "UNKNOWN_USER_ID")
    tenantId = config["configurable"].get("tenantId", "UNKNOWN_TENANT_ID")
    if local_interactive_mode:
        patch_active_agent("cli-test", "cli-test", thread_id, "transactions_agent")
    state["messages"].append({
        "role": "system",
        "content": f"If tool to be called requires tenantId='{tenantId}', userId='{userId}', thread_id='{thread_id}', include these in the JSON parameters when invoking the tool. Do not ask the user for them, there are included here for your reference."
    })
    response = await transactions_agent.ainvoke(state, config)
    # explicitly remove the system message added above from response
    print(f"DEBUG: transactions_agent response: {response}")
    if isinstance(response, dict) and "messages" in response:
        response["messages"] = [
            msg for msg in response["messages"]
            if not isinstance(msg, SystemMessage)
        ]
    return Command(update=response, goto="human")


@traceable
def human_node(state: MessagesState, config) -> None:
    interrupt(value="Ready for user input.")
    return None

def get_active_agent(state: MessagesState, config) -> str:
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    userId = config["configurable"].get("userId", "UNKNOWN_USER_ID")
    tenantId = config["configurable"].get("tenantId", "UNKNOWN_TENANT_ID")
    # print("DEBUG: get_active_agent called with state:", state)

    activeAgent = None

    # Search for last ToolMessage and try to extract `goto`
    for message in reversed(state['messages']):
        if isinstance(message, ToolMessage):
            try:
                content_json = json.loads(message.content)
                activeAgent = content_json.get("goto")
                if activeAgent:
                    print(f"DEBUG: Extracted activeAgent from ToolMessage: {activeAgent}")
                    break
            except Exception as e:
                print(f"DEBUG: Failed to parse ToolMessage content: {e}")

    # Fallback: Cosmos DB lookup if needed
    if not activeAgent:
        try:
            thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
            print(f"DEBUG: thread_id in get_active_agent: {thread_id}")
            activeAgent = chat_container.read_item(
                item=thread_id,
                partition_key=[tenantId, userId, thread_id]
            ).get('activeAgent', 'unknown')
            print(f"Active agent from DB fallback: {activeAgent}")
        except Exception as e:
            print(f"Error retrieving active agent from DB: {e}")
            activeAgent = "unknown"

    return activeAgent


builder = StateGraph(MessagesState)
builder.add_node("coordinator_agent", call_coordinator_agent)
builder.add_node("customer_support_agent", call_customer_support_agent)
builder.add_node("sales_agent", call_sales_agent)
builder.add_node("transactions_agent", call_transactions_agent)
builder.add_node("human", human_node)

builder.add_edge(START, "coordinator_agent")

builder.add_conditional_edges(
    "coordinator_agent",
    get_active_agent,
    {
        "sales_agent": "sales_agent",
        "transactions_agent": "transactions_agent",
        "customer_support_agent": "customer_support_agent",
        "coordinator_agent": "coordinator_agent",  # fallback
    }
)

checkpointer = CosmosDBSaver(database_name=DATABASE_NAME, container_name=checkpoint_container)
graph = builder.compile(checkpointer=checkpointer)


def interactive_chat():
    thread_config = {"configurable": {"thread_id": str(uuid.uuid4()), "userId": "Mark", "tenantId": "Contoso"}}
    global local_interactive_mode
    local_interactive_mode = True
    print("Welcome to the interactive multi-agent shopping assistant.")
    print("Type 'exit' to end the conversation.\n")

    user_input = input("You: ")

    while user_input.lower() != "exit":
        input_message = {"messages": [{"role": "user", "content": user_input}]}
        response_found = False

        for update in graph.stream(input_message, config=thread_config, stream_mode="updates"):
            for node_id, value in update.items():
                if isinstance(value, dict) and value.get("messages"):
                    last_message = value["messages"][-1]
                    if isinstance(last_message, AIMessage):
                        print(f"{node_id}: {last_message.content}\n")
                        response_found = True

        if not response_found:
            print("DEBUG: No AI response received.")

        user_input = input("You: ")


if __name__ == "__main__":
    if sys.platform == "win32":
        print("Setting up Windows-specific event loop policy...")
        # Set the event loop to ProactorEventLoop on Windows
        asyncio.set_event_loop_policy(asyncio.WindowsSelectorEventLoopPolicy())
    asyncio.run(setup_agents())
```
</details>

## Activity 3: Start the MCP Server

The MCP server is provided in the `mcpserver/` directory and includes all banking tools implemented with native `@mcp.tool()` decorators.

### 1. Navigate to MCP Server Directory
```bash
cd /path/to/banking-multi-agent-workshop/01_exercises/mcpserver
```

### 2. Install Dependencies
```bash
python -m venv .venv
source .venv/bin/activate  # On Windows: .venv\Scripts\activate
pip install -r requirements.txt
```

### 3. Start the MCP Server
```bash
PYTHONPATH=src python3 -m uvicorn src.mcp_http_server:app --host 0.0.0.0 --port 8080
```

You should see output like:
```
🚀 Initializing MCP Server...
✅ Banking Tools MCP server initialized with Simple Token Auth
🔐 DEVELOPMENT AUTHENTICATION: Bearer token required
🌐 Server will be available at: http://0.0.0.0:8080
INFO:     Uvicorn running on http://0.0.0.0:8080
```

### Understanding the MCP Tools

The server provides these banking tools:
- **Sales Tools**: `get_offer_information`, `create_account`, `calculate_monthly_payment`
- **Transaction Tools**: `bank_balance`, `bank_transfer`, `get_transaction_history`
- **Support Tools**: `service_request`, `get_branch_location`
- **Agent Transfer Tools**: `transfer_to_*_agent` for routing between agents

## Activity 4: Test the Complete System

### 1. Set Environment Variables
```bash
# Set MCP configuration
export USE_REMOTE_MCP_SERVER=true
export MCP_SERVER_BASE_URL=http://localhost:8080
export MCP_AUTH_TOKEN=banking-server-prod-token-2025
```

### 2. Start the Banking API
```bash
cd /path/to/banking-multi-agent-workshop/01_exercises/python
uvicorn src.app.banking_agents_api:app --reload --host 0.0.0.0 --port 8000
```

### 3. Start the Frontend (Optional)
```bash
cd /path/to/banking-multi-agent-workshop/01_exercises/frontend
npm install
ng serve
```

### 4. Test the System

1. Open your browser to `http://localhost:4200` (if using frontend) or `http://localhost:8000/docs` (API docs)
2. Test with these prompts:

```text
What's my account balance for account 12345?
Transfer $100 from account 12345 to account 67890
I need information about savings accounts with high interest rates
What are my recent transactions?
I want to open a new checking account
```

### Verify MCP Integration

Check the terminal logs to confirm:
- ✅ MCP Client connects to MCP Server successfully
- ✅ Tools are loaded dynamically from the server
- ✅ Authentication is working (bearer token mode)
- ✅ Agents use MCP tools instead of direct imports

## Wrap-up and Key Takeaways

### What You've Learned

1. **MCP Architecture**: How MCP provides standardized tool integration
2. **Native MCP Tools**: Using `@mcp.tool()` decorators for clean tool definitions  
3. **Client-Server Communication**: MCP protocol for reliable tool execution
4. **Authentication**: Simple token authentication for development/testing
5. **Separation of Concerns**: AI orchestration separated from business logic

### MCP Benefits Realized

- ✅ **Standardization**: Tools work with any MCP-compatible system
- ✅ **Loose Coupling**: MCP server can be updated independently
- ✅ **Team Autonomy**: Different teams can own different components
- ✅ **Reusability**: Tools can be shared across multiple AI applications

### Next Steps

- Explore the MCP server implementation in `/mcpserver/src/mcp_http_server.py`
- Learn about production authentication options in `/mcpserver/SECURITY.md`
- Consider implementing additional MCP tools for your specific use cases

Return to **[Home](Home.md)**

## Resources

- [Model Context Protocol Specification](https://spec.modelcontextprotocol.io/)
- [FastMCP Documentation](https://github.com/jlowin/fastmcp)
- [LangChain MCP Integration](https://python.langchain.com/docs/integrations/tools/mcp/)
- [Azure OpenAI Service](https://learn.microsoft.com/azure/cognitive-services/openai/)
- [LangGraph Documentation](https://langchain-ai.github.io/langgraph/)