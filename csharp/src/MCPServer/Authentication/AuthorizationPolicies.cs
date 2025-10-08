using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace MCPServer.Authentication;

public static class AuthorizationPolicies
{
    public const string ServerToServerPolicy = "ServerToServerPolicy";
    public const string UserOrServerPolicy = "UserOrServerPolicy";
    public const string McpToolsScope = "McpToolsScope";
}

public class ServerToServerRequirement : IAuthorizationRequirement
{
}

public class ServerToServerAuthorizationHandler : AuthorizationHandler<ServerToServerRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ServerToServerRequirement requirement)
    {
        // Check if the user is authenticated via server-to-server
        var authType = context.User.FindFirst("auth_type")?.Value;
        if (authType == "server_to_server")
        {
            context.Succeed(requirement);
        }
        
        return Task.CompletedTask;
    }
}

public class McpToolsScopeRequirement : IAuthorizationRequirement
{
    public string RequiredScope { get; }

    public McpToolsScopeRequirement(string requiredScope = "mcp:tools")
    {
        RequiredScope = requiredScope;
    }
}

public class McpToolsScopeAuthorizationHandler : AuthorizationHandler<McpToolsScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        McpToolsScopeRequirement requirement)
    {
        // Check if user has required scope
        var scopes = context.User.FindAll("scope").Select(c => c.Value);
        if (scopes.Contains(requirement.RequiredScope))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public class UserOrServerRequirement : IAuthorizationRequirement
{
}

public class UserOrServerAuthorizationHandler : AuthorizationHandler<UserOrServerRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        UserOrServerRequirement requirement)
    {
        // Allow both JWT bearer (user) and server-to-server authentication
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var authType = context.User.FindFirst("auth_type")?.Value;
            if (authType == "server_to_server" || context.User.Identity.AuthenticationType == "Bearer")
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}