using System.ComponentModel;
using System.Diagnostics;
using System.Runtime;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Logging;
using BankingAPI.Models.Configuration;
using Container = Microsoft.Azure.Cosmos.Container;
using Azure.Identity;
using BankingAPI.Models.Banking;
using Microsoft.Identity.Client;
using System.Text;
using  BankingAPI.Interfaces;
using Microsoft.VisualBasic;
using Microsoft.Extensions.Options;

namespace BankingAPI.Services
{
    public class BankingCosmosDBService: IBankDBService
{
        private readonly Container _accountData;
        private readonly Container _userData;
        private readonly Container _requestData;
        private readonly Container _offerData;

        private readonly Database _database;
        private readonly Models.Configuration.BankingCosmosDBSettings _settings;
        private readonly ILogger _logger;

        public bool IsInitialized { get; private set; }

        public BankingCosmosDBService(
            IOptions<BankingCosmosDBSettings> settings,
            ILogger<BankingCosmosDBService> logger)
        {
            _settings = settings.Value; 
            ArgumentException.ThrowIfNullOrEmpty(_settings.CosmosUri);

            _logger = logger;
            _logger.LogInformation("Initializing Cosmos DB service.");

            if (!_settings.EnableTracing)
            {
                Type defaultTrace = Type.GetType("Microsoft.Azure.Cosmos.Core.Trace.DefaultTrace,Microsoft.Azure.Cosmos.Direct");
                TraceSource traceSource = (TraceSource)defaultTrace.GetProperty("TraceSource").GetValue(null);
                traceSource.Switch.Level = SourceLevels.All;
                traceSource.Listeners.Clear();
            }

            CosmosSerializationOptions options = new()
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            };

            CosmosClient client = new CosmosClientBuilder(_settings.CosmosUri, new DefaultAzureCredential())
                .WithSerializerOptions(options)
                .WithConnectionModeGateway()
            .Build();

            _database = client?.GetDatabase(_settings.Database) ??
                        throw new ArgumentException("Unable to connect to existing Azure Cosmos DB database.");

            _accountData = _database.GetContainer(_settings.AccountsContainer.Trim());
            _userData = _database.GetContainer(_settings.UserDataContainer.Trim());
            _requestData = _database.GetContainer(_settings.RequestDataContainer.Trim());
            _offerData= _database.GetContainer(_settings.OfferDataContainer.Trim());

            _logger.LogInformation("Cosmos DB service initialized.");
        }

        public async Task<BankUser> GetUserAsync(string tenantId,string userId)
        {
            try
            {
                return await _userData.ReadItemAsync<BankUser>(
                       id: userId,
                       partitionKey: new PartitionKey(tenantId));
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                return null;
            }
        }

        public async Task<BankAccount> GetAccountDetailsAsync(string tenantId, string accountId)
        {
            try
            {
                return await _userData.ReadItemAsync<BankAccount>(
                       id: accountId,
                       partitionKey: new PartitionKey(tenantId));
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                return null;
            }
        }

