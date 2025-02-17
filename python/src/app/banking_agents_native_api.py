import uuid

from azure.cosmos.exceptions import CosmosHttpResponseError
from fastapi import FastAPI, Depends, HTTPException, Body
from langchain_core.messages import HumanMessage
from pydantic import BaseModel
from typing import List
from langgraph_checkpoint_cosmosdb import CosmosDBSaver
from langgraph.graph.state import CompiledStateGraph
from starlette.middleware.cors import CORSMiddleware
from src.app.banking_agents_native import graph, checkpointer
from src.app.azure_cosmos_db import update_userdata_container, patch_active_agent, fetch_userdata_container, \
    fetch_userdata_container_by_session, delete_userdata_item
import logging

# Setup logging
logging.basicConfig(level=logging.DEBUG)

endpointTitle = "ChatEndpoints"


def get_compiled_graph():
    return graph


app = FastAPI(title="Cosmos DB Multi-Agent Banking API", openapi_url="/cosmos-multi-agent-api.json")

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
    tokensUsed: int = 0
    name: str
    messages: List


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


def create_thread(tenantId: str, userId: str):
    sessionId = str(uuid.uuid4())
    name = "John Doe"
    age = 30
    address = "123 Main St"
    activeAgent = "unknown"
    ChatName = "New Chat"
    messages = []
    update_userdata_container({
        "id": sessionId,
        "tenantId": tenantId,
        "userId": userId,
        "sessionId": sessionId,
        "name": name,
        "age": age,
        "address": address,
        "activeAgent": activeAgent,
        "ChatName": ChatName,
        "messages": messages
    })
    return Session(id=sessionId, sessionId=sessionId, tenantId=tenantId, userId=userId, name=name, age=age,
                   address=address, activeAgent=activeAgent, ChatName=ChatName, messages=messages)


@app.get("/status", tags=[endpointTitle], description="Gets the service status", operation_id="GetServiceStatus", response_description="Success",
         response_model=str)
def get_service_status():
    return "CosmosDBService: initializing"


@app.get("/tenant/{tenantId}/user/{userId}/sessions", description="Creates a chat session for the given tenantId and userId", tags=[endpointTitle], response_model=List[Session])
def get_chat_sessions(tenantId: str, userId: str):
    items = fetch_userdata_container(tenantId, userId)
    sessions = []

    for item in items:
        sessionId = item["sessionId"]
        messages = []

        # Fetch messages using checkpointer.list
        config = {
            "configurable": {
                "thread_id": sessionId,
                "checkpoint_ns": ""  # Ensure this matches the stored data
            }
        }

        logging.debug(f"Fetching messages for sessionId: {sessionId} with config: {config}")

        checkpoints = list(checkpointer.list(config))
        logging.debug(f"Number of checkpoints retrieved: {len(checkpoints)}")

        if checkpoints:
            last_checkpoint = checkpoints[-1]
            for key, value in last_checkpoint.checkpoint.items():
                if key == "channel_values" and "messages" in value:
                    messages.extend(value["messages"])

        # Identify the first HumanMessage that is immediately prior to a non-HumanMessage
        selected_human_index = None
        for i in range(len(messages) - 1):
            if isinstance(messages[i], HumanMessage) and not isinstance(messages[i + 1], HumanMessage):
                selected_human_index = i
                break

        # Keep the selected HumanMessage and all subsequent messages
        if selected_human_index is not None:
            messages = messages[selected_human_index:]
        else:
            messages = []  # No valid human message found, return an empty list

        messages = [
            MessageModel(
                id=str(uuid.uuid4()),
                type="ai_response",
                sessionId=sessionId,
                tenantId=tenantId,
                userId=userId,
                timeStamp=msg.response_metadata.get("timestamp", "") if hasattr(msg, "response_metadata") else "",
                sender="user" if isinstance(msg, HumanMessage) else "assistant",
                senderRole="user" if isinstance(msg, HumanMessage) else "agent",
                text=msg.content if hasattr(msg, "content") else msg.get("content", ""),
                debugLogId=str(uuid.uuid4()),
                tokensUsed=msg.response_metadata.get("token_usage", {}).get("total_tokens", 0) if hasattr(msg,
                                                                                                          "response_metadata") else 0,
                rating=True,
                completionPromptId=""
            )
            for msg in messages
            if msg.content
        ]

        session = {
            "id": sessionId,
            "type": "Session",
            "sessionId": sessionId,
            "tenantId": item["tenantId"],
            "userId": item["userId"],
            "tokensUsed": item.get("tokensUsed", 0),
            "name": item.get("ChatName", "New Chat"),
            "messages": messages  # Ensure messages are included in the session
        }
        sessions.append(session)

    return sessions


