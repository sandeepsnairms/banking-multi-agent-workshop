using BankingModels;

namespace MultiAgentCopilot.Services;

/// <summary>
/// Simplified banking service interface for MCP server
/// This provides the banking operations without the full Cosmos DB dependencies
/// </summary>
public interface IBankingService
{
    Task<List<OfferTerm>> SearchOfferTermsAsync(string tenantId, AccountType accountType, string requirementDescription);
    Task<Offer> GetOfferDetailsAsync(string tenantId, string offerId);
    Task<List<BankTransaction>> GetTransactionsAsync(string tenantId, string accountId, DateTime startDate, DateTime endDate);
    Task<BankAccount> GetAccountDetailsAsync(string tenantId, string userId, string accountId);
    Task<List<BankAccount>> GetUserRegisteredAccountsAsync(string tenantId, string userId);
    Task<ServiceRequest> CreateComplaintAsync(string tenantId, string accountId, string userId, string requestAnnotation);
    Task<ServiceRequest> CreateTeleBankerRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, DateTime scheduledDateTime);
    Task<ServiceRequest> CreateFulfilmentRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, Dictionary<string, string> fulfilmentDetails);
    Task<ServiceRequest> CreateFundTransferRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, string recipientEmail, string recipientPhone, decimal debitAmount);
    Task<List<ServiceRequest>> GetServiceRequestsAsync(string tenantId, string accountId, string? userId = null, ServiceRequestType? SRType = null);
    Task<bool> AddServiceRequestDescriptionAsync(string tenantId, string accountId, string requestId, string annotationToAdd);
    Task<string> GetTeleBankerAvailabilityAsync();
}

/// <summary>
/// Mock implementation of banking service for demonstration purposes
/// In production, this would be replaced with actual implementation
/// </summary>
public class MockBankingService : IBankingService
{
    private readonly ILogger<MockBankingService> _logger;


    public async Task<BankUser> GetUserAsync(string tenantId, string userId)
    {
        Console.WriteLine($"Getting user {userId} for tenant {tenantId}");

        await Task.Delay(100); // Simulate async operation
        
        return new BankUser
        {
            Id = userId,
            TenantId = tenantId,
            Name = "John",
            PhoneNumber = "+1-555-123-4567",
            Attributes = null,  
            Email = "john.doe@example.com"
        };
    }

    public async Task<List<OfferTerm>> SearchOfferTermsAsync(string tenantId, AccountType accountType, string requirementDescription)
    {
        Console.WriteLine("Searching offer terms for {AccountType} matching '{Requirement}'", accountType, requirementDescription);
        
        await Task.Delay(100); // Simulate async operation
        
        // Return empty list since OfferTerm model is currently commented out
        return new List<OfferTerm>();
    }

    public async Task<Offer> GetOfferDetailsAsync(string tenantId, string offerId)
    {
        Console.WriteLine($"Getting offer details for {offerId}");
        
        await Task.Delay(100); // Simulate async operation
        
        return new Offer
        {
            Id = offerId,
            TenantId = tenantId,
            Name = "Sample Savings Account Offer",
            Description = "High-yield savings account with competitive rates and no monthly fees",
            AccountType = AccountType.Savings,
            EligibilityConditions = new Dictionary<string, string>
            {
                { "minimum_age", "18" },
                { "minimum_deposit", "1000" },
                { "credit_score_required", "650" }
            },
            PrerequsiteSubmissions = new Dictionary<string, string>
            {
                { "identification", "Driver's License or Passport" },
                { "address_proof", "Utility Bill or Bank Statement" },
                { "income_proof", "Pay Stub or Tax Return" }
            }
        };
    }

