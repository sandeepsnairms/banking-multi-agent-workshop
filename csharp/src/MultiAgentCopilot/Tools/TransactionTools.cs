using Microsoft.Extensions.AI;
using BankingModels;
using MultiAgentCopilot.Services;
using System.ComponentModel;

namespace MultiAgentCopilot.Tools
{
    public class TransactionTools : BaseTools
    {
        public TransactionTools(ILogger<TransactionTools> logger, MockBankingService bankService)
            : base(logger, bankService)
        {
        }

        [Description("Adds a new Account Transaction request")]
        public async Task<ServiceRequest> AddFunTransferRequest(string tenantId, string userId,
            string debitAccountId,
            decimal amount,
            string requestAnnotation,
            string? recipientPhoneNumber = null,
            string? recipientEmailId = null)
        {
            _logger.LogTrace("Adding AccountTransaction request for User ID: {UserId}, Debit Account: {DebitAccountId}", userId, debitAccountId);

            // Ensure non-null values for recipientEmailId and recipientPhoneNumber
            string emailId = recipientEmailId ?? string.Empty;
            string phoneNumber = recipientPhoneNumber ?? string.Empty;

            return await _bankService.CreateFundTransferRequestAsync(tenantId, debitAccountId, userId, requestAnnotation, emailId, phoneNumber, amount);
        }

        [Description("Get the transactions history between 2 dates")]
        public async Task<List<BankTransaction>> GetTransactionHistory(string tenantId, string userId,string accountId, DateTime startDate, DateTime endDate)
        {
            _logger.LogTrace("Fetching AccountTransaction history for Account: {AccountId}, From: {StartDate} To: {EndDate}", accountId, startDate, endDate);
            return await _bankService.GetTransactionsAsync(tenantId, accountId, startDate, endDate);
        }
    }
}