# Banking MCP Server

This project exposes the workshop banking tools through the MCP streamable HTTP transport. It connects to Azure DocumentDB and Microsoft Foundry using `DefaultAzureCredential`; no database keys or Azure OpenAI API keys are required.

## Prerequisites

- Python 3.12 or later
- An Azure identity with access to the workshop Azure DocumentDB cluster and Microsoft Foundry resource
- Azure resources provisioned from the `01_exercises` folder with `azd up`

The `azd` post-provision hook generates `mcpserver/python/.env` with the deployment-specific resource names and endpoints. This file is excluded from Git.

## Run Locally

From `01_exercises/mcpserver/python`:

### Windows PowerShell

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
$env:PYTHONPATH="src"
python src/mcp_http_server.py
```

### Linux, macOS, WSL, or Codespaces

```bash
python -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
PYTHONPATH=src python src/mcp_http_server.py
```

The MCP endpoint is `http://localhost:8080/mcp/` by default.

## Configuration

| Variable | Description | Default |
|---|---|---|
| `DOCUMENTDB_CLUSTER_NAME` | Azure DocumentDB cluster name | Required |
| `DOCUMENTDB_DATABASE_NAME` | Workshop database name | `MultiAgentBanking` |
| `AZURE_OPENAI_ENDPOINT` | Microsoft Foundry endpoint | Required |
| `AZURE_OPENAI_EMBEDDINGDEPLOYMENTID` | Embedding deployment | `text-embedding-3-small` |
| `AZURE_OPENAI_COMPLETIONSDEPLOYMENTID` | Completion deployment | `gpt-4.1-mini` |
| `MCP_AUTH_TOKEN` | Development bearer token | Optional |
| `MCP_SERVER_BASE_URL` | Base URL announced by the server | `http://localhost:8080` |
| `PORT` | Listening port | `8080` |

When `MCP_AUTH_TOKEN` is set, every HTTP request to the MCP endpoint must include:

```http
Authorization: Bearer <MCP_AUTH_TOKEN>
```

Requests with a missing or incorrect token receive HTTP 401. The static token mode is for workshop development only. Use a production identity provider or an API gateway with OAuth validation for a deployed service.

## Tools

The server registers banking tools for account balances and creation, transactions, service requests, branch lookup, product-offer vector search, loan calculations, and transfers between specialist agents. Use an MCP-compatible client to initialize a session, discover tools, and call them.

## Azure Deployment Scope

Running `azd up` from `01_exercises` provisions Azure DocumentDB, Microsoft Foundry model deployments, managed identity, and role assignments. It also generates local configuration and loads sample data. The current infrastructure template does not deploy this server as a Container App; run it locally with the commands above.

## Troubleshooting

- Authenticate locally with Azure CLI or Azure Developer CLI so `DefaultAzureCredential` can obtain tokens.
- Confirm `DOCUMENTDB_CLUSTER_NAME` and `AZURE_OPENAI_ENDPOINT` are populated in `.env` after `azd up`.
- Confirm the cluster firewall permits the client network. The workshop allow-all rule must be restricted or replaced with private networking after testing.
- Use the same `MCP_AUTH_TOKEN` in the MCP server and LangGraph client configuration.