# create a function that gets chat session by tenantId, userId, sessionId
@app.get("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/messages", description="Retrieves messages from the sessionId", tags=[endpointTitle],
         response_model=List[MessageModel])
def get_chat_session(tenantId: str, userId: str, sessionId: str):
    sessionId = sessionId
    messages = []

    # Fetch messages using checkpointer.list
    config = {
        "configurable": {
            "thread_id": sessionId,
            "checkpoint_ns": ""  # Ensure this matches the stored data
        }
    }

    logging.debug(f"Fetching messages for sessionId: {sessionId} with config: {config}")

    checkpoints = list(checkpointer.list(config))
    logging.debug(f"Number of checkpoints retrieved: {len(checkpoints)}")

    if checkpoints:
        last_checkpoint = checkpoints[-1]
        for key, value in last_checkpoint.checkpoint.items():
            if key == "channel_values" and "messages" in value:
                messages.extend(value["messages"])

    # Identify the first HumanMessage that is immediately prior to a non-HumanMessage
    selected_human_index = None
    for i in range(len(messages) - 1):
        if isinstance(messages[i], HumanMessage) and not isinstance(messages[i + 1], HumanMessage):
            selected_human_index = i
            break

    # Keep the selected HumanMessage and all subsequent messages
    if selected_human_index is not None:
        messages = messages[selected_human_index:]
    else:
        messages = []  # No valid human message found, return an empty list

    messages = [
        MessageModel(
            id=str(uuid.uuid4()),
            type="ai_response",
            sessionId=sessionId,
            tenantId=tenantId,
            userId=userId,
            timeStamp=msg.response_metadata.get("timestamp", "") if hasattr(msg, "response_metadata") else "",
            sender="user" if isinstance(msg, HumanMessage) else "assistant",
            senderRole="user" if isinstance(msg, HumanMessage) else "agent",
            text=msg.content if hasattr(msg, "content") else msg.get("content", ""),
            debugLogId=str(uuid.uuid4()),
            tokensUsed=msg.response_metadata.get("token_usage", {}).get("total_tokens", 0) if hasattr(msg,
                                                                                                      "response_metadata") else 0,
            rating=True,
            completionPromptId=""
        )
        for msg in messages
        if msg.content
    ]
    return messages


# to be implemented
@app.post("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/message/{messageId}/rate", description="Not yet implemented", tags=[endpointTitle],
          operation_id="RateMessage", response_description="Success", response_model=MessageModel)
def rate_message(tenantId: str, userId: str, sessionId: str, messageId: str, rating: bool):
    return {
        "id": messageId,
        "type": "ai_response",
        "sessionId": sessionId,
        "tenantId": tenantId,
        "userId": userId,
        "timeStamp": "2023-01-01T00:00:00Z",
        "sender": "assistant",
        "senderRole": "agent",
        "text": "This is a rated message",
        "debugLogId": str(uuid.uuid4()),
        "tokensUsed": 0,
        "rating": rating,
        "completionPromptId": ""
    }


# to be implemented
class DebugLog(BaseModel):
    id: str
    sessionId: str
    tenantId: str
    userId: str
    details: str


@app.get("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/completiondetails/{debuglogId}", description="Not yet implemented", tags=[endpointTitle],
         operation_id="GetChatCompletionDetails", response_description="Success", response_model=DebugLog)
def get_chat_completion_details(tenantId: str, userId: str, sessionId: str, debuglogId: str):
    return {
        "id": debuglogId,
        "sessionId": sessionId,
        "tenantId": tenantId,
        "userId": userId,
        "details": "This is a hardcoded debug log detail"
    }


