#!/usr/bin/env bash
# ==============================================================================
# MyBigBro - Azure Teardown Script
# Deletes the Azure Resource Group and all associated resources.
# ==============================================================================

set -euo pipefail

BOLD="\033[1m"
YELLOW="\033[1;33m"
RED="\033[0;31m"
GREEN="\033[0;32m"
CYAN="\033[0;36m"
RESET="\033[0m"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Load .env or azure-credentials.env if present
if [[ -f "${SCRIPT_DIR}/azure-credentials.env" ]]; then
    # shellcheck disable=SC1091
    source "${SCRIPT_DIR}/azure-credentials.env"
elif [[ -f "${SCRIPT_DIR}/.env" ]]; then
    # shellcheck disable=SC1091
    source "${SCRIPT_DIR}/.env"
fi

RESOURCE_GROUP="${AZURE_RESOURCE_GROUP:-rg-mybigbro}"

echo -e "${RED}${BOLD}"
echo "=================================================================="
echo "    WARNING: MyBigBro Azure Teardown"
echo "=================================================================="
echo -e "${RESET}"
echo -e "This will ${RED}${BOLD}PERMANENTLY DELETE${RESET} the Resource Group: ${BOLD}${RESOURCE_GROUP}${RESET}"
echo "This includes:"
echo "  - PostgreSQL Flexible Server & all database data"
echo "  - Azure Container Apps (api-service, capture-worker, web-ui)"
echo "  - Azure Container Registry & container images"
echo "  - Azure Blob Storage & all captured CCTV images"
echo "  - Log Analytics workspace"
echo ""

read -rp "Are you sure you want to proceed? Type 'yes' to confirm: " CONFIRM

if [[ "${CONFIRM}" != "yes" ]]; then
    echo -e "${GREEN}Teardown cancelled.${RESET}"
    exit 0
fi

echo -e "${YELLOW}Deleting Resource Group '${RESOURCE_GROUP}' in the background...${RESET}"
az group delete --name "${RESOURCE_GROUP}" --yes --no-wait

# Clean up local cache files
rm -f "${SCRIPT_DIR}/.suffix" "${SCRIPT_DIR}/.db_password" "${SCRIPT_DIR}/azure-credentials.env"

echo -e "${GREEN}${BOLD}Deletion initiated successfully.${RESET}"
echo "Azure is deleting the resources asynchronously in the background."
