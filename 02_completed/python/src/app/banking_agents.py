import logging
import os
import sys
import time
import uuid
import asyncio
import json
from langchain_core.messages import ToolMessage, SystemMessage
from langchain.schema import AIMessage
from langchain_mcp_adapters.client import MultiServerMCPClient
from typing import Literal, Optional, List
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

# 🔄 Global persistent MCP client and cache
_persistent_mcp_client: Optional[MultiServerMCPClient] = None
_mcp_tools_cache: Optional[List] = None
_native_tools_fallback_enabled = False  # 🚀 Using shared MCP server for optimal performance
_shared_mcp_client = None  # 🚀 Enhanced shared client

# 🔧 Tool version tracking for cache invalidation
import time
_module_load_time = time.time()
_agents_setup_version = None
_last_setup_time = None

print(f"🔧 MODULE LOAD: banking_agents module loaded at {_module_load_time}")

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
    filtered = []
    for tool in tools:
        # Handle both dict and object formats for compatibility
        if isinstance(tool, dict):
            tool_name = tool.get('name', '')
        else:
            tool_name = getattr(tool, 'name', '')
        
        if any(tool_name.startswith(prefix) for prefix in prefixes):
            filtered.append(tool)
    return filtered

# � Global persistent MCP client and cache
_persistent_mcp_client: Optional[MultiServerMCPClient] = None
_mcp_tools_cache: Optional[List] = None
_native_tools_fallback_enabled = True  # 🚀 NEW: Enable native fallback for performance

async def get_persistent_mcp_client():
    """Get or create a persistent MCP client that is reused across all tool calls"""
    global _persistent_mcp_client, _mcp_tools_cache, _shared_mcp_client
    
    if _persistent_mcp_client is None:
        print("🔧 MCP CLIENT: Creating new MCP client and tools")
        print("🔄 Initializing SHARED MCP client (high-performance setup)...")
        start_time = time.time()
        
        try:
            # 🚀 Use the new shared MCP client for optimal performance
            from src.app.tools.mcp_client import get_shared_mcp_client, set_mcp_context, get_mcp_context
            _shared_mcp_client = await get_shared_mcp_client()
            
            # Get tools from shared client
            _mcp_tools_cache = await _shared_mcp_client.get_tools()
            
            # For compatibility, create a wrapper that looks like MultiServerMCPClient
            class SharedMCPClientWrapper:
                def __init__(self, shared_client):
                    self.shared_client = shared_client
                
                async def get_tools(self):
                    return await self.shared_client.get_tools()
                
                async def call_tool(self, tool_name: str, arguments: dict):
                    return await self.shared_client.call_tool(tool_name, arguments)
                
                async def close(self):
                    await self.shared_client.cleanup()
            
            _persistent_mcp_client = SharedMCPClientWrapper(_shared_mcp_client)
            
            setup_duration = (time.time() - start_time) * 1000
            print(f"✅ SHARED MCP client initialized in {setup_duration:.2f}ms")
            print(f"🛠️  Cached {len(_mcp_tools_cache)} tools for reuse")
            
            # Log cached tools
            print("[DEBUG] Cached tools from SHARED MCP server:")
            for tool in _mcp_tools_cache:
                tool_name = tool.get('name') if isinstance(tool, dict) else getattr(tool, 'name', 'unknown')
                print("  -", tool_name)
                
        except Exception as e:
            print(f"❌ Failed to initialize SHARED MCP client: {e}")
            raise Exception("Failed to initialize MCP client")
    
    return _persistent_mcp_client, _mcp_tools_cache

