import random
from typing import Annotated, Literal, List, Dict, Any
from pydantic import BaseModel
from langchain_core.tools import tool
from langchain_core.tools.base import InjectedToolCallId
from langgraph.graph import StateGraph, START, MessagesState
from langgraph.prebuilt import create_react_agent, InjectedState
from langgraph.types import Command, interrupt
from langgraph_checkpoint_cosmosdb import CosmosDBSaver
from src.app.azure_open_ai import model
from src.app.azure_cosmos_db import DATABASE_NAME, CONTAINER_NAME, userdata_container

@tool
def get_product_advise():
    """Get recommendation for banking product"""
    return random.choice(["advanced main account", "savings account"])


@tool
def get_branch_location(location: str):
    """Get location of bank branches for a given area"""
    return f"there is central bank and trust branch in {location}"


@tool
def bank_balance(account_number: str):
    """Transfer to bank agent"""
    return f"The balance for account number {account_number} is $1000"


@tool
def bank_transfer(account_number: str, amount: float):
    """Transfer to bank agent"""
    return f"Successfully transferred ${amount} to account number {account_number}"


@tool
def calculate_monthly_payment(loan_amount: float, years: int) -> float:
    """Calculate the monthly payment for a loan."""
    interest_rate = 0.05  # Hardcoded annual interest rate (5%)
    monthly_rate = interest_rate / 12  # Convert annual rate to monthly
    total_payments = years * 12  # Total number of monthly payments

    if monthly_rate == 0:
        return loan_amount / total_payments  # If interest rate is 0, simple division

    monthly_payment = (loan_amount * monthly_rate * (1 + monthly_rate) ** total_payments) / \
                      ((1 + monthly_rate) ** total_payments - 1)

    return round(monthly_payment, 2)  # Rounded to 2 decimal places


def make_handoff_tool(*, agent_name: str):
    """Create a tool that can return handoff via a Command"""
    tool_name = f"transfer_to_{agent_name}"

    @tool(tool_name)
    def handoff_to_agent(
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

    return handoff_to_agent


supervisor_agent_tools = [make_handoff_tool(agent_name="banking_agent")]
supervisor_agent = create_react_agent(
    model,
    supervisor_agent_tools,
    state_modifier=(
        "You are a Chat Initiator and Request Router in a bank."
        "Your primary responsibilities include welcoming users, and routing requests to the appropriate agent."
        "If the user needs help, ask 'banking_agent' for help. "
        "You MUST include human-readable response before transferring to another agent."
    ),
)

banking_agent_tools = [
    get_product_advise,
    get_branch_location,
    bank_balance,
    bank_transfer,
    make_handoff_tool(agent_name="loan_agent"),
]
banking_agent = create_react_agent(
    model,
    banking_agent_tools,
    state_modifier=(
        "You are a banking agent that can give general advice on banking products, branch locations, balances and transfers. "
        "If the user asks for a bank loan, you call make_handoff_tool with agent_name 'loan_agent'. "
        "You MUST include human-readable response before transferring to another agent."
    ),
)

loan_agent_tools = [
    calculate_monthly_payment,
    make_handoff_tool(agent_name="banking_agent"),
]
loan_agent = create_react_agent(
    model,
    loan_agent_tools,
    state_modifier=(
        "You must ask for the loan amount and the number of years for the loan. When user provides these, calculate the monthly payment using calculate_monthly_payment tool. "
        "You MUST respond with the repayment amounts before transferring to another agent."
    ),
)


def call_supervisor_agent(state: MessagesState, config) -> Command[Literal["supervisor_agent", "human"]]:
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")  # Get thread_id from config
    print(f"Calling supervisor agent with Thread ID: {thread_id}")

    activeAgent = userdata_container.query_items(
        query=f"SELECT c.activeAgent FROM c WHERE c.id = '{thread_id}'",
        enable_cross_partition_query=True
    )
    result = list(activeAgent)
    if result:
        active_agent_value = result[0]['activeAgent']
    else:
        active_agent_value = None  # or handle the case where no result is found
    print(f"Active agent: {active_agent_value}")
    # if active agent is something other than unknown or supervisor_agent,
    # then transfer directly to that agent to respond to the last collected user input
    if active_agent_value is not None and active_agent_value != "unknown" and active_agent_value != "supervisor_agent":
        print(f"routing straight to active agent: ", active_agent_value)
        return Command(update=state, goto=active_agent_value)
    else:
        response = supervisor_agent.invoke(state)
        print(f"collecting user input")
        return Command(update=response, goto="human")


def call_banking_agent(state: MessagesState, config) -> Command[Literal["banking_agent", "human"]]:
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    print(f"Calling banking agent with Thread ID: {thread_id}")

    response = banking_agent.invoke(state)
    return Command(update=response, goto="human")


def call_loan_agent(state: MessagesState, config) -> Command[Literal["loan_agent", "human"]]:
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    print(f"Calling loan agent with Thread ID: {thread_id}")

    response = loan_agent.invoke(state)
    return Command(update=response, goto="human")


# In this implementation, the human_node with interrupt function only serves as a mechanism to stop
# the graph and collect user input. Since the graph being exposed as an API, the Command object
# return value will never be reached. In interactive mode, the Command object would
# be returned after user input collected, and the graph would continue to the active agent.
def human_node(state: MessagesState, config) -> Command[
    Literal["supervisor_agent", "banking_agent", "loan_agent", "human"]]:
    """A node for collecting user input."""
    user_input = interrupt(value="Ready for user input.")
    langgraph_triggers = config["metadata"]["langgraph_triggers"]
    if len(langgraph_triggers) != 1:
        raise AssertionError("Expected exactly 1 trigger in human node")
    active_agent = langgraph_triggers[0].split(":")[1]
    print(f"Active agent: {active_agent}")
    return Command(update={"messages": [{"role": "human", "content": user_input}]}, goto=active_agent)


builder = StateGraph(MessagesState)
builder.add_node("supervisor_agent", call_supervisor_agent)
builder.add_node("banking_agent", call_banking_agent)
builder.add_node("loan_agent", call_loan_agent)
builder.add_node("human", human_node)

builder.add_edge(START, "supervisor_agent")

checkpointer = CosmosDBSaver(database_name=DATABASE_NAME, container_name=CONTAINER_NAME)
graph = builder.compile(checkpointer=checkpointer)
