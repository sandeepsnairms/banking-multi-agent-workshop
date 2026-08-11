# Python MCP Server Security

## Implemented Controls

- Azure DocumentDB and Microsoft Foundry access use `DefaultAzureCredential` and Microsoft Entra tokens. The application does not require database keys, Azure OpenAI API keys, or production connection strings.
- When `MCP_AUTH_TOKEN` is configured, the HTTP boundary requires an exact `Authorization: Bearer <token>` header. Missing or incorrect tokens return HTTP 401.
- Bearer-token comparison uses `hmac.compare_digest`.
- GitHub OAuth variables do not enable OAuth. Startup fails closed when OAuth variables are supplied without `MCP_AUTH_TOKEN`.

## Workshop Scope

The static bearer token is provided only for local workshop communication between the LangGraph client and MCP server. It is not a production identity or authorization system. If `MCP_AUTH_TOKEN` is omitted, the server logs that authentication is disabled and accepts requests without client authentication.

The sample does not implement user login, JWT issuance, refresh tokens, role-based tool authorization, rate limiting, CORS policy, or token revocation.

## Production Requirements

Before exposing the MCP endpoint outside a trusted development environment:

1. Put the service behind an OAuth-validating API gateway or implement a supported OAuth authorization provider with audience, issuer, signature, expiry, and scope validation.
2. Require HTTPS and restrict network access. Use private networking where possible.
3. Replace the workshop allow-all DocumentDB firewall rule with approved IP ranges or private connectivity.
4. Grant managed identities only the minimum Azure roles required by the application.
5. Store non-identity application secrets in a managed secret store and rotate them regularly.
6. Add request throttling, structured audit logs, security monitoring, and alerts.
7. Add authorization policy for tenant boundaries and individual tools.
8. Run dependency, static-analysis, and container-image security scans in CI.

## Configuration

| Variable | Security purpose |
|---|---|
| `MCP_AUTH_TOKEN` | Enables workshop bearer-token enforcement |
| `DOCUMENTDB_CLUSTER_NAME` | Selects the Entra-authenticated DocumentDB cluster |
| `AZURE_OPENAI_ENDPOINT` | Selects the Entra-authenticated Microsoft Foundry endpoint |
| `AZURE_CLIENT_ID` | Optionally selects a user-assigned managed identity |

Do not commit generated `.env` files or deployment-specific `appsettings.development.json` files.