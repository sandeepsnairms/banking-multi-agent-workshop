namespace MultiAgentCopilot.MultiAgentCopilot.Models.Configuration
{
    public class MCPSettings
    {
        public MCPConnectionType ConnectionType { get; set; } = MCPConnectionType.STDIO;

        // Agent endpoint URLs
        public string CordinatorEndpointUrl { get; set; } = string.Empty;
        public string CustomerEndpointUrl { get; set; } = string.Empty;
        public string SalesEndpointUrl { get; set; } = string.Empty;
        public string TransactionsEndpointUrl { get; set; } = string.Empty;

        // Agent tool tags for filtering
        public string CordinatorToolTags { get; set; } = string.Empty;
        public string CustomerToolTags { get; set; } = string.Empty;
        public string SalesToolTags { get; set; } = string.Empty;
        public string TransactionsToolTags { get; set; } = string.Empty;

        // OAuth 2.0 Client Credentials for each agent
        public string CordinatorClientId { get; set; } = string.Empty;
        public string CordinatorClientSecret { get; set; } = string.Empty;
        public string CordinatorScope { get; set; } = "mcp:tools";

        public string CustomerClientId { get; set; } = string.Empty;
        public string CustomerClientSecret { get; set; } = string.Empty;
        public string CustomerScope { get; set; } = "mcp:tools:customer";

        public string SalesClientId { get; set; } = string.Empty;
        public string SalesClientSecret { get; set; } = string.Empty;
        public string SalesScope { get; set; } = "mcp:tools:sales";

        public string TransactionsClientId { get; set; } = string.Empty;
        public string TransactionsClientSecret { get; set; } = string.Empty;
        public string TransactionsScope { get; set; } = "mcp:tools:transactions";
    }

    public enum MCPConnectionType
    {
        HTTP,
        STDIO
    }
}
