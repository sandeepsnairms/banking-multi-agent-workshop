from pydantic import BaseModel, Field
from langgraph.graph import StateGraph, MessagesState, START, END
from langgraph.types import Command
from azure_open_ai import model
from chat_history import save_message_to_cosmos
import settings

class GetAgent(BaseModel):
    """Get the value of 'next' which is the next agent to call from the user's message."""
    next: str = Field(
        ...,
        description="The next agent to call based on the user's message; one of 'balance_agent', 'transfers_agent', "
                    "or 'FINISH'"
    )

class BankingDetails(BaseModel):
    account_number: str
    amount: float

def supervisor(state: MessagesState) -> Command[str]:
    """Supervisor dynamically determines the next agent."""
    if settings.ACTIVE_AGENT is None:
        print("[DEBUG] Supervisor: Determining the next agent...")
        prompt = ("You are a supervising agent that allocates tasks."
                  "Based on the user's input, advise them you will call an agent to handle the request. "
                  "Do not add any other information, just advise them which agent you will pass them to."
                  "If an agent hands back to you, ask the user if they need further assistance.")
        response = model.invoke(state["messages"] + [{"role": "system", "content": prompt}])

        # Use dynamic tool binding to determine the next agent
        llm_with_tools = model.bind_tools([GetAgent])
        ai_next = llm_with_tools.invoke(
            "Output only the right agent from this text and nothing else: " + response.content.strip()
        )
        next_agent = ai_next.content.strip()
        print(f"[DEBUG] Supervisor routed to next agent: {next_agent}")
        state["messages"].append({"role": "assistant", "content": response.content})
        save_message_to_cosmos(settings.CURRENT_CONVERSATION_ID, state["messages"][-1])  # Save the message
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


def balance_agent(state: MessagesState) -> Command[str]:
    """Bank balance agent responds to balance inquiries."""
    if settings.ACTIVE_AGENT is None or settings.ACTIVE_AGENT == "supervisor":
        settings.ACTIVE_AGENT = "balance_agent"
        prompt = "Please provide your account number to retrieve the balance."
        state["messages"].append({"role": "assistant", "content": prompt})
        save_message_to_cosmos(settings.CURRENT_CONVERSATION_ID, state["messages"][-1])  # Save the message
        return Command(goto=END, update=state)
    else:
        llm_with_tools = model.bind_tools([BankingDetails])
        last_message = state["messages"][-1]  # Get the last message object
        user_response = last_message.content
        print(f"User response: {user_response}")
        account_number_response = llm_with_tools.invoke(
            "Output only the account_number from this text and nothing else: " + user_response)
        account_number = account_number_response.content.strip()
        balance = "5000.00"  # Hardcoded balance for demonstration
        response_message = f"The balance for account {account_number} is {balance}."
        state["messages"].append({"role": "assistant", "content": response_message})
        settings.ACTIVE_AGENT = "supervisor"  # Reset the active agent
        save_message_to_cosmos(settings.CURRENT_CONVERSATION_ID, state["messages"][-1])
        return Command(goto="supervisor", update=state)


def transfers_agent(state: MessagesState) -> Command[str]:
    """Banking agent processes transfers."""
    if settings.ACTIVE_AGENT is None or settings.ACTIVE_AGENT == "supervisor":
        settings.ACTIVE_AGENT = "transfers_agent"
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
        account_number_response = llm_with_tools.invoke(
            "Output only the account_number from this text and nothing else: " + user_response)
        account_number = account_number_response.content.strip()
        amount_response = llm_with_tools.invoke(
            "Output only the amount from this text and nothing else: " + user_response)
        amount = "$"+amount_response.content.strip()
        transfer_message = f"Transfer of {amount} from account {account_number} has been processed."
        state["messages"].append({"role": "assistant", "content": transfer_message})
        settings.ACTIVE_AGENT = "supervisor"  # Reset the active agent
        save_message_to_cosmos(settings.CURRENT_CONVERSATION_ID, state["messages"][-1])
        return Command(goto="supervisor", update=state)


def get_compiled_graph():
    # Build the state graph
    builder = StateGraph(MessagesState)
    builder.add_node(supervisor)
    builder.add_node(balance_agent)
    builder.add_node(transfers_agent)
    builder.add_edge(START, "supervisor")
    return builder.compile()
