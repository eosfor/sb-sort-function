# Infrastructure and CI/CD Setup

This directory contains Bicep templates for deploying Azure resources and a GitHub Actions workflow for CI/CD.

## Overview

The infrastructure creates:
- **Two Service Bus namespaces**: Standard and Premium SKU
  - Each with required topics and subscriptions (NO_SESSION, ORDERED_TOPIC)
- **Two Azure Functions**: Running in Consumption Plan
  - One connected to Standard Service Bus (UseTransactions=false)
  - One connected to Premium Service Bus (UseTransactions=true)
- **Supporting resources**: Storage Account, Application Insights, App Service Plan

## Files

- `main.bicep` - Main Bicep template that creates all Azure resources
- `../.github/workflows/ci.yml` - GitHub Actions workflow for CI/CD

## Bicep Template Structure

### Resources Created

1. **Service Bus Namespaces**:
   - Standard SKU: `sbreorder-std-{uniqueString}`
   - Premium SKU: `sbreorder-prem-{uniqueString}` (capacity: 1)

2. **Service Bus Topics and Subscriptions**:
   - `NO_SESSION` topic with subscriptions:
     - `NO_SESS_SUB` (RequiresSession: false)
     - `STATE_SUB` (RequiresSession: true)
   - `ORDERED_TOPIC` topic with subscriptions:
     - `SESS_SUB` (RequiresSession: true)
     - `SYSTEM_SUB` (RequiresSession: true)

3. **Azure Functions**:
   - Two Function Apps in Consumption Plan (.NET 8, isolated worker)
   - Connection strings automatically configured via app settings

### Outputs

The template provides these outputs:
- `serviceBusStandardConnectionString` - Connection string for Standard Service Bus
- `serviceBusPremiumConnectionString` - Connection string for Premium Service Bus
- `functionAppStandardName` - Name of the Standard Function App
- `functionAppPremiumName` - Name of the Premium Function App
- `storageAccountName` - Storage account name
- `appInsightsInstrumentationKey` - Application Insights key

## GitHub Actions Workflow

### Workflow Jobs

1. **build**: Compiles the .NET application and creates deployment artifact
2. **deploy**: Deploys infrastructure using Azure Deployment Stack and deploys function code
3. **test-standard**: Runs integration tests against Standard Service Bus
4. **test-premium**: Runs integration tests against Premium Service Bus
5. **cleanup**: Deletes all resources created by the deployment stack

### Required Secrets

Configure these secrets in your GitHub repository:

- `AUTOMATION_CLIENT_ID` - Azure Service Principal Client ID
- `AZURE_TENANT_ID` - Azure Tenant ID
- `SUBSCRIPTION_ID` - Azure Subscription ID (default: 0f47daf8-38d9-4100-9afc-7ceca28f800d)

### Workflow Configuration

The workflow uses these environment variables:
- `RESOURCE_GROUP`: `github-account-test`
- `SUBSCRIPTION_ID`: `0f47daf8-38d9-4100-9afc-7ceca28f800d`
- `LOCATION`: `eastus`
- `DEPLOYMENT_STACK_NAME`: `sb-reorder-test-stack`

### Azure Deployment Stack

The deployment uses Azure Deployment Stacks, which provides:
- Consistent resource management
- Simplified cleanup (deletes all managed resources in one operation)
- Protection against accidental deletion
- Consistent state management

## Manual Deployment

To deploy manually using Azure CLI:

```bash
# Login to Azure
az login

# Set subscription
az account set --subscription 0f47daf8-38d9-4100-9afc-7ceca28f800d

# Create deployment stack
az stack group create \
  --name sb-reorder-test-stack \
  --resource-group github-account-test \
  --template-file ./infra/main.bicep \
  --parameters location=eastus \
  --deny-settings-mode none \
  --yes

# Get outputs
az stack group show \
  --name sb-reorder-test-stack \
  --resource-group github-account-test \
  --query 'outputs'

# Delete stack and all resources
az stack group delete \
  --name sb-reorder-test-stack \
  --resource-group github-account-test \
  --delete-all \
  --yes
```

## Testing

The CI workflow runs integration tests from `tests/SbReorder.Tests` against both Service Bus instances:

### Standard Service Bus Test
- Uses connection string from Standard namespace
- `ServiceBusUseTransactions=false`
- Tests message reordering without transactions

### Premium Service Bus Test
- Uses connection string from Premium namespace
- `ServiceBusUseTransactions=true`
- Tests message reordering with cross-entity transactions

## Notes

- The Bicep template includes default names with `uniqueString()` to ensure uniqueness
- Storage account names are limited to 24 characters and use lowercase alphanumeric only
- Function Apps are configured with .NET 8 isolated worker runtime
- All resources use TLS 1.2 minimum and HTTPS only
- The cleanup job runs even if tests fail (using `if: always()`)
