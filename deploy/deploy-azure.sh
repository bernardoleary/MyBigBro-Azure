#!/usr/bin/env bash
# ==============================================================================
# MyBigBro - Azure Fully Managed PaaS Deployment Script
# 
# Architecture:
#   - Database: Azure Database for PostgreSQL - Flexible Server (Standard_B1ms + PostGIS 3.5)
#   - Compute:  Azure Container Apps (ACA)
#               * api-service (Internal Ingress, Port 8080, .NET 10 ASP.NET Core)
#               * capture-worker (Background Worker Service, .NET 10)
#               * web-ui (External Ingress, Port 80, Nginx Alpine + AngularJS Map)
#   - Storage:  Azure Blob Storage (Images container for CCTV frames)
#   - Registry: Azure Container Registry (ACR Basic)
#   - Telemetry: Azure Log Analytics Workspace
# ==============================================================================

set -euo pipefail

# Text formatting
BOLD="\033[1m"
GREEN="\033[0;32m"
CYAN="\033[0;36m"
YELLOW="\033[1;33m"
RED="\033[0;31m"
RESET="\033[0m"

info()    { echo -e "${CYAN}${BOLD}[INFO]${RESET} $*"; }
success() { echo -e "${GREEN}${BOLD}[SUCCESS]${RESET} $*"; }
warn()    { echo -e "${YELLOW}${BOLD}[WARNING]${RESET} $*"; }
error()   { echo -e "${RED}${BOLD}[ERROR]${RESET} $*" >&2; }

# Locate script root and repo root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

# Load .env file if present in deploy directory
if [[ -f "${SCRIPT_DIR}/.env" ]]; then
    info "Loading configurations from ${SCRIPT_DIR}/.env"
    # shellcheck disable=SC1091
    source "${SCRIPT_DIR}/.env"
fi

# ==============================================================================
# Configurable Settings (Can be overridden via environment variables)
# ==============================================================================
LOCATION="${AZURE_LOCATION:-newzealandnorth}"
RESOURCE_GROUP="${AZURE_RESOURCE_GROUP:-rg-mybigbro}"
POSTGRES_DB="${POSTGRES_DB:-mybigbro}"
POSTGRES_USER="${POSTGRES_USER:-mybigbro}"
CONTAINER_IMAGE_TAG="${IMAGE_TAG:-latest}"
SKIP_BUILD="${SKIP_BUILD:-false}"
SKIP_DB_INIT="${SKIP_DB_INIT:-false}"

# Generate deterministic random suffix for globally unique Azure resources
if [[ -z "${RANDOM_SUFFIX:-}" ]]; then
    if [[ -f "${SCRIPT_DIR}/.suffix" ]]; then
        RANDOM_SUFFIX=$(cat "${SCRIPT_DIR}/.suffix")
    else
        RANDOM_SUFFIX=$(head -c 16 /dev/urandom | tr -dc 'a-z0-9' | head -c 5)
        echo -n "${RANDOM_SUFFIX}" > "${SCRIPT_DIR}/.suffix"
    fi
fi

STORAGE_ACCOUNT="${AZURE_STORAGE_ACCOUNT:-stbigbro${RANDOM_SUFFIX}}"
POSTGRES_SERVER="${AZURE_POSTGRES_SERVER:-psql-bigbro-${RANDOM_SUFFIX}}"
ACR_NAME="${AZURE_ACR_NAME:-acrbigbro${RANDOM_SUFFIX}}"
LOG_WORKSPACE="${AZURE_LOG_WORKSPACE:-log-bigbro-${RANDOM_SUFFIX}}"
ACA_ENV="${AZURE_ACA_ENV:-cae-bigbro-${RANDOM_SUFFIX}}"

# Ensure PostgreSQL password meets Azure complexity requirements
if [[ -z "${POSTGRES_PASSWORD:-}" ]]; then
    if [[ -f "${SCRIPT_DIR}/.db_password" ]]; then
        POSTGRES_PASSWORD=$(cat "${SCRIPT_DIR}/.db_password")
    else
        # 20 chars with upper, lower, digits, allowed special char
        POSTGRES_PASSWORD="Bb$(head -c 24 /dev/urandom | tr -dc 'A-Za-z0-9' | head -c 16)#!"
        echo -n "${POSTGRES_PASSWORD}" > "${SCRIPT_DIR}/.db_password"
        chmod 600 "${SCRIPT_DIR}/.db_password"
    fi
