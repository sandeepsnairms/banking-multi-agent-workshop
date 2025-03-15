from colorama import Fore, Style
from langchain_core.tools import tool
from typing import Annotated
from langchain_core.tools.base import InjectedToolCallId
from langgraph.prebuilt import InjectedState
from langgraph.types import Command


def transfer_to_agent_message(agent):
    print(Fore.LIGHTMAGENTA_EX + f"transfer_to_{agent}..." + Style.RESET_ALL)


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
        transfer_to_agent_message(agent_name)
        return Command(
            goto=agent_name,
            graph=Command.PARENT,
            update={"messages": state["messages"] + [tool_message]},
        )

    return transfer_to_agent
