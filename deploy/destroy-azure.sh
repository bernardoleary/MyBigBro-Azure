#!/usr/bin/env bash
# ==============================================================================
# MyBigBro - Azure Cost-Saving Teardown & Database Persistence Script
#
# Actions:
#   1. Connects to PostgreSQL Flexible Server and dumps the database contents
#      to a self-contained SQL rehydration script.
#   2. Uploads the SQL script to the persistent Azure Storage Account ('database' container).
#   3. Deletes billable compute/database resources:
#      - Azure Container Apps (api-service, capture-worker, web-ui)
#      - Azure Container Apps Managed Environment
#      - Azure Database for PostgreSQL Flexible Server
#      - Azure Container Registry
#      - Log Analytics Workspace
#   4. PRESERVES the Resource Group and Azure Storage Account (with all captured
#      CCTV images in 'images' container and the DB backup in 'database' container),
#      reducing your ongoing Azure costs to storage-only (~$0.50 - $1.00/mo).
# ==============================================================================

set -euo pipefail

BOLD="\033[1m"
YELLOW="\033[1;33m"
RED="\033[0;31m"
GREEN="\033[0;32m"
CYAN="\033[0;36m"
RESET="\033[0m"

info()    { echo -e "${CYAN}${BOLD}[INFO]${RESET} $*"; }
success() { echo -e "${GREEN}${BOLD}[SUCCESS]${RESET} $*"; }
warn()    { echo -e "${YELLOW}${BOLD}[WARNING]${RESET} $*"; }
error()   { echo -e "${RED}${BOLD}[ERROR]${RESET} $*" >&2; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Parse flags
CONFIRM_FLAG="false"
for arg in "$@"; do
    case "${arg}" in
        -y|--yes)
            CONFIRM_FLAG="true"
            ;;
    esac
done

# Load configurations from azure-credentials.env or .env if present
if [[ -f "${SCRIPT_DIR}/azure-credentials.env" ]]; then
    # shellcheck disable=SC1091
    source "${SCRIPT_DIR}/azure-credentials.env"
fi
if [[ -f "${SCRIPT_DIR}/.env" ]]; then
    # shellcheck disable=SC1091
    source "${SCRIPT_DIR}/.env"
fi

RESOURCE_GROUP="${AZURE_RESOURCE_GROUP:-rg-mybigbro}"

# Detect suffix
if [[ -z "${RANDOM_SUFFIX:-}" ]]; then
    if [[ -f "${SCRIPT_DIR}/.suffix" ]]; then
        RANDOM_SUFFIX=$(cat "${SCRIPT_DIR}/.suffix")
    elif az group exists --name "${RESOURCE_GROUP}" 2>/dev/null | grep -q true; then
        EXISTING_SA=$(az storage account list --resource-group "${RESOURCE_GROUP}" --query "[?starts_with(name, 'stbigbro')].name | [0]" -o tsv 2>/dev/null || true)
        if [[ -n "${EXISTING_SA}" && "${EXISTING_SA}" != "None" ]]; then
            RANDOM_SUFFIX="${EXISTING_SA#stbigbro}"
            echo -n "${RANDOM_SUFFIX}" > "${SCRIPT_DIR}/.suffix"
        fi
    fi
fi

RANDOM_SUFFIX="${RANDOM_SUFFIX:-}"
STORAGE_ACCOUNT="${AZURE_STORAGE_ACCOUNT:-stbigbro${RANDOM_SUFFIX}}"
POSTGRES_SERVER="${AZURE_POSTGRES_SERVER:-psql-bigbro-${RANDOM_SUFFIX}}"
POSTGRES_USER="${AZURE_POSTGRES_USER:-mybigbro}"
POSTGRES_DB="${AZURE_POSTGRES_DB:-mybigbro}"
ACR_NAME="${AZURE_ACR_NAME:-acrbigbro${RANDOM_SUFFIX}}"
LOG_WORKSPACE="${AZURE_LOG_WORKSPACE:-log-bigbro-${RANDOM_SUFFIX}}"
ACA_ENV="${AZURE_ACA_ENV:-cae-bigbro-${RANDOM_SUFFIX}}"

