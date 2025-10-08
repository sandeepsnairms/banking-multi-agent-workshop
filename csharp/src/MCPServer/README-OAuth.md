# MCPServer - Server-to-Server OAuth Authentication

This MCP Server now supports both user authentication (JWT Bearer tokens from Entra ID) and server-to-server OAuth authentication using the client credentials flow.

## Authentication Methods

### 1. User Authentication (JWT Bearer)
- Uses JWT tokens from Entra ID
- Suitable for user-facing applications
- Configured with existing JWT Bearer authentication

### 2. Server-to-Server OAuth (Client Credentials)
- Uses OAuth 2.0 Client Credentials flow
- Suitable for service-to-service communication
- Uses custom authentication handler

## OAuth Configuration

The server is configured with pre-defined OAuth clients in `appsettings.json`:

```json
{
  "OAuthSettings": {
    "TokenExpirationMinutes": 60,
    "Clients": [
      {
        "ClientId": "coordinator-agent-client",
        "ClientSecret": "coordinator-secret-key-2024",
        "AllowedScopes": ["mcp:tools", "mcp:tools:coordinator"]
      },
      {
        "ClientId": "customer-agent-client", 
        "ClientSecret": "customer-secret-key-2024",
        "AllowedScopes": ["mcp:tools", "mcp:tools:customer"]
      },
      {
        "ClientId": "sales-agent-client",
        "ClientSecret": "sales-secret-key-2024", 
        "AllowedScopes": ["mcp:tools", "mcp:tools:sales"]
      },
      {
        "ClientId": "transactions-agent-client",
        "ClientSecret": "transactions-secret-key-2024",
        "AllowedScopes": ["mcp:tools", "mcp:tools:transactions"]
      }
    ]
  }
}
```

## Server-to-Server Authentication Flow

### Step 1: Obtain Access Token

Make a POST request to the token endpoint:

```http
POST /oauth/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials&client_id=coordinator-agent-client&client_secret=coordinator-secret-key-2024&scope=mcp:tools
```

**Response:**
```json
{
  "access_token": "base64-encoded-token",
  "token_type": "Bearer",
  "expires_in": 3600,
  "scope": "mcp:tools"
}
```

### Step 2: Use Access Token

Include the access token in the Authorization header:

```http
GET /mcp/tools
Authorization: Bearer base64-encoded-token
```

## Available Endpoints

### OAuth Endpoints
- `POST /oauth/token` - Obtain access token using client credentials
- `POST /oauth/validate` - Validate an access token
- `GET /oauth/clients` - List configured clients (for development)

### Test Endpoints
- `GET /api/test/auth-info` - Get authentication information (both auth types)
- `GET /api/test/server-only` - Server-to-server authentication only
- `GET /api/test/scope-required` - Requires mcp:tools scope
- `POST /api/test/demo-token` - Get demo token for testing

### MCP Endpoints
- `GET /mcp/*` - MCP protocol endpoints (requires authentication)

## Authorization Policies

The server implements several authorization policies:

1. **ServerToServerPolicy** - Only server-to-server authentication
2. **UserOrServerPolicy** - Both user JWT and server-to-server authentication
3. **McpToolsScope** - Requires mcp:tools scope

## Testing Server-to-Server Authentication

### Using curl

1. Get a token:
```bash
curl -X POST http://localhost:7071/oauth/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials&client_id=coordinator-agent-client&client_secret=coordinator-secret-key-2024&scope=mcp:tools"
```

2. Use the token:
```bash
curl -X GET http://localhost:7071/api/test/auth-info \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

### Using the Demo Endpoint

You can also use the demo endpoint to get a token programmatically:

```bash
curl -X POST http://localhost:7071/api/test/demo-token \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "coordinator-agent-client",
    "clientSecret": "coordinator-secret-key-2024",
    "scopes": ["mcp:tools"]
  }'
```

## Security Considerations

1. **Client Secrets**: Store client secrets securely in production
2. **Token Validation**: Tokens include expiration time validation
3. **Scope Validation**: Clients can only request allowed scopes
4. **HTTPS**: Use HTTPS in production environments
5. **Token Storage**: Implement proper token storage and refresh mechanisms

## Integration with Multi-Agent Systems

This server-to-server authentication is designed for multi-agent scenarios where:
- Different agents need to authenticate independently
- Each agent has its own client credentials
- Agents can have different scopes/permissions
- Service-to-service communication is secured

The configured clients correspond to different agent types:
- `coordinator-agent-client` - Main coordination agent
- `customer-agent-client` - Customer service agent  
- `sales-agent-client` - Sales agent
- `transactions-agent-client` - Transaction processing agent