fi

# ==============================================================================
# Banner & Pre-flight Checks
# ==============================================================================
echo -e "${BOLD}"
echo "=================================================================="
echo "    MyBigBro - Azure Fully Managed PaaS Deployment"
echo "=================================================================="
echo -e "${RESET}"
info "Target Resource Group:  ${RESOURCE_GROUP}"
info "Target Location:        ${LOCATION}"
info "Storage Account:        ${STORAGE_ACCOUNT}"
info "PostgreSQL Server:      ${POSTGRES_SERVER}.postgres.database.azure.com"
info "Container Registry:     ${ACR_NAME}.azurecr.io"
info "ACA Environment:        ${ACA_ENV}"
echo ""

# 1. Verify Azure CLI
if ! command -v az &>/dev/null; then
    error "Azure CLI ('az') is not installed. Please install it: https://aka.ms/installazurecliwsl"
    exit 1
fi

# 2. Verify Azure Authentication
info "Checking Azure authentication status..."
if ! az account show &>/dev/null; then
    error "You are not logged in to Azure. Run 'az login' first."
    exit 1
fi

SUBSCRIPTION_NAME=$(az account show --query name -o tsv)
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
success "Logged in to subscription: ${SUBSCRIPTION_NAME} (${SUBSCRIPTION_ID})"

# 3. Ensure required Azure CLI extensions are installed
info "Checking Azure Container Apps CLI extension..."
if ! az extension show --name containerapp &>/dev/null; then
    info "Installing 'containerapp' Azure CLI extension..."
    az extension add --name containerapp --upgrade --yes
else
    info "'containerapp' extension is available."
fi

# 4. Register Required Resource Providers
info "Registering required Azure resource providers..."
for provider in Microsoft.App Microsoft.OperationalInsights Microsoft.DBforPostgreSQL Microsoft.Storage Microsoft.ContainerRegistry; do
    az provider register --namespace "${provider}" --output none 2>/dev/null || true
done

# ==============================================================================
# Step 1: Resource Group
# ==============================================================================
info "Step 1/7: Creating or verifying Resource Group '${RESOURCE_GROUP}'..."
az group create \
    --name "${RESOURCE_GROUP}" \
    --location "${LOCATION}" \
    --output none
success "Resource Group '${RESOURCE_GROUP}' is ready."

# ==============================================================================
# Step 2: Azure Blob Storage
# ==============================================================================
info "Step 2/7: Creating Azure Blob Storage Account '${STORAGE_ACCOUNT}'..."
if ! az storage account show --name "${STORAGE_ACCOUNT}" --resource-group "${RESOURCE_GROUP}" &>/dev/null; then
    az storage account create \
        --name "${STORAGE_ACCOUNT}" \
        --resource-group "${RESOURCE_GROUP}" \
        --location "${LOCATION}" \
        --sku Standard_LRS \
        --kind StorageV2 \
        --allow-blob-public-access true \
        --min-tls-version TLS1_2 \
        --output none
    success "Storage Account '${STORAGE_ACCOUNT}' created."
else
    info "Storage Account '${STORAGE_ACCOUNT}' already exists."
fi

STORAGE_CONNECTION_STRING=$(az storage account show-connection-string \
    --name "${STORAGE_ACCOUNT}" \
    --resource-group "${RESOURCE_GROUP}" \
    --query connectionString -o tsv)

info "Creating 'images' blob container with public read access..."
az storage container create \
    --name images \
    --account-name "${STORAGE_ACCOUNT}" \
    --connection-string "${STORAGE_CONNECTION_STRING}" \
    --public-access blob \
    --output none 2>/dev/null || true
success "Blob storage container 'images' configured."

# ==============================================================================
# Step 3: Azure Database for PostgreSQL - Flexible Server (with PostGIS)
# ==============================================================================
info "Step 3/7: Setting up Azure Database for PostgreSQL Flexible Server..."
if ! az postgres flexible-server show --name "${POSTGRES_SERVER}" --resource-group "${RESOURCE_GROUP}" &>/dev/null; then
    info "Creating PostgreSQL Flexible Server '${POSTGRES_SERVER}' (Burstable B1ms, PG 16)..."
    az postgres flexible-server create \
        --name "${POSTGRES_SERVER}" \
        --resource-group "${RESOURCE_GROUP}" \
        --location "${LOCATION}" \
        --admin-user "${POSTGRES_USER}" \
        --admin-password "${POSTGRES_PASSWORD}" \
        --sku-name Standard_B1ms \
        --tier Burstable \
        --version 16 \
        --storage-size 32 \
        --public-access 0.0.0.0 \
        --yes \
        --output none
    success "PostgreSQL Flexible Server '${POSTGRES_SERVER}' created."
