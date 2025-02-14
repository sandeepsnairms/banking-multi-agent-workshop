using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using MultiAgentCopilot.Common.Models.BusinessDomain;

namespace MultiAgentCopilot.ChatInfrastructure.Plugins
{
    internal class TransactionPlugin
    {
        private readonly ILogger<TransactionPlugin> _logger;

        public TransactionPlugin(ILogger<TransactionPlugin> logger)
        {
            _logger = logger;
        }

        [KernelFunction]
        [Description("Adds a new AccountTransaction request")]
        public void AddTransactionRequest(
            string userId,
            string debitAccountNumber,
            decimal amount,
            string debitNote,
            string? recipientPhoneNumber = null,
            string? recipientEmailId = null)
        {
            _logger.LogTrace("Adding AccountTransaction request for User ID: {userId}, Debit Account: {DebitAccountNumber}", userId, debitAccountNumber);
            _logger.LogTrace("Amount: {Amount}, Note: {DebitNote}", amount, debitNote);
            if (!string.IsNullOrEmpty(recipientPhoneNumber))
                _logger.LogTrace("Recipient Phone: {RecipientPhoneNumber}", recipientPhoneNumber);
            if (!string.IsNullOrEmpty(recipientEmailId))
                _logger.LogTrace("Recipient Email: {RecipientEmailId}", recipientEmailId);

            _logger.LogTrace("AccountTransaction request added to the database (simulated).");
        }

        [KernelFunction]
        [Description("Get the account balance")]
        public long GetAccountBalance(string accountId)
        {
            _logger.LogTrace("Fetching balance for Account: {AccountId}, Balance: 1234", accountId);
            return 1234;
        }

        [KernelFunction]
        [Description("Get the transactions history between 2 dates")]
        public List<AccountTransaction> GetTransactionHistory(string accountId, DateTime startDate, DateTime endDate)
        {
            _logger.LogTrace("Fetching AccountTransaction history for Account: {AccountId}, From: {StartDate} To: {EndDate}", accountId, startDate, endDate);
            return new List<AccountTransaction>
                {
                    new AccountTransaction
                    {
                        Date = DateTime.Now.AddDays(-10),
                        DebitAmount = 200,
                        CreditAmount = 0,
                        AccountBalance = 1800,
                        DebitNote = "Grocery Shopping",
                        CreditNote = null
                    },
                    new AccountTransaction
                    {
                        Date = DateTime.Now.AddDays(-5),
                        DebitAmount = 0,
                        CreditAmount = 1500,
                        AccountBalance = 2000,
                        DebitNote = null,
                        CreditNote = "Salary"
                    },
                    new AccountTransaction
                    {
                        Date = DateTime.Now.AddDays(-6),
                        DebitAmount = 0,
                        CreditAmount = 500,
                        AccountBalance = 2500,
                        DebitNote = null,
                        CreditNote = "Interest"
                    },
                    new AccountTransaction
                    {
                        Date = DateTime.Now.AddDays(-2),
                        DebitAmount = 60,
                        CreditAmount = 0,
                        AccountBalance = 2440,
                        DebitNote = "Card Payment",
                        CreditNote =null
                    }
                };
        }
    }
}
