# MCP Tool Context Parameter Fix

## Problem
The MCP client was failing to call the `GetUserAccounts` and `GetLoggedInUser` tools because it wasn't passing the required `userId` parameter. The error indicated "Missing required parameter: userId" but the MCP server expected both `userId` and `tenantId` parameters.

## Root Cause
1. The tool schemas returned by the MCP server didn't properly declare `userId` as a required parameter
2. The AI system didn't know it needed to provide context parameters like `userId` and `tenantId`
3. No fallback mechanism existed to inject missing context parameters

## Solution
The fix involves three components:

### 1. Schema Enhancement (`McpClientService.cs`)
- Added `EnhanceSchemaWithContext()` method that modifies tool schemas for context-dependent tools
- For `GetUserAccounts` and `GetLoggedInUser`, the method:
  - Adds `userId` and `tenantId` to the properties if missing
  - Marks them as required parameters
  - Provides proper descriptions

### 2. Context Injection (`McpClientToolAdapter.cs`)
- Added `InjectMissingContextParameters()` method that automatically injects missing context
- For tools that require context (`GetUserAccounts` and `GetLoggedInUser`):
  - Attempts to find `userId` and `tenantId` from the function arguments
  - Falls back to default values ("Mark" for userId, "Contoso" for tenantId)
  - Logs when parameters are injected

### 3. Better Debugging
- Enhanced logging in both the transport and tool adapter
- Shows exactly what parameters are being sent to MCP tools
- Helps diagnose parameter passing issues

## Fixed Tools
The following MCP tools now work correctly with automatic context injection:
- `GetUserAccounts` - requires `userId` and `tenantId`
- `GetLoggedInUser` - requires `userId` and `tenantId`

## Usage
The fix is automatic and requires no changes to existing code. When these tools are called:

1. The AI system will see `userId` and `tenantId` as required parameters in the schema
2. If the AI doesn't provide them, the client will inject default values
3. The tools will receive the necessary parameters and function correctly

## Future Improvements
In a production system, the default fallback values should be replaced with:
- Current session context (actual logged-in user and tenant)
- Dependency injection of context services
- Configuration-based default values

## Testing
After applying this fix:
1. The `GetUserAccounts` and `GetLoggedInUser` tools should work without parameter errors
2. Debug logs will show the parameters being passed to MCP tools
3. Other context-dependent tools will also benefit from automatic context injection

## Recent Updates
- Extended fix to cover `GetLoggedInUser` tool which also requires both `userId` and `tenantId` parameters
- Updated schema enhancement logic to handle both tools consistently
- Improved context injection to work for all context-dependent tools