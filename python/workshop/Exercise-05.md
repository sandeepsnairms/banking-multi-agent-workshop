# Exercise 05 - Multi-tenant API Implementation

[< Previous Exercise](./Exercise-04.md) - **[Home](../README.md)**

## Introduction

In this lab, you'll create a FastAPI application that supports multi-tenancy for the banking agents system. The API will handle session management, message processing, and agent coordination.


## Description

## Learning Path

The workshop follows a progressive learning path:

1. Start with core concepts
2. Build foundational components
3. Add advanced features
4. Integrate components
5. Deploy production-ready system


## Presentation (15 mins)

- API design principles
- Multi-tenancy patterns
- Security best practices

## Steps (30 mins)

  - [Enable Vector Search](#step-1-enable-vector-search)
  - [Update Cosmos DB Configuration](#step-2-update-cosmos-db-configuration)
  - [Create Agent Transfer Tool](#step-3-create-the-tests)
  - [Create the Banking Agents](#step-4-testing-the-implementation)


//old steps
1. Create FastAPI endpoints
2. Implement:
   - Multi-tenant support
   - Authentication/authorization
   - Basic frontend integration
3. Perform end-to-end testing

### Step 1: Basic Setup and Models

TBD need overview and explanation for each step

```python
import uuid
from azure.cosmos.exceptions import CosmosHttpResponseError
from fastapi import FastAPI, Depends, HTTPException, Body
from langchain_core.messages import HumanMessage, ToolMessage
from pydantic import BaseModel
from typing import List
from src.app.langgraph_checkpoint_cosmosdb import CosmosDBSaver
from langgraph.graph.state import CompiledStateGraph
from starlette.middleware.cors import CORSMiddleware
from src.app.banking_agents import graph, checkpointer
import logging

# Setup logging
logging.basicConfig(level=logging.DEBUG)

endpointTitle = "ChatEndpoints"

# FastAPI app initialization
app = FastAPI(title="Cosmos DB Multi-Agent Banking API", openapi_url="/cosmos-multi-agent-api.json")

# CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


# Pydantic models for request/response validation
class DebugLog(BaseModel):
    id: str
    sessionId: str
    tenantId: str
    userId: str
    details: str


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


def get_compiled_graph():
    return graph
```

### Step 2: Session Management

TBD need overview and explanation for each step

```python
def create_thread(tenantId: str, userId: str):
    """Creates a new chat session."""
    sessionId = str(uuid.uuid4())
    name = "John Doe"  # Default name, should be fetched from user profile in production
    age = 30
    address = "123 Main St"
    activeAgent = "unknown"
    ChatName = "New Chat"
    messages = []

    # Create session data in Cosmos DB
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

    return Session(
        id=sessionId,
        sessionId=sessionId,
        tenantId=tenantId,
        userId=userId,
        name=name,
        messages=messages
    )

@app.post("/tenant/{tenantId}/user/{userId}/sessions", tags=[endpointTitle], response_model=Session)
def create_chat_session(tenantId: str, userId: str):
    """Creates a new chat session for a user."""
    return create_thread(tenantId, userId)

@app.get("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/messages",
         description="Retrieves messages from the sessionId", tags=[endpointTitle],
         response_model=List[MessageModel])
def get_chat_session(tenantId: str, userId: str, sessionId: str):
    """Retrieves all messages for a specific session."""
    return _fetch_messages_for_session(sessionId, tenantId, userId)
```

### Step 3: Message Processing

TBD need overview and explanation for each step

```python
def _fetch_messages_for_session(sessionId: str, tenantId: str, userId: str) -> List[MessageModel]:
    """Fetches messages for a session from the checkpoint store."""
    messages = []
    config = {
        "configurable": {
            "thread_id": sessionId,
            "checkpoint_ns": ""
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

    # Find the last relevant message sequence
    selected_human_index = None
    for i in range(len(messages) - 1):
        if isinstance(messages[i], HumanMessage) and not isinstance(messages[i + 1], HumanMessage):
            selected_human_index = i
            break

    messages = messages[selected_human_index:] if selected_human_index is not None else []

    # Convert messages to MessageModel format
    return [
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
            tokensUsed=msg.response_metadata.get("token_usage", {}).get("total_tokens", 0) if hasattr(msg, "response_metadata") else 0,
            rating=True,
            completionPromptId=""
        )
        for msg in messages
        if msg.content
    ]
```

### Step 4: Chat Completion and Agent Routing

TBD need overview and explanation for each step

```python
@app.post("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/completion",
          tags=[endpointTitle], response_model=List[MessageModel])
def get_chat_completion(
    tenantId: str,
    userId: str,
    sessionId: str,
    request_body: str = Body(..., media_type="application/json"),
    workflow: CompiledStateGraph = Depends(get_compiled_graph)
):
    """Processes a new message and returns agent responses."""
    if not request_body.strip():
        raise HTTPException(status_code=400, detail="Request body cannot be empty")

    # Retrieve last checkpoint
    config = {"configurable": {"thread_id": sessionId, "checkpoint_ns": ""}}
    checkpoints = list(checkpointer.list(config))

    if not checkpoints:
        # No previous state, start fresh
        new_state = {
            "messages": [{"role": "user", "content": request_body}]
        }
        response_data = workflow.invoke(new_state, config, stream_mode="updates")
    else:
        # Resume from last checkpoint
        last_checkpoint = checkpoints[-1]
        last_state = last_checkpoint.checkpoint

        if "messages" not in last_state:
            last_state["messages"] = []

        last_state["messages"].append({"role": "user", "content": request_body})

        # Extract last active agent
        last_active_agent = "coordinator_agent"
        if "channel_versions" in last_state:
            for key in reversed(last_state["channel_versions"].keys()):
                if key.startswith("branch:") and "__self__:human" in key:
                    last_active_agent = key.split(":")[1]
                    break

        print(f"Resuming execution from last active agent: {last_active_agent}")
        last_state["langgraph_triggers"] = [f"resume:{last_active_agent}"]

        response_data = workflow.invoke(
            last_state,
            config,
            stream_mode="updates"
        )

    return extract_relevant_messages(response_data, tenantId, userId, sessionId)
```

### Step 5: Session Management and Cleanup

TBD need overview and explanation for each step

```python
def delete_all_thread_records(cosmos_saver: CosmosDBSaver, thread_id: str) -> None:
    """Deletes all records related to a thread from Cosmos DB."""
    query = "SELECT DISTINCT c.partition_key FROM c WHERE CONTAINS(c.partition_key, @thread_id)"
    parameters = [{"name": "@thread_id", "value": thread_id}]

    partition_keys = list(cosmos_saver.container.query_items(
        query=query, parameters=parameters, enable_cross_partition_query=True
    ))

    if not partition_keys:
        print(f"No records found for thread: {thread_id}")
        return

    print(f"Found {len(partition_keys)} partition keys related to the thread.")

    for partition in partition_keys:
        partition_key = partition["partition_key"]
        record_query = "SELECT c.id FROM c WHERE c.partition_key=@partition_key"
        record_parameters = [{"name": "@partition_key", "value": partition_key}]

        records = list(cosmos_saver.container.query_items(
            query=record_query, parameters=record_parameters, enable_cross_partition_query=True
        ))

        for record in records:
            try:
                cosmos_saver.container.delete_item(record["id"], partition_key=partition_key)
                print(f"Deleted record: {record['id']} from partition: {partition_key}")
            except CosmosHttpResponseError as e:
                print(f"Error deleting record {record['id']} (HTTP {e.status_code}): {e.message}")

@app.delete("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}", tags=[endpointTitle])
def delete_chat_session(tenantId: str, userId: str, sessionId: str):
    """Deletes a chat session and all related messages."""
    delete_userdata_item(tenantId, userId, sessionId)
    delete_all_thread_records(checkpointer, sessionId)
    return {"message": "Session deleted successfully"}
```

### Step 6: Additional Utility Endpoints

TBD need overview and explanation for each step

```python
@app.get("/status", tags=[endpointTitle])
def get_service_status():
    """Gets the service status."""
    return "CosmosDBService: initializing"

@app.post("/tenant/{tenantId}/user/{userId}/sessions/{sessionId}/message/{messageId}/rate",
          tags=[endpointTitle], response_model=MessageModel)
def rate_message(tenantId: str, userId: str, sessionId: str, messageId: str, rating: bool):
    """Rates a message (placeholder for future implementation)."""
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
```

### Step 7: Testing the implementation

TBD need overview and explanation for each step

Create a test script `test/test_api.py`:

```python
import requests
import uuid

BASE_URL = "http://localhost:8000"
TENANT_ID = "test-tenant"
USER_ID = "test-user"

def test_api():
    # Create session
    session_response = requests.post(
        f"{BASE_URL}/tenant/{TENANT_ID}/user/{USER_ID}/sessions"
    )
    session_id = session_response.json()["sessionId"]
    print(f"Created session: {session_id}")

    # Send message
    message = "I want to open a new account"
    completion_response = requests.post(
        f"{BASE_URL}/tenant/{TENANT_ID}/user/{USER_ID}/sessions/{session_id}/completion",
        json=message
    )
    print(f"Bot response: {completion_response.json()}")

    # Get messages
    messages_response = requests.get(
        f"{BASE_URL}/tenant/{TENANT_ID}/user/{USER_ID}/sessions/{session_id}/messages"
    )
    print(f"Session messages: {messages_response.json()}")

if __name__ == "__main__":
    test_api()
```

### Step 8: Running the Final Solution

1. Start the API server:

```bash
uvicorn src.app.banking_agents_api:app --reload
```

2. Run the test script:

```bash
python test/test_api.py
```

3. Access the Swagger documentation:

```
http://localhost:8000/docs
```

## Validation Checklist

- [ ] item 1
- [ ] item 2
- [ ] item 3

## Common Issues and Solutions

1. Item 1:

   - Sub item 1
   - Sub item 2
   - Sub item 3

1. Item 2:

   - Sub item 1
   - Sub item 2
   - Sub item 3

1. Item 3:

   - Sub item 1
   - Sub item 2
   - Sub item 3


## Wrap up and Conclusion

Thanks!!!

Return to **[Home](../README.md)**