
//Create a new Cosmos DB client using the connection string from the appsettings.json file
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Collections.ObjectModel;



//Create a new host builder and configure the services
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        //Add the Cosmos DB client to the services collection
        services.AddSingleton<CosmosClient>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var uri = config.GetValue<string>("CosmosUri");
            var key = config.GetValue<string>("CosmosKey");
            return new CosmosClient(uri, key);
        });

    })
    .Build();

var hostTask = host.RunAsync();


//Semantic Kernel 
//Create a new Cosmos DB database and containers using the cosmos client
var cosmosClient = host.Services.GetRequiredService<CosmosClient>();


// Retry logic for creating the database
DatabaseResponse database = null;
var retryDuration = TimeSpan.FromMinutes(5); // Total retry duration
var retryInterval = TimeSpan.FromSeconds(5); // Interval between retries
var startTime = DateTime.UtcNow;

while (true)
{
    try
    {
        // Attempt to create the database
        database = await cosmosClient.CreateDatabaseIfNotExistsAsync("MultiAgentBankingSK");
        break; // Exit the loop if successful
    }
    catch (HttpRequestException ex)
    {
        Console.WriteLine($"Exception occurred: {ex.Message}");

        // Check if the retry duration has been exceeded
        if (DateTime.UtcNow - startTime > retryDuration)
        {
            Console.WriteLine("Retry duration exceeded. Unable to connect to Cosmos DB.");
            throw; // Re-throw the exception after retries are exhausted
        }

        Console.WriteLine("Retrying in 5 seconds...");
        await Task.Delay(retryInterval); // Wait before retrying
    }
}


await database.Database.CreateContainerIfNotExistsAsync(
    new ContainerProperties
    {
        Id = "AccountsData",
        PartitionKeyDefinitionVersion = PartitionKeyDefinitionVersion.V2,
        PartitionKeyPaths = ["/tenantId", "/accountId"]
    },
    ThroughputProperties.CreateAutoscaleThroughput(1000)
);

await database.Database.CreateContainerIfNotExistsAsync(
    new ContainerProperties
    {
        Id = "ChatsData",
        PartitionKeyDefinitionVersion = PartitionKeyDefinitionVersion.V2,
        PartitionKeyPaths = ["/tenantId", "/userId", "/sessionId"]
    },
    ThroughputProperties.CreateAutoscaleThroughput(1000)
);

await database.Database.CreateContainerIfNotExistsAsync(
    new ContainerProperties
    {
        Id = "Users",
        PartitionKeyDefinitionVersion = PartitionKeyDefinitionVersion.V2,
        PartitionKeyPaths = ["/tenantId"]
    },
    ThroughputProperties.CreateAutoscaleThroughput(1000)
);

await database.Database.CreateContainerIfNotExistsAsync(
    new ContainerProperties
    {
        Id = "OffersData",
        PartitionKeyDefinitionVersion = PartitionKeyDefinitionVersion.V2,
        PartitionKeyPaths = ["/tenantId"],
        IndexingPolicy = new IndexingPolicy
        {
            Automatic = true,
            IndexingMode = IndexingMode.Consistent,
            IncludedPaths =
            {
                new IncludedPath 
                { 
                    Path = "/*" 
                }
            },
            ExcludedPaths =
            {
                new ExcludedPath 
                { 
                    Path = "/\"_etag\"/?"
                },
                new ExcludedPath
                {
                    Path = "/vector/?"
                }
            },
            VectorIndexes = { 
                new VectorIndexPath
                {
                     Path = "/vector",
                     Type = VectorIndexType.QuantizedFlat
                }
            }
        },
        VectorEmbeddingPolicy = new VectorEmbeddingPolicy(
            new Collection<Embedding>
            {
                new Embedding
                {
                    Path = "/vector",
                    DataType = VectorDataType.Float32,
                    DistanceFunction = DistanceFunction.Cosine,
                    Dimensions = 1536
                }
            }
        )
    },
    ThroughputProperties.CreateAutoscaleThroughput(1000)
);

