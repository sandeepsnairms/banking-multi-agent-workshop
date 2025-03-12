from random import random
from typing import Any

from langchain_core.tools import tool

from src.app.services import azure_cosmos_db
from src.app.services.azure_cosmos_db import DATABASE_NAME, offers_container, vector_search
from src.app.services.azure_open_ai import generate_embedding

@tool
def get_offer_information(user_prompt: str, accountType: str) -> list[dict[str, Any]]:
    """Provide information about a product based on the user prompt.
    Takes as input the user prompt as a string."""
    # Perform a vector search on the Cosmos DB container and return results to the agent
    vectors = generate_embedding(user_prompt)
    search_results = vector_search(vectors, accountType)
    return search_results

@tool
def get_branch_location(location: str):
    """Get location of bank branches for a given area"""
    return f"there is central bank and trust branch in {location}"