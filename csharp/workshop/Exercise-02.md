# Exercise 02 - Implementing a Multi-Agent System

[< Previous Exercise](./Exercise-01.md) - **[Home](../README.md)** - [Next Exercise >](./Exercise-03.md)

## Introduction

In this exercise you'll create additional agents for the customer support agent to interact with as part of this multi-agent system.

## Description


## Presentation (15 mins)

- Multi-agent systems overview
- Orchestration patterns
- Agent communication

## Steps (30 mins)

  - [Create Banking Account Tools](#step-1-create-banking-tools)
  - [Create Banking Product Tools](#step-2-create-product-tools)
  - [Create Agent Transfer Tool](#step-3-create-agent-transfer-tool)
  - [Create the Banking Agents](#step-4-create-the-banking-agents)
  - [Create the Agent Tests](#step-5-create-the-agent-tests)
  - [Testing the implementation](#step-6-testing-the-implementation)

## Project Structure

Refer to this project structure when creating new files.

```
src/
├── app/
│   ├── services/
│   │   ├── azure_open_ai.py
│   │   └── azure_cosmos_db.py
│   ├── tools/
│   │   ├── agent_transfers.py
│   │   ├── banking.py
│   │   └── product.py
│   └── banking_agents.py
└── test/
    └── test_agent.py
```


### Step 1. Create Banking Account Tools

TBD need overview and explanation for each step


Create `src/app/tools/banking.py`:

```python
from typing import Dict, Any
from datetime import datetime

def bank_balance(account_id: str) -> Dict[str, Any]:
    """Get account balance information."""
    # Hardcoded data for demo
    accounts = {
        "1234": {"balance": 2500.00, "type": "checking"},
        "5678": {"balance": 10000.00, "type": "savings"}
    }
    return accounts.get(account_id, {"error": "Account not found"})

def bank_transfer(from_account: str, to_account: str, amount: float) -> Dict[str, Any]:
    """Process a bank transfer."""
    if amount <= 0:
        return {"error": "Invalid amount"}
    if from_account not in ["1234", "5678"]:
        return {"error": "Source account not found"}
    if to_account not in ["1234", "5678"]:
        return {"error": "Destination account not found"}

    return {
        "status": "success",
        "transaction_id": f"TX_{datetime.now().strftime('%Y%m%d%H%M%S')}",
        "amount": amount,
        "from_account": from_account,
        "to_account": to_account
    }

def calculate_monthly_payment(loan_amount: float, years: int) -> Dict[str, Any]:
    """Calculate monthly loan payment."""
    rate = 0.05  # 5% annual interest rate
    monthly_rate = rate / 12
    num_payments = years * 12

    monthly_payment = (loan_amount * monthly_rate * (1 + monthly_rate)**num_payments) / ((1 + monthly_rate)**num_payments - 1)

    return {
        "monthly_payment": round(monthly_payment, 2),
        "total_payment": round(monthly_payment * num_payments, 2),
        "interest_rate": f"{rate*100}%"
    }

def create_account(holder_name: str, initial_balance: float) -> Dict[str, Any]:
    """Create a new bank account."""
    account_id = "ACC_" + datetime.now().strftime('%Y%m%d%H%M%S')
    return {
        "status": "success",
        "account_id": account_id,
        "holder_name": holder_name,
        "initial_balance": initial_balance
    }
```

### Step 2. Create Banking Product Tools

TBD need overview and explanation for each step

Create `src/app/tools/product.py`:

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

### Step 3. Create Agent Transfer Tool

TBD need overview and explanation for each step

Create `src/app/tools/agent_transfers.py`:

```python
from langchain_core.tools import tool
from typing import Annotated
from langchain_core.tools.base import InjectedToolCallId
from langgraph.prebuilt import create_react_agent, InjectedState
from langgraph.types import Command

def create_agent_transfer(*, agent_name: str):
    """Create a tool that can return handoff via a Command"""
    tool_name = f"transfer_to_{agent_name}"

    @tool(tool_name)
    def transfer_to_agent(
            state: Annotated[dict, InjectedState],
            tool_call_id: Annotated[str, InjectedToolCallId],
    ):
        """Ask another agent for help."""
        tool_message = {
            "role": "tool",
            "content": f"Successfully transferred to {agent_name}",
            "name": tool_name,
            "tool_call_id": tool_call_id,
        }
        return Command(
            goto=agent_name,
            graph=Command.PARENT,
            update={"messages": state["messages"] + [tool_message]},
        )

    return transfer_to_agent
```

### Step 4. Create the Banking Agents

TBD need overview and explanation for each step

Create `src/app/banking_agents.py`:

```python
from typing import Literal
from langgraph.graph import StateGraph, START, MessagesState
from langgraph.prebuilt import create_react_agent
from langgraph.types import Command, interrupt
from langgraph_checkpoint_cosmosdb import CosmosDBSaver

from .services.azure_open_ai import model
from .services.azure_cosmos_db import DATABASE_NAME, CONTAINER_NAME
from .tools.product import get_product_advise, get_branch_location
from .tools.banking import bank_balance, bank_transfer, calculate_monthly_payment, create_account
from .tools.agent_transfers import create_agent_transfer

# Coordinator Agent
coordinator_agent_tools = [
    create_agent_transfer(agent_name="customer_support_agent"),
    create_agent_transfer(agent_name="sales_agent"),
    create_agent_transfer(agent_name="transactions_agent"),
]

coordinator_agent = create_react_agent(
    model,
    coordinator_agent_tools,
    state_modifier=(
        "You are a Chat Initiator and Request Router in a bank. "
        "Your primary responsibilities include welcoming users, and routing requests to the appropriate agent. "
        "If the user needs general help, transfer to 'customer_support_agent' for help. "
        "If the user wants to open a new account or take out a bank loan, transfer to 'sales_agent'. "
        "If the user wants to check their account balance or make a bank transfer, transfer to 'transactions_agent'. "
        "You MUST include human-readable response before transferring to another agent."
    ),
)

# Customer Support Agent
customer_support_agent_tools = [
    get_product_advise,
    get_branch_location,
    create_agent_transfer(agent_name="sales_agent"),
    create_agent_transfer(agent_name="transactions_agent"),
]

customer_support_agent = create_react_agent(
    model,
    customer_support_agent_tools,
    state_modifier=(
        "You are a customer support agent that can give general advice on banking products and branch locations. "
        "If the user wants to open a new account or take out a bank loan, transfer to 'sales_agent'. "
        "If the user wants to check their account balance or make a bank transfer, transfer to 'transactions_agent'. "
        "You MUST include human-readable response before transferring to another agent."
    ),
)

# Sales Agent
sales_agent_tools = [
    calculate_monthly_payment,
    create_account,
    create_agent_transfer(agent_name="customer_support_agent"),
]

sales_agent = create_react_agent(
    model,
    sales_agent_tools,
    state_modifier=(
        "You are a sales agent that can help users with creating a new account, or taking out bank loans. "
        "If the user wants to create a new account, you must ask for the account holder's name and the initial balance. "
        "Call create_account tool with these values. "
        "If user wants to take out a loan, you must ask for the loan amount and the number of years for the loan. "
        "When user provides these, calculate the monthly payment using calculate_monthly_payment tool and provide the result. "
        "Do not return the monthly payment tool call output directly to the user, include it with the rest of your response. "
        "You MUST respond with the repayment amounts before transferring to another agent."
    ),
)

# Transaction Agent
transactions_agent_tools = [
    bank_balance,
    bank_transfer,
    create_agent_transfer(agent_name="customer_support_agent"),
]

transactions_agent = create_react_agent(
    model,
    transactions_agent_tools,
    state_modifier=(
        "You are a banking transactions agent that can handle account balance enquiries and bank transfers. "
        "If the user needs general help, transfer to 'customer_support_agent' for help. "
        "You MUST respond with the transaction details before transferring to another agent."
    ),
)

def call_coordinator_agent(state: MessagesState, config) -> Command[Literal["coordinator_agent", "human"]]:
    response = coordinator_agent.invoke(state)
    return Command(update=response, goto="human")

def call_customer_support_agent(state: MessagesState, config) -> Command[Literal["customer_support_agent", "human"]]:
    response = customer_support_agent.invoke(state)
    return Command(update=response, goto="human")

def call_sales_agent(state: MessagesState, config) -> Command[Literal["sales_agent", "human"]]:
    response = sales_agent.invoke(state)
    return Command(update=response, goto="human")

def call_transactions_agent(state: MessagesState, config) -> Command[Literal["transactions_agent", "human"]]:
    response = transactions_agent.invoke(state)
    return Command(update=response, goto="human")

def human_node(state: MessagesState, config) -> Command[
    Literal["coordinator_agent", "customer_support_agent", "sales_agent", "transactions_agent", "human"]]:
    """A node for collecting user input."""
    user_input = interrupt(value="Ready for user input.")
    langgraph_triggers = config["metadata"]["langgraph_triggers"]
    if len(langgraph_triggers) != 1:
        raise AssertionError("Expected exactly 1 trigger in human node")
    active_agent = langgraph_triggers[0].split(":")[1]
    return Command(update={"messages": [{"role": "human", "content": user_input}]}, goto=active_agent)

# Create state graph
builder = StateGraph(MessagesState)

# Add nodes
builder.add_node("coordinator_agent", call_coordinator_agent)
builder.add_node("customer_support_agent", call_customer_support_agent)
builder.add_node("sales_agent", call_sales_agent)
builder.add_node("transactions_agent", call_transactions_agent)
builder.add_node("human", human_node)

# Add edges
builder.add_edge(START, "coordinator_agent")

# Set up checkpointing
checkpointer = CosmosDBSaver(
    database_name=DATABASE_NAME,
    container_name=CONTAINER_NAME
)

# Compile graph
graph = builder.compile(checkpointer=checkpointer)
```

### Step 5. Create the Agent Tests

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

    print("\nBanking System Test CLI")
    print("Type 'exit' to end the conversation")
    print("\nTest scenarios:")
    print("1. General inquiry: 'What banking services do you offer?'")
    print("2. Account creation: 'I want to open a new account'")
    print("3. Balance check: 'What's my account balance for account 1234?'")
    print("4. Transfer money: 'I want to transfer $100 from account 1234 to 5678'")
    print("5. Loan inquiry: 'I want to take out a home loan'")
    print("6. Branch location: 'Where is your nearest branch?'\n")

    while True:
        user_input = input("\nYou: ")
        if user_input.lower() == "exit":
            break

        try:
            response = await graph.acall({
                "messages": [{"role": "user", "content": user_input}],
                "conversation_id": conversation_id
            })

            # Extract the last message from the response
            last_message = response["messages"][-1]["content"]
            print(f"Agent: {last_message}")

        except Exception as e:
            print(f"Error: {str(e)}")

if __name__ == "__main__":
    asyncio.run(test_conversation())
```

### Step 6: Testing the implementation

1. Run the test CLI:

```bash
python test/test_agent.py
```

2. Try these test scenarios in sequence:

```
# Test Coordinator Agent
You: Hi, I need some banking help
Expected: Welcome message and routing question

# Test Customer Support
You: What types of accounts do you offer?
You: Where is your nearest branch?

# Test Sales Agent
You: I want to open a new account
You: I want to take out a loan for $200,000 over 30 years

# Test Transaction Agent
You: What's my balance in account 1234?
You: I want to transfer $100 from account 1234 to 5678

# Test Agent Transfers
You: I need help with my account (should route to customer support)
You: I want to open a new account (should route to sales)
You: I want to check my balance (should route to transactions)
```

## Validation Checklist

- [ ] All agents initialize correctly
- [ ] Coordinator routes requests appropriately
- [ ] Tools return expected responses
- [ ] Agent transfers work smoothly
- [ ] State is maintained between interactions
- [ ] Error handling works as expected

## Common Issues and Solutions

1. Environment Variables:

   - Double-check all environment variables are set
   - Verify Azure OpenAI endpoint is accessible
   - Confirm Cosmos DB connection string is valid

2. Agent Routing:

   - Verify coordinator agent responses
   - Check transfer tool implementation
   - Monitor agent state during transfers

3. Tool Execution:

   - Validate tool return formats
   - Check error handling in tools
   - Verify tool access permissions

4. State Management:
   - Monitor Cosmos DB connections
   - Check state preservation
   - Verify conversation flow

## Next Steps

In [Exercise 3](./Exercise-03.md), we will:

- Implement real banking operations
- Add proper data persistence
- Create robust error handling
- Add transaction validation

Proceed to [Exercise 3](./Exercise-03.md)
