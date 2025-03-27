using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MultiAgentCopilot.Common.Models.Banking;
using BankingServices.Interfaces;

namespace MultiAgentCopilot.ChatInfrastructure.Plugins
{

    public class BasePlugin
    {
        protected readonly ILogger<BasePlugin> _logger;
        protected readonly IBankDataService _bankService;
        protected readonly string _userId;
        protected readonly string _tenantId;

        public BasePlugin(ILogger<BasePlugin> logger, IBankDataService bankService, string tenantId, string userId)
        {
            _logger = logger;
            _tenantId = tenantId;
            _userId = userId;
            _bankService = bankService;
        }

        [KernelFunction("GetLoggedInUser")]
        [Description("Get the current logged-in BankUser")]
        public async Task<BankUser> GetLoggedInUser()
        {
            _logger.LogTrace($"Get Logged In User for Tenant:{_tenantId}  User:{_userId}");
            return await _bankService.GetUserAsync(_tenantId, _userId);
        }

        [KernelFunction("GetCurrentDateTime")]
        [Description("Get the current date time in UTC")]
        public DateTime GetCurrentDateTime()
        {
            _logger.LogTrace($"Get Datetime: {System.DateTime.Now.ToUniversalTime()}");
            return System.DateTime.Now.ToUniversalTime();
        }

        [KernelFunction("GetUserRegisteredAccounts")]
        [Description("Get user registered accounts")]
        public async Task<List<BankAccount>> GetUserRegisteredAccounts()
        {
            _logger.LogTrace($"Fetching accounts for Tenant: {_tenantId} User ID: {_userId}");
            return await _bankService.GetUserRegisteredAccountsAsync(_tenantId, _userId);
        }
    }
}
