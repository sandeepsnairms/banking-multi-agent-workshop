using Azure.Core;
using Banking.Services;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.AI;
using MultiAgentCopilot.Models;
using MultiAgentCopilot.MultiAgentCopilot.Services;
using MultiAgentCopilot.Services;
using MultiAgentCopilot.Tools;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using System;
using System.Buffers.Text;
using System.ComponentModel;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace MultiAgentCopilot.Factories
{
    /// <summary>
    /// Diagnostics information for MCP integration validation
    /// </summary>
    public class AgentMCPDiagnostics
    {
        public AgentType AgentType { get; set; }
        public string AgentName { get; set; } = string.Empty;
        public bool IsConfigured { get; set; }
        public bool IsConnected { get; set; }
        public string? ServerUrl { get; set; }
        public int ToolCount { get; set; }
        public List<string> Tools { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    public static class AgentFactory
    {
        //TO DO: Add Agent Creation with Tools
       

        //TO DO: Add Agent Details
        

        //TO DO: Create Agent Tools
        

    }
}