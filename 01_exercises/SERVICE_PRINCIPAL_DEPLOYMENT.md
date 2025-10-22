# Service Principal Deployment for Multi-Agent Banking Workshop

This document explains how to deploy the Multi-Agent Banking application using Service Principal authentication with Azure Developer CLI (azd).

## Deployment Scripts

```powershell
# Update the variables at the top of the script with your values
.\deploy-simple.ps1
```

## Service Principal Permissions Required

Your Service Principal needs the following permissions:

### Azure RBAC Roles (at subscription or resource group level):
- **Contributor**: To create and manage resources
- **User Access Administrator**: To assign roles to managed identities and users

### Microsoft Graph API Permissions:
- **User.Read.All**: To read user information for role assignments
- **Application.ReadWrite.All**: To manage application registrations (if needed)

## Role Assignments Made

The infrastructure automatically assigns the following roles:

### For Managed Identity:
- **Cognitive Services User** on Azure OpenAI account
- **Cosmos DB Built-in Data Contributor** on Cosmos DB account

### For Specified User/Service Principal:
- **Cognitive Services User** on Azure OpenAI account  
- **Cosmos DB Built-in Data Contributor** on Cosmos DB account

## Troubleshooting

### Common Issues:

1. **"User not found" error**
   - Ensure the User Principal Name exists in the tenant
   - Verify the Service Principal has User.Read.All permissions

2. **Role assignment failures**
   - Ensure the Service Principal has User Access Administrator role
   - Check that the principal IDs are correct

3. **azd authentication failures**
   - Verify the Service Principal credentials are correct
   - Ensure the Service Principal has permissions in the target subscription

### Debug Steps:

1. Test Azure CLI authentication:
   ```powershell
   az login --service-principal -u $appId -p $appSecret --tenant $tenantId
   az account show
   ```

2. Verify user lookup:
   ```powershell
   Get-AzADUser -UserPrincipalName $userPrincipalName
   ```

3. Check azd environment:
   ```powershell
   azd env list
   azd env show -e your-environment-name
   ```

## Environment Variables

The following environment variables are set automatically:

- `AZURE_ENV_NAME`: Environment name with lab instance ID
- `AZURE_LOCATION`: Deployment location
- `AZURE_PRINCIPAL_ID`: User object ID for role assignments
- `AZURE_PRINCIPAL_TYPE`: Type of principal (User/ServicePrincipal)
- `OWNER_EMAIL`: Owner email for resource tagging

These are used by the Bicep templates for proper resource configuration and role assignments.