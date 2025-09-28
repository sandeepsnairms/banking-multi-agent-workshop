# MCP Server Usage Examples

This document provides comprehensive examples of how to use the MCP Server with OAuth 2.0 authentication, dynamic tool discovery, and the new MCP protocol endpoints.

## Quick Start

### 1. Start the Server

**Using scripts:**
```bash
# On Linux/macOS
./start-server.sh

# On Windows
start-server.bat
```

**Using .NET CLI:**
```bash
cd MCPServer
dotnet run
```

The server will be available at:
- HTTP: http://localhost:5000
- Swagger UI: http://localhost:5000

### 2. Run Test Scripts

```bash
# Test OAuth authentication and MCP endpoints
./test-oauth.sh        # Linux/macOS
./test-oauth.ps1       # Windows PowerShell
./test-oauth.bat       # Windows Command Prompt
```

## OAuth 2.0 Authentication

### Step 1: Get OAuth Access Token

All MCP operations require OAuth 2.0 Bearer authentication:

```bash
curl -X POST http://localhost:5000/oauth/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=coordinator-agent-client" \
  -d "client_secret=coordinator-secret-2024" \
  -d "scope=mcp:tools"
```

**Response:**
```json
{
  "access_token": "eyJjbGllbnRfaWQiOiJjb29yZGluYXRvci1hZ2VudC1jbGllbnQi...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "scope": "mcp:tools"
}
```

## MCP Protocol Usage with Dynamic Tool Discovery

### Step 2: List Available Tools (Dynamic Discovery)

The server automatically discovers tools from registered services and static classes:

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
          },
          "tenantId": {
            "type": "string",
            "description": "Tenant ID (optional, defaults to 'default-tenant')"
          }
        },
        "required": ["accountType", "requirement"]
      }
    },
    {
      "name": "Echo",
      "description": "Echoes the input message back to the client.",
      "inputSchema": {
        "type": "object",
        "properties": {
          "message": {
            "type": "string",
            "description": "The message to echo back"
          }
        },
        "required": ["message"]
      }
    },
    {
      "name": "GetTransactionHistory",
      "description": "Get transaction history for a bank account",
      "inputSchema": {
        "type": "object",
        "properties": {
          "accountId": {
            "type": "string",
            "description": "Bank account ID"
          },
          "startDate": {
            "type": "string",
            "description": "Start date for transaction history"
          },
          "endDate": {
            "type": "string", 
            "description": "End date for transaction history"
          }
        },
        "required": ["accountId", "startDate", "endDate"]
      }
    }
  ]
}
```

### Step 3: Execute Tools

#### Banking Operations

**Search for Banking Offers:**
```bash
curl -X POST http://localhost:5000/mcp/tools/call \
  -H "Authorization: Bearer YOUR_OAUTH_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "SearchOffers",
    "arguments": {
      "accountType": "Savings",
      "requirement": "High interest rate account for emergency savings"
    }
  }'
```

**Response:**
```json
{
  "content": [
    {
      "type": "text",
      "text": "[{\"id\":\"offer-001\",\"name\":\"High-Yield Savings\",\"interestRate\":4.5,\"minimumBalance\":1000,\"description\":\"Competitive interest rate with no monthly fees\"}]"
    }
  ]
}
```

**Get Transaction History:**
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

**Get Account Details:**
```bash
curl -X POST http://localhost:5000/mcp/tools/call \
  -H "Authorization: Bearer YOUR_OAUTH_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "GetAccountDetails",
    "arguments": {
      "accountId": "Acc001",
      "userId": "user123"
    }
  }'
```

**Create Service Request:**
```bash
curl -X POST http://localhost:5000/mcp/tools/call \
  -H "Authorization: Bearer YOUR_OAUTH_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "CreateServiceRequest",
    "arguments": {
      "requestType": "Complaint",
      "description": "ATM card was not dispensed but amount was debited",
      "accountId": "Acc001",
      "userId": "user123"
    }
  }'
```

#### Utility Tools

**Echo Tool:**
```bash
curl -X POST http://localhost:5000/mcp/tools/call \
  -H "Authorization: Bearer YOUR_OAUTH_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Echo",
    "arguments": {
      "message": "Hello, MCP Server with OAuth!"
    }
  }'
