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

        public abstract IList<AIFunction> GetTools();

        protected AIFunction CreateGetLoggedInUserTool()
        {
            return AIFunctionFactory.Create(async () =>
            {
                _logger.LogTrace($"Get Logged In User for Tenant:{_tenantId} User:{_userId}");
                return await _bankService.GetUserAsync(_tenantId, _userId);
            }, "GetLoggedInUser", "Get the current logged-in BankUser");
        }

        protected AIFunction CreateGetCurrentDateTimeTool()
        {
            return AIFunctionFactory.Create(() =>
            {
                var now = DateTime.Now.ToUniversalTime();
                _logger.LogTrace($"Get Datetime: {now}");
                return now;
            }, "GetCurrentDateTime", "Get the current date time in UTC");
        }

        protected AIFunction CreateGetUserRegisteredAccountsTool()
        {
            return AIFunctionFactory.Create(async () =>
            {
                _logger.LogTrace($"Fetching accounts for Tenant: {_tenantId} User ID: {_userId}");
                return await _bankService.GetUserRegisteredAccountsAsync(_tenantId, _userId);
            }, "GetUserRegisteredAccounts", "Get user registered accounts");
        }
    }
}