# create a post function that renames the ChatName in the user data container
@app.post("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/rename", description="Renames the chat session", tags=[endpointTitle], response_model=Session)
def rename_chat_session(tenantId: str, userId: str, sessionId: str, newChatSessionName: str):
    items = fetch_userdata_container_by_session(tenantId, userId, sessionId)
    if not items:
        raise HTTPException(status_code=404, detail="Session not found")

    item = items[0]
    item["ChatName"] = newChatSessionName
    update_userdata_container(item)

    return Session(id=item["sessionId"], sessionId=item["sessionId"], tenantId=item["tenantId"], userId=item["userId"],
                   name=item["ChatName"], age=item["age"],
                   address=item["address"], activeAgent=item["activeAgent"], ChatName=newChatSessionName,
                   messages=item["messages"])


def delete_all_thread_records(cosmos_saver: CosmosDBSaver, thread_id: str) -> None:
    """
    Deletes all records related to a given thread in CosmosDB by first identifying all partition keys
    and then deleting every record under each partition key.
    """

    # Step 1: Identify all partition keys related to the thread
    query = "SELECT DISTINCT c.partition_key FROM c WHERE CONTAINS(c.partition_key, @thread_id)"
    parameters = [{"name": "@thread_id", "value": thread_id}]

    partition_keys = list(cosmos_saver.container.query_items(
        query=query, parameters=parameters, enable_cross_partition_query=True
    ))

    if not partition_keys:
        print(f"No records found for thread: {thread_id}")
        return

    print(f"Found {len(partition_keys)} partition keys related to the thread.")

    # Step 2: Delete all records under each partition key
    for partition in partition_keys:
        partition_key = partition["partition_key"]

        # Query all records under the current partition
        record_query = "SELECT c.id FROM c WHERE c.partition_key=@partition_key"
        record_parameters = [{"name": "@partition_key", "value": partition_key}]

        records = list(cosmos_saver.container.query_items(
            query=record_query, parameters=record_parameters, enable_cross_partition_query=True
        ))

        for record in records:
            record_id = record["id"]
            try:
                cosmos_saver.container.delete_item(record_id, partition_key=partition_key)
                print(f"Deleted record: {record_id} from partition: {partition_key}")
            except CosmosHttpResponseError as e:
                print(f"Error deleting record {record_id} (HTTP {e.status_code}): {e.message}")

    print(f"Successfully deleted all records for thread: {thread_id}")


# deletes the session user data container and all messages in the checkpointer store
@app.delete("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}", tags=[endpointTitle], )
def delete_chat_session(tenantId: str, userId: str, sessionId: str):
    delete_userdata_item(tenantId, userId, sessionId)

    # Delete all messages in the checkpointer store
    config = {
        "configurable": {
            "thread_id": sessionId,
            "checkpoint_ns": ""  # Ensure this matches the stored data
        }
    }
    delete_all_thread_records(checkpointer, sessionId)

    return {"message": "Session deleted successfully"}


@app.post("/tenant/{tenantId}/user/{userId}/sessions", tags=[endpointTitle], response_model=Session)
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

    patch_active_agent(tenantId, userId, sessionId, last_agent_name)

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
            tokensUsed=msg.response_metadata.get("token_usage", {}).get("total_tokens", 0) if hasattr(msg,
                                                                                                      "response_metadata") else 0,
            rating=True,
            completionPromptId=""
        )
        for msg in filtered_messages
        if msg.content
    ]


@app.post("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/completion", tags=[endpointTitle],
          response_model=List[MessageModel])
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
        {"configurable": {"thread_id": sessionId, "checkpoint_ns": ""}},
        stream_mode="updates"
    )

    return extract_relevant_messages(response_data, tenantId, userId, sessionId)


@app.post("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/summarize-name", tags=[endpointTitle],
          operation_id="SummarizeChatSessionName", response_description="Success", response_model=str)
def summarize_chat_session_name(tenantId: str, userId: str, sessionId: str,
                                request_body: str = Body(..., media_type="application/json")):
    return "Summarized Chat Session Name not yet implemented"


@app.post("/tenant/{tenantId}/user/{userId}/semanticcache/reset", tags=[endpointTitle],
          operation_id="ResetSemanticCache", response_description="Success")
def reset_semantic_cache(tenantId: str, userId: str):
    return {"message": "Semantic cache reset not yet implemented"}