```

**Response:**
```json
{
  "content": [
    {
      "type": "text",
      "text": "Hello, MCP Server with OAuth!"
    }
  ]
}
```

**Reverse Echo Tool:**
```bash
curl -X POST http://localhost:5000/mcp/tools/call \
  -H "Authorization: Bearer YOUR_OAUTH_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "ReverseEcho",
    "arguments": {
      "message": "Hello, World!"
    }
  }'
```

**Response:**
```json
{
  "content": [
    {
      "type": "text",
      "text": "!dlroW ,olleH"
    }
  ]
}
```

## Programming Language Examples

### Python MCP Client with OAuth

```python
import requests
import json
from typing import Optional, Dict, Any

class MCPClient:
    def __init__(self, base_url: str = "http://localhost:5000"):
        self.base_url = base_url
        self.session = requests.Session()
        self.token: Optional[str] = None
    
    def authenticate(self, client_id: str, client_secret: str, scope: str = "mcp:tools") -> bool:
        """Get OAuth 2.0 Bearer token"""
        response = self.session.post(
            f"{self.base_url}/oauth/token",
            data={
                "grant_type": "client_credentials",
                "client_id": client_id,
                "client_secret": client_secret,
                "scope": scope
            },
            headers={"Content-Type": "application/x-www-form-urlencoded"}
        )
        
        if response.status_code == 200:
            data = response.json()
            self.token = data["access_token"]
            self.session.headers.update({"Authorization": f"Bearer {self.token}"})
            return True
        return False
    
    def list_tools(self) -> Optional[Dict]:
        """List available tools with dynamic discovery"""
        response = self.session.post(f"{self.base_url}/mcp/tools/list")
        return response.json() if response.status_code == 200 else None
    
    def call_tool(self, name: str, arguments: Dict[str, Any]) -> Optional[Dict]:
        """Execute a tool"""
        response = self.session.post(
            f"{self.base_url}/mcp/tools/call",
            json={"name": name, "arguments": arguments}
        )
        return response.json() if response.status_code == 200 else None

# Usage Example
def main():
    client = MCPClient()
    
    # Authenticate with OAuth 2.0
    if not client.authenticate(
        client_id="coordinator-agent-client",
        client_secret="coordinator-secret-2024",
        scope="mcp:tools"
    ):
        print("OAuth authentication failed")
        return
    
    print("? OAuth authentication successful")
    
    # List tools (dynamic discovery)
    tools_response = client.list_tools()
    if tools_response and "tools" in tools_response:
        print("\n?? Available Tools (dynamically discovered):")
        for tool in tools_response["tools"]:
            print(f"  • {tool['name']}: {tool['description']}")
    
    # Test Banking Tools
    print("\n?? Testing Banking Operations:")
    
    # Search for banking offers
    offers_response = client.call_tool("SearchOffers", {
        "accountType": "Savings",
        "requirement": "High interest rate with low minimum balance"
    })
    
    if offers_response and "content" in offers_response:
        print(f"? Found banking offers: {offers_response['content'][0]['text']}")
    
    # Get transaction history  
    transactions_response = client.call_tool("GetTransactionHistory", {
        "accountId": "Acc001",
        "startDate": "2024-01-01T00:00:00Z",
        "endDate": "2024-12-31T23:59:59Z"
    })
    
    if transactions_response and "content" in transactions_response:
        print(f"? Transaction history retrieved")
    
    # Test Utility Tools
    print("\n?? Testing Utility Tools:")
    
    echo_response = client.call_tool("Echo", {"message": "Hello from Python with OAuth!"})
    if echo_response and "content" in echo_response:
        print(f"? Echo result: {echo_response['content'][0]['text']}")
    
    reverse_response = client.call_tool("ReverseEcho", {"message": "Python rocks!"})
    if reverse_response and "content" in reverse_response:
        print(f"? Reverse echo result: {reverse_response['content'][0]['text']}")

if __name__ == "__main__":
    main()
```

### JavaScript/Node.js MCP Client with OAuth

```javascript
const axios = require('axios');

class MCPClient {
    constructor(baseUrl = 'http://localhost:5000') {
        this.baseUrl = baseUrl;
        this.client = axios.create({ baseURL: baseUrl });
        this.token = null;
    }

