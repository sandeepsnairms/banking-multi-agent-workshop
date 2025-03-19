using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MultiAgentCopilot.Common.Models.Banking;

namespace BankingServices.Interfaces
{
    public interface IBankDataService
    {
        Task<BankUser> GetUserAsync(string tenantId, string userId);

        Task<List<BankAccount>> GetUserRegisteredAccountsAsync(string tenantId, string userId);

        Task<BankAccount> GetAccountDetailsAsync(string tenantId, string userId, string accountId);

        Task<ServiceRequest> CreateComplaintAsync(string tenantId, string accountId, string userId, string requestAnnotation);

        Task<Offer> GetOfferDetailsByNameAsync(string tenantId, string offerName);

    }
}
