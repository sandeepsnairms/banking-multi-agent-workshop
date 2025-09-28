using System.Text.Json.Serialization;

namespace MCPServer.Models;

// MCP Protocol information - for documentation purposes
public static class McpProtocol
{
    public const string Name = "Banking MCP Server";
    public const string ServerVersion = "1.0.0";
    public const string Implementation = "Banking-focused MCP with JWT Bearer Authentication";
    public const string Description = "HTTP-based MCP server providing banking operations with OAuth 2.0 JWT Bearer authentication";
}