    async authenticate(clientId, clientSecret, scope = 'mcp:tools') {
        try {
            const response = await this.client.post('/oauth/token', 
                new URLSearchParams({
                    grant_type: 'client_credentials',
                    client_id: clientId,
                    client_secret: clientSecret,
                    scope: scope
                }),
                {
                    headers: {
                        'Content-Type': 'application/x-www-form-urlencoded'
                    }
                }
            );
            
            this.token = response.data.access_token;
            this.client.defaults.headers.common['Authorization'] = `Bearer ${this.token}`;
            return true;
        } catch (error) {
            console.error('OAuth authentication failed:', error.message);
            return false;
        }
    }

    async listTools() {
        try {
            const response = await this.client.post('/mcp/tools/list');
            return response.data;
        } catch (error) {
            console.error('Failed to list tools:', error.message);
            return null;
        }
    }

    async callTool(name, arguments) {
        try {
            const response = await this.client.post('/mcp/tools/call', {
                name: name,
                arguments: arguments
            });
            return response.data;
        } catch (error) {
            console.error(`Failed to call tool ${name}:`, error.message);
            return null;
        }
    }
}

// Usage
async function main() {
    const client = new MCPClient();
    
    // Authenticate with OAuth 2.0
    const authenticated = await client.authenticate(
        'coordinator-agent-client',
        'coordinator-secret-2024',
        'mcp:tools'
    );
    
    if (!authenticated) {
        console.log('OAuth authentication failed');
        return;
    }
    
    console.log('? OAuth authentication successful');
    
    // List dynamically discovered tools
    const toolsResponse = await client.listTools();
    if (toolsResponse && toolsResponse.tools) {
        console.log('\n?? Available Tools (dynamically discovered):');
        toolsResponse.tools.forEach(tool => {
            console.log(`  • ${tool.name}: ${tool.description}`);
        });
    }
    
    // Test Banking Operations
    console.log('\n?? Testing Banking Operations:');
    
    const offersResponse = await client.callTool('SearchOffers', {
        accountType: 'CreditCard',
        requirement: 'Low APR credit card with rewards program'
    });
    
    if (offersResponse && offersResponse.content) {
        console.log('? Found credit card offers');
    }
    
    // Test Utility Tools
    console.log('\n?? Testing Utility Tools:');
    
    const echoResponse = await client.callTool('Echo', { 
        message: 'Hello from JavaScript with OAuth!' 
    });
    
    if (echoResponse && echoResponse.content) {
        console.log(`? Echo result: ${echoResponse.content[0].text}`);
    }
}

main().catch(console.error);
```

## C# MCP Client Example

```csharp
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

public class MCPClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public MCPClient(string baseUrl = "http://localhost:5000")
    {
        _baseUrl = baseUrl;
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public async Task<bool> AuthenticateAsync(string clientId, string clientSecret, string scope = "mcp:tools")
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "client_credentials"),
            new("client_id", clientId),
            new("client_secret", clientSecret),
            new("scope", scope)
        };

        using var content = new FormUrlEncodedContent(parameters);
        
        try
        {
            var response = await _httpClient.PostAsync("/oauth/token", content);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<JsonElement>(json);
                var accessToken = tokenResponse.GetProperty("access_token").GetString();
                
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", accessToken);
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Authentication failed: {ex.Message}");
        }
        
        return false;
    }

    public async Task<JsonElement?> ListToolsAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("/mcp/tools/list", 
                new StringContent("{}", Encoding.UTF8, "application/json"));
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<JsonElement>(json);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to list tools: {ex.Message}");
        }
        
        return null;
    }

    public async Task<JsonElement?> CallToolAsync(string name, object arguments)
    {
        try
        {
            var request = new { name, arguments };
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/mcp/tools/call", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<JsonElement>(responseJson);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to call tool {name}: {ex.Message}");
        }
        
        return null;
    }
}

