using System.ComponentModel;
using MultiAgentCopilot.Models.Banking;
using MultiAgentCopilot.Services;

namespace MultiAgentCopilot.Plugins
{
    public class CoordinatorPlugin: BasePlugin
    {
        public CoordinatorPlugin(ILogger<BasePlugin> logger, BankingDataService bankService, string tenantId, string userId )
           : base(logger, bankService, tenantId, userId)
        {
        }

        [Description("Get account details and balance for a specific account")]
        public async Task<BankAccount> GetAccountDetails(string accountId)
        {
            _logger.LogTrace($"Fetching account details for Tenant: {_tenantId} User: {_userId}, Account: {accountId}");
            return await _bankService.GetAccountDetailsAsync(_tenantId, _userId, accountId);
        }

        [Description("Get basic user information")]
        public async Task<BankUser> GetUserInfo()
        {
            _logger.LogTrace($"Fetching user info for Tenant: {_tenantId} User: {_userId}");
            return await _bankService.GetUserAsync(_tenantId, _userId);
        }
    }
}
