import logging
import os
import re
import time
from datetime import datetime
from typing import Dict, List

from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv
from motor.motor_asyncio import AsyncIOMotorClient
from pymongo import MongoClient, ReturnDocument
from pymongo.auth_oidc import OIDCCallback, OIDCCallbackContext, OIDCCallbackResult

logging.basicConfig(level=logging.ERROR)

load_dotenv(override=False)

DOCUMENTDB_CLUSTER_NAME = os.getenv("DOCUMENTDB_CLUSTER_NAME")
DATABASE_NAME = os.getenv("DOCUMENTDB_DATABASE_NAME", "MultiAgentBanking")
DOCUMENTDB_TOKEN_SCOPE = "https://ossrdbms-aad.database.windows.net/.default"


class AzureIdentityTokenCallback(OIDCCallback):
    def __init__(self, credential: DefaultAzureCredential) -> None:
        self.credential = credential

    def fetch(self, context: OIDCCallbackContext) -> OIDCCallbackResult:
        token = self.credential.get_token(DOCUMENTDB_TOKEN_SCOPE)
        return OIDCCallbackResult(
            access_token=token.token,
            expires_in_seconds=max(0, token.expires_on - time.time()),
        )


if not DOCUMENTDB_CLUSTER_NAME:
    raise ValueError("DOCUMENTDB_CLUSTER_NAME must be configured")

credential = DefaultAzureCredential()
documentdb_uri = (
    f"mongodb+srv://{DOCUMENTDB_CLUSTER_NAME}.global.mongocluster.cosmos.azure.com/"
)
documentdb_client = MongoClient(
    documentdb_uri,
    authMechanism="MONGODB-OIDC",
    authMechanismProperties={"OIDC_CALLBACK": AzureIdentityTokenCallback(credential)},
    retryWrites=False,
)
async_documentdb_client = AsyncIOMotorClient(
    documentdb_uri,
    authMechanism="MONGODB-OIDC",
    authMechanismProperties={"OIDC_CALLBACK": AzureIdentityTokenCallback(credential)},
    retryWrites=False,
)
database = documentdb_client[DATABASE_NAME]

chat_container = database["ChatsData"]
checkpoint_container = database["Checkpoints"]
checkpoint_writes_container = database["CheckpointWrites"]
chat_history_container = database["ChatHistory"]
users_container = database["Users"]
offers_container = database["Offers"]
account_container = database["Accounts"]
transaction_container = database["Transactions"]
service_request_container = database["ServiceRequests"]
debug_container = database["Debug"]


def get_documentdb_client():
    return documentdb_client


def get_cosmos_client():
    """Compatibility alias for existing callers."""
    return documentdb_client


def _upsert(collection, data):
    document_id = data.get("id")
    if not document_id:
        raise ValueError("Document must contain an id field")
    tenant_id = data.get("tenantId")
    query = {"id": document_id}
    if tenant_id:
        query["tenantId"] = tenant_id
    collection.replace_one(query, data, upsert=True)


def vector_search(vectors, accountType, tenantId):
    start_time = time.time()
    print(
        "DOCUMENT_DB: Starting vector search for "
        f"accountType={accountType}, vector_dims={len(vectors) if vectors else 0}"
    )
    try:
        results = offers_container.aggregate(
            [
                {
                    "$search": {
                        "cosmosSearch": {
                            "vector": vectors,
                            "path": "vector",
                            "k": 10,
                        },
                        "returnStoredSource": True,
                    }
                },
                {"$match": {"tenantId": tenantId, "accountType": accountType}},
                {"$limit": 3},
                {"$project": {"_id": 0, "id": 1, "name": 1, "description": 1, "accountType": 1, "terms": 1}},
            ]
        )
        result_list = list(results)
        duration_ms = (time.time() - start_time) * 1000
        print(
            f"DOCUMENT_DB: Vector search took {duration_ms:.2f}ms, "
            f"returned {len(result_list)} results"
        )
        return result_list
    except Exception as error:
        logging.error("Error in vector_search: %s", error)
        return []


def update_chat_container(data):
    _upsert(chat_container, data)


def update_offers_container(data):
    _upsert(offers_container, data)


def update_account_container(data):
    target = {
        "BankAccount": account_container,
        "BankTransaction": transaction_container,
        "ServiceRequest": service_request_container,
    }.get(data.get("type"))
    if target is None:
        raise ValueError("Unsupported account document type")
    _upsert(target, data)


