using Microsoft.Extensions.AI;
using MultiAgentCopilot.Services;
using System.ComponentModel;

namespace MultiAgentCopilot.Tools
{
    public class CoordinatorTools : BaseTools
    {
        public CoordinatorTools(ILogger<CoordinatorTools> logger, BankingDataService bankService, string tenantId, string userId)
            : base(logger, bankService, tenantId, userId)
        {
        }

        // Coordinator uses only the base tools for general coordination
        // GetLoggedInUser, GetCurrentDateTime, and GetUserRegisteredAccounts are inherited from BaseTools
    }
}