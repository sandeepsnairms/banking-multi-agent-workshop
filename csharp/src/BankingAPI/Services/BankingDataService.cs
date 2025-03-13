using System.ComponentModel;
using System.Diagnostics;
using System.Runtime;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Logging;
using MultiAgentCopilot.Common.Models.Configuration;
using Container = Microsoft.Azure.Cosmos.Container;
using Azure.Identity;
using MultiAgentCopilot.Common.Models.Banking;
using Microsoft.Identity.Client;
using System.Text;
using  BankingServices.Interfaces;
using Microsoft.VisualBasic;
using Microsoft.Extensions.Options;
using MultiAgentCopilot.Common.Helper;
using MultiAgentCopilot.Common.Models.Chat;
using Microsoft.Azure.Cosmos.Serialization.HybridRow.Schemas;
using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.AzureCosmosDBNoSQL;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;
using Microsoft.SemanticKernel.Embeddings;

namespace BankingServices.Services
{
    public class BankingDataService: IBankDataService
    {
        private readonly Container _accountData;
        private readonly Container _userData;
        private readonly Container _requestData;
        private readonly Container _offerData;

        private readonly Database _database;
        private readonly CosmosDBSettings _settings;
        private readonly ILogger _logger;

        public bool IsInitialized { get; private set; }

        readonly Kernel _semanticKernel;

        public BankingDataService(
            CosmosDBSettings settings,
            SemanticKernelServiceSettings skSettings,
            ILoggerFactory loggerFactory )
        {
            _settings = settings; 
            ArgumentException.ThrowIfNullOrEmpty(_settings.CosmosUri);

            _logger = loggerFactory.CreateLogger<BankingDataService>();

            _logger.LogInformation("Initializing Banking service.");

            if (!_settings.EnableTracing)
            {
                Type? defaultTrace = Type.GetType("Microsoft.Azure.Cosmos.Core.Trace.DefaultTrace,Microsoft.Azure.Cosmos.Direct");
               
                if (defaultTrace != null)
                {
                    TraceSource? traceSource = (TraceSource?)defaultTrace.GetProperty("TraceSource")?.GetValue(null);
                    if (traceSource != null)
                    {
                        traceSource.Switch.Level = SourceLevels.All;
                        traceSource.Listeners.Clear();
                    }
                }                 
                
            }

            CosmosSerializationOptions options = new()
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            };

