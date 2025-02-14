import os
from azure.cosmos import CosmosClient, PartitionKey

# Azure Cosmos DB configuration
COSMOS_DB_URL = os.getenv("COSMOSDB_ENDPOINT")
COSMOS_DB_KEY = os.getenv("COSMOSDB_KEY")
DATABASE_NAME = "MultiAgentBankingDemoDB"
CONTAINER_NAME = "Chat"

cosmos_client = None
database = None
container = None

# Define Cosmos DB container for user data
USERDATA_CONTAINER = "UserData"
userdata_container = None


# Initialize Cosmos DB client
try:
    cosmos_client = CosmosClient(COSMOS_DB_URL, credential=COSMOS_DB_KEY)
    database = cosmos_client.create_database_if_not_exists(DATABASE_NAME)
    container = database.create_container_if_not_exists(
        id=CONTAINER_NAME,
        partition_key=PartitionKey(path="/partition_key"),
        offer_throughput=400,
    )
    print(f"[DEBUG] Connected to Cosmos DB: {DATABASE_NAME}/{CONTAINER_NAME}")

    database = cosmos_client.get_database_client(DATABASE_NAME)
    userdata_container = database.create_container_if_not_exists(
        id=USERDATA_CONTAINER, partition_key=PartitionKey(path=["/tenantId", "/userId", "/sessionId"], kind="MultiHash")
    )
except Exception as e:
    print(f"[ERROR] Error initializing Cosmos DB: {e}")
    raise e