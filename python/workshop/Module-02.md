# Module 02 - Connecting Agents to Memory

[< Creating Your First Agent](./Module-01.md) - **[Home](Home.md)** - [Agent Specialization >](./Module-03.md)

## Introduction

In this Module you'll connect your agent to Azure Cosmos DB to provide memory for chat history and state management for your agents to provide durability and context-awareness in your agent interactions.


## Learning Objectives and Activities

- Learn the basics for Azure Cosmos DB for storing state and chat history
- Learn how to integrate agent framworks to Azure Cosmos DB
- Test connectivity to Azure Cosmos DB works

## Module Exercises

1. [Activity 1: Session Memory Persistence in Agent Frameworks](#activity-1-session-memory-persistence-in-agent-frameworks)
1. [Activity 2: Connecting Agent Frameworks to Azure Cosmos DB](#activity-2-connecting-agent-frameworks-to-azure-cosmos-db)
1. [Activity 3: Test your Work](#activity-5-test-your-work)


## Activity 1: Session Memory Persistence in Agent Frameworks

In this session you will get an overview of memory and how it works for Semantic Kernel Agents and LangGraph and learn the basics for how to configure and connect both to Azure Cosmos DB as a memory store for both chat history and/or state management.


## Activity 2: Connecting Agent Frameworks to Azure Cosmos DB

In this hands-on exercise, you will learn how to initialize Azure Cosmos DB and integrate with an agent framework to provide persistent memory for chat history and state management.

The problem with our agents so far is that state is only maintained in memory and is lost when the agent graph is restarted. To solve this problem, we will use Azure Cosmos DB to store the state of the agent. Azure Cosmos DB is a globally distributed, multi-model database service for any scale. It is designed to provide low latency, high availability, and consistency with comprehensive service level agreements (SLAs). We will also use Azure Cosmos DB to store chat history.

Adding state management using Cosmos DB is easy with the checkpointer plugin. First, add the following imports to the top of the file:

```python
from langgraph_checkpoint_cosmosdb import CosmosDBSaver
from src.app.services.azure_cosmos_db import DATABASE_NAME, checkpoint_container, chat_container, update_chat_container, \
    patch_active_agent  
```

Next, locate the following lines in the `banking_agents.py` file:

```python
checkpointer = MemorySaver()
graph = builder.compile(checkpointer=checkpointer)
```

Replace with the below:

```python
checkpointer = CosmosDBSaver(database_name=DATABASE_NAME, container_name=checkpoint_container)
graph = builder.compile(checkpointer=checkpointer)
```

From this point on, the agent will save its state to Azure Cosmos DB. The `CosmosDBSaver` class will save the state of the agent to the `DATABASE_NAME` database and the `checkpoint_container` container.

We're also going to make some changes to the coordinator agent's calling function to store chat history. Locate the following code in the `banking_agents.py` file:

```python
def call_coordinator_agent(state: MessagesState, config) -> Command[Literal["coordinator_agent", "human"]]:
    response = coordinator_agent.invoke(state)
    return Command(update=response, goto="human")
```

Replace it with the following code:

```python
def call_coordinator_agent(state: MessagesState, config) -> Command[Literal["coordinator_agent", "human"]]:
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    userId = config["configurable"].get("userId", "UNKNOWN_USER_ID")
    tenantId = config["configurable"].get("tenantId", "UNKNOWN_TENANT_ID")

    logging.debug(f"Calling coordinator agent with Thread ID: {thread_id}")

    # Get the active agent from Cosmos DB with a point lookup
    partition_key = [tenantId, userId, thread_id]
    activeAgent = None
    try:
        activeAgent = chat_container.read_item(item=thread_id, partition_key=partition_key).get('activeAgent',
                                                                                                   'unknown')
    except Exception as e:
        logging.debug(f"No active agent found: {e}")

    if activeAgent is None:
        if local_interactive_mode:
            update_chat_container({
                "id": thread_id,
                "tenantId": "T1",
                "userId": "U1",
                "sessionId": thread_id,
                "name": "cli-test",
                "age": "cli-test",
                "address": "cli-test",
                "activeAgent": "unknown",
                "ChatName": "cli-test",
                "messages": []
            })

    logging.debug(f"Active agent from point lookup: {activeAgent}")

    # If active agent is something other than unknown or coordinator_agent, transfer directly to that agent
    if activeAgent is not None and activeAgent not in ["unknown", "coordinator_agent"]:
        logging.debug(f"Routing straight to last active agent: {activeAgent}")
        return Command(update=state, goto=activeAgent)
    else:
        response = coordinator_agent.invoke(state)
        return Command(update=response, goto="human")
```

Finally locate the following code in the `banking_agents.py` file:

```python
def call_customer_support_agent(state: MessagesState, config) -> Command[Literal["customer_support_agent", "human"]]:
    response = customer_support_agent.invoke(state)
    return Command(update=response, goto="human")
```

Replace it with the following code:

```python
def call_customer_support_agent(state: MessagesState, config) -> Command[Literal["customer_support_agent", "human"]]:
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    if local_interactive_mode:
        patch_active_agent(tenantId="T1", userId="U1", sessionId=thread_id,
                           activeAgent="customer_support_agent")
    response = customer_support_agent.invoke(state)
    return Command(update=response, goto="human")
```




### What did we do?

What we have done:

- Ensured that state and chat history is saved to Azure Cosmos DB so that it lasts beyond the lifetime of the application
- Storing an "active agent" in Cosmos DB, adding a check to see if the active agent is known. If so, we are routing directly to that agent. 
- We are patching the active agent in the Chat container after transfer. This is important to ensure that turn-by-turn routing is deterministic when it is known which agent asked the last question.
  - Note: it is feasible to rely on the LLM to reason about which agent to route to, but its behavior is less reliable and may not be suitable for all use cases.

## Activity 3: Test your Work

With the hands-on exercises complete it is time to test your work!

Before testing, lets make a small amendment to your `interactive_chat` function to hardcode the thread ID. The thread ID is the unique identifier for the conversation state. Until now we have not made use of it. We're hardcoding it here to demosntrate how the converation can be picked up later even when the application has stopped:

```python
hardcoded_thread_id = "hardcoded-thread-id-01"

def interactive_chat():
    thread_config = {"configurable": {"thread_id": hardcoded_thread_id, "userId": "cli-test", "tenantId": "cli-test"}}    
```

Once that update is done, run the `banking_agents.py` file and test the agent. Try asking for help and wait for it to transfer to the support agent:

```bash
Type 'exit' to end the conversation.

You: I want some help
transfer_to_customer_support_agent...
customer_support_agent: Hi there! How can I assist you today? If you need help with opening a new account, taking out a loan, checking your account balance, making a transfer, or anything else, let me know!

You: 
```

To prove that agent state is preserved, shut down the agent. Have a look at the Chat container in your Cosmos DB account. You should see the agent state and chat history stored there, with "customer_support_agent" as the active agent.

Now restart the application by running the `banking_agents.py` file again. Ask it for some help again. The coordinator agent should pick up the conversation where it left off and route straight to the customer support agent:

```bash
Welcome to the single-agent banking assistant.
Type 'exit' to end the conversation.

You: I want some help
customer_support_agent: Of course! Please let me know what kind of help you need. Are you looking to open a new account, take out a loan, check your account balance, make a transfer, or something else? I'm here to assist!

You: 
```

You may also want to look at the checkpoints container in your Cosmos DB account. You should see the agent state stored there. There is much more data stored in this container, as it is not only maintaining the chat history, but also the state of the agent, and any other agent, including computations in between transfers. This allows for a richer conversational experience as the full agent state is remembered and checkpointed regularly. 

**TBD - this needs langauge specific instructions**

### Validation Checklist

Your implementation is successful if:

- [ ] Your app compiles with no warnings or errors.
- [ ] Your agent successfully connects to Azure Cosmos DB. (**TBD how do we test this?**)


### Common Issues and Troubleshooting

1. Issue 1:
    - TBD
    - TBD

1. Issue 2:
    - TBD
    - TBD

1. Issue 3:
    - TBD
    - TBD


### Module Solution

<details>
  <summary>If you are encountering errors or issues with your code for this module, please refer to the following code.</summary>

<br>

Your `banking_agents.py` file should now look like this:
```python
import logging
import os
from langchain.schema import AIMessage
from typing import Literal
from langgraph.graph import StateGraph, START, MessagesState
from langgraph.prebuilt import create_react_agent
from langgraph.types import Command, interrupt
from langgraph_checkpoint_cosmosdb import CosmosDBSaver

from src.app.services.azure_cosmos_db import DATABASE_NAME, checkpoint_container, chat_container, update_chat_container, \
    patch_active_agent
from src.app.services.azure_open_ai import model
from src.app.tools.coordinator import create_agent_transfer

local_interactive_mode = False

logging.basicConfig(level=logging.ERROR)

PROMPT_DIR = os.path.join(os.path.dirname(__file__), 'prompts')

def load_prompt(agent_name):
    """Loads the prompt for a given agent from a file."""
    file_path = os.path.join(PROMPT_DIR, f"{agent_name}.prompty")
    print(f"Loading prompt for {agent_name} from {file_path}")
    try:
        with open(file_path, "r", encoding="utf-8") as file:
            return file.read().strip()
    except FileNotFoundError:
        print(f"Prompt file not found for {agent_name}, using default placeholder.")
        return "You are an AI banking assistant."  # Fallback default prompt


coordinator_agent_tools = [
    create_agent_transfer(agent_name="customer_support_agent"),
]

coordinator_agent = create_react_agent(
    model,
    tools=coordinator_agent_tools,
    state_modifier=load_prompt("coordinator_agent"),
)

customer_support_agent_tools = []
customer_support_agent = create_react_agent(
    model,
    customer_support_agent_tools,
    state_modifier=load_prompt("customer_support_agent"),
)


def call_coordinator_agent(state: MessagesState, config) -> Command[Literal["coordinator_agent", "human"]]:
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    userId = config["configurable"].get("userId", "UNKNOWN_USER_ID")
    tenantId = config["configurable"].get("tenantId", "UNKNOWN_TENANT_ID")

    logging.debug(f"Calling coordinator agent with Thread ID: {thread_id}")

    # Get the active agent from Cosmos DB with a point lookup
    partition_key = [tenantId, userId, thread_id]
    activeAgent = None
    try:
        activeAgent = chat_container.read_item(item=thread_id, partition_key=partition_key).get('activeAgent',
                                                                                                'unknown')
    except Exception as e:
        logging.debug(f"No active agent found: {e}")

    if activeAgent is None:
        if local_interactive_mode:
            update_chat_container({
                "id": thread_id,
                "tenantId": "T1",
                "userId": "U1",
                "sessionId": thread_id,
                "name": "cli-test",
                "age": "cli-test",
                "address": "cli-test",
                "activeAgent": "unknown",
                "ChatName": "cli-test",
                "messages": []
            })

    logging.debug(f"Active agent from point lookup: {activeAgent}")

    # If active agent is something other than unknown or coordinator_agent, transfer directly to that agent
    if activeAgent is not None and activeAgent not in ["unknown", "coordinator_agent"]:
        logging.debug(f"Routing straight to last active agent: {activeAgent}")
        return Command(update=state, goto=activeAgent)
    else:
        response = coordinator_agent.invoke(state)
        return Command(update=response, goto="human")


def call_customer_support_agent(state: MessagesState, config) -> Command[Literal["customer_support_agent", "human"]]:
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    if local_interactive_mode:
        patch_active_agent(tenantId="T1", userId="U1", sessionId=thread_id,
                           activeAgent="customer_support_agent")
    response = customer_support_agent.invoke(state)
    return Command(update=response, goto="human")


# The human_node with interrupt function serves as a mechanism to stop
# the graph and collect user input for multi-turn conversations.
def human_node(state: MessagesState, config) -> None:
    """A node for collecting user input."""
    interrupt(value="Ready for user input.")
    return None


builder = StateGraph(MessagesState)
builder.add_node("coordinator_agent", call_coordinator_agent)
builder.add_node("customer_support_agent", call_customer_support_agent)
builder.add_node("human", human_node)

builder.add_edge(START, "coordinator_agent")

checkpointer = CosmosDBSaver(database_name=DATABASE_NAME, container_name=checkpoint_container)
graph = builder.compile(checkpointer=checkpointer)

hardcoded_thread_id = "hardcoded-thread-id-01"


def interactive_chat():
    thread_config = {"configurable": {"thread_id": hardcoded_thread_id, "userId": "U1", "tenantId": "T1"}}
    global local_interactive_mode
    local_interactive_mode = True
    print("Welcome to the single-agent banking assistant.")
    print("Type 'exit' to end the conversation.\n")

    user_input = input("You: ")
    conversation_turn = 1

    while user_input.lower() != "exit":

        input_message = {"messages": [{"role": "user", "content": user_input}]}

        response_found = False  # Track if we received an AI response

        for update in graph.stream(
                input_message,
                config=thread_config,
                stream_mode="updates",
        ):
            for node_id, value in update.items():
                if isinstance(value, dict) and value.get("messages"):
                    last_message = value["messages"][-1]  # Get last message
                    if isinstance(last_message, AIMessage):
                        print(f"{node_id}: {last_message.content}\n")
                        response_found = True

        if not response_found:
            print("DEBUG: No AI response received.")

        # Get user input for the next round
        user_input = input("You: ")
        conversation_turn += 1


if __name__ == "__main__":
    interactive_chat()
```
</details>

## Next Steps

Proceed to [Agent Specialization](./Module-03.md)

## Resources

- [Semantic Kernel Agent Framework](https://learn.microsoft.com/semantic-kernel/frameworks/agent)
- [LangGraph](https://langchain-ai.github.io/langgraph/concepts/)
- [Azure OpenAI Service documentation](https://learn.microsoft.com/azure/cognitive-services/openai/)
- [Azure Cosmos DB Vector Database](https://learn.microsoft.com/azure/cosmos-db/vector-database)
