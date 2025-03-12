import os
from azure.cosmos import CosmosClient, PartitionKey
from azure.identity import DefaultAzureCredential, ManagedIdentityCredential

# Azure Cosmos DB configuration
COSMOS_DB_URL = os.getenv("COSMOSDB_ENDPOINT")
DATABASE_NAME = "MultiAgentBanking"
CHECKPOINT_CONTAINER = "Checkpoints"

cosmos_client = None
database = None
container = None

# Define Cosmos DB container for user data
SESSION_CONTAINER = "Chat"
DEBUG_CONTAINER = "Debug"
users_container = None
offers_container = None
session_container = None
account_container = None
debug_container = None

try:
    credential = DefaultAzureCredential()
    cosmos_client = CosmosClient(COSMOS_DB_URL, credential=credential)
    print("[DEBUG] Connected to Cosmos DB successfully using DefaultAzureCredential.")
except Exception as dac_error:
    print(f"[ERROR] Failed to authenticate using DefaultAzureCredential: {dac_error}")
    raise dac_error

# Initialize Cosmos DB client
try:
    database = cosmos_client.get_database_client(DATABASE_NAME)
    CHECKPOINT_CONTAINER = database.create_container_if_not_exists(
        id=CHECKPOINT_CONTAINER,
        partition_key=PartitionKey(path="/partition_key"),
    )
    users_container = database.create_container_if_not_exists(
        id="Users",
        partition_key=PartitionKey(path="/tenantId"),
    )

    offers_container = database.create_container_if_not_exists(
        id="Offers",
        partition_key=PartitionKey(path="/tenantId"),
    )

    account_container = database.create_container_if_not_exists(
        id="AccountsData",
        partition_key=PartitionKey(path="/accountId"),
    )
    debug_container = database.create_container_if_not_exists(
        id=DEBUG_CONTAINER,
        partition_key=PartitionKey(path="/sessionId"),
    )
    print(f"[DEBUG] Connected to Cosmos DB: {DATABASE_NAME}")

    database = cosmos_client.get_database_client(DATABASE_NAME)
    session_container = database.create_container_if_not_exists(
        # Create a Cosmos DB container with hierarchical partition key
        id=SESSION_CONTAINER, partition_key=PartitionKey(path=["/tenantId", "/userId", "/sessionId"], kind="MultiHash")
    )
except Exception as e:
    print(f"[ERROR] Error initializing Cosmos DB: {e}")
    raise e


def vector_search(vectors, accountType):
    # Execute the query
    results = offers_container.query_items(
        query='''
        SELECT c.offerId, c.text, c.name
                        FROM c
                        WHERE c.type = 'Term'
                        AND c.accountType = @accountType
                        AND VectorDistance(c.vector, @referenceVector)> 0.075
        ''',
        parameters=[
            {"name": "@accountType", "value": accountType},
            {"name": "@referenceVector", "value": vectors}
        ],
        enable_cross_partition_query=True, populate_query_metrics=True)
    print("Executed vector search in Azure Cosmos DB... \n")
    results = list(results)
    # Extract the necessary information from the results
    formatted_results = []
    for result in results:
        score = result.pop('SimilarityScore')
        formatted_result = {
            'SimilarityScore': score,
            'document': result
        }
        formatted_results.append(formatted_result)
    return formatted_results


# update the user data container
def update_session_container(data):
    try:
        session_container.upsert_item(data)
        print(f"[DEBUG] User data saved to Cosmos DB: {data}")
    except Exception as e:
        print(f"[ERROR] Error saving user data to Cosmos DB: {e}")
        raise e


def update_offers_container(data):
    try:
        offers_container.upsert_item(data)
        print(f"[DEBUG] Offers data saved to Cosmos DB: {data}")
    except Exception as e:
        print(f"[ERROR] Error saving Offers data to Cosmos DB: {e}")
        raise e


def update_account_container(data):
    try:
        account_container.upsert_item(data)
        print(f"[DEBUG] Account data saved to Cosmos DB: {data}")
    except Exception as e:
        print(f"[ERROR] Error saving Account data to Cosmos DB: {e}")
        raise e


def update_users_container(data):
    try:
        users_container.upsert_item(data)
        print(f"[DEBUG] Users data saved to Cosmos DB: {data}")
    except Exception as e:
        print(f"[ERROR] Error saving Users data to Cosmos DB: {e}")
        raise e


# fetch the user data from the container by tenantId, userId
def fetch_session_container_by_tenant_and_user(tenantId, userId):
    try:
        query = f"SELECT * FROM c WHERE c.tenantId = '{tenantId}' AND c.userId = '{userId}'"
        items = list(session_container.query_items(query=query, enable_cross_partition_query=True))
        print(f"[DEBUG] Fetched {len(items)} user data for tenantId: {tenantId}, userId: {userId}")
        return items
    except Exception as e:
        print(f"[ERROR] Error fetching user data for tenantId: {tenantId}, userId: {userId}: {e}")
        raise e


# fetch the user data from the container by tenantId, userId, sessionId
def fetch_session_container_by_session(tenantId, userId, sessionId):
    try:
        query = f"SELECT * FROM c WHERE c.tenantId = '{tenantId}' AND c.userId = '{userId}' AND c.sessionId = '{sessionId}'"
        items = list(session_container.query_items(query=query, enable_cross_partition_query=True))
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
            session_container.patch_item(item=sessionId, partition_key=pk,
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
        items = list(session_container.query_items(query=query, enable_cross_partition_query=True))
        if len(items) == 0:
            print(f"[DEBUG] No user data found for tenantId: {tenantId}, userId: {userId}, sessionId: {sessionId}")
            return
        for item in items:
            session_container.delete_item(item, partition_key=[tenantId, userId, sessionId])
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