async def setup_agents():
    global coordinator_agent, customer_support_agent, transactions_agent, sales_agent
    
    print("🔧 SETUP: Setting up agents with fresh MCP client...")
    
    # Clear all caches when explicitly recreating (e.g., after module reload)
    global _persistent_mcp_client, _shared_mcp_client
    _persistent_mcp_client = None
    _shared_mcp_client = None
    _mcp_tools_cache = None
    coordinator_agent = None
    customer_support_agent = None
    
    # 🔧 CRITICAL FIX: Also clear the SharedMCPClient's global cache
    from src.app.tools.mcp_client import cleanup_shared_mcp_client
    await cleanup_shared_mcp_client()
    print("🔧 SETUP: Cleared SharedMCPClient global cache")  
    transactions_agent = None
    sales_agent = None
    print("🔧 CLEARED: All agent and MCP client caches cleared for fresh setup")

    print("Setting up agents with persistent MCP client...")
    
    # Get persistent client and cached tools
    _shared_mcp_client = None
    print("🔧 DEBUG: Cleared MCP client cache - forcing tool regeneration")
    
    mcp_client, all_tools = await get_persistent_mcp_client()

    # Assign tools to agents based on tool name prefix
    coordinator_tools = filter_tools_by_prefix(all_tools, ["transfer_to_"])
    support_tools = filter_tools_by_prefix(all_tools, ["service_request", "get_branch_location", "transfer_to_sales_agent", "transfer_to_transactions_agent"])
    sales_tools = filter_tools_by_prefix(all_tools, ["get_offer_information", "create_account", "calculate_monthly_payment", "transfer_to_customer_support_agent", "transfer_to_transactions_agent"])
    transactions_tools = filter_tools_by_prefix(all_tools, ["bank_transfer", "get_transaction_history", "bank_balance", "transfer_to_customer_support_agent"])

    # Debug: Print tool information for transactions agent
    print(f"🔧 DEBUG: Transactions agent tools ({len(transactions_tools)} total):")
    for tool in transactions_tools:
        tool_name = getattr(tool, 'name', 'UNKNOWN')
        tool_args = getattr(tool, 'args_schema', None)
        if tool_args:
            print(f"  - {tool_name}: {tool_args.__name__} schema with fields {list(tool_args.__fields__.keys())}")
        else:
            print(f"  - {tool_name}: No args schema")

    # Create agents with their respective tools
    coordinator_agent = create_react_agent(model, coordinator_tools, state_modifier=load_prompt("coordinator_agent"))
    customer_support_agent = create_react_agent(model, support_tools, state_modifier=load_prompt("customer_support_agent"))
    sales_agent = create_react_agent(model, sales_tools, state_modifier=load_prompt("sales_agent"))
    transactions_agent = create_react_agent(model, transactions_tools, state_modifier=load_prompt("transactions_agent"))

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
        
        # Check if any tool responses indicate a transfer request
        transfer_target = None
        print(f"🔧 DEBUG: Checking response for transfer requests")
        print(f"🔧 DEBUG: Response type: {type(response)}")
        print(f"🔧 DEBUG: Response contents: {response}")
        
        # Check if this is a LangGraph AddableValuesDict response
        if isinstance(response, dict):
            print(f"🔧 DEBUG: Response is dict with keys: {list(response.keys())}")
            # Look for messages in the response
            if 'messages' in response and response['messages']:
                print(f"🔧 DEBUG: Found {len(response['messages'])} messages in response dict")
                for i, message in enumerate(response['messages']):
                    print(f"🔧 DEBUG: Message {i}: type={type(message)}")
                    if hasattr(message, 'content'):
                        print(f"🔧 DEBUG: Message {i} content: {message.content}")
                        if isinstance(message.content, str) and message.content.strip():
                            try:
                                import json
                                # Try to parse JSON response
                                content_data = json.loads(message.content)
                                if content_data.get("goto"):
                                    transfer_target = content_data["goto"]
                                    print(f"🔄 COORDINATOR: Found JSON transfer in message content: {transfer_target}")
                                    break
                            except (json.JSONDecodeError, TypeError, AttributeError):
                                # Check for old format
                                if message.content.startswith("TRANSFER_REQUEST:"):
                                    transfer_target = message.content.split(":", 1)[1]
                                    print(f"� COORDINATOR: Found legacy transfer: {transfer_target}")
                                    break
                    
                    # Check if message has tool_calls
                    if hasattr(message, 'tool_calls') and message.tool_calls:
                        print(f"🔧 DEBUG: Message {i} has {len(message.tool_calls)} tool calls")
                        for j, tool_call in enumerate(message.tool_calls):
                            print(f"� DEBUG: Tool call {j}: {tool_call}")
            
            # Also check for any other relevant keys in response
            for key, value in response.items():
                if key != 'messages':
                    print(f"� DEBUG: Response[{key}]: {value} (type: {type(value)})")
        
        else:
            print(f"🔧 DEBUG: Response is not a dict")
        
        if transfer_target:
            return Command(update=response, goto=transfer_target)
        else:
            return Command(update=response, goto="human")


@traceable(run_type="llm")
async def call_customer_support_agent(state: MessagesState, config) -> Command[Literal["customer_support_agent", "human"]]:
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    userId = config["configurable"].get("userId", "UNKNOWN_USER_ID")
    tenantId = config["configurable"].get("tenantId", "UNKNOWN_TENANT_ID")
    if local_interactive_mode:
        patch_active_agent("cli-test", "cli-test", thread_id, "customer_support_agent")
    
    from langchain_core.messages import SystemMessage
    
    # Add system message with tenant/user context for the LLM to use when calling tools
    system_message = SystemMessage(content=f"When calling service_request tool, always include these parameters: tenantId='{tenantId}', userId='{userId}'")
    state["messages"].append(system_message)
    
    response = await customer_support_agent.ainvoke(state)
    
    # Remove the system message added above from response
    if isinstance(response, dict) and "messages" in response:
        response["messages"] = [
            msg for msg in response["messages"]
            if not isinstance(msg, SystemMessage)
        ]
    
    return Command(update=response, goto="human")


