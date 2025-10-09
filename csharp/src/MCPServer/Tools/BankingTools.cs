using System.ComponentModel;
using System.Text.Json;
using Banking.Models;
using Banking.Services;
using ModelContextProtocol.Server;

namespace MCPServer.Tools;

/// <summary>
/// Banking search tool for finding offers and products
/// </summary>
[McpServerToolType]
public class BankingTools
{
    private readonly Banking.Services.BankingDataService _bankingService;
    private readonly ILogger<BankingTools> _logger;

    public BankingTools(Banking.Services.BankingDataService bankingService, ILogger<BankingTools> logger)
    {
        _bankingService = bankingService;
        _logger = logger;
        
        // DEBUG: Log constructor initialization
        _logger.LogInformation("?? BankingTools initialized successfully with banking service: {BankingServiceType}", 
            bankingService?.GetType().Name ?? "NULL");
    }


    [McpServerTool, Description("Get the current logged-in BankUser")]
    public async Task<BankUser> GetLoggedInUser(string tenantId, string userId)
    {
        _logger.LogInformation("?? DEBUG: GetLoggedInUser called with TenantId={TenantId}, UserId={UserId}", tenantId, userId);
        
        try
        {
            var result = await _bankingService.GetUserAsync(tenantId, userId);
            _logger.LogInformation("? DEBUG: GetLoggedInUser successful, returned user: {UserName}", result?.Name ?? "NULL");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "? DEBUG: GetLoggedInUser failed with exception: {Message}", ex.Message);
            throw;
        }
    }

    [McpServerTool, Description("Get the current date time in UTC")]
    public DateTime GetCurrentDateTime()
    {
        _logger.LogInformation("?? DEBUG: GetCurrentDateTime called");
        
        var now = DateTime.Now.ToUniversalTime();
        _logger.LogInformation("? DEBUG: GetCurrentDateTime returning: {DateTime}", now);
        return now;
    }

    [McpServerTool, Description("Search for banking offers and products using semantic search")]
    public async Task<List<OfferTerm>> SearchOffers(
        string accountType,
        string requirement,
        string? tenantId = null)
    {
        _logger.LogInformation("?? DEBUG: SearchOffers CALLED with accountType={AccountType}, requirement={Requirement}, tenantId={TenantId}", 
            accountType, requirement, tenantId);
        
        try
        {
            tenantId ??= "default-tenant";
            
            // Parse account type
            if (!Enum.TryParse<AccountType>(accountType, true, out var accType))
            {
                accType = AccountType.Savings; // Default to Savings
            }

            _logger.LogInformation("?? DEBUG: Executing search for {AccountType} offers matching: {Requirement}", accountType, requirement);
            
            var offerTerms = await _bankingService.SearchOfferTermsAsync(tenantId, accType, requirement);
            
            _logger.LogInformation("? DEBUG: SearchOffers completed - found {OfferCount} offers", offerTerms.Count);
           
            return offerTerms;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "?? DEBUG: SearchOffers failed with exception");
            return new List<OfferTerm>();
        }
    }

    [McpServerTool, Description("Get detailed information for a specific banking offer")]
    public async Task<Offer> GetOfferDetails(
        string offerId,
        string? tenantId = null)
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


    [McpServerTool, Description("Get transaction history for a bank account")]
    public async Task<List<BankTransaction>> GetTransactionHistory(
         string accountId,
         DateTime startDate,
         DateTime endDate,
         string? tenantId = null)
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

    [McpServerTool, Description("Get account details for a user")]
    public async Task<BankAccount> GetAccountDetails(
        string accountId,
        string userId,
        string? tenantId = null)
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

    [McpServerTool, Description("Get all registered accounts for a user")]
    public async Task<List<BankAccount>> GetUserAccounts(
        string userId,
        string? tenantId = null)
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


    [McpServerTool, Description("Create a customer service request")]
    public async Task<ServiceRequest> CreateServiceRequest(
        string requestType,
        string description,
        string? accountId = null,
        string? userId = null,
        string? tenantId = null)
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

    [McpServerTool, Description("Get service requests for an account")]
    public async Task<List<ServiceRequest>> GetServiceRequests(
        string accountId,
        string? userId = null,
        string? requestType = null,
        string? tenantId = null)
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

    [McpServerTool, Description("Add annotation to an existing service request")]
    public async Task<bool> AddServiceRequestAnnotation(
        string requestId,
        string accountId,
        string annotation,
        string? tenantId = null)
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

    [McpServerTool, Description("Get telebanker availability information")]
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