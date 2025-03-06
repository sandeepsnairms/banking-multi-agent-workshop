# Exercise 04 - Implementing Vector Search

[< Previous Exercise](./Exercise-03.md) - **[Home](../README.md)** - [Next Exercise >](./Exercise-05.md)

## Introduction

This lab is too short. Also it doesn't make sense in the context and flow of this workshop.

In this lab, you'll implement vector search for banking products using Azure Cosmos DB NoSQL API's vector search capabilities to provide intelligent product recommendations.

## Description


## Learning Objectives

- Enable vector search in Azure Cosmos DB NoSQL
- Set up vector-enabled container with proper indexing
- Implement product embeddings using Azure OpenAI
- Create semantic search for banking products

## Presentation (15 mins)

- RAG architecture
- Semantic search concepts
- Vector indexing

## Steps (30 mins)

  - [Enable Vector Search](#step-1-enable-vector-search)
  - [Update Cosmos DB Configuration](#step-2-update-cosmos-db-configuration)
  - [Create Agent Transfer Tool](#step-3-create-the-tests)
  - [Create the Banking Agents](#step-4-testing-the-implementation)

//Old steps
1. Implement vector search for:
   - Account types
   - Credit cards
   - Loans/mortgages
2. Set up:
   - Vector indexes
   - Semantic cache
   - Product recommendations
3. Test search functionality

### Step 1: Enable Vector Search

TBD need overview and explanation for each step

First, enable vector search in your Azure Cosmos DB account:

```bash
az cosmosdb update \
     --resource-group <resource-group-name> \
     --name <account-name> \
     --capabilities EnableNoSQLVectorSearch
```

### Step 2: Update Cosmos DB Configuration

TBD need overview and explanation for each step

Update `src/app/services/azure_cosmos_db.py`:

```python
from azure.cosmos.aio import CosmosClient
from azure.cosmos import PartitionKey

class CosmosDB:
    async def initialize(self):
        try:
            # Existing containers remain the same
            await self.database.create_container_if_not_exists(
                id="accounts",
                partition_key=PartitionKey(path="/account_id")
            )
            await self.database.create_container_if_not_exists(
                id="transactions",
                partition_key=PartitionKey(path="/transaction_id")
            )

            # Create products container with vector search
            vector_policy = {
                "vectorEmbeddings": [
                    {
                        "path": "/embedding",
                        "dataType": "float32",
                        "distanceFunction": "cosine",
                        "dimensions": 1536
                    }
                ]
            }

            indexing_policy = {
                "indexingMode": "consistent",
                "automatic": True,
                "includedPaths": [
                    {
                        "path": "/*"
                    }
                ],
                "excludedPaths": [
                    {
                        "path": "/_etag/?"
                    },
                    {
                        "path": "/embedding/*"
                    }
                ],
                "vectorIndexes": [
                    {
                        "path": "/embedding",
                        "type": "diskANN"
                    }
                ]
            }

            await self.database.create_container_if_not_exists(
                id="products",
                partition_key=PartitionKey(path="/category"),
                indexing_policy=indexing_policy,
                vector_policy=vector_policy
            )

            self.accounts = self.database.get_container_client("accounts")
            self.transactions = self.database.get_container_client("transactions")
            self.products = self.database.get_container_client("products")

            return True
        except Exception as e:
            print(f"Error initializing Cosmos DB: {str(e)}")
            return False
```

We need to implement the following later:

### Step 3: Product operations implementation with vector search
TBD need overview and explanation for each step

### Step 4: Azure OpenAI embeddings integration
TBD need overview and explanation for each step

### Step 5: Updated product tools with vector search
TBD need overview and explanation for each step

### Step 6: Create Tests
TBD need overview and explanation for each step

### Step 7: Testing the implementation
TBD need overview and explanation for each step


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

3. Item 3:

   - Sub item 1
   - Sub item 2
   - Sub item 3


## Next Steps

In [Exercise 5](./Exercise-05.md), we will:

1. Create FastAPI endpoints
2. Implement:
   - Multi-tenant support
   - Authentication/authorization
   - Basic frontend integration
3. Perform end-to-end testing

Proceed to [Exercise 5](./Exercise-05.md)