if [[ -z "${POSTGRES_PASSWORD:-}" ]]; then
    if [[ -f "${SCRIPT_DIR}/.db_password" ]]; then
        POSTGRES_PASSWORD=$(cat "${SCRIPT_DIR}/.db_password")
    fi
fi

# Confirmation banner
echo -e "${YELLOW}${BOLD}"
echo "=================================================================="
echo "    MyBigBro - Azure Teardown & Database Persistence"
echo "=================================================================="
echo -e "${RESET}"
echo -e "Target Resource Group:       ${BOLD}${RESOURCE_GROUP}${RESET}"
echo -e "Storage Account (PRESERVED): ${GREEN}${BOLD}${STORAGE_ACCOUNT}${RESET}"
echo -e "PostgreSQL Server (BACKUP):  ${CYAN}${BOLD}${POSTGRES_SERVER}${RESET}"
echo ""
echo -e "This operation will:"
echo -e "  1. ${GREEN}Backup${RESET} the database to ${BOLD}${STORAGE_ACCOUNT}/database/database-backup.sql${RESET}."
echo -e "  2. ${GREEN}PRESERVE${RESET} the Resource Group '${RESOURCE_GROUP}' and Storage Account '${STORAGE_ACCOUNT}'."
echo -e "  3. ${RED}DELETE${RESET} all billable compute/database resources:"
echo "       - Azure Container Apps (web-ui, capture-worker, api-service)"
echo "       - Container Apps Environment (${ACA_ENV})"
echo "       - PostgreSQL Flexible Server (${POSTGRES_SERVER})"
echo "       - Azure Container Registry (${ACR_NAME})"
echo "       - Log Analytics Workspace (${LOG_WORKSPACE})"
echo ""
echo -e "Your ongoing Azure costs will be ${GREEN}${BOLD}reduced to the Storage Account only (~\$0.50-\$1.00/mo)${RESET}."
echo ""

if [[ "${CONFIRM_FLAG}" != "true" ]]; then
    read -rp "Are you sure you want to proceed? Type 'yes' to confirm: " CONFIRM
    if [[ "${CONFIRM}" != "yes" ]]; then
        echo -e "${GREEN}Teardown cancelled.${RESET}"
        exit 0
    fi
fi

# ------------------------------------------------------------------------------
# 1. Verify Storage Account and Prepare Container
# ------------------------------------------------------------------------------
info "Verifying Storage Account '${STORAGE_ACCOUNT}' in '${RESOURCE_GROUP}'..."
if ! az storage account show --name "${STORAGE_ACCOUNT}" --resource-group "${RESOURCE_GROUP}" &>/dev/null; then
    error "Storage Account '${STORAGE_ACCOUNT}' not found in resource group '${RESOURCE_GROUP}'."
    error "Cannot persist database backup without storage account. Aborting teardown."
    exit 1
fi

STORAGE_CONNECTION_STRING=$(az storage account show-connection-string \
    --name "${STORAGE_ACCOUNT}" \
    --resource-group "${RESOURCE_GROUP}" \
    --query connectionString -o tsv)

info "Ensuring 'database' container exists in '${STORAGE_ACCOUNT}'..."
az storage container create \
    --name database \
    --account-name "${STORAGE_ACCOUNT}" \
    --connection-string "${STORAGE_CONNECTION_STRING}" \
    --output none 2>/dev/null || true

