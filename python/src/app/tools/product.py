from random import random

from langchain_core.tools import tool


@tool
def get_product_advise():
    """Get recommendation for banking product"""
    return random.choice(["advanced main account", "savings account"])

@tool
def get_branch_location(location: str):
    """Get location of bank branches for a given area"""
    return f"there is central bank and trust branch in {location}"