// Usage Example
class Program
{
    static async Task Main(string[] args)
    {
        var client = new MCPClient();
        
        // Authenticate
        var authenticated = await client.AuthenticateAsync(
            "coordinator-agent-client", 
            "coordinator-secret-2024", 
            "mcp:tools"
        );
        
        if (!authenticated)
        {
            Console.WriteLine("OAuth authentication failed");
            return;
        }
        
        Console.WriteLine("? OAuth authentication successful");
        
        // List tools
        var tools = await client.ListToolsAsync();
        if (tools.HasValue && tools.Value.TryGetProperty("tools", out var toolsArray))
        {
            Console.WriteLine("\n?? Available Tools:");
            foreach (var tool in toolsArray.EnumerateArray())
            {
                var name = tool.GetProperty("name").GetString();
                var description = tool.GetProperty("description").GetString();
                Console.WriteLine($"  • {name}: {description}");
            }
        }
        
        // Test banking operations
        Console.WriteLine("\n?? Testing Banking Operations:");
        
        var offersResult = await client.CallToolAsync("SearchOffers", new 
        {
            accountType = "Savings",
            requirement = "High yield savings account"
        });
        
        if (offersResult.HasValue)
        {
            Console.WriteLine("? Banking offers search completed");
        }
        
        // Test utility tools
        var echoResult = await client.CallToolAsync("Echo", new { message = "Hello from C#!" });
        if (echoResult.HasValue && echoResult.Value.TryGetProperty("content", out var content))
        {
            var text = content[0].GetProperty("text").GetString();
            Console.WriteLine($"? Echo result: {text}");
        }
    }
}
```

## Testing with HTTPie

```bash
# Install HTTPie: pip install httpie

# Get OAuth token
http --form POST localhost:5000/oauth/token \
  grant_type=client_credentials \
  client_id=coordinator-agent-client \
  client_secret=coordinator-secret-2024 \
  scope=mcp:tools

# List tools (replace TOKEN with actual token)
http POST localhost:5000/mcp/tools/list Authorization:"Bearer TOKEN"

# Search banking offers
http POST localhost:5000/mcp/tools/call Authorization:"Bearer TOKEN" \
  name=SearchOffers \
  arguments:='{"accountType":"Savings","requirement":"High interest rate"}'

# Get transaction history
http POST localhost:5000/mcp/tools/call Authorization:"Bearer TOKEN" \
  name=GetTransactionHistory \
  arguments:='{"accountId":"Acc001","startDate":"2024-01-01T00:00:00Z","endDate":"2024-12-31T23:59:59Z"}'

# Echo test
http POST localhost:5000/mcp/tools/call Authorization:"Bearer TOKEN" \
  name=Echo arguments:='{"message":"Hello HTTPie!"}'
```

## Error Handling

### OAuth Errors

```json
{
  "error": "invalid_client",
  "error_description": "Invalid client credentials"
}
```

### MCP Tool Errors

```json
{
  "content": [
    {
      "type": "text", 
      "text": "Error: Tool 'InvalidTool' not found"
    }
  ]
}
```

### Parameter Validation Errors

```json
{
  "error": "Parameter validation failed: 'accountType' is required"
}
```

## Available OAuth Clients

| Client ID | Secret | Scope |
|-----------|--------|-------|
| `coordinator-agent-client` | `coordinator-secret-2024` | `mcp:tools` |

## Health Monitoring

### Health Check
```bash
curl -X GET http://localhost:5000/health
```

**Response:**
```json
{
  "status": "healthy",
  "timestamp": "2024-12-15T09:30:00.123456Z",
  "server_name": "Banking MCP Server",
  "server_version": "1.0.0",
  "implementation": "Banking-focused MCP with JWT Bearer Authentication",
  "description": "HTTP-based MCP server providing banking operations with OAuth 2.0 JWT Bearer authentication"
}
```

## Docker Usage

### Build and Run
```bash
# Build Docker image
docker build -t banking-mcp-server .

# Run container
docker run -p 5000:80 banking-mcp-server
```

## Key Benefits

1. **Dynamic Tool Discovery**: Tools are automatically discovered from registered services
2. **OAuth 2.0 Security**: Industry standard authentication
3. **Banking Integration**: Real banking operations with semantic search
4. **Type Safety**: Full parameter validation and type checking
5. **Extensible**: Easy to add new tools by registering services
6. **Standards Compliant**: Proper MCP protocol implementation

This comprehensive guide demonstrates the OAuth 2.0 secured MCP Server with dynamic tool discovery, enabling seamless integration with banking services and utility tools.