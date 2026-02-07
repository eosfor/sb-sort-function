# Configuration Change: P0v3 Plan in West US 2

## Changes Made / Внесенные изменения

### 1. App Service Plan / План App Service
Changed from **Basic B1** to **Premium v3 P0v3**

**File**: `infra/main.bicep`

**Before / До:**
```bicep
sku: {
  name: 'B1'
  tier: 'Basic'
}
```

**After / После:**
```bicep
sku: {
  name: 'P0v3'
  tier: 'PremiumV3'
}
```

### 2. Region / Регион
Changed from **East US** to **West US 2**

**File**: `.github/workflows/ci.yml`

**Before / До:**
```yaml
LOCATION: 'eastus'
```

**After / После:**
```yaml
LOCATION: 'westus2'
```

## Premium v3 P0v3 Benefits / Преимущества Premium v3 P0v3

### Specifications / Характеристики:
- **vCPUs**: 2 cores / 2 ядра
- **Memory**: 4 GB RAM
- **Storage**: 250 GB
- **Network**: Enhanced networking / Улучшенная сеть
- **Scaling**: Auto-scaling support / Поддержка автомасштабирования

### Key Features / Ключевые возможности:
✅ **Better Performance** - More CPU and RAM than Basic B1
✅ **VNet Integration** - Private network connectivity
✅ **Deployment Slots** - Blue-green deployments
✅ **Auto-scaling** - Automatic scaling based on load
✅ **Always On** - No cold starts
✅ **Advanced Diagnostics** - Better monitoring capabilities
✅ **Traffic Manager** - Global load balancing support

### Comparison / Сравнение:

| Feature | Basic B1 | Premium P0v3 |
|---------|----------|--------------|
| vCPUs | 1 | 2 |
| RAM | 1.75 GB | 4 GB |
| Storage | 10 GB | 250 GB |
| VNet Integration | ❌ | ✅ |
| Deployment Slots | ❌ | ✅ |
| Auto-scaling | ❌ | ✅ |
| Custom Domains/SSL | ✅ | ✅ |
| Always On | ✅ | ✅ |

## West US 2 Region Benefits / Преимущества региона West US 2

- **Availability Zones**: Supports AZs for high availability
- **Latest Hardware**: Modern datacenter infrastructure
- **Low Latency**: Optimized for West Coast US applications
- **Service Availability**: Full Azure service catalog
- **Compliance**: Multiple compliance certifications

## Cost Considerations / Стоимость

⚠️ **Note**: Premium v3 P0v3 is more expensive than Basic B1:
- **Basic B1**: ~$13/month
- **Premium P0v3**: ~$100/month

However, you get significantly better performance and features suitable for production workloads.

## Testing / Тестирование

You can now manually trigger the workflow to deploy to West US 2 with P0v3 plan:

```bash
# Via GitHub CLI
gh workflow run ci.yml --ref copilot/sub-pr-2

# Or via GitHub UI
# Go to Actions → CI - Build, Deploy, Test and Cleanup → Run workflow
# Select branch: copilot/sub-pr-2 → Run workflow
```

The deployment will now:
1. Deploy all resources to **West US 2** region
2. Use **Premium v3 P0v3** App Service Plan
3. Provide better performance for Function Apps
4. Support advanced features like VNet integration if needed

---

## Summary / Резюме

✅ **App Service Plan**: B1 → P0v3 (Premium v3)
✅ **Region**: East US → West US 2
✅ **Performance**: Улучшена (2 vCPUs, 4 GB RAM)
✅ **Features**: Расширены (VNet, slots, auto-scaling)

Commit: b27a6c0