def update_users_container(data):
    _upsert(users_container, data)


def fetch_chat_container_by_tenant_and_user(tenantId, userId):
    return list(chat_container.find({"tenantId": tenantId, "userId": userId}, {"_id": 0}))


def fetch_chat_container_by_session(tenantId, userId, sessionId):
    return list(
        chat_container.find(
            {"tenantId": tenantId, "userId": userId, "sessionId": sessionId},
            {"_id": 0},
        )
    )


def patch_active_agent(tenantId, userId, sessionId, activeAgent):
    chat_container.update_one(
        {"tenantId": tenantId, "userId": userId, "sessionId": sessionId},
        {"$set": {"activeAgent": activeAgent}},
    )


def patch_account_record(tenantId, account_id, balance):
    result = account_container.update_one(
        {"tenantId": tenantId, "accountId": account_id},
        {"$set": {"balance": balance}},
    )
    if result.matched_count == 0:
        raise LookupError(f"Account {account_id} not found")


def delete_userdata_item(tenantId, userId, sessionId):
    chat_container.delete_many(
        {"tenantId": tenantId, "userId": userId, "sessionId": sessionId}
    )


def create_account_record(account_data):
    _upsert(account_container, account_data)


def create_service_request_record(account_data):
    _upsert(service_request_container, account_data)


def fetch_latest_account_number():
    items = account_container.find({}, {"_id": 0, "accountId": 1})
    account_numbers = [
        int(item["accountId"][1:])
        for item in items
        if item.get("accountId", "").startswith("A")
        and item["accountId"][1:].isdigit()
    ]
    return max(account_numbers, default=0)


def fetch_latest_transaction_number(tenantId, account_number):
    item = transaction_container.find_one(
        {"tenantId": tenantId, "accountId": account_number},
        {"_id": 0, "id": 1},
        sort=[("transactionDateTime", -1)],
    )
    if not item:
        return 0
    numeric_part = re.sub(r"\D", "", item["id"])
    return int(numeric_part) if numeric_part else 0


def fetch_account_by_number(account_number, tenantId, userId):
    return account_container.find_one(
        {
            "accountId": account_number,
            "tenantId": tenantId,
            "userId": userId,
        },
        {"_id": 0},
    )


def fetch_transactions_by_date_range(
    tenantId: str, accountId: str, startDate: datetime, endDate: datetime
) -> List[Dict]:
    return list(
        transaction_container.find(
            {
                "tenantId": tenantId,
                "accountId": accountId,
                "transactionDateTime": {
                    "$gte": startDate.isoformat() + "Z",
                    "$lte": endDate.isoformat() + "Z",
                },
            },
            {"_id": 0},
        ).sort("transactionDateTime", 1)
    )


def update_active_agent_in_latest_message(sessionId: str, new_active_agent: str):
    chat_history_container.find_one_and_update(
        {"sessionId": sessionId},
        {"$set": {"sender": new_active_agent}},
        sort=[("timeStamp", -1)],
        return_document=ReturnDocument.AFTER,
    )


def store_chat_history(data):
    _upsert(chat_history_container, data)


def fetch_chat_history_by_session(sessionId):
    return list(chat_history_container.find({"sessionId": sessionId}, {"_id": 0}))


def delete_chat_history_by_session(sessionId):
    chat_history_container.delete_many({"sessionId": sessionId})


def create_transaction_record(transaction_data):
    _upsert(transaction_container, transaction_data)


def fetch_accounts_by_user(tenantId: str, userId: str) -> List[Dict]:
    owner_filter = {"tenantId": tenantId, "userId": userId}
    return list(account_container.find(owner_filter, {"_id": 0}))


def fetch_transactions_by_account_id(tenantId: str, accountId: str) -> List[Dict]:
    return list(
        transaction_container.find(
            {
                "tenantId": tenantId,
                "accountId": accountId,
            },
            {"_id": 0},
        ).sort("transactionDateTime", -1)
    )


def fetch_service_requests_by_tenant(
    tenantId: str, userId: str = None
) -> List[Dict]:
    query = {"tenantId": tenantId}
    if userId:
        query["userId"] = userId
    return list(
        service_request_container.find(query, {"_id": 0}).sort("requestedOn", -1)
    )