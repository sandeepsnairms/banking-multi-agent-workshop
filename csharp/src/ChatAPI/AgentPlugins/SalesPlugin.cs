using Microsoft.SemanticKernel;
using System.ComponentModel;
using  MultiAgentCopilot.Models.Banking;
using MultiAgentCopilot.Services;

namespace MultiAgentCopilot.Plugins
{
    internal class SalesPlugin : BasePlugin
    {
        public SalesPlugin(ILogger<BasePlugin> logger, BankingDataService bankService, string tenantId, string userId )
            : base(logger, bankService, tenantId, userId)
        {
        }

        [KernelFunction]
        [Description("Search offer terms of all available offers using vector search")]
        public async Task<List<OfferTerm>> SearchOfferTerms(AccountType accountType, string requirementDescription)
        {
            _logger.LogTrace($"Searching terms of all available offers matching '{requirementDescription}'");
            return await _bankService.SearchOfferTermsAsync(_tenantId, accountType, requirementDescription);
        }

        [KernelFunction]
        [Description("Search an offer by name")]
        public async Task<Offer> GetOfferDetailsByName(string offerName)
        {
            _logger.LogTrace($"Fetching Offer by name");
            return await _bankService.GetOfferDetailsByNameAsync(_tenantId, offerName);
        }


        [KernelFunction]
        [Description("Get detail for an offer")]
        public async Task<Offer> GetOfferDetails(string offerId)
        {
            _logger.LogTrace($"Fetching Offer");
            return await _bankService.GetOfferDetailsAsync(_tenantId, offerId);
        }


        [KernelFunction]
        [Description("Register a new account.")]
        public async Task<ServiceRequest> RegisterAccount(string userId, AccountType accType, Dictionary<string,string> fulfilmentDetails)
        {
            _logger.LogTrace($"Registering Account. User ID: {userId}, Account Type: {accType}");
            return await _bankService.CreateFulfilmentRequestAsync(_tenantId, string.Empty,_userId,string.Empty,fulfilmentDetails);
        }

    }
}
