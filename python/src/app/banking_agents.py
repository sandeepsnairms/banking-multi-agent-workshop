from typing import Literal
from langgraph.graph import StateGraph, START, MessagesState
from langgraph.prebuilt import create_react_agent
from langgraph.types import Command, interrupt
from langgraph_checkpoint_cosmosdb import CosmosDBSaver
from src.app.services.azure_open_ai import model
from src.app.services.azure_cosmos_db import DATABASE_NAME, CHECKPOINT_CONTAINER, session_container
from src.app.tools.sales import get_offer_information, calculate_monthly_payment, create_account
from src.app.tools.transactions import bank_balance, bank_transfer, get_transaction_history
from src.app.tools.support import service_request, get_branch_location
from src.app.tools.coordinator import create_agent_transfer


coordinator_agent_tools = [
    create_agent_transfer(agent_name="customer_support_agent"),
    create_agent_transfer(agent_name="sales_agent"),
]

coordinator_agent = create_react_agent(
    model,
    coordinator_agent_tools,
    state_modifier=(
        "You are a Chat Initiator and Request Router in a bank."
        "Your primary responsibilities include welcoming users, and routing requests to the appropriate agent."
        "If the user needs general help, transfer to 'customer_support' for help. "
        "If the user wants to open a new account or take our a bank loan, transfer to 'sales_agent'. "
        "If the user wants to check their account balance or make a bank transfer, transfer to 'transactions_agent'. "
        "You MUST include human-readable response before transferring to another agent."
    ),
)

customer_support_agent_tools = [
    get_branch_location,
    service_request,
    create_agent_transfer(agent_name="sales_agent"),
    create_agent_transfer(agent_name="transactions_agent"),
]
customer_support_agent = create_react_agent(
    model,
    customer_support_agent_tools,
    state_modifier=(
        "You are a customer support agent that can give general advice on banking products and branch locations "
        "If the user wants to open a new account or take our a bank loan, transfer to 'sales_agent'. "
        "If the user wants to check their account balance, make a bank transfer, or get transaction history, transfer to 'transactions_agent'. "
        "If the user wants to make a complaint or speak to someone, ask for the user's phone number and email address, and say you will get someone to call them back, call 'service_request' tool with these values and pass config along with a summary of what they said into the requestSummary parameter. "
        "You MUST include human-readable response before transferring to another agent."
    ),
)

transactions_agent_tools = [
    bank_balance,
    bank_transfer,
    get_transaction_history,
    create_agent_transfer(agent_name="customer_support_agent"),
]
transactions_agent = create_react_agent(
    model,
    transactions_agent_tools,
    state_modifier=(
        "You are a banking transactions agent that can handle account balance enquiries and bank transfers."
        "If the user wants to make a deposit or withdrawal or transfer, ask for the amount and the account number which they want to transfer from and to. "
        "Then call 'bank_transfer' tool with toAccount, fromAccount, and amount values. "
        "Make sure you confirm the transaction details with the user before calling the 'bank_transfer' tool. "
        "then call 'bank_transfer' tool with these values. "
        "Ff the user wants to know transaction history, ask for the start and end date, and call 'get_transaction_history' tool with these values. "
        "If the user needs general help, transfer to 'customer_support' for help. "
        "You MUST respond with the repayment amounts before transferring to another agent."
    ),
)



sales_agent_tools = [
    get_offer_information,
    calculate_monthly_payment,
    create_account,
    create_agent_transfer(agent_name="customer_support_agent"),
    create_agent_transfer(agent_name="transactions_agent"),
]

sales_agent = create_react_agent(
    model,
    sales_agent_tools,
    state_modifier=(
        "You are a sales agent that can help users with creating a new account, or taking out bank loans. "
        "If the user wants to check their account balance, make a bank transfer, or get transaction history, transfer to 'transactions_agent'. "
        "If the user wants to create a new account, you must ask for the account holder's name and the initial balance. "
        "Call create_account tool with these values, and also pass the config. Be sure to tell the user their full new account number including A prefix. "
        "If customer wants to open anything other than a banking account, advise that you can only open a banking account and if they want any other sort of account they will need to contact the branch."
        "If user wants to take out a loan, you can offer a loan quote. You must ask for the loan amount and the number of years for the loan. "
        "When user provides these, calculate the monthly payment using calculate_monthly_payment tool and provide the result as part of the response. "
        "Do not return the monthly payment tool call output directly to the user, include it with the rest of your response. "
        "If the user wants to move ahead with the loan, advise that they need to come into the branch to complete the application. "
        "If the wants information about a product or offer, ask whether they want Credit Card or Savings. "
        "If you have that information, call 'get_offer_information' tool with the user prompt, and the accountType ('CreditCard' or 'Savings'). "
        "You MUST respond with the repayment amounts before transferring to another agent."
    ),
)


