using Microsoft.Extensions.AI;
using MultiAgentCopilot.Services;
using System.ComponentModel;
using Banking.Models;
using Banking.Services;

namespace MultiAgentCopilot.Tools
{
    public class TransactionTools : BaseTools
    {
        public TransactionTools(ILogger<TransactionTools> logger, BankingDataService bankService)
            : base(logger, bankService)
        {
        }
        
    }
}