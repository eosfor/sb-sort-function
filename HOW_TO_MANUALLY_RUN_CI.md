# How to Manually Run the CI Workflow

The CI workflow has been configured to run only manually. This prevents automatic execution on push or pull request events.

## Running the Workflow from Your Branch

### Option 1: Via GitHub Web UI

1. Navigate to the repository on GitHub: `https://github.com/eosfor/sb-sort-function`

2. Click on the **Actions** tab at the top of the repository

3. In the left sidebar, select **"CI - Build, Deploy, Test and Cleanup"** workflow

4. On the right side, you'll see a **"Run workflow"** button (dropdown)

5. Click the **"Run workflow"** dropdown:
   - Select your branch from the "Branch" dropdown (e.g., `copilot/sub-pr-2` or any other branch)
   - Click the green **"Run workflow"** button

6. The workflow will start executing. You can click on the workflow run to see its progress and logs.

### Option 2: Via GitHub CLI (gh)

If you have GitHub CLI installed, you can trigger the workflow from the command line:

```bash
# Trigger the workflow on a specific branch (replace with your branch name)
gh workflow run "CI - Build, Deploy, Test and Cleanup" --ref your-branch-name

# Example: Run on copilot/sub-pr-2 branch
gh workflow run "CI - Build, Deploy, Test and Cleanup" --ref copilot/sub-pr-2

# Or using the workflow file name
gh workflow run ci.yml --ref your-branch-name

# View the workflow runs
gh run list --workflow=ci.yml

# Watch the latest run
gh run watch
```

### Option 3: Via GitHub API

You can also trigger the workflow using the GitHub REST API:

```bash
curl -X POST \
  -H "Accept: application/vnd.github+json" \
  -H "Authorization: Bearer YOUR_GITHUB_TOKEN" \
  -H "X-GitHub-Api-Version: 2022-11-28" \
  https://api.github.com/repos/eosfor/sb-sort-function/actions/workflows/ci.yml/dispatches \
  -d '{"ref":"your-branch-name"}'
```

Replace `your-branch-name` with the actual branch name you want to run the workflow on.

## Prerequisites

Before running the workflow, ensure:
- The `AZURE_CREDENTIALS` secret is properly configured in the repository settings
- You have the necessary permissions to trigger workflows in the repository
- All required Azure resources and permissions are set up as described in `CI_SETUP.md`

## What the Workflow Does

The manual CI workflow performs the following steps:
1. **Build Application**: Restores dependencies, builds, and publishes the .NET Function App
2. **Deploy Infrastructure and Applications**: Deploys Azure infrastructure using Bicep and deploys the Function Apps
3. **Test Standard Service Bus**: Runs tests against Standard tier Service Bus
4. **Test Premium Service Bus**: Runs tests against Premium tier Service Bus  
5. **Cleanup Resources**: Deletes the deployment stack and all managed resources

## Viewing Results

After triggering the workflow:
- Monitor progress in the GitHub Actions tab
- View detailed logs for each job and step
- Check for any errors or failures in the deployment or tests
- Review the cleanup step to ensure resources are properly removed

## Troubleshooting

If the workflow fails:
1. Check the job logs in the Actions tab for error messages
2. Verify that all secrets are properly configured
3. Ensure your Azure credentials have the necessary permissions
4. Review the `PIPELINE_VALIDATION.md` document for common issues
