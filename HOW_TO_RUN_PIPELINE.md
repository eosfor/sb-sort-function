# How to Run the CI Pipeline

## Prerequisites Checklist

Before running the pipeline, complete these steps:

### 1. Create Azure Service Principal

```bash
# Login to Azure
az login

# Create Service Principal
az ad sp create-for-rbac \
  --name "github-sb-reorder-sp" \
  --role Contributor \
  --scopes /subscriptions/0f47daf8-38d9-4100-9afc-7ceca28f800d/resourceGroups/github-account-test \
  --json-auth
```

Copy the JSON output - you'll need it for the next step.

### 2. Configure GitHub Secret

1. Go to: https://github.com/eosfor/sb-sort-function/settings/secrets/actions
2. Click "New repository secret"
3. Name: `AZURE_CREDENTIALS`
4. Value: Paste the JSON from step 1
5. Click "Add secret"

### 3. Verify Resource Group Exists

```bash
# Check if resource group exists
az group show --name github-account-test --subscription 0f47daf8-38d9-4100-9afc-7ceca28f800d
```

If it doesn't exist, create it:
```bash
az group create \
  --name github-account-test \
  --location eastus \
  --subscription 0f47daf8-38d9-4100-9afc-7ceca28f800d
```

## Running the Pipeline

### Option 1: Merge to Main (Recommended for production)

```bash
# Ensure you're on the feature branch
git checkout copilot/create-service-bus-instances

# Create a pull request or merge to main
# The workflow will trigger automatically on push to main
```

### Option 2: Manual Trigger (Testing)

1. First, push the workflow to main:
   ```bash
   git checkout main
   git merge copilot/create-service-bus-instances
   git push origin main
   ```

2. Then trigger manually:
   - Go to: https://github.com/eosfor/sb-sort-function/actions
   - Click "CI - Build, Deploy, Test and Cleanup"
   - Click "Run workflow"
   - Select branch: `main`
   - Click "Run workflow"

### Option 3: Test via Pull Request

1. Create a PR from the feature branch to main
2. The workflow will run automatically on the PR
3. Review results before merging

## Monitoring the Pipeline

1. Go to: https://github.com/eosfor/sb-sort-function/actions
2. Click on the running workflow
3. Monitor each job:
   - **Build** (2-3 min): Compiles and publishes the application
   - **Deploy** (10-15 min): Creates Azure resources and deploys code
   - **Test Standard** (2-5 min): Tests Standard Service Bus
   - **Test Premium** (2-5 min): Tests Premium Service Bus
   - **Cleanup** (2-5 min): Deletes all resources

## Expected Timeline

| Job | Duration | Status |
|-----|----------|--------|
| Build | 2-3 min | ⏳ |
| Deploy | 10-15 min | ⏳ |
| Test Standard | 2-5 min | ⏳ |
| Test Premium | 2-5 min | ⏳ |
| Cleanup | 2-5 min | ⏳ |
| **Total** | **20-35 min** | ⏳ |

## Interpreting Results

### ✅ Success
All jobs complete with green checkmarks. Resources are automatically cleaned up.

### ❌ Failure Scenarios

#### Build Fails
- Check: .NET version compatibility
- Check: NuGet package availability
- Solution: Review build logs for specific errors

#### Deploy Fails
- Check: Azure credentials are valid
- Check: Resource group exists
- Check: Sufficient quota for Premium Service Bus
- Solution: Review deployment logs in Azure portal

#### Tests Fail
- Check: Service Bus topics and subscriptions created
- Check: Function Apps deployed successfully
- Check: Connection strings configured correctly
- Solution: Review test output for specific failures

#### Cleanup Fails
- Manual cleanup required:
  ```bash
  az stack group delete \
    --name sb-reorder-test-stack \
    --resource-group github-account-test \
    --delete-all \
    --yes
  ```

## Cost Considerations

The pipeline creates:
- **Premium Service Bus**: ~$0.90-1.00 per hour (most expensive)
- **Standard Service Bus**: ~$0.01 per hour
- **Function Apps (Consumption)**: Pay per execution
- **Storage Account**: Minimal cost
- **Application Insights**: Free tier sufficient

**Total cost per run**: ~$0.30-0.50 (assuming 30-minute runtime)

Resources are automatically deleted after tests complete.

## Troubleshooting

### Pipeline doesn't appear in Actions tab
- Ensure workflow file is on `main` or `develop` branch
- Check `.github/workflows/ci.yml` exists
- Verify YAML syntax is valid

### Authentication errors
- Verify `AZURE_CREDENTIALS` secret is configured
- Check Service Principal hasn't expired
- Ensure Service Principal has Contributor role

### Deployment Stack errors
- Ensure Azure CLI version supports deployment stacks (2.50.0+)
- Check resource group exists
- Verify no policy restrictions

### Premium Service Bus quota
- Premium SKU requires pre-approval in some regions
- Consider using Standard for both instances if quota is limited
- Modify `infra/main.bicep` to use Standard for both

## Quick Test Commands

To test locally before running pipeline:

```bash
# Test build
dotnet build --configuration Release

# Test publish
dotnet publish src/src.csproj --configuration Release --output ./publish

# Validate Bicep
az bicep build --file ./infra/main.bicep

# Test integration tests (without Service Bus)
dotnet test tests/SbReorder.Tests --configuration Release
```

All commands should complete successfully before running the pipeline.

---

**Ready to run?** Follow the steps above and monitor the pipeline execution in GitHub Actions!
