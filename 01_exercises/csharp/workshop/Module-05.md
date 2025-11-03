# Module 05 - MCP Integration

## Introduction

In this module, you'll learn how to integrate external MCP (Model Context Protocol) server tools to extend your agents' capabilities beyond in-process functions. This enables your agents to interact with external services and systems through standardized protocols.

## Learning Objectives

- Understand MCP (Model Context Protocol) architecture and benefits
- Learn how to configure and consume external MCP server tools
- Implement agent-to-MCP server communication and authentication
- Test and debug external tool integration in multi-agent scenarios

## Module Exercises

1. [Activity 1: Configure MCP Client Integration](#activity-1-configure-mcp-client-integration)
1. [Activity 2: Implement MCP Tool Service](#activity-2-implement-mcp-tool-service)
1. [Activity 3: Test your Work](#activity-3-test-your-work)

## Activity 1: Configure MCP Client Integration

When dealing with multiple agents, integrating external tools through MCP servers allows for better scalability and separation of concerns. MCP (Model Context Protocol) is a standardized protocol that enables agents to securely access external tools and services.

### MCP Client Creation

MCP servers provide tools and resources that agents can consume remotely. This architecture allows for better modularity and enables agents to access specialized capabilities without embedding all functionality directly in the agent code.

1. In VS Code, navigate to the **MultiAgentCopilot** project.
1. Navigate to the **/Services** folder
1. Navigate to **MCPToolService.cs**
1. Replace **CreateMcpClientAsync()** method with the code below

This method creates an authenticated MCP client for connecting to external tool servers. It handles transport configuration and authentication for secure communication.

```csharp
/// <summary>
/// Creates or retrieves a cached MCP client for the specified agent type
/// </summary>
private async Task<McpClient> CreateMcpClientAsync(AgentType agentType, MCPServerSettings settings)
{
    // Create authenticated transport with enhanced error handling
    IClientTransport clientTransport;
    try
    {
        // Create transport with authentication and streamable-http support
        var wrapper = new AuthenticatedHttpWrapper(settings.Url, settings.Key);
        clientTransport = wrapper.CreateTransport();
        
        // Configure transport for streamable-http if needed
        if (clientTransport is HttpClientTransport httpTransport)
        {
            await ConfigureTransportForStreamableHttp(httpTransport, settings);
        }

    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to create transport for agent {AgentType}: {Message}", agentType, ex.Message);
        throw new InvalidOperationException($"Failed to create MCP transport for agent '{agentType}': {ex.Message}", ex);
    }

    // Create MCP client
    try
    {
        var mcpClient = await McpClient.CreateAsync(clientTransport);
        _logger.LogInformation("MCP client created successfully for agent: {AgentType}", agentType);
        return mcpClient;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to create MCP client for agent {AgentType}: {Message}", agentType, ex.Message);
        
        
        throw new InvalidOperationException($"Failed to create MCP client for agent '{agentType}': {ex.Message}", ex);
    }
}
```

In our workshop, each agent can connect to a different MCP server based on their specialization. Let's configure the MCP server settings for each agent type.

1. Stay in **MCPToolService.cs**
1. Replace **GetMCPServerSettings()** method with the code below

This method retrieves MCP server configuration for specific agent types. It validates server settings and ensures proper authentication configuration.

```csharp
private MCPServerSettings GetMCPServerSettings(AgentType agentType)
{
    var agentName = AgentFactory.GetAgentName(agentType);

    
    // Find the server configuration for this agent type
    var serverSettings = _mcpSettings.Servers?.FirstOrDefault(s => 
        string.Equals(s.AgentName, agentName, StringComparison.OrdinalIgnoreCase));

    if (serverSettings == null)
    {
        throw new InvalidOperationException($"MCP server configuration for agent '{agentName}' not found in MCPSettings.Servers.");
    }

    // Validate the server settings
    if (string.IsNullOrWhiteSpace(serverSettings.Url))
    {
        throw new InvalidOperationException($"MCP server URL for agent '{agentName}' is not configured.");
    }

    if (string.IsNullOrWhiteSpace(serverSettings.Key))
    {
        throw new InvalidOperationException($"MCP server API key for agent '{agentName}' is not configured.");
    }

    return serverSettings;
}
``` 

## Activity 2: Implement MCP Tool Service

1. Stay in **MCPToolService.cs**
1. Replace the **GetMcpTools()** method with the code below

This method retrieves and filters MCP tools for specific agents. It handles connection errors gracefully and provides appropriate logging for debugging.

```csharp
public async Task<IList<McpClientTool>> GetMcpTools(AgentType agent)
{
    _logger.LogInformation("Getting MCP tools for agent: {AgentType}", agent);

    try
    {
        // Get agent configuration
        var settings = GetMCPServerSettings(agent);

        // Get or create MCP client with authentication and streamable-http support
        var mcpClient = await CreateMcpClientAsync(agent,settings);

        // List available tools from the MCP server
        var tools = await  mcpClient.ListToolsAsync();

        var filteredTools = FilterToolsByTags(tools, settings.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToArray());

        _logger.LogInformation("Filtered {ToolCount} tools for agent {AgentType} with {Tags}", filteredTools.Count, agent, settings.Tags);

        // Log each tool for debugging
        foreach (var tool in filteredTools)
        {
            _logger.LogDebug("Tool available - Name: {ToolName}, Description: {ToolDescription}", 
                tool.Name, tool.Description);
        }

        return filteredTools;
    }
    catch (InvalidOperationException)
    {
        // Configuration errors - already logged, re-throw
        throw;
    }
    catch (HttpRequestException httpEx)
    {
        _logger.LogError(httpEx, "HTTP error when connecting to MCP server for agent {AgentType}: {Message}", 
            agent, httpEx.Message);
        
        // Check if it's an authentication error
        if (httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized"))
        {
            _logger.LogError("Authentication failed - check X-MCP-API-Key configuration for agent {AgentType}", agent);
        }

        return new List<McpClientTool>();
    }
    catch (TaskCanceledException timeoutEx)
    {
        _logger.LogError(timeoutEx, "Timeout when connecting to MCP server for agent {AgentType}: {Message}", 
            agent, timeoutEx.Message);
        return new List<McpClientTool>();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error getting MCP tools for agent {AgentType}: {Message} | StackTrace: {StackTrace}", 
            agent, ex.Message, ex.StackTrace);
        return new List<McpClientTool>();
    }    
}

```

### Update Agent Factory to Use MCP Tools

1. In VS Code, navigate to the **/Factories** folder
1. Navigate to **AgentFactory.cs**
1. Search for **//TO DO: Add Agent Creation with MCP Tools** and paste the code below

```csharp
public static async Task<List<AIAgent>> CreateAllAgentsWithMCPToolsAsync(IChatClient chatClient, MCPToolService mcpService, ILoggerFactory loggerFactory)
{ 
    var agents = new List<AIAgent>();
    ILogger logger = loggerFactory.CreateLogger("AgentFactory");

    // Get all agent types from the enum
    var agentTypes = Enum.GetValues<AgentType>();

    // Create agents for each agent type
    foreach (var agentType in agentTypes)
    {
        logger.LogInformation("Creating agent {AgentType} with MCP tools", agentType);

        var aiFunctions = await mcpService.GetMcpTools(agentType);

        var agent = chatClient.CreateAIAgent(
                instructions: GetAgentPrompt(agentType),
                name: GetAgentName(agentType),
                description: GetAgentDescription(agentType),
                tools: aiFunctions.ToArray()
            );

        agents.Add(agent);
        logger.LogInformation("Created agent {AgentName} with {ToolCount} MCP tools", agent.Name, aiFunctions.Count());
    }

    logger.LogInformation("Successfully created {AgentCount} banking agents", agents.Count);
    return agents;
}
```

## Integrate MCP Tools with Agent Framework

1. Navigate to **AgentFrameworkService.cs**
1. Search for **//TO DO: Add MCP Service Option** and paste the code below **within** the method. Do **not** replace the entire method.

This code integrates MCP tool service as an option for agent creation. It provides a fallback mechanism when MCP tools are preferred over in-process tools.

```csharp
 else if (_mcpService != null)
 {
     _agents = AgentFactory.CreateAllAgentsWithMCPToolsAsync(_chatClient, _mcpService, _loggerFactory).GetAwaiter().GetResult();
 }                
 else
 {
     _logger.LogError("No tool services available - cannot create agents");
 }
```

1. Next search for **//TO DO: Add SetMCPToolService** and paste the code below

This method enables setting the MCP tool service for agent framework integration. It provides a way to configure external tool capabilities for agents.
```csharp
    /// <summary>
    /// Sets the MCP (Model Context Protocol) tool service.
    /// </summary>
    /// <param name="mcpService">The MCP tool service instance.</param>
    /// <returns>True if the service was set successfully.</returns>
    
    public bool SetMCPToolService(MCPToolService mcpService)
    {
        _mcpService = mcpService ?? throw new ArgumentNullException(nameof(mcpService));
        _logger.LogInformation("MCPToolService has been set");
        return true;
    }
```

## Set MCP Tools in Chat Service

1. Navigate to **ChatService.cs**
1. Search for **//TO DO: Invoke SetMCPToolService** and paste the code below **within** the method. Do **not** replace the entire method.

```csharp

_afService.SetMCPToolService(_mcpService);

``` 

### Enable MCP Tools as the Preferred Option

1. Finally, open **MultiAgentCopilot\appsettings.development.json** and set the value of **UseMCPTools** to true.


## Activity 3: Test your Work

### Start the MCPServer
1. Within VS Code, open a new terminal.
1. Navigate to the `mcpserver` folder, `cd .\01_exercises\mcpserver\csharp\`
1. Run the below command to run the MCP Server.
    ```shell
    
    dotnet run
    
    ```


### Start the Backend

- Return to the open terminal for the backend app in VS Code. Ensure you are in `01_exercises\csharp\src\MultiAgentCopilot`. Type `dotnet run`

### Start a Chat Session

1. Return to the frontend application in your browser.
1. Start a new conversation.
1. Try the below prompts. Provide more information if prompted.
    1. Who can help me here?
    1. Transfer $50 to my friend.
    1. When prompted for account and email, enter, "Account is Acc001 and Email is Sandeep@contoso.com"
    1. Looking for a Savings account with high interest rate.
    1. File a complaint about theft from my account.
    1. When prompted confirm its the same account or enter a new account (Acc001 to Acc009) and provide any details it asks for.
    1. How much did I spend on groceries? (If prompted, say over the last 6 months)
    1. Provide me a statement of my account. (If prompted, give it an account number ranging from *Acc001* to *Acc009*)

### Stop the Application

1. In the backend terminal, press **Ctrl + C** to stop the application.

## Validation Checklist

- [ ] Depending on the user prompt the agent selection is dynamic.
- [ ] All the agents have context of the previous messages in the conversation.
- [ ] The agents are able to invoke the right plugin function to interact with **BankingService**.
- [ ] Vector search  works as expected.

## Common Issues and Solutions

1. Multiple agents respond together or Wrong agent responding:

   - View the 'DebugLog' by using the **Bug** icon in each impacted AI response.
   - Study the Termination Reason
   - Edit the appropriate Prompty files to resolve the conflict.

1. Agent responses are invalid:

   - Change in model and/or its version can cause invalid/irrelevant agent behavior.
   - Thorough testing with updated prompts will be required.

## Module Solution

The following sections include the completed code for this Module. Copy and paste these into your project if you run into issues and cannot resolve.


<details>
  <summary>Completed code for <strong>\MultiAgentCopilot\Helper\GroupChatWorkflowHelper.cs</strong></summary>
<br>

```csharp


```
</details>
<details>
  <summary>Completed code for <strong>\MultiAgentCopilot\Services\AgentFrameworkService.cs</strong></summary>
<br>

```csharp


```
<details>
  <summary>Completed code for <strong>\MultiAgentCopilot\Services\ChatService.cs</strong></summary>
<br>

```csharp


```
</details>
<details>
  <summary>Completed code for <strong>\MultiAgentCopilot\Services\MCPToolService.cs</strong></summary>
<br>

```csharp


```
</details>
<details>
  <summary>Completed code for <strong>\MultiAgentCopilot\Factories\AgentFactory.cs</strong></summary>
<br>

```csharp


```
</details>


## Next Steps

Congratulations!!! You have completed this hands-on-lab!!!

You can see the full source code for this lab at <https://github.com/AzureCosmosDB/banking-multi-agent-workshop/>. We hope you enjoyed this lab.
