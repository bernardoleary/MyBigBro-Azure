@description('The Azure region where resources will be deployed.')
param location string = resourceGroup().location

@description('Suffix to ensure globally unique resource names.')
param uniqueSuffix string = substring(uniqueString(resourceGroup().id), 0, 6)

@description('Administrator username for the PostgreSQL Flexible Server.')
param postgresAdminUser string = 'mybigbro'

@description('Administrator password for the PostgreSQL Flexible Server.')
@secure()
param postgresAdminPassword string

@description('Database name.')
param postgresDatabaseName string = 'mybigbro'

@description('Container image tag to deploy.')
param imageTag string = 'latest'

// Resource Names
var storageAccountName = 'stbigbro${uniqueSuffix}'
var postgresServerName = 'psql-bigbro-${uniqueSuffix}'
var acrName = 'acrbigbro${uniqueSuffix}'
var logWorkspaceName = 'log-bigbro-${uniqueSuffix}'
var acaEnvName = 'cae-bigbro-${uniqueSuffix}'

// 1. Storage Account & Blob Container
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: true
    minimumTlsVersion: 'TLS1_2'
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource imagesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'images'
  properties: {
    publicAccess: 'Blob'
  }
}

// 2. PostgreSQL Flexible Server with PostGIS
resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2023-12-01-preview' = {
  name: postgresServerName
  location: location
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '16'
    administratorLogin: postgresAdminUser
    administratorLoginPassword: postgresAdminPassword
    storage: {
      storageSizeGB: 32
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
  }
}

// Enable PostGIS extension parameter
resource postgresPostgisConfig 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2023-12-01-preview' = {
  parent: postgresServer
  name: 'azure.extensions'
  properties: {
    value: 'POSTGIS'
    source: 'user-override'
  }
}

// Allow Azure Services Firewall Rule
resource postgresFirewallAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-12-01-preview' = {
  parent: postgresServer
  name: 'AllowAllAzureIPs'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource postgresDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-12-01-preview' = {
  parent: postgresServer
  name: postgresDatabaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// 3. Azure Container Registry
resource acr 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: acrName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
}

// 4. Log Analytics Workspace
resource logWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logWorkspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// 5. Azure Container Apps Environment
resource acaEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: acaEnvName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logWorkspace.properties.customerId
        sharedKey: logWorkspace.listKeys().primarySharedKey
      }
    }
  }
}

// Variables for app connection strings
var postgresHost = '${postgresServer.name}.postgres.database.azure.com'
var dbConnectionString = 'Host=${postgresHost};Port=5432;Database=${postgresDatabaseName};Username=${postgresAdminUser};Password=${postgresAdminPassword};Ssl Mode=Require;Trust Server Certificate=true'
var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
var publicBlobUrl = 'https://${storageAccount.name}.blob.${environment().suffixes.storage}'

// 6. Container App: api-service (.NET 10 Web API)
resource apiServiceApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'api-service'
  location: location
  properties: {
    managedEnvironmentId: acaEnv.id
    configuration: {
      ingress: {
        external: false
        targetPort: 8080
        transport: 'auto'
      }
      registries: [
        {
          server: acr.properties.loginServer
          username: acr.listCredentials().username
          passwordSecretRef: 'acr-password'
        }
      ]
      secrets: [
        {
          name: 'acr-password'
          value: acr.listCredentials().passwords[0].value
        }
        {
          name: 'db-conn-str'
          value: dbConnectionString
        }
        {
          name: 'storage-conn-str'
          value: storageConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api-service'
          image: '${acr.properties.loginServer}/mybigbro-api:${imageTag}'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'CaptureWorker__UseAsyncQueue'
              value: 'true'
            }
            {
              name: 'Storage__Provider'
              value: 'Azure'
            }
            {
              name: 'Storage__AzureContainer'
              value: 'images'
            }
            {
              name: 'Storage__PublicRootUrl'
              value: publicBlobUrl
            }
            {
              name: 'ConnectionStrings__MyBigBroPostgres'
              secretRef: 'db-conn-str'
            }
            {
              name: 'Storage__AzureConnectionString'
              secretRef: 'storage-conn-str'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
}

// 7. Container App: capture-worker (.NET 10 Worker)
resource captureWorkerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'capture-worker'
  location: location
  properties: {
    managedEnvironmentId: acaEnv.id
    configuration: {
      registries: [
        {
          server: acr.properties.loginServer
          username: acr.listCredentials().username
          passwordSecretRef: 'acr-password'
        }
      ]
      secrets: [
        {
          name: 'acr-password'
          value: acr.listCredentials().passwords[0].value
        }
        {
          name: 'db-conn-str'
          value: dbConnectionString
        }
        {
          name: 'storage-conn-str'
          value: storageConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'capture-worker'
          image: '${acr.properties.loginServer}/mybigbro-capture-worker:${imageTag}'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'DOTNET_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'Storage__Provider'
              value: 'Azure'
            }
            {
              name: 'Storage__AzureContainer'
              value: 'images'
            }
            {
              name: 'Storage__PublicRootUrl'
              value: publicBlobUrl
            }
            {
              name: 'ConnectionStrings__MyBigBroPostgres'
              secretRef: 'db-conn-str'
            }
            {
              name: 'Storage__AzureConnectionString'
              secretRef: 'storage-conn-str'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

// 8. Container App: web-ui (Nginx Reverse Proxy & Map UI)
resource webUiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'web-ui'
  location: location
  properties: {
    managedEnvironmentId: acaEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 80
        transport: 'auto'
      }
      registries: [
        {
          server: acr.properties.loginServer
          username: acr.listCredentials().username
          passwordSecretRef: 'acr-password'
        }
      ]
      secrets: [
        {
          name: 'acr-password'
          value: acr.listCredentials().passwords[0].value
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'web-ui'
          image: '${acr.properties.loginServer}/mybigbro-web-ui:${imageTag}'
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'API_UPSTREAM'
              value: 'api-service'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
}

output webUiUrl string = 'https://${webUiApp.properties.configuration.ingress.fqdn}'
output acrLoginServer string = acr.properties.loginServer
output postgresServerFqdn string = postgresHost
output storageAccountName string = storageAccount.name
