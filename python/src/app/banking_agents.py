import logging
import uuid
from langchain.schema import AIMessage
from typing import Literal
from langgraph.graph import StateGraph, START, MessagesState
from langgraph.prebuilt import create_react_agent
from langgraph.types import Command, interrupt
from langgraph_checkpoint_cosmosdb import CosmosDBSaver
from src.app.services.azure_open_ai import model
from src.app.services.azure_cosmos_db import DATABASE_NAME, checkpoint_container, chat_container, \
    update_chat_container, patch_active_agent
from src.app.tools.sales import get_offer_information, calculate_monthly_payment, create_account
from src.app.tools.transactions import bank_balance, bank_transfer, get_transaction_history
from src.app.tools.support import service_request, get_branch_location
from src.app.tools.coordinator import create_agent_transfer

local_interactive_mode = False

logging.basicConfig(level=logging.DEBUG)

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
        "If the user wants to make a complaint or speak to someone, ask for the user's phone number and email address, "
        "and say you will get someone to call them back, call 'service_request' tool with these values and pass config along with a summary of what they said into the requestSummary parameter. "
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
        "If the wants information about a product or offer, ask whether they want Credit Card or Savings, then call 'get_offer_information' tool with the user_prompt, and the accountType ('CreditCard' or 'Savings'). "
        "You MUST respond with the repayment amounts before transferring to another agent."
    ),
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
        patch_active_agent(tenantId="cli-test", userId="cli-test", sessionId=thread_id,
                           activeAgent="customer_support_agent")
    response = customer_support_agent.invoke(state)
    return Command(update=response, goto="human")


def call_sales_agent(state: MessagesState, config) -> Command[Literal["sales_agent", "human"]]:
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    if local_interactive_mode:
        patch_active_agent(tenantId="cli-test", userId="cli-test", sessionId=thread_id,
                           activeAgent="sales_agent")
    response = sales_agent.invoke(state, config)  # Invoke sales agent with state
    return Command(update=response, goto="human")


def call_transactions_agent(state: MessagesState, config) -> Command[Literal["transactions_agent", "human"]]:
    thread_id = config["configurable"].get("thread_id", "UNKNOWN_THREAD_ID")
    if local_interactive_mode:
        patch_active_agent(tenantId="cli-test", userId="cli-test", sessionId=thread_id,
                           activeAgent="transactions_agent")
    response = transactions_agent.invoke(state)
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
builder.add_node("sales_agent", call_sales_agent)
builder.add_node("transactions_agent", call_transactions_agent)
builder.add_node("human", human_node)

builder.add_edge(START, "coordinator_agent")

checkpointer = CosmosDBSaver(database_name=DATABASE_NAME, container_name=checkpoint_container)
graph = builder.compile(checkpointer=checkpointer)


def interactive_chat():
    thread_config = {"configurable": {"thread_id": str(uuid.uuid4()), "userId": "cli-test", "tenantId": "cli-test"}}
    global local_interactive_mode
    local_interactive_mode = True
    print("Welcome to the interactive multi-agent shopping assistant.")
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
