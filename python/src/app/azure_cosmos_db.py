import os
from azure.cosmos import CosmosClient, PartitionKey

# Azure Cosmos DB configuration
COSMOS_DB_URL = os.getenv("COSMOSDB_AI_ENDPOINT")
COSMOS_DB_KEY = os.getenv("COSMOSDB_AI_KEY")
DATABASE_NAME = "BankingAgentDemoDB"
CONTAINER_NAME = "ChatHistory"

cosmos_client = None
database = None
container = None

# Initialize Cosmos DB client
try:
    cosmos_client = CosmosClient(COSMOS_DB_URL, credential=COSMOS_DB_KEY)
    database = cosmos_client.create_database_if_not_exists(DATABASE_NAME)
    container = database.create_container_if_not_exists(
        id=CONTAINER_NAME,
        partition_key=PartitionKey(path="/conversation_id"),
        offer_throughput=400,
    )
    print(f"[DEBUG] Connected to Cosmos DB: {DATABASE_NAME}/{CONTAINER_NAME}")
except Exception as e:
    print(f"[ERROR] Error initializing Cosmos DB: {e}")
    raise e