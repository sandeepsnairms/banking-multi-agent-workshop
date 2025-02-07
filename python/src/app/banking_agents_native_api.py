import uuid
from fastapi import FastAPI, Depends
from pydantic import BaseModel
from typing import List
from langgraph.graph.state import CompiledStateGraph
from starlette.middleware.cors import CORSMiddleware

# Importing the LangGraph workflow
from src.app.banking_agents_native import graph


def get_compiled_graph():
    return graph


app = FastAPI()

# Enable CORS (Allow Frontend to Access API from Another Domain)
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Change "*" to a specific frontend URL for security, e.g., ["http://localhost:3000"]
    allow_credentials=True,
    allow_methods=["*"],  # Allows all HTTP methods (GET, POST, etc.)
    allow_headers=["*"],  # Allows all headers
)


class ConversationRequest(BaseModel):
    user_message: str
    conversation_id: str = None


class MessageModel(BaseModel):
    content: str
    role: str  # Renamed from "type"


class SimpleResponseModel(BaseModel):
    conversation_id: str
    responses: List[MessageModel]


def get_config(conversation_id: str = None):
    if conversation_id is None or conversation_id == "":
        conversation_id = str(uuid.uuid4())
    return {"configurable": {"thread_id": conversation_id}}


def extract_relevant_messages(response_data):
    """
    Extracts messages after the last 'human' message.
    - Renames 'ai' to 'assistant'
    - Renames 'type' to 'role'
    - Removes empty 'content' messages
    - Ensures duplicate messages before the last human message are not included
    """
    messages = []

    # Traverse through response structure
    for section in response_data:
        for key, value in section.items():
            if isinstance(value, list):
                for conv in value:
                    if isinstance(conv, dict) and "messages" in conv:
                        messages.extend(conv["messages"])
            elif isinstance(value, dict) and "messages" in value:
                messages.extend(value["messages"])

    # Identify the index of the last human message
    last_human_index = None
    for i in range(len(messages) - 1, -1, -1):
        if hasattr(messages[i], "type") and messages[i].type == "human":
            last_human_index = i
            break

    # If there's no human message, return everything
    if last_human_index is None:
        filtered_messages = messages
    else:
        filtered_messages = messages[last_human_index + 1:]  # Get only messages after last human message

    # Convert to desired format and filter empty content
    relevant_messages = [
        MessageModel(
            content=msg.content,
            role="assistant" if msg.type == "ai" else msg.type  # Renaming "type" to "role"
        )
        for msg in filtered_messages if hasattr(msg, "content") and msg.content.strip()
    ]

    return relevant_messages  # Only return messages after the last human message


@app.post("/conversation", tags=["conversation"], response_model=SimpleResponseModel)
def conversation(
        request: ConversationRequest,
        workflow: CompiledStateGraph = Depends(get_compiled_graph)
):
    config = get_config(request.conversation_id)
    response_data = workflow.invoke(
        {"messages": [{"role": "user", "content": request.user_message}]},
        config,
        stream_mode="updates"
    )

    simplified_response = extract_relevant_messages(response_data)

    return SimpleResponseModel(
        conversation_id=config["configurable"]["thread_id"],
        responses=simplified_response
    )

# Run FastAPI server (only for local development)
if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="0.0.0.0", port=8000)
