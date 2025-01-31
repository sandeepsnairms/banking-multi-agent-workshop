from http.client import HTTPException
from typing import List
import uuid
from fastapi import FastAPI, HTTPException
import settings
from azure_cosmos_db import container

def fetch_conversation_messages(conversation_id: str) -> List[dict]:
    """Fetch all messages for a given conversation_id and reconstruct the messages list."""
    try:
        query = f"SELECT * FROM c WHERE c.conversation_id = '{conversation_id}' ORDER BY c._ts ASC"
        print(f"[DEBUG] Fetching messages for conversation_id: {conversation_id}")
        items = list(container.query_items(query=query, enable_cross_partition_query=True))
        print(f"[DEBUG] Fetched {len(items)} messages for conversation_id: {conversation_id}")
        print(f"Setting active agent")
        if len(items) > 0:
            settings.ACTIVE_AGENT = items[-1]["active_agent"]
        return [item["message"] for item in items]
    except Exception as e:
        print(f"[ERROR] Error fetching messages for conversation_id {conversation_id}: {e}")
        raise HTTPException(status_code=500, detail=f"Error fetching messages: {str(e)}")

def save_message_to_cosmos(conversation_id: str,  message: dict):
    """Save a single message as a separate document in Cosmos DB."""
    try:
        document = {
            "id": str(uuid.uuid4()),  # Unique ID for the document
            "conversation_id": conversation_id,
            "active_agent": settings.ACTIVE_AGENT,
            "message": message,
        }
        container.upsert_item(document)
        print(f"[DEBUG] Message saved to Cosmos DB: {document}")
    except Exception as e:
        print(f"[ERROR] Error saving message to Cosmos DB: {e}")
        raise HTTPException(status_code=500, detail=f"Error saving message: {str(e)}")