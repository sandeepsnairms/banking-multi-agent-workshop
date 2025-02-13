using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MultiAgentCopilot.Common.Models.BusinessDomain;


namespace MultiAgentCopilot.ChatInfrastructure.Plugins
{
    

    internal class CommonPlugin
    {

        private readonly ILogger<CommonPlugin> _logger;

        public CommonPlugin(ILogger<CommonPlugin> logger)
        {
            _logger = logger;
        }


        [KernelFunction("GetLoggedInUser")]
        [Description("Get the current logged-in BankUser")]
        public async Task<BankUser> GetLoggedInUser()
        {
            var usr = new BankUser { Id = "User123", Name = "John" }; // Simulate returning a hardcoded BankUser            
            _logger.LogTrace($"Get BankUser: {usr.Id}");
            // Dummy implementation for getting the current BankUser ID
            return usr;// new BankUser { Id = "User123", Name = "John" }; // Simulate returning a hardcoded BankUser
        }


        [KernelFunction("GetCurrentDateTime")]
        [Description("Get the current date time in UTC")]
        public DateTime GetCurrentDateTime()
        {

            _logger.LogTrace($"Get Datetime: {System.DateTime.Now.ToUniversalTime()}");
            // Dummy implementation for getting the current BankUser ID
            return System.DateTime.Now.ToUniversalTime();// new BankUser { Id = "User123", Name = "John" }; // Simulate returning a hardcoded BankUser
        }
    }
}
