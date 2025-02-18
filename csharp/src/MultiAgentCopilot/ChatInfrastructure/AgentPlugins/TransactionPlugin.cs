using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using BankingAPI.Models.Banking;
using BankingAPI.Interfaces;

namespace MultiAgentCopilot.ChatInfrastructure.Plugins
{
    public class TransactionPlugin: BasePlugin
    {
        public TransactionPlugin(ILogger<BasePlugin> logger, IBankDBService bankService, string tenantId, string userId )
         : base(logger, bankService, tenantId, userId)
        {
        }

        [KernelFunction]
        [Description("Adds a new AccountTransaction request")]
        public async Task<ServiceRequest> AddFunTransferRequest(
            string debitAccountId,
            decimal amount,
            string requestAnnotation,
            string? recipientPhoneNumber = null,
            string? recipientEmailId = null)
        {
            _logger.LogTrace("Adding AccountTransaction request for User ID: {_userId}, Debit Account: {DebitAccountNumber}");
            return await _bankService.CreateFundTransferRequestAsync(_tenantId, debitAccountId, _userId, requestAnnotation,recipientEmailId,recipientPhoneNumber, amount);   
        }
           

        [KernelFunction]
        [Description("Get the transactions history between 2 dates")]
        public async Task<List<BankTransaction>> GetTransactionHistory(string accountId, DateTime startDate, DateTime endDate)
        {
            _logger.LogTrace("Fetching AccountTransaction history for Account: {AccountId}, From: {StartDate} To: {EndDate}", accountId, startDate, endDate);
            return await _bankService.GetTransactionsAsync(_tenantId,accountId, startDate, endDate);

        }
    }
}
