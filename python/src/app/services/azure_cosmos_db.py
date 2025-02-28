import os
from azure.cosmos import CosmosClient, PartitionKey
from azure.identity import DefaultAzureCredential

# Azure Cosmos DB configuration
COSMOS_DB_URL = os.getenv("COSMOSDB_ENDPOINT")
DATABASE_NAME = "MultiAgentBankingDemoDB"
CONTAINER_NAME = "Chat"

cosmos_client = None
database = None
container = None
credential = DefaultAzureCredential()

# Define Cosmos DB container for user data
USERDATA_CONTAINER = "UserData"
userdata_container = None
account_container = None

# Initialize Cosmos DB client
try:
    cosmos_client = CosmosClient(COSMOS_DB_URL, credential=credential)
    database = cosmos_client.get_database_client(DATABASE_NAME)
    container = database.create_container_if_not_exists(
        id=CONTAINER_NAME,
        partition_key=PartitionKey(path="/partition_key"),
        offer_throughput=400,
    )
    account_container = database.create_container_if_not_exists(
        id="Account",
        partition_key=PartitionKey(path="/accountId"),
        offer_throughput=400,
    )
    print(f"[DEBUG] Connected to Cosmos DB: {DATABASE_NAME}/{CONTAINER_NAME}")

    database = cosmos_client.get_database_client(DATABASE_NAME)
    userdata_container = database.create_container_if_not_exists(
        # Create a Cosmos DB container with hierarchical partition key
        id=USERDATA_CONTAINER, partition_key=PartitionKey(path=["/tenantId", "/userId", "/sessionId"], kind="MultiHash")
    )
except Exception as e:
    print(f"[ERROR] Error initializing Cosmos DB: {e}")
    raise e


# update the user data container
def update_userdata_container(data):
    try:
        userdata_container.upsert_item(data)
        print(f"[DEBUG] User data saved to Cosmos DB: {data}")
    except Exception as e:
        print(f"[ERROR] Error saving user data to Cosmos DB: {e}")
        raise e


# fetch the user data from the container by tenantId, userId
def fetch_userdata_container(tenantId, userId):
    try:
        query = f"SELECT * FROM c WHERE c.tenantId = '{tenantId}' AND c.userId = '{userId}'"
        items = list(userdata_container.query_items(query=query, enable_cross_partition_query=True))
        print(f"[DEBUG] Fetched {len(items)} user data for tenantId: {tenantId}, userId: {userId}")
        return items
    except Exception as e:
        print(f"[ERROR] Error fetching user data for tenantId: {tenantId}, userId: {userId}: {e}")
        raise e


# fetch the user data from the container by tenantId, userId, sessionId
def fetch_userdata_container_by_session(tenantId, userId, sessionId):
    try:
        query = f"SELECT * FROM c WHERE c.tenantId = '{tenantId}' AND c.userId = '{userId}' AND c.sessionId = '{sessionId}'"
        items = list(userdata_container.query_items(query=query, enable_cross_partition_query=True))
        print(
            f"[DEBUG] Fetched {len(items)} user data for tenantId: {tenantId}, userId: {userId}, sessionId: {sessionId}")
        return items
    except Exception as e:
        print(
            f"[ERROR] Error fetching user data for tenantId: {tenantId}, userId: {userId}, sessionId: {sessionId}: {e}")
        raise e


# patch the active agent in the user data container using patch operation
def patch_active_agent(tenantId, userId, sessionId, activeAgent):
    try:
        #filter = "from c WHERE p.used = false"

        operations = [
            {'op': 'replace', 'path': '/activeAgent', 'value': activeAgent}
        ]

        try:
            pk = [tenantId, userId, sessionId]
            userdata_container.patch_item(item=sessionId, partition_key=pk,
                                          patch_operations=operations)
        except Exception as e:
            print('\nError occurred. {0}'.format(e.message))
    except Exception as e:
        print(
            f"[ERROR] Error patching active agent for tenantId: {tenantId}, userId: {userId}, sessionId: {sessionId}: {e}")
        raise e

    # deletes the user data from the container by tenantId, userId, sessionId


def delete_userdata_item(tenantId, userId, sessionId):
    try:
        query = f"SELECT * FROM c WHERE c.tenantId = '{tenantId}' AND c.userId = '{userId}' AND c.sessionId = '{sessionId}'"
        items = list(userdata_container.query_items(query=query, enable_cross_partition_query=True))
        if len(items) == 0:
            print(f"[DEBUG] No user data found for tenantId: {tenantId}, userId: {userId}, sessionId: {sessionId}")
            return
        for item in items:
            userdata_container.delete_item(item, partition_key=[tenantId, userId, sessionId])
            print(f"[DEBUG] Deleted user data for tenantId: {tenantId}, userId: {userId}, sessionId: {sessionId}")
    except Exception as e:
        print(
            f"[ERROR] Error deleting user data for tenantId: {tenantId}, userId: {userId}, sessionId: {sessionId}: {e}")
        raise e


# Create the "Account" container partitioned by accountId


# Function to create an account record
def create_account_record(account_data):
    try:
        account_container.upsert_item(account_data)
        print(f"[DEBUG] Account record created: {account_data}")
    except Exception as e:
        print(f"[ERROR] Error creating account record: {e}")
        raise e


from azure.cosmos import exceptions


def fetch_latest_account_number():
    try:
        query = "SELECT VALUE MAX(c.accountId) FROM c"
        items = list(account_container.query_items(query=query, enable_cross_partition_query=True))
        print(f"[DEBUG] Fetched {len(items)} account numbers")
        if items:
            latest_account_id = items[0]
            if latest_account_id is None:
                return 0
            print(f"[DEBUG] Latest account ID: {latest_account_id}")
            latest_account_number = int(latest_account_id[1:])  # Assuming accountId is in the format 'a<number>'
            print(f"[DEBUG] Latest account number: {latest_account_number}")
            return latest_account_number

        else:
            return None
    except Exception as e:
        print(f"[ERROR] Error fetching latest account number: {e}")
        raise e


# Function to create a transaction record
def create_transaction_record(transaction_data):
    try:
        account_container.upsert_item(transaction_data)
        print(f"[DEBUG] Transaction record created: {transaction_data}")
    except Exception as e:
        print(f"[ERROR] Error creating transaction record: {e}")
        raise e