//LangGraph
//Create a new Cosmos DB database and containers using the cosmos client
database = await cosmosClient.CreateDatabaseIfNotExistsAsync("MultiAgentBankingLC");

await database.Database.CreateContainerIfNotExistsAsync(
    new ContainerProperties
    {
        Id = "AccountsData",
        PartitionKeyDefinitionVersion = PartitionKeyDefinitionVersion.V2,
        PartitionKeyPaths = ["/tenantId", "/accountId"]
    },
    ThroughputProperties.CreateAutoscaleThroughput(1000)
);

await database.Database.CreateContainerIfNotExistsAsync(
    new ContainerProperties
    {
        Id = "ChatsData",
        PartitionKeyDefinitionVersion = PartitionKeyDefinitionVersion.V2,
        PartitionKeyPaths = ["/tenantId", "/userId", "/sessionId"]
    },
    ThroughputProperties.CreateAutoscaleThroughput(1000)
);

await database.Database.CreateContainerIfNotExistsAsync(
    new ContainerProperties
    {
        Id = "Users",
        PartitionKeyDefinitionVersion = PartitionKeyDefinitionVersion.V2,
        PartitionKeyPaths = ["/tenantId"]
    },
    ThroughputProperties.CreateAutoscaleThroughput(1000)
);

await database.Database.CreateContainerIfNotExistsAsync(
    new ContainerProperties
    {
        Id = "OffersData",
        PartitionKeyDefinitionVersion = PartitionKeyDefinitionVersion.V2,
        PartitionKeyPaths = ["/tenantId"],
        IndexingPolicy = new IndexingPolicy
        {
            Automatic = true,
            IndexingMode = IndexingMode.Consistent,
            IncludedPaths =
            {
                new IncludedPath
                {
                    Path = "/*"
                }
            },
            ExcludedPaths =
            {
                new ExcludedPath
                {
                    Path = "/\"_etag\"/?"
                },
                new ExcludedPath
                {
                    Path = "/vector/?"
                }
            },
            VectorIndexes = {
                new VectorIndexPath
                {
                     Path = "/vector",
                     Type = VectorIndexType.QuantizedFlat
                }
            }
        },
        VectorEmbeddingPolicy = new VectorEmbeddingPolicy(
            new Collection<Embedding>
            {
                new Embedding
                {
                    Path = "/vector",
                    DataType = VectorDataType.Float32,
                    DistanceFunction = DistanceFunction.Cosine,
                    Dimensions = 1536
                }
            }
        )
    },
    ThroughputProperties.CreateAutoscaleThroughput(1000)
);

await database.Database.CreateContainerIfNotExistsAsync(
    new ContainerProperties
    {
        Id = "Checkpoints",
        PartitionKeyDefinitionVersion = PartitionKeyDefinitionVersion.V2,
        PartitionKeyPaths = ["/partition_key"]
    },
    ThroughputProperties.CreateAutoscaleThroughput(1000)
);

await database.Database.CreateContainerIfNotExistsAsync(
    new ContainerProperties
    {
        Id = "ChatHistory",
        PartitionKeyDefinitionVersion = PartitionKeyDefinitionVersion.V2,
        PartitionKeyPaths = ["/sessionId"]
    },
    ThroughputProperties.CreateAutoscaleThroughput(1000)
);

await database.Database.CreateContainerIfNotExistsAsync(
    new ContainerProperties
    {
        Id = "Debug",
        PartitionKeyDefinitionVersion = PartitionKeyDefinitionVersion.V2,
        PartitionKeyPaths = ["/sessionId"]
    },
    ThroughputProperties.CreateAutoscaleThroughput(1000)
);

Console.WriteLine($"Databases and containers created successfully.");

// Stop the host gracefully
await host.StopAsync();

await hostTask;