# ------------------------------------------------------------------------------
# 2. Persist Database Contents to Storage Account
# ------------------------------------------------------------------------------
if az postgres flexible-server show --name "${POSTGRES_SERVER}" --resource-group "${RESOURCE_GROUP}" &>/dev/null; then
    info "PostgreSQL server '${POSTGRES_SERVER}' is online. Initiating database backup..."

    # Temporarily whitelist deployer IP if needed
    CLIENT_IP=$(curl -s https://api.ipify.org || true)
    if [[ -n "${CLIENT_IP}" ]]; then
        info "Checking firewall rule for client IP (${CLIENT_IP})..."
        az postgres flexible-server firewall-rule create \
            --resource-group "${RESOURCE_GROUP}" \
            --server-name "${POSTGRES_SERVER}" \
            --name AllowDeployerIP \
            --start-ip-address "${CLIENT_IP}" \
            --end-ip-address "${CLIENT_IP}" \
            --output none 2>/dev/null || \
        az postgres flexible-server firewall-rule update \
            --resource-group "${RESOURCE_GROUP}" \
            --server-name "${POSTGRES_SERVER}" \
            --name AllowDeployerIP \
            --start-ip-address "${CLIENT_IP}" \
            --end-ip-address "${CLIENT_IP}" \
            --output none 2>/dev/null || true
    fi

    POSTGRES_HOST="${POSTGRES_SERVER}.postgres.database.azure.com"
    LOCAL_BACKUP_FILE="/tmp/mybigbro-backup-${RANDOM_SUFFIX}.sql"
    rm -f "${LOCAL_BACKUP_FILE}"

    info "Extracting full database dump via pg_dump..."
    if command -v pg_dump &>/dev/null; then
        PGPASSWORD="${POSTGRES_PASSWORD}" pg_dump \
            -h "${POSTGRES_HOST}" -p 5432 \
            -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" \
            --clean --if-exists --no-owner --no-privileges \
            --exclude-table-data=spatial_ref_sys > "${LOCAL_BACKUP_FILE}"
    elif command -v docker &>/dev/null; then
        docker run --rm \
            -e PGPASSWORD="${POSTGRES_PASSWORD}" \
            postgres:16-alpine \
            pg_dump -h "${POSTGRES_HOST}" -p 5432 \
            -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" \
            --clean --if-exists --no-owner --no-privileges \
            --exclude-table-data=spatial_ref_sys > "${LOCAL_BACKUP_FILE}"
    else
        error "Neither 'pg_dump' nor 'docker' is available locally to dump the database."
        exit 1
    fi

    # Sanitize psql 18 specific metadata commands for universal rehydration compatibility
    sed -i -E '/^\\(restrict|unrestrict)/d' "${LOCAL_BACKUP_FILE}" 2>/dev/null || true
    sed -i '/transaction_timeout/d' "${LOCAL_BACKUP_FILE}" 2>/dev/null || true

    BACKUP_SIZE=$(ls -lh "${LOCAL_BACKUP_FILE}" | awk '{print $5}')
    info "Database dumped successfully (${BACKUP_SIZE})."

    # Upload to storage account (both canonical database-backup.sql and timestamped copy)
    TIMESTAMP=$(date -u +"%Y%m%d%H%M%S")
    info "Uploading backup to '${STORAGE_ACCOUNT}/database/database-backup.sql'..."
    az storage blob upload \
        --account-name "${STORAGE_ACCOUNT}" \
        --container-name database \
        --name database-backup.sql \
        --file "${LOCAL_BACKUP_FILE}" \
        --connection-string "${STORAGE_CONNECTION_STRING}" \
        --overwrite \
        --output none

    az storage blob upload \
        --account-name "${STORAGE_ACCOUNT}" \
        --container-name database \
        --name "database-backup-${TIMESTAMP}.sql" \
        --file "${LOCAL_BACKUP_FILE}" \
        --connection-string "${STORAGE_CONNECTION_STRING}" \
        --overwrite \
        --output none

    rm -f "${LOCAL_BACKUP_FILE}"
    success "Database backup successfully saved in storage account."
else
    warn "PostgreSQL server '${POSTGRES_SERVER}' does not exist or was already deleted. Skipping dump."
fi

# ------------------------------------------------------------------------------
# 3. Teardown Billable Compute, Database, Registry & Telemetry Resources
# ------------------------------------------------------------------------------

# 1. Container Apps
info "Deleting Azure Container Apps..."
APPS=$(az containerapp list --resource-group "${RESOURCE_GROUP}" --query "[].name" -o tsv 2>/dev/null || true)
for app in ${APPS}; do
    info "Deleting Container App '${app}'..."
    az containerapp delete --name "${app}" --resource-group "${RESOURCE_GROUP}" --yes --output none
    success "Container App '${app}' deleted."
done

# 2. Container Apps Environment
if az containerapp env show --name "${ACA_ENV}" --resource-group "${RESOURCE_GROUP}" &>/dev/null; then
    info "Deleting Container Apps Environment '${ACA_ENV}'..."
    az containerapp env delete --name "${ACA_ENV}" --resource-group "${RESOURCE_GROUP}" --yes --output none
    success "Container Apps Environment '${ACA_ENV}' deleted."
fi

# 3. PostgreSQL Flexible Server
if az postgres flexible-server show --name "${POSTGRES_SERVER}" --resource-group "${RESOURCE_GROUP}" &>/dev/null; then
    info "Deleting PostgreSQL Flexible Server '${POSTGRES_SERVER}'..."
    az postgres flexible-server delete --name "${POSTGRES_SERVER}" --resource-group "${RESOURCE_GROUP}" --yes --output none
    success "PostgreSQL Flexible Server '${POSTGRES_SERVER}' deleted."
fi

# 4. Azure Container Registry
if az acr show --name "${ACR_NAME}" --resource-group "${RESOURCE_GROUP}" &>/dev/null; then
    info "Deleting Azure Container Registry '${ACR_NAME}'..."
    az acr delete --name "${ACR_NAME}" --resource-group "${RESOURCE_GROUP}" --yes --output none
    success "Container Registry '${ACR_NAME}' deleted."
fi

# 5. Log Analytics Workspace
if az monitor log-analytics workspace show --resource-group "${RESOURCE_GROUP}" --workspace-name "${LOG_WORKSPACE}" &>/dev/null; then
    info "Deleting Log Analytics Workspace '${LOG_WORKSPACE}'..."
    az monitor log-analytics workspace delete --resource-group "${RESOURCE_GROUP}" --workspace-name "${LOG_WORKSPACE}" --yes --force true --output none
    success "Log Analytics Workspace '${LOG_WORKSPACE}' deleted."
fi

# Update azure-credentials.env with preserved status
CREDENTIALS_FILE="${SCRIPT_DIR}/azure-credentials.env"
cat <<EOF > "${CREDENTIALS_FILE}"
# MyBigBro Azure Deployment State (Post-Teardown)
# Updated at: $(date -u +"%Y-%m-%dT%H:%M:%SZ")
# Status: Compute and Database resources destroyed. Storage Account and Database backup preserved in Azure.

AZURE_RESOURCE_GROUP="${RESOURCE_GROUP}"
AZURE_LOCATION="${AZURE_LOCATION:-newzealandnorth}"

AZURE_STORAGE_ACCOUNT="${STORAGE_ACCOUNT}"
AZURE_STORAGE_CONNECTION_STRING="${STORAGE_CONNECTION_STRING}"

AZURE_POSTGRES_SERVER="${POSTGRES_SERVER}"
AZURE_POSTGRES_USER="${POSTGRES_USER}"
AZURE_POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-}"
AZURE_POSTGRES_DB="${POSTGRES_DB}"

