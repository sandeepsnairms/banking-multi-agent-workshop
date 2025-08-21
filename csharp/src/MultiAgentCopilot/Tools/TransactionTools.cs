using Microsoft.Extensions.AI;
using MultiAgentCopilot.Models.Banking;
using MultiAgentCopilot.Services;
using System.ComponentModel;

namespace MultiAgentCopilot.Tools
{
    public class TransactionTools : BaseTools
    {
        public TransactionTools(ILogger<TransactionTools> logger, BankingDataService bankService, string tenantId, string userId)
            : base(logger, bankService, tenantId, userId)
        {
        }

        public override IList<AIFunction> GetTools()
        {
            return new List<AIFunction>
            {
                CreateGetLoggedInUserTool(),
                CreateGetCurrentDateTimeTool(),
                CreateGetUserRegisteredAccountsTool(),
                CreateAddFunTransferRequestTool(),
                CreateGetTransactionHistoryTool()
            };
        }

        private AIFunction CreateAddFunTransferRequestTool()
        {
            return AIFunctionFactory.Create(async (
                string debitAccountId,
                decimal amount,
                string requestAnnotation,
                string? recipientPhoneNumber = null,
                string? recipientEmailId = null) =>
            {
                _logger.LogTrace("Adding AccountTransaction request for User ID: {UserId}, Debit Account: {DebitAccountId}", _userId, debitAccountId);

                // Ensure non-null values for recipientEmailId and recipientPhoneNumber
                string emailId = recipientEmailId ?? string.Empty;
                string phoneNumber = recipientPhoneNumber ?? string.Empty;

                return await _bankService.CreateFundTransferRequestAsync(_tenantId, debitAccountId, _userId, requestAnnotation, emailId, phoneNumber, amount);
            }, "AddFunTransferRequest", "Adds a new Account Transaction request");
        }

        private AIFunction CreateGetTransactionHistoryTool()
        {
            return AIFunctionFactory.Create(async (string accountId, DateTime startDate, DateTime endDate) =>
            {
                _logger.LogTrace("Fetching AccountTransaction history for Account: {AccountId}, From: {StartDate} To: {EndDate}", accountId, startDate, endDate);
                return await _bankService.GetTransactionsAsync(_tenantId, accountId, startDate, endDate);
            }, "GetTransactionHistory", "Get the transactions history between 2 dates");
        }
    }
}