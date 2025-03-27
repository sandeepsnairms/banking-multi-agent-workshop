using Microsoft.SemanticKernel;
using MultiAgentCopilot.ChatInfrastructure.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MultiAgentCopilot.ChatInfrastructure.Plugins;
using BankingServices.Interfaces;
using MultiAgentCopilot.ChatInfrastructure.Models;

namespace MultiAgentCopilot.ChatInfrastructure.Factories
{
    internal static class PluginFactory
    {
        internal static Kernel GetAgentKernel(Kernel kernel, AgentType agentType, ILoggerFactory loggerFactory, IBankDataService bankService, string tenantId, string userId)
        {
            Kernel agentKernel = kernel.Clone();
            switch (agentType)
            {
                case AgentType.Sales:
                    var salesPlugin = new SalesPlugin(loggerFactory.CreateLogger<SalesPlugin>(), bankService, tenantId, userId);
                    agentKernel.Plugins.AddFromObject(salesPlugin);
                    break;
                case AgentType.Transactions:
                    var transactionsPlugin = new TransactionPlugin(loggerFactory.CreateLogger<TransactionPlugin>(), bankService, tenantId, userId);
                    agentKernel.Plugins.AddFromObject(transactionsPlugin);
                    break;
                case AgentType.CustomerSupport:
                    var customerSupportPlugin = new CustomerSupportPlugin(loggerFactory.CreateLogger<CustomerSupportPlugin>(), bankService, tenantId, userId);
                    agentKernel.Plugins.AddFromObject(customerSupportPlugin);
                    break;
                case AgentType.Coordinator:
                    var CoordinatorPlugin = new CoordinatorPlugin(loggerFactory.CreateLogger<CoordinatorPlugin>(), bankService, tenantId, userId);
                    agentKernel.Plugins.AddFromObject(CoordinatorPlugin);
                    break;
                default:
                    throw new ArgumentException("Invalid plugin name");
            }

            return agentKernel;
        }
    }
}