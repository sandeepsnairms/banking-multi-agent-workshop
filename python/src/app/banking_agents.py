import random
from typing import Literal
from langchain_core.messages import SystemMessage, HumanMessage, AIMessage
from langchain_core.tools import tool
from langgraph.prebuilt import create_react_agent
from pydantic import BaseModel, Field
from langgraph.graph import StateGraph, MessagesState, START, END
from langgraph.types import Command
from azure_open_ai import model
from chat_history import save_message_to_cosmos
import settings

class BankingDetails(BaseModel):
    account_number: str
    amount: float

def supervisor(state: MessagesState) -> Command[str]:
    """Supervisor dynamically determines the next agent."""
    if settings.ACTIVE_AGENT is None:
        print("[DEBUG] Supervisor: Determining the next agent...")
        prompt = ("You are a supervising agent that routes the user to the right agent based on their input. "
                  "Output only the right agent from this text and nothing else. This should be one of:"
                  " 'balance', 'transfers', 'support', 'supervisor', or 'FINISH' if the user no longer wants help.")
        response = model.invoke(state["messages"] + [{"role": "system", "content": prompt}])
        next_agent = response.content.strip()
        if next_agent == "FINISH":
            state["messages"].append({"role": "assistant", "content": "Thank you for using our banking services."})
            save_message_to_cosmos(settings.CURRENT_CONVERSATION_ID, state["messages"][-1])
        else:
            state["messages"].append({"role": "assistant", "content": "Transferring to "+next_agent+" agent."})
        save_message_to_cosmos(settings.CURRENT_CONVERSATION_ID, state["messages"][-1])  # Save the message
        print(f"[DEBUG] Supervisor routing to next agent: {next_agent}")
        return Command(goto=next_agent, update=state)
    if settings.ACTIVE_AGENT == "supervisor":
        # ask user if there is anything else they need help with
        prompt = "Is there anything else you need help with?"
        state["messages"].append({"role": "assistant", "content": prompt})
        settings.ACTIVE_AGENT = None
        save_message_to_cosmos(settings.CURRENT_CONVERSATION_ID, state["messages"][-1])
        return Command(goto=END, update=state)
    else:
        print(f"[DEBUG] Supervisor: Active agent is {settings.ACTIVE_AGENT}, routing back to {settings.ACTIVE_AGENT}")
        return Command(goto=settings.ACTIVE_AGENT, update=state)


@tool
def get_product_advise():
    """Get recommendation for banking product"""
    return random.choice(["advanced main account", "savings account"])


@tool
def get_branch_location(location: str):
    """Get location of bank branches for a given area"""
    return f"there is central bank and trust branch in {location}"


support_agent_tools = [
    get_product_advise,
    get_branch_location,
]

prompt = SystemMessage(
    "You are a general customer support agent that can give general advise on banking products and branch locations. "
    "If the user asks for something else, give some general advise or say you cannot help with that. You MUST include "
    "human-readable response.")
react_support_agent = create_react_agent(
    model,
    tools=support_agent_tools,
    state_modifier=prompt,
)


def support(
        state: MessagesState,
) -> Command[Literal["support", "supervisor", "balance", "transfers"]]:
    """Support agent provides general advice on banking products and branch locations."""
    if settings.ACTIVE_AGENT is None or settings.ACTIVE_AGENT == "supervisor":
        settings.ACTIVE_AGENT = "support"
        prompt = "How can I help?"
        state["messages"].append({"role": "assistant", "content": prompt})
        save_message_to_cosmos(settings.CURRENT_CONVERSATION_ID, state["messages"][-1])  # Save the message
        return Command(goto=END, update=state)
    else:
        response_message = react_support_agent.invoke(state)
        message = get_last_ai_message(response_message["messages"])
        state["messages"].append({"role": "assistant", "content": message.content})
        settings.ACTIVE_AGENT = "supervisor"  # Reset the active agent
        save_message_to_cosmos(settings.CURRENT_CONVERSATION_ID, state["messages"][-1])
        return Command(goto="supervisor", update=state)


def balance(state: MessagesState) -> Command[str]:
    """Bank balance agent responds to balance inquiries."""
    if settings.ACTIVE_AGENT is None or settings.ACTIVE_AGENT == "supervisor":
        settings.ACTIVE_AGENT = "balance"
        prompt = "Please provide your account number to retrieve the balance."
        state["messages"].append({"role": "assistant", "content": prompt})
        save_message_to_cosmos(settings.CURRENT_CONVERSATION_ID, state["messages"][-1])  # Save the message
        return Command(goto=END, update=state)
    else:
        llm_with_tools = model.bind_tools([BankingDetails])
        last_message = state["messages"][-1]  # Get the last message object
        user_response = last_message.content
        print(f"User response: {user_response}")
        account_number_response = llm_with_tools.invoke("Output only the account_number from this text and nothing else: " + user_response)
        account_number = account_number_response.content.strip()
        balance = "5000.00"  # Hardcoded balance for demonstration
        response_message = f"The balance for account {account_number} is {balance}."
        state["messages"].append({"role": "assistant", "content": response_message})
        settings.ACTIVE_AGENT = "supervisor"  # Reset the active agent
        save_message_to_cosmos(settings.CURRENT_CONVERSATION_ID, state["messages"][-1])
        return Command(goto="supervisor", update=state)


def transfers(state: MessagesState) -> Command[str]:
    """Banking agent processes transfers."""
    if settings.ACTIVE_AGENT is None or settings.ACTIVE_AGENT == "supervisor":
        settings.ACTIVE_AGENT = "transfers"
        prompt = "Please provide your account number and the transfer amount."
        state["messages"].append({"role": "assistant", "content": prompt})
        save_message_to_cosmos(settings.CURRENT_CONVERSATION_ID, state["messages"][-1])  # Save the message
        return Command(goto=END, update=state)
    else:
        # Example dynamic data extraction for transfer
        llm_with_tools = model.bind_tools([BankingDetails])
        last_message = state["messages"][-1]  # Get the last message object
        user_response = last_message.content
        print(f"User response: {user_response}")
        account_number_response = llm_with_tools.invoke("Output only the account_number from this text and nothing else: " + user_response)
        account_number = account_number_response.content.strip()
        amount_response = llm_with_tools.invoke("Output only the amount from this text and nothing else: " + user_response)
        amount = "$" + amount_response.content.strip()
        message = f"Transfer of {amount} from account {account_number} has been processed."
        state["messages"].append({"role": "assistant", "content": message})
        settings.ACTIVE_AGENT = "supervisor"  # Reset the active agent
        save_message_to_cosmos(settings.CURRENT_CONVERSATION_ID, state["messages"][-1])
        return Command(goto="supervisor", update=state)


def get_last_human_message(messages_state):
    # Iterate in reverse order to find the last human message
    for message in reversed(messages_state):
        if isinstance(message, HumanMessage):
            return message
    return None


def get_last_ai_message(messages_state):
    # Iterate in reverse order to find the last human message
    for message in reversed(messages_state):
        if isinstance(message, AIMessage):
            return message
    return None


def get_compiled_graph():
    # Build the state graph
    builder = StateGraph(MessagesState)
    builder.add_node(supervisor)
    builder.add_node(balance)
    builder.add_node(transfers)
    builder.add_node(support)
    builder.add_edge(START, "supervisor")
    builder.add_node("FINISH", lambda state: Command(goto=END, update=state))
    return builder.compile()
