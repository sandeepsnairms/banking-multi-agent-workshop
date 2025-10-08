namespace MultiAgentCopilot.MultiAgentCopilot.Models.Configuration
{
    public class MCPSettings
    {
        public MCPConnectionType ConnectionType { get; set; } = MCPConnectionType.STDIO;

        public List<MCPServerSettings> Servers { get; set; } = new();
        /// <summary>
        /// Configuration settings for MCP server
        /// </summary>
        public class MCPServerSettings
        {
            public string AgentName { get; set; } = string.Empty;
            public string BaseUrl { get; set; } = string.Empty;
            public OAuthSettings OAuth { get; set; } = new();
        }

        /// <summary>
        /// OAuth configuration settings
        /// </summary>
        public class OAuthSettings
        {
            public string ClientId { get; set; } = string.Empty;
            public string ClientSecret { get; set; } = string.Empty;
            public string TokenEndpoint { get; set; } = string.Empty;
            public string ValidateEndpoint { get; set; } = string.Empty;
        }
    }

    public enum MCPConnectionType
    {
        HTTP,
        STDIO
    }
}
