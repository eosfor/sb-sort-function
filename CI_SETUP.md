# CI/CD Setup Guide

This document provides instructions for setting up the CI/CD pipeline for the Service Bus Reorder Function.

## Prerequisites

1. **Azure Service Principal** with Contributor access to the resource group `github-account-test`
2. **GitHub Repository Secrets** configured

## Setting up Azure Service Principal

If you don't have a Service Principal yet, create one:

```bash
# Create a service principal with Contributor role on the resource group
az ad sp create-for-rbac \
  --name "github-sb-reorder-sp" \
  --role Contributor \
  --scopes /subscriptions/0f47daf8-38d9-4100-9afc-7ceca28f800d/resourceGroups/github-account-test \
  --json-auth
```

This command will output a JSON object that you'll use for the GitHub secret.

## Configuring GitHub Secrets

### Option 1: Using AZURE_CREDENTIALS (Recommended)

1. Go to your GitHub repository
2. Navigate to **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret**
4. Name: `AZURE_CREDENTIALS`
5. Value: Paste the JSON output from the `az ad sp create-for-rbac` command:
   ```json
   {
     "clientId": "<client-id>",
     "clientSecret": "<client-secret>",
     "subscriptionId": "0f47daf8-38d9-4100-9afc-7ceca28f800d",
     "tenantId": "<tenant-id>"
   }
   ```

### Option 2: Using Individual Secrets

If you prefer to use individual secrets (requires workflow modification):

1. `AUTOMATION_CLIENT_ID` - The `clientId` from the Service Principal
2. `AUTOMATION_CLIENT_SECRET` - The `clientSecret` from the Service Principal
3. `SUBSCRIPTION_ID` - Value: `0f47daf8-38d9-4100-9afc-7ceca28f800d`
4. `AZURE_TENANT_ID` - The `tenantId` from your Azure AD

## Workflow Overview

The CI/CD pipeline consists of 5 jobs:

1. **build** - Builds the .NET application and creates a deployment artifact
2. **deploy** - Deploys infrastructure using Azure Deployment Stack and deploys function code
3. **test-standard** - Runs integration tests against the Standard Service Bus
4. **test-premium** - Runs integration tests against the Premium Service Bus
5. **cleanup** - Deletes all resources (runs even if tests fail)

## Triggering the Workflow

The workflow is triggered by:
- Push to `main` or `develop` branches
- Pull requests to `main` or `develop` branches
- Manual trigger via **Actions** → **CI - Build, Deploy, Test and Cleanup** → **Run workflow**

## What Gets Deployed

The deployment creates:
- **2 Service Bus Namespaces** (Standard and Premium SKU)
  - Each with topics: `NO_SESSION`, `ORDERED_TOPIC`
  - Each with required subscriptions
- **2 Azure Function Apps** (Consumption Plan, .NET 8)
  - One connected to Standard Service Bus
  - One connected to Premium Service Bus
- **Storage Account** (for Azure Functions)
- **Application Insights** (for monitoring)
- **App Service Plan** (Consumption tier)

## Resource Cleanup

All resources are automatically deleted at the end of the workflow using Azure Deployment Stack's `--delete-all` option. This ensures:
- No orphaned resources left behind
- Cost control for test environments
- Clean state for next test run

## Viewing Test Results

1. Go to **Actions** tab in your GitHub repository
2. Click on the latest workflow run
3. View results from:
   - `Test Standard Service Bus` job
   - `Test Premium Service Bus` job

## Troubleshooting

### Authentication Errors

If you see authentication errors:
1. Verify the Service Principal has Contributor role on the resource group
2. Check that the `AZURE_CREDENTIALS` secret is properly formatted JSON
3. Ensure the Service Principal hasn't expired

### Deployment Failures

If deployment fails:
1. Check the Azure portal for any quota or capacity issues
2. Verify the resource group `github-account-test` exists
3. Check if there are any policy restrictions in your subscription

### Test Failures

If tests fail:
1. Check that Service Bus namespaces were created successfully
2. Verify Function Apps are running
3. Review test logs for specific error messages
4. Ensure topics and subscriptions were created properly

## Manual Cleanup

If automatic cleanup fails, you can manually delete the deployment stack:

```bash
az login

az stack group delete \
  --name sb-reorder-test-stack \
  --resource-group github-account-test \
  --delete-all \
  --yes
```

## Further Information

- See [infra/README.md](infra/README.md) for detailed infrastructure documentation
- See [README.md](README.md) for application documentation
