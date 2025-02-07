import random
from typing import Annotated, Literal

from langchain_core.tools import tool
from langchain_core.tools.base import InjectedToolCallId
from langgraph.graph import MessagesState, StateGraph, START
from langgraph.prebuilt import create_react_agent, InjectedState
from langgraph.types import Command, interrupt
from langgraph_checkpoint_cosmosdb import CosmosDBSaver
from src.app.azure_open_ai import model
from src.app.azure_cosmos_db import DATABASE_NAME, CONTAINER_NAME


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
    """
    Calculate the monthly payment for a loan.
    """
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
            # navigate to another agent node in the PARENT graph
            goto=agent_name,
            graph=Command.PARENT,
            # This is the state update that the agent `agent_name` will see when it is invoked.
            # We're passing agent's FULL internal message history AND adding a tool message to make sure
            # the resulting chat history is valid.
            update={"messages": state["messages"] + [tool_message]},
        )

    return handoff_to_agent


# Define travel advisor tools and ReAct agent
supervisor_agent_tools = [
    make_handoff_tool(agent_name="banking_agent"),
]
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

# Define support agent tools and ReAct agent
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
        "You are a banking agent that can give general advise on banking products, branch locations, balances and transfers. "
        "If the user wants a bank loan, transfer to the 'loan_agent' for help. "
        "You MUST include human-readable response before transferring to another agent."
    ),
)

loan_agent_tools = [
    calculate_monthly_payment,
]
loan_agent = create_react_agent(
    model,
    loan_agent_tools,
    state_modifier=(
        "You must ask for the loan amount and the number of years for the loan. When user provides these, calculate the monthly payment using calculate_monthly_payment tool. "
        "You MUST respond with the repayment amounts before transferring to another agent."
    ),
)


def call_supervisor_agent(
        state: MessagesState,
) -> Command[Literal["supervisor_agent", "human"]]:
    response = supervisor_agent.invoke(state)
    return Command(update=response, goto="human")


def call_banking_agent(
        state: MessagesState,
) -> Command[Literal["banking_agent", "human"]]:
    response = banking_agent.invoke(state)
    return Command(update=response, goto="human")


def call_loan_agent(
        state: MessagesState,
) -> Command[Literal["loan_agent", "human"]]:
    response = loan_agent.invoke(state)
    return Command(update=response, goto="human")


def human_node(
        state: MessagesState, config
) -> Command[Literal["supervisor_agent", "banking_agent", "loan_agent", "human"]]:
    """A node for collecting user input."""

    user_input = interrupt(value="Ready for user input.")

    # identify the last active agent
    # (the last active node before returning to human)
    langgraph_triggers = config["metadata"]["langgraph_triggers"]
    if len(langgraph_triggers) != 1:
        raise AssertionError("Expected exactly 1 trigger in human node")

    active_agent = langgraph_triggers[0].split(":")[1]

    return Command(
        update={
            "messages": [
                {
                    "role": "human",
                    "content": user_input,
                }
            ]
        },
        goto=active_agent,
    )


builder = StateGraph(MessagesState)
builder.add_node("supervisor_agent", call_supervisor_agent)
builder.add_node("banking_agent", call_banking_agent)
builder.add_node("loan_agent", call_loan_agent)

# This adds a node to collect human input, which will route
# back to the active agent.
builder.add_node("human", human_node)

# We'll always start with supervisor agent.
builder.add_edge(START, "supervisor_agent")

# Compile the graph and set up CosmosDB as the checkpointer storage
checkpointer = CosmosDBSaver(database_name=DATABASE_NAME, container_name=CONTAINER_NAME)
graph = builder.compile(checkpointer=checkpointer)