else
    info "PostgreSQL Server '${POSTGRES_SERVER}' already exists."
fi

# Ensure application database exists
info "Verifying database '${POSTGRES_DB}' on server '${POSTGRES_SERVER}'..."
az postgres flexible-server db create \
    --resource-group "${RESOURCE_GROUP}" \
    --server-name "${POSTGRES_SERVER}" \
    --name "${POSTGRES_DB}" \
    --output none 2>/dev/null || true
success "Database '${POSTGRES_DB}' is ready."

# Allow all Azure internal services through the firewall (0.0.0.0 rule)
info "Configuring PostgreSQL firewall to allow Azure Container Apps..."
az postgres flexible-server firewall-rule create \
    --resource-group "${RESOURCE_GROUP}" \
    --server-name "${POSTGRES_SERVER}" \
    --name AllowAllAzureIPs \
    --start-ip-address 0.0.0.0 \
    --end-ip-address 0.0.0.0 \
    --output none 2>/dev/null || true

# Allow current client IP for schema execution
CLIENT_IP=$(curl -s https://api.ipify.org || true)
if [[ -n "${CLIENT_IP}" ]]; then
    info "Temporarily whitelisting deployer IP (${CLIENT_IP}) for schema setup..."
    az postgres flexible-server firewall-rule create \
        --resource-group "${RESOURCE_GROUP}" \
        --server-name "${POSTGRES_SERVER}" \
        --name AllowDeployerIP \
        --start-ip-address "${CLIENT_IP}" \
        --end-ip-address "${CLIENT_IP}" \
        --output none 2>/dev/null || true
fi

# Enable PostGIS extension in server configuration
info "Enabling PostGIS extension on PostgreSQL Flexible Server..."
CURRENT_EXTENSIONS=$(az postgres flexible-server parameter show \
    --resource-group "${RESOURCE_GROUP}" \
    --server-name "${POSTGRES_SERVER}" \
    --name azure.extensions \
    --query value -o tsv 2>/dev/null || echo "")

if [[ "${CURRENT_EXTENSIONS}" != *"POSTGIS"* ]]; then
    az postgres flexible-server parameter set \
        --resource-group "${RESOURCE_GROUP}" \
        --server-name "${POSTGRES_SERVER}" \
        --name azure.extensions \
        --value "POSTGIS" \
        --output none
    success "PostGIS extension enabled in 'azure.extensions'."
else
    info "PostGIS extension already enabled in 'azure.extensions'."
fi

# Seed Database Schema & 105 NZTA Webcams
POSTGRES_HOST="${POSTGRES_SERVER}.postgres.database.azure.com"
if [[ "${SKIP_DB_INIT}" != "true" ]]; then
    info "Executing schema initialisation (01-init-schema.sql)..."
    if command -v psql &>/dev/null; then
        PGPASSWORD="${POSTGRES_PASSWORD}" psql -h "${POSTGRES_HOST}" -p 5432 -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -v "sslmode=require" -f "${REPO_ROOT}/docker/postgres/01-init-schema.sql" >/dev/null
        info "Seeding 105 NZTA traffic webcams (02-seed-webcams.sql)..."
        PGPASSWORD="${POSTGRES_PASSWORD}" psql -h "${POSTGRES_HOST}" -p 5432 -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -v "sslmode=require" -f "${REPO_ROOT}/docker/postgres/02-seed-webcams.sql" >/dev/null
    elif command -v docker &>/dev/null; then
        docker run --rm -v "${REPO_ROOT}/docker/postgres:/sql" -e PGPASSWORD="${POSTGRES_PASSWORD}" postgres:16-alpine psql -h "${POSTGRES_HOST}" -p 5432 -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -v "sslmode=require" -f /sql/01-init-schema.sql >/dev/null
        info "Seeding 105 NZTA traffic webcams (02-seed-webcams.sql)..."
        docker run --rm -v "${REPO_ROOT}/docker/postgres:/sql" -e PGPASSWORD="${POSTGRES_PASSWORD}" postgres:16-alpine psql -h "${POSTGRES_HOST}" -p 5432 -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -v "sslmode=require" -f /sql/02-seed-webcams.sql >/dev/null
    else
        warn "Neither 'psql' nor 'docker' is available locally. Skipping database seeding."
    fi
    success "Database schema and seed webcams loaded successfully."
fi

# Construct connection string
POSTGRES_HOST="${POSTGRES_SERVER}.postgres.database.azure.com"
DB_CONNECTION_STRING="Host=${POSTGRES_HOST};Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD};Ssl Mode=Require;Trust Server Certificate=true"

# ==============================================================================
# Step 4: Azure Container Registry (ACR) & Image Builds
# ==============================================================================
info "Step 4/7: Creating Azure Container Registry '${ACR_NAME}'..."
if ! az acr show --name "${ACR_NAME}" --resource-group "${RESOURCE_GROUP}" &>/dev/null; then
    az acr create \
        --name "${ACR_NAME}" \
        --resource-group "${RESOURCE_GROUP}" \
        --sku Basic \
        --admin-enabled true \
        --output none
    success "Container Registry '${ACR_NAME}' created."
else
    info "Container Registry '${ACR_NAME}' already exists."
fi

ACR_LOGIN_SERVER=$(az acr show --name "${ACR_NAME}" --resource-group "${RESOURCE_GROUP}" --query loginServer -o tsv)
ACR_ADMIN_PASSWORD=$(az acr credential show --name "${ACR_NAME}" --resource-group "${RESOURCE_GROUP}" --query "passwords[0].value" -o tsv)

if [[ "${SKIP_BUILD}" != "true" ]]; then
    info "Building container images inside ACR via 'az acr build' (no local Docker required)..."
    
    info "Building 'mybigbro-api:${CONTAINER_IMAGE_TAG}'..."
    az acr build \
        --registry "${ACR_NAME}" \
        --image "mybigbro-api:${CONTAINER_IMAGE_TAG}" \
        --file "${REPO_ROOT}/docker/api/Dockerfile" \
        "${REPO_ROOT}" \
        --output none
    
    info "Building 'mybigbro-capture-worker:${CONTAINER_IMAGE_TAG}'..."
    az acr build \
        --registry "${ACR_NAME}" \
        --image "mybigbro-capture-worker:${CONTAINER_IMAGE_TAG}" \
        --file "${REPO_ROOT}/docker/worker/Dockerfile" \
        "${REPO_ROOT}" \
        --output none

    info "Building 'mybigbro-web-ui:${CONTAINER_IMAGE_TAG}'..."
    az acr build \
        --registry "${ACR_NAME}" \
        --image "mybigbro-web-ui:${CONTAINER_IMAGE_TAG}" \
        --file "${REPO_ROOT}/docker/web-ui/Dockerfile" \
        "${REPO_ROOT}" \
        --output none
    success "All 3 container images built and published to ACR."
else
    info "Skipping container image builds (SKIP_BUILD=true)."
fi

# ==============================================================================
# Step 5: Azure Container Apps Environment & Log Analytics
# ==============================================================================
info "Step 5/7: Setting up Log Analytics and Container Apps Environment..."
if ! az monitor log-analytics workspace show --resource-group "${RESOURCE_GROUP}" --workspace-name "${LOG_WORKSPACE}" &>/dev/null; then
    az monitor log-analytics workspace create \
        --resource-group "${RESOURCE_GROUP}" \
        --workspace-name "${LOG_WORKSPACE}" \
        --location "${LOCATION}" \
        --output none
    success "Log Analytics Workspace created."
fi

LOG_WORKSPACE_ID=$(az monitor log-analytics workspace show \
    --resource-group "${RESOURCE_GROUP}" \
    --workspace-name "${LOG_WORKSPACE}" \
    --query customerId -o tsv)

LOG_WORKSPACE_KEY=$(az monitor log-analytics workspace get-shared-keys \
    --resource-group "${RESOURCE_GROUP}" \
    --workspace-name "${LOG_WORKSPACE}" \
    --query primarySharedKey -o tsv)

if ! az containerapp env show --name "${ACA_ENV}" --resource-group "${RESOURCE_GROUP}" &>/dev/null; then
    info "Creating Container Apps Environment '${ACA_ENV}'..."
    az containerapp env create \
        --name "${ACA_ENV}" \
        --resource-group "${RESOURCE_GROUP}" \
        --location "${LOCATION}" \
        --logs-workspace-id "${LOG_WORKSPACE_ID}" \
        --logs-workspace-key "${LOG_WORKSPACE_KEY}" \
        --output none
    success "Container Apps Environment '${ACA_ENV}' created."
else
    info "Container Apps Environment '${ACA_ENV}' already exists."
fi

# ==============================================================================
# Step 6: Deploy Container Apps (api-service, capture-worker, web-ui)
# ==============================================================================
info "Step 6/7: Deploying microservices to Container Apps..."

PUBLIC_BLOB_URL="https://${STORAGE_ACCOUNT}.blob.core.windows.net"

# --- 1. API Service (.NET 10 Web API) ---
info "Deploying 'api-service' (internal ingress on port 8080)..."
az containerapp create \
    --name api-service \
    --resource-group "${RESOURCE_GROUP}" \
    --environment "${ACA_ENV}" \
    --image "${ACR_LOGIN_SERVER}/mybigbro-api:${CONTAINER_IMAGE_TAG}" \
    --registry-server "${ACR_LOGIN_SERVER}" \
    --registry-username "${ACR_NAME}" \
    --registry-password "${ACR_ADMIN_PASSWORD}" \
    --ingress internal \
    --target-port 8080 \
    --transport auto \
    --cpu 0.25 \
    --memory 0.5Gi \
    --min-replicas 1 \
    --max-replicas 3 \
    --secrets \
        db-conn-str="${DB_CONNECTION_STRING}" \
        storage-conn-str="${STORAGE_CONNECTION_STRING}" \
    --env-vars \
        ASPNETCORE_ENVIRONMENT=Production \
        CaptureWorker__UseAsyncQueue=true \
        Storage__Provider=Azure \
        Storage__AzureContainer=images \
        Storage__PublicRootUrl="${PUBLIC_BLOB_URL}" \
        ConnectionStrings__MyBigBroPostgres=secretref:db-conn-str \
        Storage__AzureConnectionString=secretref:storage-conn-str \
    --output none
success "Container App 'api-service' deployed."

# Enable HTTP internally on api-service and fetch its internal FQDN
az containerapp ingress update \
    --name api-service \
    --resource-group "${RESOURCE_GROUP}" \
    --allow-insecure true \
    --output none

API_INTERNAL_FQDN=$(az containerapp show \
    --name api-service \
    --resource-group "${RESOURCE_GROUP}" \
    --query properties.configuration.ingress.fqdn -o tsv)

# --- 2. Capture Worker (.NET 10 Background Microservice) ---
info "Deploying 'capture-worker' (background worker, no ingress)..."
az containerapp create \
    --name capture-worker \
    --resource-group "${RESOURCE_GROUP}" \
    --environment "${ACA_ENV}" \
    --image "${ACR_LOGIN_SERVER}/mybigbro-capture-worker:${CONTAINER_IMAGE_TAG}" \
    --registry-server "${ACR_LOGIN_SERVER}" \
    --registry-username "${ACR_NAME}" \
    --registry-password "${ACR_ADMIN_PASSWORD}" \
    --cpu 0.25 \
    --memory 0.5Gi \
    --min-replicas 1 \
    --max-replicas 1 \
    --secrets \
        db-conn-str="${DB_CONNECTION_STRING}" \
        storage-conn-str="${STORAGE_CONNECTION_STRING}" \
    --env-vars \
        DOTNET_ENVIRONMENT=Production \
        Storage__Provider=Azure \
        Storage__AzureContainer=images \
        Storage__PublicRootUrl="${PUBLIC_BLOB_URL}" \
        ConnectionStrings__MyBigBroPostgres=secretref:db-conn-str \
        Storage__AzureConnectionString=secretref:storage-conn-str \
    --output none
success "Container App 'capture-worker' deployed."

# --- 3. Web UI (Nginx + AngularJS Single Page App) ---
info "Deploying 'web-ui' (external ingress on port 80)..."
az containerapp create \
    --name web-ui \
    --resource-group "${RESOURCE_GROUP}" \
    --environment "${ACA_ENV}" \
    --image "${ACR_LOGIN_SERVER}/mybigbro-web-ui:${CONTAINER_IMAGE_TAG}" \
    --registry-server "${ACR_LOGIN_SERVER}" \
    --registry-username "${ACR_NAME}" \
    --registry-password "${ACR_ADMIN_PASSWORD}" \
    --ingress external \
    --target-port 80 \
    --transport auto \
    --cpu 0.25 \
    --memory 0.5Gi \
    --min-replicas 1 \
    --max-replicas 3 \
    --env-vars \
        API_UPSTREAM="${API_INTERNAL_FQDN}" \
    --output none
success "Container App 'web-ui' deployed."

# ==============================================================================
# Step 7: Output & Verification
# ==============================================================================
WEB_UI_FQDN=$(az containerapp show \
    --name web-ui \
    --resource-group "${RESOURCE_GROUP}" \
    --query properties.configuration.ingress.fqdn -o tsv)

CREDENTIALS_FILE="${SCRIPT_DIR}/azure-credentials.env"
cat <<EOF > "${CREDENTIALS_FILE}"
# Generated MyBigBro Azure Deployment Secrets
# Generated at: $(date -u +"%Y-%m-%dT%H:%M:%SZ")

AZURE_SUBSCRIPTION_ID="${SUBSCRIPTION_ID}"
AZURE_RESOURCE_GROUP="${RESOURCE_GROUP}"
AZURE_LOCATION="${LOCATION}"

AZURE_STORAGE_ACCOUNT="${STORAGE_ACCOUNT}"
AZURE_STORAGE_CONNECTION_STRING="${STORAGE_CONNECTION_STRING}"

AZURE_POSTGRES_SERVER="${POSTGRES_SERVER}"
AZURE_POSTGRES_USER="${POSTGRES_USER}"
AZURE_POSTGRES_PASSWORD="${POSTGRES_PASSWORD}"
AZURE_POSTGRES_DB="${POSTGRES_DB}"
AZURE_POSTGRES_CONNECTION_STRING="${DB_CONNECTION_STRING}"

AZURE_ACR_NAME="${ACR_NAME}"
AZURE_ACR_SERVER="${ACR_LOGIN_SERVER}"

AZURE_ACA_ENV="${ACA_ENV}"
AZURE_WEB_UI_URL="https://${WEB_UI_FQDN}"
EOF
chmod 600 "${CREDENTIALS_FILE}"

echo ""
echo -e "${GREEN}${BOLD}=================================================================="
echo "    DEPLOYMENT COMPLETE!"
echo -e "==================================================================${RESET}"
echo ""
echo -e "  ${BOLD}Web UI Map URL:${RESET}         ${CYAN}https://${WEB_UI_FQDN}${RESET}"
echo -e "  ${BOLD}API Health Check:${RESET}       ${CYAN}https://${WEB_UI_FQDN}/healthz${RESET}"
echo -e "  ${BOLD}WebCams REST API:${RESET}       ${CYAN}https://${WEB_UI_FQDN}/api/webcams${RESET}"
echo -e "  ${BOLD}Captured Images API:${RESET}    ${CYAN}https://${WEB_UI_FQDN}/api/geomarkers/capturedimages?top=10${RESET}"
echo ""
echo -e "  ${BOLD}Credentials Saved To:${RESET}   ${CREDENTIALS_FILE} (chmod 600, gitignored)"
echo ""
echo -e "${BOLD}Quick Smoke Test Commands:${RESET}"
echo "------------------------------------------------------------------"
echo "# 1. Test API Health Endpoint:"
echo "curl -k -i https://${WEB_UI_FQDN}/healthz"
echo ""
echo "# 2. Query 105 Seeded PostGIS Webcams:"
echo "curl -s https://${WEB_UI_FQDN}/api/webcams | jq '.[0:3]'"
echo ""
echo "# 3. Simulate a GPS device moving into camera range:"
echo "curl -X POST https://${WEB_UI_FQDN}/api/geomarkers \\"
echo "  -H 'Content-Type: application/json' \\"
echo "  -d '{\"MarkerDateTime\":\"$(date -u +"%Y-%m-%dT%H:%M:%SZ")\",\"XCoord\":174.746,\"YCoord\":-36.869,\"DeviceName\":\"azure-cloud-device\"}'"
echo "------------------------------------------------------------------"
