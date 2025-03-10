# Exercise 01 - Creating Your First Agent

[< Previous Exercise](./Exercise-00.md) - **[Home](../README.md)** - [Next Exercise >](./Exercise-02.md)

## Introduction

In this exercise, you'll implement your first agent as part of a multi-agent banking system using LangGraph, starting with a customer support agent. You'll also set up Azure services, create basic banking tools, and implement state management using Cosmos DB.

## Description



## Learning Objectives

- Set up Azure OpenAI and Cosmos DB integrations
- Create a customer support agent using LangGraph
- Implement basic banking tools
- Create state management with Cosmos DB checkpointing
- Test the agent through a CLI interface


## Presentation (15 mins)

- Introduction to Agents
- LangGraph and Semantic Kernal Agent fundamentals
- State management concepts

## Steps (30 mins)

1. [Environment Setup](#step-1-environment-setup)
2. [Configure Azure Services](#step-2-configure-azure-services)
3. [Implement Banking Tools](#step-3-implement-banking-tools)
4. [Create Banking Agents](#step-4-create-banking-agents)
5. [Create Test CLI](#step-5-create-test-cli)
6. [Testing the Implementation](#testing-the-implementation)

### Step 1: Environment Setup

TBD need overview and explanation for each step

Set up the required environment variables:

```bash
# Azure OpenAI Configuration
export AZURE_OPENAI_API_KEY="your-api-key"
export AZURE_OPENAI_ENDPOINT="your-endpoint"
export AZURE_OPENAI_MODEL="gpt-4"

# Cosmos DB Configuration
export COSMOS_ENDPOINT="your-cosmos-endpoint"
export COSMOS_KEY="your-cosmos-key"
export COSMOS_DATABASE="banking"
export COSMOS_CONTAINER="chat_history"
```

### Step 2: Configure Azure Services

TBD need overview and explanation for each step

1. **Azure OpenAI Configuration**
   Create `src/app/azure_open_ai.py`:

   ```python
   from openai import AsyncAzureOpenAI
   import os

   model = AsyncAzureOpenAI(
       api_key=os.getenv("AZURE_OPENAI_API_KEY"),
       api_version="2024-02-15-preview",
       azure_endpoint=os.getenv("AZURE_OPENAI_ENDPOINT")
   )
   ```

2. **Cosmos DB Configuration**
   Create `src/app/azure_cosmos_db.py`:

   ```python
   from azure.cosmos.aio import CosmosClient
   import os

   DATABASE_NAME = os.getenv("COSMOS_DATABASE", "banking")
   CONTAINER_NAME = os.getenv("COSMOS_CONTAINER", "chat_history")

   client = CosmosClient(
       url=os.getenv("COSMOS_ENDPOINT"),
       credential=os.getenv("COSMOS_KEY")
   )

   database = client.get_database_client(DATABASE_NAME)
   container = database.get_container_client(CONTAINER_NAME)
   ```

### Step 3: Implement Banking Tools

TBD need overview and explanation for each step

Create `src/app/banking.py`:

```python
from typing import Dict, Any

def get_product_advise() -> Dict[str, Any]:
    """Get basic information about banking products."""
    return {
        "accounts": {
            "checking": "Basic checking account with no monthly fees",
            "savings": "High-yield savings account with 3.5% APY",
            "premium": "Premium checking with added benefits"
        },
        "cards": {
            "basic": "No annual fee credit card",
            "rewards": "Cash back rewards card",
            "premium": "Travel rewards card with lounge access"
        }
    }

def get_branch_location() -> Dict[str, Any]:
    """Get information about bank branches."""
    return {
        "locations": [
            {
                "name": "Main Branch",
                "address": "123 Banking St, Financial District",
                "hours": "9 AM - 5 PM",
                "services": ["Full Service", "ATM", "Safe Deposit"]
            },
            {
                "name": "West Side Branch",
                "address": "456 Commerce Ave, West Side",
                "hours": "9 AM - 6 PM",
                "services": ["Full Service", "ATM"]
            }
        ]
    }
```

### Step 4: Create Banking Agents

TBD need overview and explanation for each step

Create `src/app/banking_agents.py`:

```python
from typing import Dict, Any, List, TypedDict
from langgraph.graph import StateGraph, START
from langgraph.prebuilt.agents import create_react_agent
from langgraph_checkpoint_cosmosdb import CosmosDBSaver

from .banking import get_product_advise, get_branch_location
from .azure_open_ai import model
from .azure_cosmos_db import DATABASE_NAME, CONTAINER_NAME

# Define tools for customer support agent
customer_support_agent_tools = [
    get_product_advise,
    get_branch_location,
]

# Create customer support agent
customer_support_agent = create_react_agent(
    model,
    customer_support_agent_tools,
    state_modifier=(
        "You are a customer support agent that can give general advice on banking products and branch locations. "
        "Use the tools available to provide accurate information about our products and branches. "
        "Be professional and courteous in your responses."
    ),
)

class MessagesState(TypedDict):
    messages: List[Dict[str, str]]
    current_agent: str

async def call_customer_support_agent(state: MessagesState) -> MessagesState:
    result = await customer_support_agent.ainvoke(state)
    return result

async def human_node(state: MessagesState) -> MessagesState:
    return state

# Create state graph
builder = StateGraph(MessagesState)

# Add nodes
builder.add_node("customer_support_agent", call_customer_support_agent)
builder.add_node("human", human_node)

# Add edges
builder.add_edge(START, "customer_support_agent")

# Set up checkpointing
checkpointer = CosmosDBSaver(
    database_name=DATABASE_NAME,
    container_name=CONTAINER_NAME
)

# Compile graph
graph = builder.compile(checkpointer=checkpointer)
```

### Step 5: Create the Tests CLI

TBD need overview and explanation for each step

Create `test/test_agent.py`:

```python
import asyncio
import sys
import uuid
sys.path.append("../src/app")

from banking_agents import graph

async def test_conversation():
    conversation_id = str(uuid.uuid4())

    while True:
        user_input = input("\nYou: ")
        if user_input.lower() == "exit":
            break

        response = await graph.acall({
            "messages": [{"role": "user", "content": user_input}],
            "current_agent": "customer_support_agent",
            "conversation_id": conversation_id
        })

        print(f"Agent: {response['messages'][-1]['content']}")

if __name__ == "__main__":
    asyncio.run(test_conversation())
```

### Step 6: Testing the Implementation

TBD need overview and explanation for each step

1. Ensure all environment variables are set correctly
2. Run the test CLI:

   ```bash
   python test/test_agent.py
   ```

3. Try these sample queries:

   ```
   You: What types of accounts do you offer?
   You: Where is your nearest branch?
   You: What are the branch working hours?
   You: Tell me about your credit cards
   ```

4. Type `exit` to end the conversation.

## Validation Checklist

Your implementation is successful if:

- [ ] Agent provides accurate product information using the `get_product_advise` tool
- [ ] Branch location queries are answered correctly using the `get_branch_location` tool
- [ ] Responses are professional and helpful
- [ ] Conversation state is properly saved in Cosmos DB
- [ ] CLI interface works smoothly

## Common Issues and Troubleshooting

1. Tool Integration:

  - Verify tool functions return proper data structures
  - Check tool access in agent responses

2. State Management:

  - Verify Cosmos DB connection string
  - Check checkpoint saving functionality
  - Ensure proper database and container names

3. Agent Responses:

  - Ensure proper tool usage in responses
  - Verify response formatting

4. Environment Setup:
  - Check all environment variables are set
  - Verify Azure OpenAI model deployment
  - Confirm Cosmos DB container exists

## Next Steps

In [Exercise 2](./Exercise-02.md), we will:

- Add agent transfer capabilities
- Implement the sales and transaction agents
- Create more sophisticated banking tools

Proceed to [Exercise 2](./Exercise-02.md)