@traceable(run_type="llm")
async def call_sales_agent(state: MessagesState, config) -> Command[Literal["sales_agent", "human"]]:
    start_time = time.time()
    print("⏱️  LANGGRAPH: Starting sales agent execution")
    
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    userId = config["configurable"].get("userId", "UNKNOWN_USER_ID")
    tenantId = config["configurable"].get("tenantId", "UNKNOWN_TENANT_ID")
    if local_interactive_mode:
        patch_active_agent("cli-test", "cli-test", thread_id, "sales_agent")
    
    from langchain_core.messages import SystemMessage
    
    # Add system message with tenant/user context for the LLM to use when calling tools
    system_message = SystemMessage(content=f"When calling create_account tool, always include these parameters: tenantId='{tenantId}', userId='{userId}'")
    state["messages"].append(system_message)
    
    agent_start_time = time.time()
    response = await sales_agent.ainvoke(state, config)
    agent_duration_ms = (time.time() - agent_start_time) * 1000
    
    # Remove the system message added above from response
    if isinstance(response, dict) and "messages" in response:
        response["messages"] = [
            msg for msg in response["messages"]
            if not isinstance(msg, SystemMessage)
        ]
    
    total_duration_ms = (time.time() - start_time) * 1000
    print(f"⏱️  LANGGRAPH: Sales agent invoke took {agent_duration_ms:.2f}ms")
    print(f"⏱️  LANGGRAPH: Total sales agent call took {total_duration_ms:.2f}ms")
    
    return Command(update=response, goto="human")


@traceable(run_type="llm")
async def call_transactions_agent(state: MessagesState, config) -> Command[Literal["transactions_agent", "human"]]:
    # 🔧 SMART REFRESH: Only recreate if module was reloaded (--reload detected)
    global _agents_setup_version, _last_setup_time
    if _agents_setup_version != _module_load_time:
        print(f"🔧 DETECTED RELOAD: Module reloaded, refreshing agents (setup_version={_agents_setup_version}, module_load_time={_module_load_time})")
        # Clear SharedMCP cache before recreating agents
        from src.app.tools.mcp_client import cleanup_shared_mcp_client
        await cleanup_shared_mcp_client()
        await setup_agents()
        _agents_setup_version = _module_load_time
        _last_setup_time = time.time()
    
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    userId = config["configurable"].get("userId", "UNKNOWN_USER_ID")
    tenantId = config["configurable"].get("tenantId", "UNKNOWN_TENANT_ID")
    if local_interactive_mode:
        patch_active_agent("cli-test", "cli-test", thread_id, "transactions_agent")
    
    # Add system message with tenant/user context for the LLM to use when calling tools
    from langchain_core.messages import SystemMessage

    system_msg_content = f"IMPORTANT: When calling the bank_balance, bank_transfer, or get_transaction_history tools, you MUST always include these exact parameters: tenantId='{tenantId}', userId='{userId}', thread_id='{thread_id}'. Do not call these tools without all required parameters."
    print(f"🔧 DEBUG: Adding system message to transactions agent: {system_msg_content}")

    
    # Add as proper SystemMessage object 
    system_message = SystemMessage(content=system_msg_content)
    state["messages"].append(system_message)
    
    response = await transactions_agent.ainvoke(state, config)
    # explicitly remove the system message added above from response
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

async def cleanup_persistent_mcp_client():
    """Properly shutdown the persistent MCP client and shared MCP client"""
    global _persistent_mcp_client, _shared_mcp_client
    
    print("🔄 Shutting down MCP clients...")
    
    # Clean up shared MCP client first (higher priority)
    if _shared_mcp_client:
        try:
            await _shared_mcp_client.cleanup()
            _shared_mcp_client = None
            print("✅ Shared MCP client cleaned up")
        except Exception as e:
            print(f"⚠️ Error cleaning up shared MCP client: {e}")
    
    # Then clean up persistent MCP client
    if _persistent_mcp_client:
        try:
            if hasattr(_persistent_mcp_client, 'close'):
                await _persistent_mcp_client.close()
            elif hasattr(_persistent_mcp_client, '__aenter__'):
                # If it's an async context manager, we might need different handling
                pass
            print("✅ Persistent MCP client shutdown complete")
        except Exception as e:
            print(f"⚠️  Error during MCP client shutdown: {e}")
        finally:
            _persistent_mcp_client = None


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