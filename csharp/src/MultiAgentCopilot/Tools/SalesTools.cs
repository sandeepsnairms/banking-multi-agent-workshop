using Microsoft.Extensions.AI;
using MultiAgentCopilot.Models.Banking;
using MultiAgentCopilot.Services;
using System.ComponentModel;

namespace MultiAgentCopilot.Tools
{
    public class SalesTools : BaseTools
    {
        public SalesTools(ILogger<SalesTools> logger, BankingDataService bankService, string tenantId, string userId)
            : base(logger, bankService, tenantId, userId)
        {
        }

        public override IList<AIFunction> GetTools()
        {
            return new List<AIFunction>
            {
                CreateGetLoggedInUserTool(),
                CreateGetCurrentDateTimeTool(),
                CreateGetUserRegisteredAccountsTool(),
                CreateRegisterAccountTool(),
                CreateSearchOfferTermsTool(),
                CreateGetOfferDetailsTool()
            };
        }

        private AIFunction CreateRegisterAccountTool()
        {
            return AIFunctionFactory.Create(async (string userId, AccountType accType, Dictionary<string, string> fulfilmentDetails) =>
            {
                _logger.LogTrace($"Registering Account. User ID: {userId}, Account Type: {accType}");
                return await _bankService.CreateFulfilmentRequestAsync(_tenantId, string.Empty, _userId, string.Empty, fulfilmentDetails);
            }, "RegisterAccount", "Register a new account.");
        }

        private AIFunction CreateSearchOfferTermsTool()
        {
            return AIFunctionFactory.Create(async (AccountType accountType, string requirementDescription) =>
            {
                _logger.LogTrace($"Searching terms of all available offers matching '{requirementDescription}'");
                return await _bankService.SearchOfferTermsAsync(_tenantId, accountType, requirementDescription);
            }, "SearchOfferTerms", "Search offer terms of all available offers using vector search");
        }

        private AIFunction CreateGetOfferDetailsTool()
        {
            return AIFunctionFactory.Create(async (string offerId) =>
            {
                _logger.LogTrace($"Fetching Offer");
                return await _bankService.GetOfferDetailsAsync(_tenantId, offerId);
            }, "GetOfferDetails", "Get detail for an offer");
        }
    }
}