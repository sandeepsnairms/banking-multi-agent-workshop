using Microsoft.Extensions.AI;
using BankingModels;
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

        [Description("Check if account is registered to user")]
        public async Task<bool> IsAccountRegisteredToUser(string accountId)
        {
            _logger.LogTrace($"Validating account for Tenant: {_tenantId} User ID: {_userId}- {accountId}");
            var accountDetails = await _bankService.GetAccountDetailsAsync(_tenantId, _userId, accountId);
            return accountDetails != null;
        }

        [Description("Search the database for pending requests")]
        public async Task<List<ServiceRequest>> CheckPendingServiceRequests(string? accountId = null, ServiceRequestType? srType = null)
        {
            _logger.LogTrace($"Searching database for matching requests for Tenant: {_tenantId} User: {_userId}");
            return await _bankService.GetServiceRequestsAsync(_tenantId, accountId ?? string.Empty, null, srType);
        }

        [Description("Adds a telebanker callback request for the specified account.")]
        public async Task<ServiceRequest> AddTeleBankerRequest(string accountId, string requestAnnotation, DateTime callbackTime)
        {
            _logger.LogTrace($"Adding Tele Banker request for Tenant: {_tenantId} User: {_userId}, account: {accountId}");
            return await _bankService.CreateTeleBankerRequestAsync(_tenantId, accountId, _userId, requestAnnotation, callbackTime);
        }

        [Description("Get list of available slots for telebankers specializing in an account type")]
        public async Task<string> GetTeleBankerSlots(AccountType accountType)
        {
            _logger.LogTrace($"Checking availability for Tele Banker for Tenant: {_tenantId} AccountType: {accountType}");
            return await _bankService.GetTeleBankerAvailabilityAsync();
        }

        [Description("Create new complaint")]
        public async Task<ServiceRequest> CreateComplaint(string accountId, string requestAnnotation)
        {
            _logger.LogTrace($"Adding new service request for Tenant: {_tenantId} User: {_userId}, Account: {accountId}");
            return await _bankService.CreateComplaintAsync(_tenantId, accountId, _userId, requestAnnotation);
        }

        [Description("Updates an existing service request with additional details")]
        public async Task<bool> UpdateExistingServiceRequest(string requestId, string accountId, string requestAnnotation)
        {
            _logger.LogTrace($"Updating service request for Request: {requestId}");
            return await _bankService.AddServiceRequestDescriptionAsync(_tenantId, accountId, requestId, requestAnnotation);
        }
    }
}