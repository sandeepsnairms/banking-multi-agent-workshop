#!/bin/bash

echo "?? Testing OAuth 2.0-Only MCP Server"
echo "===================================="

BASE_URL="http://localhost:5000"

echo "?? 1. Getting server information..."
curl -s "${BASE_URL}/info" | jq '.' || echo "Server not running or JSON parsing failed"
echo ""

echo "?? 2. Getting OAuth token for coordinator agent..."
RESPONSE=$(curl -s -X POST "${BASE_URL}/oauth/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=coordinator-agent-client" \
  -d "client_secret=coordinator-secret-2024" \
  -d "scope=mcp:tools:coordinator")

echo "OAuth Response: $RESPONSE"

# Extract access token from response
ACCESS_TOKEN=$(echo "$RESPONSE" | jq -r '.access_token // empty')

if [ -z "$ACCESS_TOKEN" ]; then
    echo "? Failed to get OAuth token"
    exit 1
fi

echo "? Got OAuth token: ${ACCESS_TOKEN:0:50}..."
echo ""

echo "?? 3. Listing MCP tools with OAuth token..."
curl -s -X POST "${BASE_URL}/mcp/tools/list" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" | jq '.' || echo "Tools list failed"
echo ""

echo "?? 4. Testing Echo tool with OAuth..."
curl -s -X POST "${BASE_URL}/mcp/tools/call" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Echo",
    "arguments": {
      "message": "OAuth 2.0 Authentication Working!"
    }
  }' | jq '.' || echo "Echo test failed"
echo ""

echo "?? 5. Testing ReverseEcho tool with OAuth..."
curl -s -X POST "${BASE_URL}/mcp/tools/call" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "ReverseEcho",
    "arguments": {
      "message": "OAuth Test"
    }
  }' | jq '.' || echo "ReverseEcho test failed"
echo ""

echo "? 6. Validating OAuth token..."
curl -s -X POST "${BASE_URL}/oauth/validate" \
  -H "Authorization: Bearer $ACCESS_TOKEN" | jq '.' || echo "Token validation failed"
echo ""

echo "?? 7. Testing without token (should fail)..."
curl -s -X POST "${BASE_URL}/mcp/tools/list" \
  -H "Content-Type: application/json" | jq '.' || echo "No token test completed (expected to fail)"
echo ""

echo "?? OAuth 2.0-only MCP Server testing completed!"
echo ""
echo "?? To test other agents, use their respective client credentials:"
echo "   • Customer: customer-agent-client / customer-secret-2024"
echo "   • Sales: sales-agent-client / sales-secret-2024" 
echo "   • Transactions: transactions-agent-client / transactions-secret-2024"