            DefaultAzureCredential credential;
            if (string.IsNullOrEmpty(_settings.UserAssignedIdentityClientID))
            {
                credential = new DefaultAzureCredential();
            }
            else
            {
                credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ManagedIdentityClientId = _settings.UserAssignedIdentityClientID
                });

            }

            CosmosClient client = new CosmosClientBuilder(_settings.CosmosUri, credential)
                .WithSerializerOptions(options)
                .WithConnectionModeGateway()
            .Build();

            _database = client?.GetDatabase(_settings.Database) ??
                        throw new ArgumentException("Unable to connect to existing Azure Cosmos DB database.");

            _accountData = _database.GetContainer(_settings.AccountsContainer.Trim());
            _userData = _database.GetContainer(_settings.UserDataContainer.Trim());
            _requestData = _database.GetContainer(_settings.RequestDataContainer.Trim());
            _offerData= _database.GetContainer(_settings.OfferDataContainer.Trim());

            _logger.LogInformation("Banking service initialized for Cosmos DB.");


            // Set up Semantic Kernel with Azure OpenAI and Managed Identity
            var builder = Kernel.CreateBuilder();

            builder.Services.AddSingleton<ILoggerFactory>(loggerFactory);


            builder.AddAzureOpenAITextEmbeddingGeneration(
                skSettings.AzureOpenAISettings.EmbeddingsDeployment,
                skSettings.AzureOpenAISettings.Endpoint,
                credential);

            _semanticKernel = builder.Build();


           _logger.LogInformation("Banking service initialized.");
        }

        public async Task<BankUser> GetUserAsync(string tenantId,string userId)
        {
            try
            {
                var partitionKey = PartitionManager.GetUserDataFullPK(tenantId);

                return await _userData.ReadItemAsync<BankUser>(
                       id: userId,
                       partitionKey: partitionKey);
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                return null;
            }
        }

        public async Task<BankAccount> GetAccountDetailsAsync(string tenantId, string userId, string accountId)
        {
            try
            {
                var partitionKey = PartitionManager.GetAccountsDataFullPK(tenantId, accountId);

                return await _accountData.ReadItemAsync<BankAccount>(
                       id: accountId,
                       partitionKey: partitionKey);
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                return null;
            }
        }

        public async Task<List<BankAccount>> GetUserRegisteredAccountsAsync(string tenantId, string userId)
        {
            try
            {
                QueryDefinition query = new QueryDefinition("SELECT * FROM c WHERE c.type = @type and c.userId=@userId")
                     .WithParameter("@type", nameof(BankAccount))
                     .WithParameter("@userId", userId);

                var partitionKey = PartitionManager.GetAccountsPartialPK(tenantId);
                FeedIterator<BankAccount> response = _accountData.GetItemQueryIterator<BankAccount>(query, null, new QueryRequestOptions() { PartitionKey = partitionKey });

                List<BankAccount> output = new();
                while (response.HasMoreResults)
                {
                    FeedResponse<BankAccount> results = await response.ReadNextAsync();
                    output.AddRange(results);
                }

                return output;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                return null;
            }
        }      

        public async Task<ServiceRequest> CreateComplaintAsync(string tenantId, string accountId, string userId, string requestAnnotation)
        {
            var req = new ServiceRequest(ServiceRequestType.Complaint, tenantId, accountId, userId, requestAnnotation, string.Empty, string.Empty, 0,  DateTime.MinValue, null);
            return await AddServiceRequestAsync(req);
        }

        private async Task<ServiceRequest> AddServiceRequestAsync(ServiceRequest req)
        {
            try
            {
                var partitionKey = PartitionManager.GetAccountsDataFullPK(req.TenantId,req.AccountId);
                ItemResponse<ServiceRequest> response = await _accountData.CreateItemAsync(req, partitionKey);
                return response.Resource;
            }
            catch (CosmosException ex) 
            {
                _logger.LogError(ex.ToString());
                return null;
            }
        }


        public async Task<List<OfferTermBasic>> SearchOfferTermsAsync(string tenantId, AccountType accountType, string requirementDescription)
        {
            // Generate Embedding

            var embeddingModel = _semanticKernel.Services.GetRequiredService<ITextEmbeddingGenerationService>();

            var embedding = await embeddingModel.GenerateEmbeddingAsync(requirementDescription);

            // Convert ReadOnlyMemory<float> to IList<float>
            var embeddingList = embedding.ToArray();

            // Perform Vector Search in Cosmos DB
            try
            {
                var queryDefinition = new QueryDefinition(@"
                       SELECT c.offerId, c.text, c.name
                        FROM c
                        WHERE c.type = 'Term'
                        AND c.accountType = @accountType
                        AND VectorDistance(c.vector, @referenceVector)> 0.075
                        ")
                    .WithParameter("@accountType", accountType.ToString())
                    .WithParameter("@referenceVector", embeddingList);

                List<OfferTermBasic> offerTerms = new List<OfferTermBasic>();
                using (FeedIterator<OfferTermBasic> feedIterator = _offerData.GetItemQueryIterator<OfferTermBasic>(queryDefinition, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(tenantId) }))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<OfferTermBasic> response = await feedIterator.ReadNextAsync();
                        offerTerms.AddRange(response);
                    }
                }

                return offerTerms;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                return new List<OfferTermBasic>();
                //}
            }
        }

        public async Task<Offer> GetOfferDetailsAsync(string tenantId, string offerId)
        {
            try
            {
                var partitionKey = new PartitionKey(tenantId);

                return await _offerData.ReadItemAsync<Offer>(
                       id: offerId,
                       partitionKey: new PartitionKey(tenantId));
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                return null;
            }
        }
    }
}
