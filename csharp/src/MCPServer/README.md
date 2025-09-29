# Banking MCP Server

A Model Context Protocol (MCP) Server for banking operations with OAuth 2.0 authentication and Server-Sent Events (SSE) support.

## Features

- **MCP Protocol Support**: Both REST API and Server-Sent Events (SSE) transport
- **OAuth 2.0 Authentication**: Secure access with client credentials flow
- **Banking Tools**: Comprehensive banking operations including:
  - Account management and searching
  - Transaction history and processing
  - Service request management
  - Offer information retrieval
- **Multiple Transport Methods**:
  - REST API endpoints for simple integration
  - Server-Sent Events (SSE) for real-time MCP protocol communication
  - Full MCP protocol message handling
- **OpenTelemetry Integration**: Built-in tracing and metrics
- **Swagger Documentation**: Interactive API documentation

## Architecture

The server now supports both the original REST API approach and full MCP protocol with SSE:

### REST API Mode (Original)
- `POST /mcp/tools/list` - List available tools
- `POST /mcp/tools/call` - Execute tools

### MCP Protocol Mode (New)
- `GET /mcp/sse` - Server-Sent Events stream for real-time communication
- `POST /mcp` - Handle MCP protocol messages (JSON-RPC 2.0)
- `GET /mcp/capabilities` - Get server capabilities

### OAuth 2.0 Endpoints
- `POST /oauth/token` - Get access token

## Client Implementation Changes

The MultiAgentCopilot client has been updated to use a simplified HTTP-based approach instead of the complex MCP client library:

- **Removed**: Complex `ModelContextProtocol.Client` library that was causing 404 errors
- **Added**: Simple HTTP-based `MCPToolService` that works with both REST and MCP protocol endpoints
- **Fixed**: OAuth token handling and authentication flow
- **Improved**: Error handling and logging

## Quick Start

### Start the Server

```bash
cd MCPServer
dotnet run
```

The server will start on `http://localhost:5000` by default.

### Test with OAuth

1. **Get an access token:**
```bash
curl -X POST "http://localhost:5000/oauth/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials&client_id=banking-client&client_secret=banking-secret-key&scope=mcp:tools"
```

2. **Use the token to access protected endpoints:**
```bash
curl -X POST "http://localhost:5000/mcp/tools/list" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{}"
```

### Test Server-Sent Events

```bash
curl -N "http://localhost:5000/mcp/sse" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -H "Accept: text/event-stream"
```

### Use Test Scripts

- **Linux/Mac**: `./test-mcp-sse.sh`
- **Windows**: `test-mcp-sse.bat`

## Configuration

The server uses the following default OAuth credentials (for development):

- **Client ID**: `banking-client`
- **Client Secret**: `banking-secret-key`
- **Scope**: `mcp:tools`

## API Documentation

When running in development mode, Swagger UI is available at `http://localhost:5000`.

## Available Tools

The server provides the following banking tools:

1. **SearchOffers** - Search for banking offers by query
2. **GetAccountDetails** - Get account information by account ID
3. **GetTransactionHistory** - Get transaction history for date range
4. **CreateServiceRequest** - Create a new service request
5. **GetServiceRequests** - Retrieve existing service requests

Each tool supports proper parameter validation and returns structured responses.

## Troubleshooting

### 404 Errors with MCP Client

If you're getting 404 errors, it's likely because the MCP client library expects different endpoints. The updated client implementation fixes this by:

1. Using direct HTTP calls instead of the MCP client library
2. Supporting both REST API and MCP protocol endpoints
3. Proper OAuth token handling

### OAuth Issues

- Ensure you're using the correct client credentials
- Check that the token hasn't expired (tokens are valid for 1 hour)
- Verify the `Authorization: Bearer <token>` header format

### SSE Connection Issues

- Make sure you're including the `Accept: text/event-stream` header
- Verify OAuth authentication is working
- Check that the connection isn't being terminated by proxies or firewalls

## Development

### Prerequisites

- .NET 9.0 or later
- Any IDE supporting .NET development

### Running the Server

```bash
cd MCPServer
dotnet restore
dotnet run
```

### Testing

The server includes comprehensive test scripts and supports both manual testing via Swagger UI and automated testing via the provided scripts.

## Integration with MultiAgentCopilot

The MultiAgentCopilot client has been updated to work seamlessly with this server:

- Automatic OAuth token acquisition and renewal
- Support for both REST and MCP protocol modes
- Proper error handling and fallback mechanisms
- Agent-specific tool filtering based on tags

## Protocol Compliance

The server implements:

- **MCP Protocol Version**: 2024-11-05
- **JSON-RPC 2.0**: For MCP protocol messages
- **OAuth 2.0**: Client credentials flow
- **OpenAPI 3.0**: For REST API documentation

## Security

- OAuth 2.0 authentication required for all protected endpoints
- Token-based access control
- CORS support with configurable policies
- Request/response logging for audit purposes