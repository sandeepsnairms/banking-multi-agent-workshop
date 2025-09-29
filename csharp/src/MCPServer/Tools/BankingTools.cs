using System.ComponentModel;
using System.Text.Json;
using MCPServer.Services;
using BankingModels;

namespace MCPServer.Tools;

/// <summary>
/// Banking search tool for finding offers and products
/// </summary>
public class BankingTools
{
    private readonly IBankingService _bankingService;
    private readonly ILogger<BankingTools> _logger;

    public BankingTools(IBankingService bankingService, ILogger<BankingTools> logger)
    {
        _bankingService = bankingService;
        _logger = logger;
    }

    [Description("Search for banking offers and products using semantic search")]
    [McpToolTags("offers", "sales", "general")]
    public async Task<List<OfferTerm>> SearchOffers(
        [Description("Type of account (Savings, Checking, etc.)")] string accountType,
        [Description("Customer requirements or preferences")] string requirement,
        [Description("Tenant ID (optional, defaults to 'default-tenant')")] string? tenantId = null)
    {
        try
        {
            tenantId ??= "default-tenant";
            
            // Parse account type
            if (!Enum.TryParse<AccountType>(accountType, true, out var accType))
            {
                accType = AccountType.Savings; // Default to Savings
            }

            _logger.LogInformation("Searching for {AccountType} offers matching: {Requirement}", accountType, requirement);
            
            var offerTerms = await _bankingService.SearchOfferTermsAsync(tenantId, accType, requirement);            
           
            return offerTerms;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching offers");
            return new List<OfferTerm>();
        }
    }

    [Description("Get detailed information for a specific banking offer")]
    [McpToolTags("offers", "sales", "general")]
    public async Task<Offer> GetOfferDetails(
        [Description("Unique identifier of the offer")] string offerId,
        [Description("Tenant ID (optional, defaults to 'default-tenant')")] string? tenantId = null)
    {
        try
        {
            tenantId ??= "default-tenant";
            
            _logger.LogInformation("Getting details for offer: {OfferId}", offerId);
            
            var offer = await _bankingService.GetOfferDetailsAsync(tenantId, offerId);          


            return offer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting offer details");
            return null;
        }
    }


    [Description("Get transaction history for a bank account")]
    [McpToolTags("transactions", "accounts", "general")]
    public async Task<List<BankTransaction>> GetTransactionHistory(
        [Description("Bank account ID")] string accountId,
        [Description("Start date for transaction history")] DateTime startDate,
        [Description("End date for transaction history")] DateTime endDate,
        [Description("Tenant ID (optional, defaults to 'default-tenant')")] string? tenantId = null)
    {
        try
        {
            tenantId ??= "default-tenant";
            
            _logger.LogInformation("Getting transaction history for account {AccountId} from {StartDate} to {EndDate}", 
                accountId, startDate, endDate);
            
            var transactions = await _bankingService.GetTransactionsAsync(tenantId, accountId, startDate, endDate);
                                  
            return transactions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transaction history");
           return new List<BankTransaction>();
        }
    }

    [Description("Get account details for a user")]
    [McpToolTags("accounts", "general")]
    public async Task<BankAccount> GetAccountDetails(
        [Description("Bank account ID")] string accountId,
        [Description("User ID")] string userId,
        [Description("Tenant ID (optional, defaults to 'default-tenant')")] string? tenantId = null)
    {
        try
        {
            tenantId ??= "default-tenant";
            
            _logger.LogInformation("Getting account details for account {AccountId} and user {UserId}", accountId, userId);
            
            var account = await _bankingService.GetAccountDetailsAsync(tenantId, userId, accountId);
                      
           return account;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting account details");
            return null;
        }
    }

    [Description("Get all registered accounts for a user")]
    [McpToolTags("accounts", "general")]
    public async Task<List<BankAccount>> GetUserAccounts(
        [Description("User ID")] string userId,
        [Description("Tenant ID (optional, defaults to 'default-tenant')")] string? tenantId = null)
    {
        try
        {
            tenantId ??= "default-tenant";
            
            _logger.LogInformation("Getting all accounts for user {UserId}", userId);
            
            var accounts = await _bankingService.GetUserRegisteredAccountsAsync(tenantId, userId);

            return accounts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user accounts");
            return new List<BankAccount>();
        }
    }


