import uuid
from fastapi import FastAPI, Depends, HTTPException, Body
from langchain_core.messages import HumanMessage
from pydantic import BaseModel
from typing import List
from langgraph.graph.state import CompiledStateGraph
from starlette.middleware.cors import CORSMiddleware
from src.app.banking_agents_native import graph
from src.app.azure_cosmos_db import userdata_container
import logging

# Setup logging
logging.basicConfig(level=logging.DEBUG)


def get_compiled_graph():
    return graph


app = FastAPI(title="Cosmos DB Multi-Agent Banking API")

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


class Session(BaseModel):
    id: str
    type: str = "session"
    sessionId: str
    tenantId: str
    userId: str


def create_thread(tenantId: str, userId: str):
    sessionId = str(uuid.uuid4())
    userdata_container.upsert_item({
        "id": sessionId,
        "tenantId": tenantId,
        "userId": userId,
        "sessionId": sessionId,
        "activeAgent": "unknown"
    })
    return Session(id=sessionId, sessionId=sessionId, tenantId=tenantId, userId=userId)


@app.post("/tenant/{tenantId}/user/{userId}/sessions", response_model=Session)
def create_chat_session(tenantId: str, userId: str):
    return create_thread(tenantId, userId)


class MessageModel(BaseModel):
    id: str
    type: str
    sessionId: str
    tenantId: str
    userId: str
    timeStamp: str
    sender: str
    senderRole: str
    text: str
    debugLogId: str
    tokensUsed: int
    rating: bool
    completionPromptId: str


def extract_relevant_messages(response_data, tenantId, userId, sessionId):
    if not response_data:
        return []

    last_agent_node = None
    last_agent_name = "unknown"
    for i in range(len(response_data) - 1, -1, -1):
        if "__interrupt__" in response_data[i]:
            if i > 0:
                last_agent_node = response_data[i - 1]
                last_agent_name = list(last_agent_node.keys())[0]
            break

    userdata_container.upsert_item({
        "id": sessionId,
        "tenantId": tenantId,
        "userId": userId,
        "sessionId": sessionId,
        "activeAgent": last_agent_name
    })
    if not last_agent_node:
        return []

    messages = []
    for key, value in last_agent_node.items():
        if isinstance(value, dict) and "messages" in value:
            messages.extend(value["messages"])

    last_user_index = -1
    for i in range(len(messages) - 1, -1, -1):
        if isinstance(messages[i], HumanMessage):
            last_user_index = i
            break

    if last_user_index == -1:
        return []

    filtered_messages = messages[last_user_index:]

    return [
        MessageModel(
            id=str(uuid.uuid4()),
            type="ai_response",
            sessionId=sessionId,
            tenantId=tenantId,
            userId=userId,
            timeStamp=msg.response_metadata.get("timestamp", "") if hasattr(msg, "response_metadata") else "",
            sender="user" if isinstance(msg, HumanMessage) else "assistant",
            senderRole="user" if isinstance(msg, HumanMessage) else last_agent_name,
            text=msg.content if hasattr(msg, "content") else msg.get("content", ""),
            debugLogId=str(uuid.uuid4()),
            tokensUsed=msg.response_metadata.get("token_usage", {}).get("total_tokens", 0) if hasattr(msg, "response_metadata") else 0,
            rating=True,
            completionPromptId=""
        )
        for msg in filtered_messages
        if msg.content
    ]


@app.post("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/completion", response_model=List[MessageModel])
def get_chat_completion(
        tenantId: str,
        userId: str,
        sessionId: str,
        request_body: str = Body(..., media_type="application/json"),  # Expect raw string in request body
        workflow: CompiledStateGraph = Depends(get_compiled_graph)
):
    if not request_body.strip():
        raise HTTPException(status_code=400, detail="Request body cannot be empty")

    response_data = workflow.invoke(
        {"messages": [{"role": "user", "content": request_body}]},
        {"configurable": {"thread_id": sessionId}},
        stream_mode="updates"
    )

    return extract_relevant_messages(response_data, tenantId, userId, sessionId)