AZURE_ACR_NAME="${ACR_NAME}"
AZURE_ACA_ENV="${ACA_ENV}"
AZURE_LOG_WORKSPACE="${LOG_WORKSPACE}"
EOF
chmod 600 "${CREDENTIALS_FILE}"

echo ""
echo -e "${GREEN}${BOLD}=================================================================="
echo "    TEARDOWN COMPLETE: Azure Costs Reduced to Storage Only"
echo -e "==================================================================${RESET}"
echo ""
echo -e "  ${BOLD}Resource Group:${RESET}         ${GREEN}${RESOURCE_GROUP} (Preserved)${RESET}"
echo -e "  ${BOLD}Storage Account:${RESET}        ${GREEN}${STORAGE_ACCOUNT} (Preserved)${RESET}"
echo -e "  ${BOLD}Database Backup:${RESET}        ${GREEN}blob://${STORAGE_ACCOUNT}/database/database-backup.sql (Persisted)${RESET}"
echo -e "  ${BOLD}Captured Images:${RESET}        ${GREEN}blob://${STORAGE_ACCOUNT}/images (Preserved)${RESET}"
echo ""
echo -e "  ${BOLD}Deleted Resources:${RESET}"
echo -e "    - Container Apps:     ${RED}Deleted${RESET}"
echo -e "    - ACA Environment:    ${RED}Deleted${RESET}"
echo -e "    - PostgreSQL Server:  ${RED}Deleted${RESET}"
echo -e "    - Container Registry: ${RED}Deleted${RESET}"
echo -e "    - Log Analytics:      ${RED}Deleted${RESET}"
echo ""
echo -e "  To redeploy and rehydrate the entire environment at any time, run:"
echo -e "    ${CYAN}./deploy/deploy-azure.sh${RESET}"
echo ""