def call_coordinator_agent(state: MessagesState, config) -> Command[Literal["coordinator_agent", "human"]]:
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")  # Get thread_id from config
    print(f"Calling coordinator agent with Thread ID: {thread_id}")

    userId = config["configurable"].get("userId", "UNKNOWN_USER_ID")
    tenantId = config["configurable"].get("tenantId", "UNKNOWN_TENANT_ID")
    lastActiveAgent = config["configurable"].get("activeAgent", "UNKNOWN_AGENT")
    print(f"Last active agent: {lastActiveAgent}")
    # Get the active agent from Cosmos DB
    activeAgent = session_container.query_items(
        query=f"SELECT c.activeAgent FROM c WHERE c.id = '{thread_id}' AND c.userId = '{userId}' AND c.tenantId = '{tenantId}'",
        enable_cross_partition_query=True
    )
    result = list(activeAgent)
    if result:
        active_agent_value = result[0]['activeAgent']
    else:
        active_agent_value = None  # or handle the case where no result is found
    print(f"Active agent: {active_agent_value}")
    # if active agent is something other than unknown or coordinator_agent,
    # then transfer directly to that agent to respond to the last collected user input
    # Note: this should be redundant (never called) as we get last agent from latest graph state in checkpoint
    # and route back to it using that (see get_chat_completion in banking_agents_api.py).
    # However, left it implemented for belt and braces.
    if active_agent_value is not None and active_agent_value != "unknown" and active_agent_value != "coordinator_agent":
        print(f"routing straight to active agent: ", active_agent_value)
        return Command(update=state, goto=active_agent_value)
    else:
        response = coordinator_agent.invoke(state)
        print(f"collecting user input")
        return Command(update=response, goto="human")


def call_customer_support_agent(state: MessagesState, config) -> Command[Literal["customer_support_agent", "human"]]:
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    #set active agent in config
    config["configurable"]["activeAgent"] = "customer_support_agent"
    print(f"Calling customer_support agent with Thread ID: {thread_id}")

    response = customer_support_agent.invoke(state)
    return Command(update=response, goto="human")


def call_sales_agent(state: MessagesState, config) -> Command[Literal["sales_agent", "human"]]:
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    # Get userId from state
    print(f"Calling sales agent with Thread ID: {thread_id}")
    config["configurable"]["activeAgent"] = "customer_support_agent"
    response = sales_agent.invoke(state, config)  # Invoke sales agent with state
    return Command(update=response, goto="human")


def call_transactions_agent(state: MessagesState, config) -> Command[Literal["transactions_agent", "human"]]:
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    print(f"Calling transactions agent with Thread ID: {thread_id}")
    config["configurable"]["activeAgent"] = "transactions_agent"
    response = transactions_agent.invoke(state)
    return Command(update=response, goto="human")


# The human_node with interrupt function only serves as a mechanism to stop
# the graph and collect user input. Since the graph is being exposed as an API, the Command object
# return value will never be reached, and instead we route back to the agent that asked the question
# by getting latest graph state from checkpoint and retrieving the last agent from there so we can route
# to the right agent (see get_chat_completion in banking_agents_api.py).
# In interactive mode, the Command object would be returned after user input collected, and the graph
# would continue to the active agent per logic below.
def human_node(state: MessagesState, config) -> Command[
    Literal["coordinator_agent", "customer_support_agent", "sales_agent", "human"]]:
    """A node for collecting user input."""
    user_input = interrupt(value="Ready for user input.")
    langgraph_triggers = config["metadata"]["langgraph_triggers"]
    if len(langgraph_triggers) != 1:
        raise AssertionError("Expected exactly 1 trigger in human node")
    active_agent = langgraph_triggers[0].split(":")[1]
    print(f"Active agent: {active_agent}")
    return Command(update={"messages": [{"role": "human", "content": user_input}]}, goto=active_agent)


builder = StateGraph(MessagesState)
builder.add_node("coordinator_agent", call_coordinator_agent)
builder.add_node("customer_support_agent", call_customer_support_agent)
builder.add_node("sales_agent", call_sales_agent)
builder.add_node("transactions_agent", call_transactions_agent)
builder.add_node("human", human_node)

builder.add_edge(START, "coordinator_agent")

checkpointer = CosmosDBSaver(database_name=DATABASE_NAME, container_name=CHECKPOINT_CONTAINER)
graph = builder.compile(checkpointer=checkpointer)
