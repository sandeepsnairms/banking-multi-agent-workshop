import logging
import os
import re
import time
from datetime import datetime
from typing import Dict, List

from azure.identity import DefaultAzureCredential
from dotenv import load_dotenv
from pymongo import MongoClient
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


documentdb_client = None
database = None
offers_container = None
account_container = None


def initialize_documentdb_client():
    global documentdb_client, database, offers_container, account_container
    if documentdb_client is not None:
        return
    if not DOCUMENTDB_CLUSTER_NAME:
        logging.warning("DOCUMENTDB_CLUSTER_NAME is not configured")
        return
    try:
        credential = DefaultAzureCredential()
        uri = f"mongodb+srv://{DOCUMENTDB_CLUSTER_NAME}.global.mongocluster.cosmos.azure.com/"
        documentdb_client = MongoClient(
            uri,
            authMechanism="MONGODB-OIDC",
            authMechanismProperties={"OIDC_CALLBACK": AzureIdentityTokenCallback(credential)},
            retryWrites=False,
        )
        database = documentdb_client[DATABASE_NAME]
        offers_container = database["OffersData"]
        account_container = database["AccountsData"]
        print(f"[DEBUG] MCP Server: Azure DocumentDB collections initialized in {DATABASE_NAME}")
    except Exception as error:
        logging.error("MCP Server: Failed to initialize Azure DocumentDB: %s", error)


initialize_documentdb_client()


def is_documentdb_available():
    return account_container is not None and offers_container is not None


def is_cosmos_available():
    """Compatibility alias for existing callers."""
    return is_documentdb_available()


def get_documentdb_client():
    return documentdb_client


def get_cosmos_client():
    """Compatibility alias for existing callers."""
    return documentdb_client


def _upsert(collection, data):
    document_id = data.get("id")
    if not document_id:
        raise ValueError("Document must contain an id field")
    collection.replace_one({"id": document_id}, data, upsert=True)


def vector_search(vectors, accountType):
    if not is_documentdb_available():
        logging.error("MCP Server: Azure DocumentDB is unavailable")
        return []
    try:
        return list(
            offers_container.aggregate(
                [
                    {
                        "$search": {
                            "cosmosSearch": {
                                "vector": vectors,
                                "path": "vector",
                                "k": 3,
                                "filter": {
                                    "$and": [
                                        {"type": {"$eq": "Term"}},
                                        {"accountType": {"$eq": accountType}},
                                    ]
                                },
                            },
                            "returnStoredSource": True,
                        }
                    },
                    {"$project": {"_id": 0, "offerId": 1, "text": 1, "name": 1}},
                    {"$limit": 3},
                ]
            )
        )
    except Exception as error:
        logging.error("MCP Server: Vector search failed: %s", error)
        return []


def create_account_record(account_data):
    if not is_documentdb_available():
        raise RuntimeError("Azure DocumentDB is unavailable")
    _upsert(account_container, account_data)


def create_service_request_record(account_data):
    create_account_record(account_data)


def fetch_latest_account_number():
    items = account_container.find({"type": "BankAccount"}, {"_id": 0, "accountId": 1})
    numbers = [
        int(item["accountId"][1:])
        for item in items
        if item.get("accountId", "").startswith("A")
        and item["accountId"][1:].isdigit()
    ]
    return max(numbers, default=0)


def fetch_latest_transaction_number(account_number):
    item = account_container.find_one(
        {"type": "BankTransaction", "accountId": account_number},
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
            "type": "BankAccount",
            "accountId": account_number,
            "tenantId": tenantId,
            "userId": userId,
        },
        {"_id": 0},
    )


def patch_account_record(tenantId, account_id, balance, userId=None):
    query = {"type": "BankAccount", "tenantId": tenantId, "accountId": account_id}
    if userId:
        query["userId"] = userId
    result = account_container.update_one(query, {"$set": {"balance": balance}})
    if result.matched_count == 0:
        raise LookupError(f"Account {account_id} not found")


def fetch_transactions_by_date_range(
    accountId: str, startDate: datetime, endDate: datetime
) -> List[Dict]:
    return list(
        account_container.find(
            {
                "accountId": accountId,
                "transactionDateTime": {
                    "$gte": startDate.isoformat() + "Z",
                    "$lte": endDate.isoformat() + "Z",
                },
                "type": "BankTransaction",
            },
            {"_id": 0},
        ).sort("transactionDateTime", 1)
    )


def create_transaction_record(transaction_data):
    _upsert(account_container, transaction_data)