#!/bin/bash

# Test script for MCP Server with SSE support
echo "Testing MCP Server with SSE support..."

BASE_URL="http://localhost:5000"
# Use correct client credentials that match the OAuth controller
CLIENT_ID="coordinator-agent-client"
CLIENT_SECRET="coordinator-secret-key-2024"
SCOPE="mcp:tools"

echo "Using OAuth credentials:"
echo "Client ID: $CLIENT_ID"
echo "Scope: $SCOPE"

echo ""
echo "1. Testing server availability..."
if ! curl -s -f "$BASE_URL/health" > /dev/null; then
    echo "? Server is not running or not accessible at $BASE_URL"
    echo "Please start the server with: dotnet run"
    exit 1
fi
echo "? Server is running!"

echo ""
echo "2. Getting OAuth token..."
TOKEN_RESPONSE=$(curl -s -X POST "$BASE_URL/oauth/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials&client_id=$CLIENT_ID&client_secret=$CLIENT_SECRET&scope=$SCOPE")

echo "Token response: $TOKEN_RESPONSE"

# Extract token from response
if command -v jq &> /dev/null; then
    ACCESS_TOKEN=$(echo $TOKEN_RESPONSE | jq -r '.access_token // empty')
else
    # Fallback method without jq
    ACCESS_TOKEN=$(echo $TOKEN_RESPONSE | grep -o '"access_token":"[^"]*' | cut -d'"' -f4)
fi

if [ -z "$ACCESS_TOKEN" ] || [ "$ACCESS_TOKEN" = "null" ]; then
    echo "? Failed to get access token"
    echo "Response: $TOKEN_RESPONSE"
    exit 1
fi

echo "? Access token obtained: ${ACCESS_TOKEN:0:20}..."

echo ""
echo "3. Testing health endpoint..."
curl -s "$BASE_URL/health" | if command -v jq &> /dev/null; then jq .; else cat; fi

echo ""
echo "4. Testing MCP capabilities endpoint..."
curl -s "$BASE_URL/mcp/capabilities" | if command -v jq &> /dev/null; then jq .; else cat; fi

echo ""
echo "5. Testing OAuth clients endpoint..."
curl -s "$BASE_URL/oauth/clients" | if command -v jq &> /dev/null; then jq .; else cat; fi

echo ""
echo "6. Testing MCP tools list (REST API)..."
curl -s -X POST "$BASE_URL/mcp/tools/list" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{}" | if command -v jq &> /dev/null; then jq .; else cat; fi

echo ""
echo "7. Testing MCP protocol endpoint..."
curl -s -X POST "$BASE_URL/mcp" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc": "2.0", "method": "tools/list", "id": "test-1"}' | if command -v jq &> /dev/null; then jq .; else cat; fi

echo ""
echo "8. Testing tool execution..."
curl -s -X POST "$BASE_URL/mcp" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc": "2.0", "method": "tools/call", "id": "test-2", "params": {"name": "SearchOffers", "arguments": {"query": "high interest savings"}}}' | if command -v jq &> /dev/null; then jq .; else cat; fi

echo ""
echo "9. Testing Server-Sent Events endpoint (first 10 lines)..."
echo "SSE Stream Output:"
timeout 5 curl -s -N "$BASE_URL/mcp/sse" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Accept: text/event-stream" | head -10

echo ""
echo "? Testing complete!"
echo ""
echo "Summary:"
echo "- Server availability: ? Tested"
echo "- OAuth authentication: ? Tested" 
echo "- MCP capabilities: ? Tested"
echo "- OAuth clients list: ? Tested"
echo "- REST API tools list: ? Tested"
echo "- MCP protocol messages: ? Tested"
echo "- Tool execution: ? Tested"
echo "- Server-Sent Events: ? Tested"
echo ""
echo "Available OAuth clients for testing:"
echo "- coordinator-agent-client / coordinator-secret-key-2024"
echo "- customer-agent-client / customer-secret-key-2024"
echo "- sales-agent-client / sales-secret-key-2024"
echo "- transactions-agent-client / transactions-secret-key-2024"