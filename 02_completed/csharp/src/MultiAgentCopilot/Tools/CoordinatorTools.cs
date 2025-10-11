using Microsoft.Extensions.AI;
using MultiAgentCopilot.Services;
using System.ComponentModel;
using Banking.Services;
namespace MultiAgentCopilot.Tools
{
    public class CoordinatorTools : BaseTools
    {
        public CoordinatorTools(ILogger<CoordinatorTools> logger, BankingDataService bankService)
            : base(logger, bankService)
        {
        }

        // Coordinator uses only the base tools for general coordination
        // GetLoggedInUser, GetCurrentDateTime, and GetUserRegisteredAccounts are inherited from BaseTools
    }
}