targetScope = 'resourceGroup'

@description('Base name for resources')
param baseName string = 'sbreorder'

@description('Location for all resources')
param location string = resourceGroup().location

@description('Service Bus Standard namespace name')
param serviceBusStandardName string = '${baseName}-std-${uniqueString(resourceGroup().id)}'

@description('Service Bus Premium namespace name')
param serviceBusPremiumName string = '${baseName}-prem-${uniqueString(resourceGroup().id)}'

@description('Storage account name for Azure Functions')
param storageAccountName string = 'st${uniqueString(resourceGroup().id)}'

@description('App Service Plan name')
param appServicePlanName string = '${baseName}-plan-${uniqueString(resourceGroup().id)}'

@description('Function App name for Standard Service Bus')
param functionAppStandardName string = '${baseName}-std-${uniqueString(resourceGroup().id)}'

@description('Function App name for Premium Service Bus')
param functionAppPremiumName string = '${baseName}-prem-${uniqueString(resourceGroup().id)}'

@description('Application Insights name')
param appInsightsName string = '${baseName}-ai-${uniqueString(resourceGroup().id)}'

// Service Bus Standard namespace
resource serviceBusStandard 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusStandardName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    minimumTlsVersion: '1.2'
  }
}

// Service Bus Premium namespace
resource serviceBusPremium 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusPremiumName
  location: location
  sku: {
    name: 'Premium'
    tier: 'Premium'
    capacity: 1
  }
  properties: {
    minimumTlsVersion: '1.2'
  }
}

// Topics and subscriptions for Standard Service Bus
resource standardNoSessionTopic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  parent: serviceBusStandard
  name: 'NO_SESSION'
  properties: {
    defaultMessageTimeToLive: 'PT1H'
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: false
    enableBatchedOperations: true
    supportOrdering: true
  }
}

resource standardNoSessionNoSessSub 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: standardNoSessionTopic
  name: 'NO_SESS_SUB'
  properties: {
    requiresSession: false
    lockDuration: 'PT1M'
    defaultMessageTimeToLive: 'PT1H'
    maxDeliveryCount: 10
  }
}

resource standardNoSessionStateSub 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: standardNoSessionTopic
  name: 'STATE_SUB'
  properties: {
    requiresSession: true
    lockDuration: 'PT1M'
    defaultMessageTimeToLive: 'PT1H'
    maxDeliveryCount: 10
  }
}

resource standardOrderedTopic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  parent: serviceBusStandard
  name: 'ORDERED_TOPIC'
  properties: {
    defaultMessageTimeToLive: 'PT1H'
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: false
    enableBatchedOperations: true
    supportOrdering: true
  }
}

resource standardOrderedSessSub 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: standardOrderedTopic
  name: 'SESS_SUB'
  properties: {
    requiresSession: true
    lockDuration: 'PT1M'
    defaultMessageTimeToLive: 'PT1H'
    maxDeliveryCount: 10
  }
}

resource standardOrderedSystemSub 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: standardOrderedTopic
  name: 'SYSTEM_SUB'
  properties: {
    requiresSession: true
    lockDuration: 'PT1M'
    defaultMessageTimeToLive: 'PT1H'
    maxDeliveryCount: 10
  }
}

// Topics and subscriptions for Premium Service Bus
resource premiumNoSessionTopic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  parent: serviceBusPremium
  name: 'NO_SESSION'
  properties: {
    defaultMessageTimeToLive: 'PT1H'
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: false
    enableBatchedOperations: true
    supportOrdering: true
  }
}

resource premiumNoSessionNoSessSub 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: premiumNoSessionTopic
  name: 'NO_SESS_SUB'
  properties: {
    requiresSession: false
    lockDuration: 'PT1M'
    defaultMessageTimeToLive: 'PT1H'
    maxDeliveryCount: 10
  }
}

resource premiumNoSessionStateSub 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: premiumNoSessionTopic
  name: 'STATE_SUB'
  properties: {
    requiresSession: true
    lockDuration: 'PT1M'
    defaultMessageTimeToLive: 'PT1H'
    maxDeliveryCount: 10
  }
}

resource premiumOrderedTopic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  parent: serviceBusPremium
  name: 'ORDERED_TOPIC'
  properties: {
    defaultMessageTimeToLive: 'PT1H'
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: false
    enableBatchedOperations: true
    supportOrdering: true
  }
}

resource premiumOrderedSessSub 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: premiumOrderedTopic
  name: 'SESS_SUB'
  properties: {
    requiresSession: true
    lockDuration: 'PT1M'
    defaultMessageTimeToLive: 'PT1H'
    maxDeliveryCount: 10
  }
}

resource premiumOrderedSystemSub 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: premiumOrderedTopic
  name: 'SYSTEM_SUB'
  properties: {
    requiresSession: true
    lockDuration: 'PT1M'
    defaultMessageTimeToLive: 'PT1H'
    maxDeliveryCount: 10
  }
}

// Authorization rules for connection strings
resource standardAuthRule 'Microsoft.ServiceBus/namespaces/authorizationRules@2022-10-01-preview' = {
  parent: serviceBusStandard
  name: 'RootManageSharedAccessKey'
  properties: {
    rights: [
      'Listen'
      'Manage'
      'Send'
    ]
  }
}

resource premiumAuthRule 'Microsoft.ServiceBus/namespaces/authorizationRules@2022-10-01-preview' = {
  parent: serviceBusPremium
  name: 'RootManageSharedAccessKey'
  properties: {
    rights: [
      'Listen'
      'Manage'
      'Send'
    ]
  }
}

// Storage Account for Azure Functions
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }
}

// Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Request_Source: 'rest'
  }
}

// App Service Plan (Consumption)
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {}
}

// Function App for Standard Service Bus
resource functionAppStandard 'Microsoft.Web/sites@2023-01-01' = {
  name: functionAppStandardName
  location: location
  kind: 'functionapp'
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppStandardName)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'FUNCTIONS_WORKER_PROCESS_COUNT'
          value: '1'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsights.properties.InstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'ServiceBusConnection'
          value: listKeys(standardAuthRule.id, standardAuthRule.apiVersion).primaryConnectionString
        }
        {
          name: 'ServiceBusUseTransactions'
          value: 'false'
        }
      ]
      netFrameworkVersion: 'v8.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
    }
    httpsOnly: true
  }
}

// Function App for Premium Service Bus
resource functionAppPremium 'Microsoft.Web/sites@2023-01-01' = {
  name: functionAppPremiumName
  location: location
  kind: 'functionapp'
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppPremiumName)
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'FUNCTIONS_WORKER_PROCESS_COUNT'
          value: '1'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsights.properties.InstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'ServiceBusConnection'
          value: listKeys(premiumAuthRule.id, premiumAuthRule.apiVersion).primaryConnectionString
        }
        {
          name: 'ServiceBusUseTransactions'
          value: 'true'
        }
      ]
      netFrameworkVersion: 'v8.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
    }
    httpsOnly: true
  }
}

// Outputs
output serviceBusStandardConnectionString string = listKeys(standardAuthRule.id, standardAuthRule.apiVersion).primaryConnectionString
output serviceBusPremiumConnectionString string = listKeys(premiumAuthRule.id, premiumAuthRule.apiVersion).primaryConnectionString
output functionAppStandardName string = functionAppStandard.name
output functionAppPremiumName string = functionAppPremium.name
output storageAccountName string = storageAccount.name
output appInsightsInstrumentationKey string = appInsights.properties.InstrumentationKey