    [Description("Create a customer service request")]
    [McpToolTags("services", "general")]
    public async Task<ServiceRequest> CreateServiceRequest(
        [Description("Type of service request (FundTransfer, Complaint, TeleBankerCallBack, Fulfilment)")] string requestType,
        [Description("Description of the issue or request")] string description,
        [Description("Associated account ID (optional)")] string? accountId = null,
        [Description("User ID")] string? userId = null,
        [Description("Tenant ID (optional, defaults to 'default-tenant')")] string? tenantId = null)
    {
        try
        {
            tenantId ??= "default-tenant";
            userId ??= "default-user";
            accountId ??= "";
            
            _logger.LogInformation("Creating service request of type {RequestType} for user {UserId}", requestType, userId);
            
            ServiceRequest? serviceRequest = null;
            
            switch (requestType.ToLowerInvariant())
            {
                case "complaint":
                    serviceRequest = await _bankingService.CreateComplaintAsync(tenantId, accountId, userId, description);
                    break;
                    
                case "telebanker":
                case "telebankercallback":
                    var callbackTime = DateTime.UtcNow.AddHours(1); // Default to 1 hour from now
                    serviceRequest = await _bankingService.CreateTeleBankerRequestAsync(tenantId, accountId, userId, description, callbackTime);
                    break;
                    
                case "fulfilment":
                    var fulfilmentDetails = new Dictionary<string, string>
                    {
                        { "description", description },
                        { "requestedDate", DateTime.UtcNow.ToString("yyyy-MM-dd") }
                    };
                    serviceRequest = await _bankingService.CreateFulfilmentRequestAsync(tenantId, accountId, userId, description, fulfilmentDetails);
                    break;
                    
                case "fundtransfer":
                    // For fund transfer, we'd need additional parameters like amount, recipient
                    serviceRequest = await _bankingService.CreateFundTransferRequestAsync(tenantId, accountId, userId, description, "", "", 0);
                    break;
                    
                default:
                    return null;
            }
            
            if (serviceRequest == null)
            {
                return null;
            }           
            
            
            return serviceRequest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating service request");
            return null;
        }
    }

    [Description("Get service requests for an account")]
    [McpToolTags("services", "general")]
    public async Task<List<ServiceRequest>> GetServiceRequests(
        [Description("Associated account ID")] string accountId,
        [Description("User ID (optional)")] string? userId = null,
        [Description("Service request type filter (optional)")] string? requestType = null,
        [Description("Tenant ID (optional, defaults to 'default-tenant')")] string? tenantId = null)
    {
        try
        {
            tenantId ??= "default-tenant";
            
            ServiceRequestType? srType = null;
            if (!string.IsNullOrEmpty(requestType) && Enum.TryParse<ServiceRequestType>(requestType, true, out var parsedType))
            {
                srType = parsedType;
            }
            
            _logger.LogInformation("Getting service requests for account {AccountId}", accountId);
            
            var requests = await _bankingService.GetServiceRequestsAsync(tenantId, accountId, userId, srType);               
            
            return requests;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting service requests");
            return new List<ServiceRequest>();
        }
    }

    [Description("Add annotation to an existing service request")]
    [McpToolTags("services", "general")]
    public async Task<bool> AddServiceRequestAnnotation(
        [Description("Service request ID")] string requestId,
        [Description("Associated account ID")] string accountId,
        [Description("Annotation to add")] string annotation,
        [Description("Tenant ID (optional, defaults to 'default-tenant')")] string? tenantId = null)
    {
        try
        {
            tenantId ??= "default-tenant";
            
            _logger.LogInformation("Adding annotation to service request {RequestId}", requestId);
            
            var success = await _bankingService.AddServiceRequestDescriptionAsync(tenantId, accountId, requestId, annotation);
                      
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding service request annotation");
            return false;
        }
    }

    [Description("Get telebanker availability information")]
    [McpToolTags("services", "general")]
    public async Task<string> GetTeleBankerAvailability()
    {
        try
        {
            _logger.LogInformation("Getting telebanker availability");
            
            var availability = await _bankingService.GetTeleBankerAvailabilityAsync();
                                  
            return availability;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting telebanker availability");
            return string.Empty;
        }
    }
}