    public async Task<List<BankTransaction>> GetTransactionsAsync(string tenantId, string accountId, DateTime startDate, DateTime endDate)
    {
        Console.WriteLine($"Getting transactions for account {accountId} from {startDate} to {endDate}");

        await Task.Delay(100); // Simulate async operation
        
        return new List<BankTransaction>
        {
            new BankTransaction
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                AccountId = accountId,
                TransactionDateTime = startDate.AddDays(1),
                DebitAmount = 10000, // $100.00 (stored as cents)
                CreditAmount = 0,
                AccountBalance = 150000L, // $1500.00
                Details = "ATM Withdrawal - Main Street Branch"
            },
            new BankTransaction
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                AccountId = accountId,
                TransactionDateTime = endDate.AddDays(-1),
                DebitAmount = 0,
                CreditAmount = 250000, // $2500.00 (stored as cents)
                AccountBalance = 400000L, // $4000.00
                Details = "Direct Deposit - Salary Payment"
            }
        };
    }

    public async Task<BankAccount> GetAccountDetailsAsync(string tenantId, string userId, string accountId)
    {
        Console.WriteLine($"Getting account details for {accountId}");

        await Task.Delay(100); // Simulate async operation
        
        return new BankAccount
        {
            Id = accountId,
            TenantId = tenantId,
            Name = "Primary Savings Account",
            AccountType = AccountType.Savings,
            Balance = 250000L, // $2500.00 (stored as cents)
            AccountStatus = AccountStatus.Active,
            CardType = CardType.Visa,
            CardNumber = 1234567890123456L,
            Limit = 500000L, // $5000.00 daily limit
            InterestRate = 450, // 4.50% (stored as basis points)
            ShortDescription = "High-yield savings account with competitive interest rates"
        };
    }

    public async Task<List<BankAccount>> GetUserRegisteredAccountsAsync(string tenantId, string userId)
    {
        Console.WriteLine($"Getting accounts for user {userId}");

        await Task.Delay(100); // Simulate async operation
        
        return new List<BankAccount>
        {
            new BankAccount
            {
                Id = "acc-001",
                TenantId = tenantId,
                Name = "Primary Savings Account",
                AccountType = AccountType.Savings,
                Balance = 250000L, // $2500.00
                AccountStatus = AccountStatus.Active,
                CardType = CardType.Visa,
                CardNumber = 1234567890123456L,
                Limit = 500000L,
                InterestRate = 450, // 4.50%
                ShortDescription = "High-yield savings account"
            },
            new BankAccount
            {
                Id = "acc-002",
                TenantId = tenantId,
                Name = "Premium Credit Card",
                AccountType = AccountType.CreditCard,
                Balance = -50000L, // -$500.00 (credit card balance)
                AccountStatus = AccountStatus.Active,
                CardType = CardType.MasterCard,
                CardNumber = 9876543210987654L,
                Limit = 1000000L, // $10,000 credit limit
                InterestRate = 1899, // 18.99% APR
                ShortDescription = "Premium rewards credit card"
            }
        };
    }

    public async Task<ServiceRequest> CreateComplaintAsync(string tenantId, string accountId, string userId, string requestAnnotation)
    {
        Console.WriteLine($"Creating complaint for user {userId}");

        await Task.Delay(100); // Simulate async operation
        
        return new ServiceRequest(
            ServiceRequestType.Complaint,
            tenantId,
            accountId,
            userId,
            requestAnnotation,
            string.Empty,
            string.Empty,
            0,
            DateTime.MinValue,
            null
        );
    }

    public async Task<ServiceRequest> CreateTeleBankerRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, DateTime scheduledDateTime)
    {
        Console.WriteLine($"Creating telebanker request for user {userId}");

        await Task.Delay(100); // Simulate async operation
        
        return new ServiceRequest(
            ServiceRequestType.TeleBankerCallBack,
            tenantId,
            accountId,
            userId,
            requestAnnotation,
            string.Empty,
            string.Empty,
            0,
            scheduledDateTime,
            null
        );
    }

    public async Task<ServiceRequest> CreateFulfilmentRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, Dictionary<string, string> fulfilmentDetails)
    {
        Console.WriteLine($"Creating fulfilment request for user {userId}");

        await Task.Delay(100); // Simulate async operation
        
        return new ServiceRequest(
            ServiceRequestType.Fulfilment,
            tenantId,
            accountId,
            userId,
            requestAnnotation,
            string.Empty,
            string.Empty,
            0,
            DateTime.MinValue,
            fulfilmentDetails
        );
    }

    public async Task<ServiceRequest> CreateFundTransferRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, string recipientEmail, string recipientPhone, decimal debitAmount)
    {
        Console.WriteLine($"Creating fund transfer request for user {userId}");

        await Task.Delay(100); // Simulate async operation
        
        return new ServiceRequest(
            ServiceRequestType.FundTransfer,
            tenantId,
            accountId,
            userId,
            requestAnnotation,
            recipientEmail,
            recipientPhone,
            debitAmount,
            DateTime.MinValue,
            null
        );
    }

    public async Task<List<ServiceRequest>> GetServiceRequestsAsync(string tenantId, string accountId, string? userId = null, ServiceRequestType? SRType = null)
    {
        Console.WriteLine($"Getting service requests for account {accountId}");

        await Task.Delay(100); // Simulate async operation
        
        return new List<ServiceRequest>
        {
            new ServiceRequest(
                ServiceRequestType.Complaint,
                tenantId,
                accountId,
                userId ?? "default-user",
                "Sample complaint about unauthorized transaction",
                string.Empty,
                string.Empty,
                0,
                DateTime.MinValue,
                null
            )
        };
    }

    public async Task<bool> AddServiceRequestDescriptionAsync(string tenantId, string accountId, string requestId, string annotationToAdd)
    {
        Console.WriteLine($"Adding annotation to service request {requestId}");

        await Task.Delay(100); // Simulate async operation
        
        return true; // Mock success
    }

    public async Task<string> GetTeleBankerAvailabilityAsync()
    {
        await Task.Delay(100); // Simulate async operation
        
        return "Monday to Friday, 8 AM to 8 PM Pacific Time";
    }
}