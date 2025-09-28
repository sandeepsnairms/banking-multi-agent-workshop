# MCP Server

A Model Context Protocol (MCP) Server implementation using **ModelContextProtocol.AspNetCore** with **OAuth 2.0 JWT Bearer authentication** and HTTP transport with dynamic tool discovery for banking operations.

## Features

- **MCP Protocol Implementation**: Uses ModelContextProtocol.AspNetCore library for MCP compatibility
- **JWT Bearer Authentication**: OAuth 2.0 JWT Bearer token authentication with custom token validation
- **Protected MCP Endpoints**: All MCP operations require valid OAuth Bearer tokens
- **Dynamic Tool Discovery**: Automatically discovers and registers available tools from services
- **Banking Tools**: Comprehensive banking operations with semantic search capabilities
- **Authorization Policies**: Scoped access control with different permission levels
- **HTTP Transport**: MCP over HTTP with JSON-RPC 2.0 support
- **OpenTelemetry**: Built-in observability with OpenTelemetry
- **Swagger Documentation**: Interactive API documentation with OAuth support
- **Health Monitoring**: Health check endpoints

## Implementation Details

This server uses **OAuth 2.0 JWT Bearer authentication** with **dynamic tool discovery**:
- **ModelContextProtocol.AspNetCore**: For MCP protocol compatibility
- **Microsoft.AspNetCore.Authentication.JwtBearer**: For JWT Bearer authentication
- **Custom Token Validation**: Validates our OAuth 2.0 tokens in JWT Bearer context
- **Dynamic Tool Discovery**: Automatically discovers tools from registered services and static classes
- **Authorization Policies**: Role-based access control for different agent types
- **Banking Services**: Integration with mock banking services for demonstration

## Authentication

### OAuth 2.0 JWT Bearer Flow

The server uses OAuth 2.0 tokens validated through JWT Bearer authentication:

#### Get OAuth Token
```bash
curl -X POST http://localhost:5000/oauth/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials&client_id=coordinator-agent-client&client_secret=coordinator-secret-2024&scope=mcp:tools:coordinator"
```

**Response:**
```json
{
  "access_token": "eyJjbGllbnRfaWQiOiJjb29yZGluYXRvci1hZ2VudC1jbGllbnQi...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "scope": "mcp:tools:coordinator"
}
```

#### Authorization Policies

| Policy | Required Scope | Description |
|--------|----------------|-------------|
| `McpTools` | `mcp:tools` | Basic MCP tools access |

#### OAuth Clients Configuration

| Client ID | Client Secret | Allowed Scopes |
|-----------|---------------|----------------|
| `coordinator-agent-client` | `coordinator-secret-2024` | `mcp:tools` |

## Available MCP Tools

### Dynamic Tool Discovery

The server automatically discovers tools from:

1. **BankingTools Service** - Banking operations with semantic search
2. **Static Tool Classes** - Echo and utility tools

### Banking Tools (BankingTools)

- **SearchOffers**: Search for banking offers using semantic search
  - Parameters: `accountType` (string), `requirement` (string), `tenantId` (optional string)
  
- **GetOfferDetails**: Get detailed information for a specific banking offer
  - Parameters: `offerId` (string), `tenantId` (optional string)
  
- **GetTransactionHistory**: Get transaction history for a bank account
  - Parameters: `accountId` (string), `startDate` (DateTime), `endDate` (DateTime), `tenantId` (optional string)
  
- **GetAccountDetails**: Get account details for a user
  - Parameters: `accountId` (string), `userId` (string), `tenantId` (optional string)
  
- **GetUserAccounts**: Get all registered accounts for a user
  - Parameters: `userId` (string), `tenantId` (optional string)
  
- **CreateServiceRequest**: Create a customer service request
  - Parameters: `requestType` (string), `description` (string), `accountId` (optional string), `userId` (optional string), `tenantId` (optional string)
  
- **GetServiceRequests**: Get service requests for an account
  - Parameters: `accountId` (string), `userId` (optional string), `requestType` (optional string), `tenantId` (optional string)
  
- **AddServiceRequestAnnotation**: Add annotation to an existing service request
  - Parameters: `requestId` (string), `accountId` (string), `annotation` (string), `tenantId` (optional string)
  
- **GetTeleBankerAvailability**: Get telebanker availability information
  - Parameters: None

### Echo Tools (Static)

- **Echo**: Echoes the input message back to the client
  - Parameters: `message` (string) - The message to echo back
  
- **ReverseEcho**: Reverses the input message and echoes it back to the client
  - Parameters: `message` (string) - The message to reverse and echo back

## MCP Protocol Endpoints

### List Available Tools
```bash
curl -X POST http://localhost:5000/mcp/tools/list \
  -H "Authorization: Bearer YOUR_OAUTH_TOKEN" \
  -H "Content-Type: application/json"
```

