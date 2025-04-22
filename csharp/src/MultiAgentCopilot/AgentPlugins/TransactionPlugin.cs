using Microsoft.SemanticKernel;
using System.ComponentModel;
using MultiAgentCopilot.Models.Banking;
using MultiAgentCopilot.Services;

namespace MultiAgentCopilot.Plugins
{
    public class TransactionPlugin : BasePlugin
    {
        public TransactionPlugin(ILogger<BasePlugin> logger, BankingDataService bankService, string tenantId, string userId)
         : base(logger, bankService, tenantId, userId)
        {
        }

    }
}
