using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MultiAgentCopilot.Common.Models.Banking;

namespace BankingAPI.Interfaces
{
    public interface IBankDBService
    {
        Task<BankUser> GetUserAsync(string tenantId, string userId);

        Task<BankAccount> GetAccountDetailsAsync(string tenantId, string userId, string accountId);

        Task<List<BankAccount>> GetUserRegisteredAccountsAsync(string tenantId, string userId);

        Task<List<BankTransaction>> GetTransactionsAsync(string tenantId, string accountId, DateTime startDate, DateTime endDate);

        Task<ServiceRequest> CreateFundTransferRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, string recipientEmail, string recipientPhone, decimal debitAmount);

        Task<ServiceRequest> CreateTeleBankerRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, DateTime scheduledDateTime);

        Task<ServiceRequest> CreateComplaintAsync(string tenantId, string accountId, string userId, string requestAnnotation);

        Task<ServiceRequest> CreateFulfilmentRequestAsync(string tenantId, string accountId, string userId, string requestAnnotation, Dictionary<string, string> fulfilmentDetails);

        Task<List<ServiceRequest>> GetServiceRequestsAsync(string tenantId, string accountId, string? userId = null, ServiceRequestType? SRType = null);

        Task<bool> AddServiceRequestDescriptionAsync(string tenantId, string accountId, string requestId, string annotationToAdd);

        Task<List<Offer>> GetOffersAsync(string tenantId, AccountType accountType);

        Task<List<String>> GetTeleBankerAvailabilityAsync(string tenantId, AccountType accountType);
    }
}