**Response:**
```json
{
  "tools": [
    {
      "name": "SearchOffers",
      "description": "Search for banking offers and products using semantic search",
      "inputSchema": {
        "type": "object",
        "properties": {
          "accountType": {
            "type": "string",
            "description": "Type of account (Savings, Checking, etc.)"
          },
          "requirement": {
            "type": "string", 
            "description": "Customer requirements or preferences"
          }
        },
        "required": ["accountType", "requirement"]
      }
    }
  ]
}
```

### Execute Tool
```bash
curl -X POST http://localhost:5000/mcp/tools/call \
  -H "Authorization: Bearer YOUR_OAUTH_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Echo",
    "arguments": {
      "message": "Hello with JWT Bearer!"
    }
  }'
```

**Response:**
```json
{
  "content": [
    {
      "type": "text",
      "text": "Hello with JWT Bearer!"
    }
  ]
}
```

## Tool Implementation

### Banking Tools with Services
Banking tools are implemented as services with dependency injection:

```csharp
public class BankingTools
{
    private readonly IBankingService _bankingService;
    
    [Description("Search for banking offers and products using semantic search")]
    public async Task<List<OfferTerm>> SearchOffers(
        [Description("Type of account")] string accountType,
        [Description("Customer requirements")] string requirement,
        [Description("Tenant ID")] string? tenantId = null)
    {
        // Implementation using banking service
    }
}
```

### Static Tools
Static tools are simple classes with Description attributes:

```csharp
public class EchoTool
{
    [Description("Echoes the input message back to the client.")]
    public static string Echo([Description("The message to echo back")] string message)
    {
        return message;
    }
}
```

### Dynamic Tool Discovery
The MCP controller automatically discovers tools using reflection:

```csharp
[HttpPost("tools/list")]
public IActionResult ListTools()
{
    var tools = DiscoverTools(); // Automatically finds all available tools
    return Ok(new { tools });
}
```

## Program.cs Configuration

The server is configured with JWT Bearer authentication, authorization policies, and dynamic service registration:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register MCP tools for dependency injection - NO HARD-CODING!
builder.Services.AddSingleton<IBankingService, MockBankingService>();
builder.Services.AddSingleton<BankingTools>();

// Configure JWT Bearer Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    // Custom token validation for OAuth 2.0 tokens
});

// Configure Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("McpTools", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("scope", "mcp:tools"));
});

var app = builder.Build();

// Map controllers for OAuth and MCP endpoints
app.MapControllers();
```

## API Endpoints

### OAuth 2.0 Endpoints (Unprotected)
- `POST /oauth/token` - OAuth 2.0 token endpoint (client credentials flow)
- `POST /oauth/validate` - Validate OAuth token
- `GET /oauth/clients` - List available OAuth clients (development only)

### MCP Protocol Endpoints (JWT Bearer Protected)
- `POST /mcp/tools/list` - List available MCP tools with dynamic discovery *(Requires: `mcp:tools` scope)*
- `POST /mcp/tools/call` - Execute MCP tools dynamically *(Requires: `mcp:tools` scope)*

### Infrastructure Endpoints (Unprotected)
- `GET /health` - Health check with server information
- `/` - Swagger UI documentation (development only)

## Getting Started

### Prerequisites
- .NET 9.0 SDK
- Visual Studio 2022 or VS Code

### Running the Server

1. Build the project:
   ```bash
   dotnet build
   ```

2. Run the server:
   ```bash
   dotnet run
   ```

3. The server will start on:
   - HTTP: http://localhost:5000

4. Access Swagger UI at: http://localhost:5000

## Testing

### Automated Test Scripts

Choose the appropriate test script for your platform:

#### Linux/macOS
```bash
./test-oauth.sh
```

#### Windows - PowerShell (Recommended)
```powershell
.\test-oauth.ps1
```

#### Windows - Command Prompt with Parsing
```cmd
test-oauth.bat
```

#### Windows - Simple Commands (Manual)
```cmd
test-oauth-simple.bat
```

### Manual Testing with Banking Tools

#### 1. Get OAuth Token
```bash
curl -X POST http://localhost:5000/oauth/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=coordinator-agent-client" \
  -d "client_secret=coordinator-secret-2024" \
  -d "scope=mcp:tools"
```

#### 2. List Available Tools
```bash
curl -X POST http://localhost:5000/mcp/tools/list \
  -H "Authorization: Bearer YOUR_OAUTH_TOKEN" \
  -H "Content-Type: application/json"
```

#### 3. Search Banking Offers
```bash
curl -X POST http://localhost:5000/mcp/tools/call \
  -H "Authorization: Bearer YOUR_OAUTH_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "SearchOffers",
    "arguments": {
      "accountType": "Savings",
      "requirement": "High interest rate account for emergency fund"
    }
  }'
