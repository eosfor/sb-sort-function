# Workflow Failure Analysis and Fix

## Problem / Проблема

The CI/CD workflow was failing during the infrastructure deployment step with the following error:

```
"code": "SubscriptionIsOverQuotaForSku",
"message": "Operation cannot be completed without additional quota. 
Additional details - Location:  
Current Limit (Dynamic VMs): 0 
Current Usage: 0
Amount required for this deployment (Dynamic VMs): 1 
(Minimum) New Limit that you should request to enable this deployment: 1."
```

## Root Cause / Причина

The Bicep infrastructure template (`infra/main.bicep`) was configured to use an Azure Function App with a **Consumption Plan** (also known as Dynamic Plan):

```bicep
sku: {
  name: 'Y1'
  tier: 'Dynamic'
}
```

The Azure subscription being used doesn't have quota allocated for **Dynamic VMs**, which are required for Consumption-based Function Apps. The subscription has:
- **Current Limit for Dynamic VMs**: 0
- **Required for deployment**: 1

This is a quota limitation at the subscription level.

## Solution / Решение

Changed the App Service Plan from **Consumption (Y1/Dynamic)** to **Basic (B1)** tier:

```bicep
sku: {
  name: 'B1'
  tier: 'Basic'
}
```

### Why This Works / Почему это работает

1. **Basic Plan (B1)** uses dedicated compute resources instead of Dynamic VMs
2. **No Dynamic VM quota** is required for Basic plans
3. Basic plans are suitable for development and testing workloads
4. The B1 tier provides:
   - 1 Core
   - 1.75 GB RAM
   - 10 GB Storage
   - Always-on support

### Trade-offs / Компромиссы

**Consumption Plan (Y1):**
- ✅ Pay only for execution time
- ✅ Automatic scaling
- ❌ Requires Dynamic VM quota
- ❌ Cold start delays

**Basic Plan (B1):**
- ✅ No Dynamic VM quota needed
- ✅ Always-on (no cold starts)
- ✅ Predictable performance
- ❌ Fixed monthly cost (even when idle)
- ❌ Manual scaling required

For this CI/CD pipeline, the Basic plan is appropriate because:
- The deployment is temporary (resources are cleaned up after tests)
- Predictable performance is better for testing
- No quota issues to deal with

## Files Changed / Измененные файлы

- `infra/main.bicep` - Changed App Service Plan SKU from Y1/Dynamic to B1/Basic

## Testing / Тестирование

You can now manually trigger the workflow again:

```bash
# Via GitHub CLI
gh workflow run ci.yml --ref copilot/sub-pr-2

# Or via GitHub UI
# Go to Actions → CI - Build, Deploy, Test and Cleanup → Run workflow
```

The deployment should now succeed without quota errors.

## Alternative Solutions (Not Implemented) / Альтернативные решения

If you prefer to use Consumption plan:

1. **Request quota increase**: Contact Azure support to increase Dynamic VM quota for your subscription
2. **Use different region**: Some regions might have available quota
3. **Use different subscription**: If you have access to another subscription with available quota

For this fix, we chose the Basic plan approach as it's the quickest solution that doesn't require Azure support intervention.

---

## Summary / Резюме

**Проблема**: Workflow падал из-за отсутствия квоты для Dynamic VM в подписке Azure.

**Решение**: Изменили App Service Plan с Consumption (Y1/Dynamic) на Basic (B1), который не требует квоты Dynamic VM.

**Результат**: Deployment теперь должен работать без ошибок квоты.
