using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using MultiAgentCopilot.Common.Models.Banking;
using BankingServices.Interfaces;

namespace MultiAgentCopilot.ChatInfrastructure.Plugins
{
    internal class SalesPlugin : BasePlugin
    {
        public SalesPlugin(ILogger<BasePlugin> logger, IBankDBService bankService, string tenantId, string userId )
            : base(logger, bankService, tenantId, userId)
        {
        }
        
        [KernelFunction]
        [Description("List all available offers")]
        public async Task<List<Offer>> GetOffers(AccountType accountType)
        {
            _logger.LogTrace($"Fetching Offers");
            return await _bankService.GetOffersAsync(_tenantId, accountType);
        }

        [KernelFunction]
        [Description("Register a new account.")]
        public async Task<ServiceRequest> RegisterAccount(string userId, AccountType accType, Dictionary<string,string> fulfilmentDetails)
        {
            _logger.LogTrace($"Registering Account. User ID: {userId}, Account Type: {accType}");
            return await _bankService.CreateFulfilmentRequestAsync(_tenantId, string.Empty,_userId,null,fulfilmentDetails);
        }

    }
}