```

#### 4. Get Transaction History
```bash
curl -X POST http://localhost:5000/mcp/tools/call \
  -H "Authorization: Bearer YOUR_OAUTH_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "GetTransactionHistory",
    "arguments": {
      "accountId": "Acc001",
      "startDate": "2024-01-01T00:00:00Z",
      "endDate": "2024-12-31T23:59:59Z"
    }
  }'
```

### Client Integration (MultiAgentCopilot)

The MultiAgentCopilot can connect to this server using standard HTTP Bearer authentication:

```csharp
// Set Bearer token for all requests
httpClient.DefaultRequestHeaders.Authorization = 
    new AuthenticationHeaderValue("Bearer", accessToken);

// Discover available tools
var toolsResponse = await httpClient.PostAsync("/mcp/tools/list", content);

// Execute banking operations
var callResponse = await httpClient.PostAsync("/mcp/tools/call", callContent);
```

## Benefits of Dynamic Tool Discovery

### Development Benefits
- **No Hard-Coding**: Tools are automatically discovered from registered services
- **Easy Extension**: Simply register new services to add tools
- **Type Safety**: Full C# type safety with parameter validation
- **Documentation**: Automatic API documentation generation

### Runtime Benefits
- **Flexible**: Tools can be added or removed without code changes
- **Discoverable**: Clients can query available tools dynamically
- **Validated**: Parameter validation and type conversion handled automatically
- **Secure**: Full OAuth 2.0 and JWT Bearer authentication

### Integration Benefits
- **Service-Based**: Integrates with existing service architecture
- **Dependency Injection**: Full DI container support
- **Middleware Pipeline**: Leverages ASP.NET Core pipeline
- **Standards Compliant**: MCP and OAuth 2.0 compliant

## Key Features

1. **Dynamic Tool Discovery**: Automatically discovers tools from services and static classes
2. **JWT Bearer Authentication**: ASP.NET Core native JWT Bearer authentication
3. **Authorization Policies**: Fine-grained policy-based authorization
4. **Banking Integration**: Real banking service integration with mock implementation
5. **Semantic Search**: Vector search capabilities for banking offers
6. **Claims-Based Security**: Rich claims system for authorization decisions
7. **Built-in Observability**: OpenTelemetry integration
8. **Production Ready**: Enterprise-grade authentication and authorization

## Adding New Tools

To add a new MCP tool:

1. **Service-Based Tool** (Recommended):
```csharp
// 1. Create service interface
public interface IMyService
{
    Task<string> DoSomethingAsync(string input);
}

// 2. Create service implementation
public class MyService : IMyService
{
    [Description("Does something useful")]
    public async Task<string> DoSomethingAsync(
        [Description("Input parameter")] string input)
    {
        return await ProcessAsync(input);
    }
}

// 3. Register service in Program.cs
builder.Services.AddSingleton<IMyService, MyService>();
```

2. **Static Tool**:
```csharp
public class MyStaticTool
{
    [Description("Static tool that does something")]
    public static string DoSomething([Description("Parameter description")] string param)
    {
        return "result";
    }
}
```

The MCP controller will automatically discover and register these tools!

## Security Considerations

1. **Bearer Token Security**: Always use HTTPS in production
2. **Token Expiry**: Configure appropriate token lifetimes
3. **Scope Validation**: Proper authorization policy configuration
4. **Parameter Validation**: Automatic parameter type validation
5. **Service Security**: Secure service implementations
6. **Audit Logging**: Log all authentication and authorization events

## Project Structure

```
MCPServer/
??? Controllers/
?   ??? MCPController.cs            # MCP protocol endpoints with dynamic discovery
?   ??? OAuthController.cs          # OAuth 2.0 token management
??? Tools/
?   ??? BankingTools.cs             # Banking operations tools
?   ??? EchoTools.cs                # Static utility tools
?   ??? McpToolTagsAttribute.cs     # Tool metadata attributes
??? Services/
?   ??? MockBankingService.cs       # Mock banking service implementation
?   ??? IBankingService.cs          # Banking service interface
??? Models/
?   ??? McpModels.cs               # MCP protocol models
?   ??? AuthModels.cs              # OAuth authentication models
??? Program.cs                      # Server configuration and service registration
??? test-oauth.sh                  # Linux/macOS test script
??? test-oauth.ps1                 # Windows PowerShell test script
??? test-oauth.bat                 # Windows batch test script
??? test-oauth-simple.bat          # Windows manual test script
```

## License

This project demonstrates JWT Bearer authentication with OAuth 2.0 and dynamic tool discovery for MCP Server implementation suitable for enterprise banking applications with fine-grained authorization control.