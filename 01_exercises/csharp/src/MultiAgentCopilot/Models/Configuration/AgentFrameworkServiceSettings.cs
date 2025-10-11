namespace  MultiAgentCopilot.Models.Configuration
{
    public record AgentFrameworkServiceSettings
    {        
        public AzureOpenAISettings AzureOpenAISettings { get; init; }
        public MCPSettings MCPSettings { get; init; }
        public bool UseMCPTools { get; init; } = false;
    }


    public class MCPSettings
    {
        public MCPConnectionType ConnectionType { get; set; } = MCPConnectionType.STDIO;

        public List<MCPServerSettings> Servers { get; set; } = new();
        /// <summary>
        /// Configuration settings for MCP server
        /// </summary>           
    }

    public class MCPServerSettings
    {
        public string AgentName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
    }

    public enum MCPConnectionType
    {
        HTTP,
        STDIO
    }
}