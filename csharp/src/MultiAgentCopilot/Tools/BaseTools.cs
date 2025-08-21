using Microsoft.Extensions.AI;
using MultiAgentCopilot.Models.Banking;
using MultiAgentCopilot.Services;
using System.ComponentModel;

namespace MultiAgentCopilot.Tools
{
    public abstract class BaseTools
    {
        protected readonly ILogger _logger;
        protected readonly BankingDataService _bankService;
        protected readonly string _userId;
        protected readonly string _tenantId;

        protected BaseTools(ILogger logger, BankingDataService bankService, string tenantId, string userId)
        {
            _logger = logger;
            _tenantId = tenantId;
            _userId = userId;
            _bankService = bankService;
        }

        [Description("Get the current logged-in BankUser")]
        public async Task<BankUser> GetLoggedInUser()
        {
            _logger.LogTrace($"Get Logged In User for Tenant:{_tenantId} User:{_userId}");
            return await _bankService.GetUserAsync(_tenantId, _userId);
        }

        [Description("Get the current date time in UTC")]
        public DateTime GetCurrentDateTime()
        {
            var now = DateTime.Now.ToUniversalTime();
            _logger.LogTrace($"Get Datetime: {now}");
            return now;
        }

        [Description("Get user registered accounts")]
        public async Task<List<BankAccount>> GetUserRegisteredAccounts()
        {
            _logger.LogTrace($"Fetching accounts for Tenant: {_tenantId} User ID: {_userId}");
            return await _bankService.GetUserRegisteredAccountsAsync(_tenantId, _userId);
        }
    }
}