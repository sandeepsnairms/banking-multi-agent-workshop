using Microsoft.Extensions.AI;
using MultiAgentCopilot.Services;
using System.ComponentModel;
using Banking.Models;
using Banking.Services;

namespace MultiAgentCopilot.Tools
{
    public abstract class BaseTools
    {
        protected readonly ILogger _logger;
        protected readonly BankingDataService _bankService;


        protected BaseTools(ILogger logger, BankingDataService bankService)
        {
            _logger = logger;
            _bankService = bankService;
        }

        [Description("Get the current logged-in BankUser")]
        public async Task<BankUser> GetLoggedInUser(string tenantId, string userId)
        {
            _logger.LogTrace($"Get Logged In User for Tenant:{tenantId} User:{userId}");
            return await _bankService.GetUserAsync(tenantId, userId );
        }

        [Description("Get the current date time in UTC")]
        public DateTime GetCurrentDateTime()
        {
            var now = DateTime.Now.ToUniversalTime();
            _logger.LogTrace($"Get Datetime: {now}");
            return now;
        }

        [Description("Get user registered accounts")]
        public async Task<List<BankAccount>> GetUserRegisteredAccounts(string tenantId, string userId)
        {
            _logger.LogTrace($"Fetching accounts for Tenant: {tenantId} User ID: {userId}");
            return await _bankService.GetUserRegisteredAccountsAsync(tenantId, userId);
        }
    }
}