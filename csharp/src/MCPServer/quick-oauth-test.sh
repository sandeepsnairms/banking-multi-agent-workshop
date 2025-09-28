#!/bin/bash

echo "?? Quick OAuth Test"
echo "=================="

BASE_URL="http://localhost:5000"

echo "Testing OAuth clients endpoint..."
curl -s "${BASE_URL}/oauth/clients" | jq '.' || echo "OAuth clients endpoint failed or jq not available"
echo ""

echo "Testing OAuth token request..."
curl -s -X POST "${BASE_URL}/oauth/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=coordinator-agent-client" \
  -d "client_secret=coordinator-secret-2024" \
  -d "scope=mcp:tools:coordinator" | jq '.' || echo "OAuth token request failed or jq not available"
echo ""

echo "If you see an access_token above, OAuth is working!"
echo "If you see an error, check the server logs for details."