        public async Task<List<BankTransaction>> GetTransactionsAsync(string tenanatId, string accountId, DateTime startDate, DateTime endDate)
        {
            try
            {
                QueryDefinition queryDefinition = new QueryDefinition(
                    "SELECT * FROM c WHERE c.accountId = @accountId AND c.transactionDate >= @startDate AND c.transactionDate <= @endDate")
                    .WithParameter("@accountId", accountId)
                    .WithParameter("@startDate", startDate)
                    .WithParameter("@endDate", endDate);              

                List<BankTransaction> transactions = new List<BankTransaction>();
                using (FeedIterator<BankTransaction> feedIterator = _accountData.GetItemQueryIterator<BankTransaction>(queryDefinition, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(tenanatId) }))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<BankTransaction> response = await feedIterator.ReadNextAsync();
                        transactions.AddRange(response);
                    }
                }

                return transactions;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogError(ex.ToString());
                return new List<BankTransaction>();
            }
        }

        public async Task<ServiceRequest> CreateFundTransferRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, string recipientEmail, string recipientPhone, decimal debitAmount)
        {
            var req= new ServiceRequest(ServiceRequestType.FundTransfer, tenantId, accountId, userId, requestAnnotation, recipientEmail, recipientPhone, debitAmount,  DateTime.MinValue,null);
            return await AddServiceRequestAsync(req);
        }

        public async Task<ServiceRequest> CreateTeleBankerRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, DateTime scheduledDateTime)
        {
            var req = new ServiceRequest(ServiceRequestType.TeleBankerCallBack, tenantId, accountId, userId, requestAnnotation, string.Empty, string.Empty, 0,  scheduledDateTime,null);
            return await AddServiceRequestAsync(req);
        }

        public async Task<List<String>> GetTeleBankerAvailabilityAsync(string tenantId, AccountType accountType)
        {
            return null;
        }

        public async Task<ServiceRequest> CreateComplaintAsync(string tenantId, string accountId, string userId, string requestAnnotation)
        {
            var req = new ServiceRequest(ServiceRequestType.Complaint, tenantId, accountId, userId, requestAnnotation, string.Empty, string.Empty, 0,  DateTime.MinValue, null);
            return await AddServiceRequestAsync(req);
        }

        public async Task<ServiceRequest> CreateFulfilmentRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, Dictionary<string,string> fulfilmentDetails)
        {
            var req = new ServiceRequest(ServiceRequestType.Fulfilment, tenantId, accountId, userId, requestAnnotation, string.Empty, string.Empty, 0,  DateTime.MinValue, fulfilmentDetails);
            return await AddServiceRequestAsync(req);
        }


        private async Task<ServiceRequest> AddServiceRequestAsync(ServiceRequest req)
        {
            try
            { 
                ItemResponse<ServiceRequest> response = await _accountData.CreateItemAsync(req, new PartitionKey(req.TenantId));
                return response.Resource;
            }
            catch (CosmosException ex) 
            {
                _logger.LogError(ex.ToString());
                return null;
            }
        }

      
        public async Task<List<ServiceRequest>> GetServiceRequestsAsync(string tenantId, string accountId, string? userId = null, ServiceRequestType? SRType = null)
        {
            try
            {
                var queryBuilder = new StringBuilder("SELECT * FROM c WHERE c.accountId = @accountId");
                var queryDefinition = new QueryDefinition(queryBuilder.ToString())
                    .WithParameter("@accountId", accountId);

                if (!string.IsNullOrEmpty(userId))
                {
                    queryBuilder.Append(" AND c.userId = @userId");
                    queryDefinition = queryDefinition.WithParameter("@userId", userId);
                }

                if (SRType.HasValue)
                {
                    queryBuilder.Append(" AND c.SRType = @SRType");
                    queryDefinition = queryDefinition.WithParameter("@SRType", SRType);
                }

                queryDefinition = new QueryDefinition(queryBuilder.ToString());

                List<ServiceRequest> reqs = new List<ServiceRequest>();
                using (FeedIterator<ServiceRequest> feedIterator = _requestData.GetItemQueryIterator<ServiceRequest>(queryDefinition, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(tenantId) }))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<ServiceRequest> response = await feedIterator.ReadNextAsync();
                        reqs.AddRange(response);
                    }
                }

                return reqs;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                return new List<ServiceRequest>();
            }
        }


        public async Task<bool> AddServiceRequestDescriptionAsync(string tenantId, string requestId, string annotationToAdd)
        {
            try
            {
                var patchOperations = new List<PatchOperation>
                {
                    PatchOperation.Add("/Notes/-", $"[{DateTime.Now.ToUniversalTime().ToString()}] : {annotationToAdd}]")
                };

                ItemResponse<ServiceRequest> response = await _requestData.PatchItemAsync<ServiceRequest>(
                    id: requestId,
                    partitionKey: new PartitionKey(tenantId),
                    patchOperations: patchOperations
                );

                return true;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                return false;
            }
        }


        public async Task<List<BankAccount>> GetUserRegisteredAccountsAsync(string tenantId, string userId, string? accountId = null)
        {
            try
            {
                QueryDefinition queryDefinition;

                if (string.IsNullOrEmpty(accountId))
                {
                    queryDefinition = new QueryDefinition(
                        "SELECT * FROM c WHERE c.userId = @userId")
                        .WithParameter("@userId", userId);
                }
                else
                {
                    queryDefinition = new QueryDefinition(
                        "SELECT * FROM c WHERE c.userId = @userId AND c.accountId = @accountId")
                        .WithParameter("@userId", userId)
                        .WithParameter("@accountId", accountId);
                }

                List<BankAccount> accounts = new List<BankAccount>();
                using (FeedIterator<BankAccount> feedIterator = _accountData.GetItemQueryIterator<BankAccount>(queryDefinition, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(tenantId) }))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<BankAccount> response = await feedIterator.ReadNextAsync();
                        accounts.AddRange(response);
                    }
                }

                return accounts;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                return new List<BankAccount>();
            }
        }


        public async Task<List<Offer>> GetOffersAsync(string tenantId, AccountType accountType)
        {
            try
            {
                var queryDefinition = new QueryDefinition("SELECT * FROM c where c.accountType = @accountType")
                    .WithParameter("@accountType", accountType);
              
                List<Offer> offers = new List<Offer>();
                using (FeedIterator<Offer> feedIterator = _offerData.GetItemQueryIterator<Offer>(queryDefinition, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(tenantId) }))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<Offer> response = await feedIterator.ReadNextAsync();
                        offers.AddRange(response);
                    }
                }

                return offers;
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex.ToString());
                return new List<Offer>();
            }
        }
    }
}
