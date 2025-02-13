using Microsoft.SemanticKernel;
using MultiAgentCopilot.ChatInfrastructure.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MultiAgentCopilot.ChatInfrastructure.Plugins;
using MultiAgentCopilot.ChatInfrastructure.Models;

namespace MultiAgentCopilot.ChatInfrastructure.Factories
{
    internal static class PluginFactory
    {
        internal static Kernel GetAgentKernel(Kernel kernel, AgentType agentType, ILoggerFactory loggerFactory)
        {
            Kernel agentKernel = kernel.Clone();
            switch (agentType)
            {
                case AgentType.Sales:
                    var salesPlugin = new SalesPlugin(loggerFactory.CreateLogger<SalesPlugin>());
                    agentKernel.Plugins.AddFromObject(salesPlugin);
                    break;
                case AgentType.Transactions:
                    var transactionsPlugin = new TransactionPlugin(loggerFactory.CreateLogger<TransactionPlugin>());
                    agentKernel.Plugins.AddFromObject(transactionsPlugin);
                    break;
                case AgentType.CustomerSupport:
                    var customerSupportPlugin = new CustomerSupportPlugin(loggerFactory.CreateLogger<CustomerSupportPlugin>());
                    agentKernel.Plugins.AddFromObject(customerSupportPlugin);
                    break;
                case AgentType.Cordinator:
                    var cordinatorPlugin = new CordinatorPlugin(loggerFactory.CreateLogger<CordinatorPlugin>());
                    agentKernel.Plugins.AddFromObject(cordinatorPlugin);
                    //var agentPlugin = KernelPluginFactory.CreateFromObject(cordinatorPlugin, agentName);
                    //agentKernel.Plugins.Add(agentPlugin);
                    break;
                default:
                    throw new ArgumentException("Invalid plugin name");
            }

            var commonPlugin = new CommonPlugin(loggerFactory.CreateLogger<CommonPlugin>());
            agentKernel.Plugins.AddFromObject(commonPlugin);
            return agentKernel;
        }
    }
}
