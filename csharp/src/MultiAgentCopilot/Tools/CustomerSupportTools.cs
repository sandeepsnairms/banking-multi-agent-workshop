using Microsoft.Extensions.AI;
using BankingModels;
using MultiAgentCopilot.Services;
using System.ComponentModel;

namespace MultiAgentCopilot.Tools
{
    public class CustomerSupportTools : BaseTools
    {
        public CustomerSupportTools(ILogger<CustomerSupportTools> logger, MockBankingService bankService)
            : base(logger, bankService)
        {
        }

        [Description("Check if account is registered to user")]
        public async Task<bool> IsAccountRegisteredToUser(string tenantId, string userId,string accountId)
        {
            _logger.LogTrace($"Validating account for Tenant: {tenantId} User ID: {userId}- {accountId}");
            var accountDetails = await _bankService.GetAccountDetailsAsync(tenantId, userId, accountId);
            return accountDetails != null;
        }

        [Description("Search the database for pending requests")]
        public async Task<List<ServiceRequest>> CheckPendingServiceRequests(string tenantId, string userId,string? accountId = null, ServiceRequestType? srType = null)
        {
            _logger.LogTrace($"Searching database for matching requests for Tenant: {tenantId} User: {userId}");
            return await _bankService.GetServiceRequestsAsync(tenantId, accountId ?? string.Empty, null, srType);
        }

        [Description("Adds a telebanker callback request for the specified account.")]
        public async Task<ServiceRequest> AddTeleBankerRequest(string tenantId, string userId,string accountId, string requestAnnotation, DateTime callbackTime)
        {
            _logger.LogTrace($"Adding Tele Banker request for Tenant: {tenantId} User: {userId}, account: {accountId}");
            return await _bankService.CreateTeleBankerRequestAsync(tenantId, accountId, userId, requestAnnotation, callbackTime);
        }

        [Description("Get list of available slots for telebankers specializing in an account type")]
        public async Task<string> GetTeleBankerSlots(string tenantId, string userId,AccountType accountType)
        {
            _logger.LogTrace($"Checking availability for Tele Banker for Tenant: {tenantId} AccountType: {accountType}");
            return await _bankService.GetTeleBankerAvailabilityAsync();
        }

        [Description("Create new complaint")]
        public async Task<ServiceRequest> CreateComplaint(string tenantId, string userId,string accountId, string requestAnnotation)
        {
            _logger.LogTrace($"Adding new service request for Tenant: {tenantId} User: {userId}, Account: {accountId}");
            return await _bankService.CreateComplaintAsync(tenantId, accountId, userId, requestAnnotation);
        }

        [Description("Updates an existing service request with additional details")]
        public async Task<bool> UpdateExistingServiceRequest(string tenantId, string requestId, string accountId, string requestAnnotation)
        {
            _logger.LogTrace($"Updating service request for Request: {requestId}");
            return await _bankService.AddServiceRequestDescriptionAsync(tenantId, accountId, requestId, requestAnnotation);
        }
    }
}