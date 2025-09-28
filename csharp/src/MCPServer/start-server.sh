#!/bin/bash

echo "?? Starting MCP Server..."
echo "========================"

# Check if .NET 9 is installed
if ! command -v dotnet &> /dev/null; then
    echo "? .NET is not installed. Please install .NET 9 SDK."
    exit 1
fi

# Build the project
echo "?? Building MCPServer..."
cd MCPServer
dotnet build

if [ $? -ne 0 ]; then
    echo "? Build failed!"
    exit 1
fi

echo "? Build successful!"
echo ""

# Start the server
echo "?? Starting server..."
echo "Server will be available at:"
echo "  • HTTP:  http://localhost:5000"
echo "  • HTTPS: https://localhost:5001"
echo "  • Swagger UI: http://localhost:5000/"
echo ""
echo "Press Ctrl+C to stop the server"
echo ""

dotnet run