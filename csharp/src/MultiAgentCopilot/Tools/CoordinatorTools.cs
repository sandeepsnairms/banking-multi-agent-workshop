using Microsoft.Extensions.AI;
using MultiAgentCopilot.Services;

namespace MultiAgentCopilot.Tools
{
    public class CoordinatorTools : BaseTools
    {
        public CoordinatorTools(ILogger<CoordinatorTools> logger, BankingDataService bankService, string tenantId, string userId)
            : base(logger, bankService, tenantId, userId)
        {
        }

        public override IList<AIFunction> GetTools()
        {
            return new List<AIFunction>
            {
                CreateGetLoggedInUserTool(),
                CreateGetCurrentDateTimeTool(),
                CreateGetUserRegisteredAccountsTool()
            };
        }
    }
}