using Microsoft.Extensions.AI;
using MultiAgentCopilot.Models.Banking;
using MultiAgentCopilot.Services;
using System.ComponentModel;

namespace MultiAgentCopilot.Tools
{
    public class CustomerSupportTools : BaseTools
    {
        public CustomerSupportTools(ILogger<CustomerSupportTools> logger, BankingDataService bankService, string tenantId, string userId)
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
                CreateIsAccountRegisteredToUserTool(),
                CreateCheckPendingServiceRequestsTool(),
                CreateAddTeleBankerRequestTool(),
                CreateGetTeleBankerSlotsTool(),
                CreateCreateComplaintTool(),
                CreateUpdateExistingServiceRequestTool()
            };
        }

        private AIFunction CreateIsAccountRegisteredToUserTool()
        {
            return AIFunctionFactory.Create(async (string accountId) =>
            {
                _logger.LogTrace($"Validating account for Tenant: {_tenantId} User ID: {_userId}- {accountId}");
                var accountDetails = await _bankService.GetAccountDetailsAsync(_tenantId, _userId, accountId);
                return accountDetails != null;
            }, "IsAccountRegisteredToUser", "Check if account is registered to user");
        }

        private AIFunction CreateCheckPendingServiceRequestsTool()
        {
            return AIFunctionFactory.Create(async (string? accountId = null, ServiceRequestType? srType = null) =>
            {
                _logger.LogTrace($"Searching database for matching requests for Tenant: {_tenantId} User: {_userId}");
                return await _bankService.GetServiceRequestsAsync(_tenantId, accountId ?? string.Empty, null, srType);
            }, "CheckPendingServiceRequests", "Search the database for pending requests");
        }

        private AIFunction CreateAddTeleBankerRequestTool()
        {
            return AIFunctionFactory.Create(async (string accountId, string requestAnnotation, DateTime callbackTime) =>
            {
                _logger.LogTrace($"Adding Tele Banker request for Tenant: {_tenantId} User: {_userId}, account: {accountId}");
                return await _bankService.CreateTeleBankerRequestAsync(_tenantId, accountId, _userId, requestAnnotation, callbackTime);
            }, "AddTeleBankerRequest", "Adds a telebanker callback request for the specified account.");
        }

        private AIFunction CreateGetTeleBankerSlotsTool()
        {
            return AIFunctionFactory.Create(async (AccountType accountType) =>
            {
                _logger.LogTrace($"Checking availability for Tele Banker for Tenant: {_tenantId} AccountType: {accountType}");
                return await _bankService.GetTeleBankerAvailabilityAsync();
            }, "GetTeleBankerSlots", "Get list of available slots for telebankers specializing in an account type");
        }

        private AIFunction CreateCreateComplaintTool()
        {
            return AIFunctionFactory.Create(async (string accountId, string requestAnnotation) =>
            {
                _logger.LogTrace($"Adding new service request for Tenant: {_tenantId} User: {_userId}, Account: {accountId}");
                return await _bankService.CreateComplaintAsync(_tenantId, accountId, _userId, requestAnnotation);
            }, "CreateComplaint", "Create new complaint");
        }

        private AIFunction CreateUpdateExistingServiceRequestTool()
        {
            return AIFunctionFactory.Create(async (string requestId, string accountId, string requestAnnotation) =>
            {
                _logger.LogTrace($"Updating service request for Request: {requestId}");
                return await _bankService.AddServiceRequestDescriptionAsync(_tenantId, accountId, requestId, requestAnnotation);
            }, "UpdateExistingServiceRequest", "Updates an existing service request with additional details");
        }
    }
}