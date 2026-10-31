# Azure Fully Managed PaaS Deployment for MyBigBro

This directory contains the automated deployment scripts and Infrastructure-as-Code (IaC) to deploy **MyBigBro** to **Azure Fully Managed PaaS** using:

1. **Azure Database for PostgreSQL - Flexible Server** (`Standard_B1ms` Burstable Tier with PostGIS 3.5 spatial extension)
2. **Azure Container Apps (ACA)**:
   - `api-service`: Internal ingress on port 8080 (.NET 10 ASP.NET Core Web API)
   - `capture-worker`: Background worker service (.NET 10)
   - `web-ui`: External public ingress on port 80 (Nginx Alpine + AngularJS OpenStreetMap UI)
3. **Azure Blob Storage**: Public `images` container for captured CCTV snapshot frames
4. **Azure Container Registry (ACR)**: Basic tier for hosting container images built via `az acr build`
5. **Azure Log Analytics Workspace**: Centralized logging and monitoring for Container Apps

---

## Architecture Diagram

```
                 Internet (HTTP/S)
                        │
                        ▼
       ┌─────────────────────────────────┐
       │      Azure Container Apps       │
       │                                 │
       │   ┌─────────────────────────┐   │
       │   │         web-ui          │   │  (External Ingress :80)
       │   │ (Nginx + AngularJS Map) │   │
       │   └────────────┬────────────┘   │
       │                │                │
       │        (Internal Ingress)       │
       │                │                │
       │                ▼                │
       │   ┌─────────────────────────┐   │
       │   │       api-service       │   │  (Internal Ingress :8080)
       │   │     (.NET 10 API)       │   │
       │   └────────────┬────────────┘   │
       │                │                │
       │                │                │
       │   ┌────────────┴────────────┐   │
       │   │     capture-worker      │   │  (Continuous Background Microservice)
       │   │    (.NET 10 Worker)     │   │
       │   └────────────┬────────────┘   │
       └────────────────┼────────────────┘
                        │
       ┌────────────────┴────────────────────────┐
       │                                         │
       ▼                                         ▼
┌──────────────────────────────┐  ┌──────────────────────────────┐
│ Azure PostgreSQL Flex Server │  │      Azure Blob Storage      │
│  - Burstable Standard_B1ms   │  │  - Container: images         │
│  - PostGIS 3.5 Extension     │  │  - Public Read for JPEGs     │
│  - 105 Seeded NZTA Webcams   │  │  - Low Latency Cloud Store   │
└──────────────────────────────┘  └──────────────────────────────┘
```

---

## Estimated Cost

| Service | SKU / Sizing | Monthly Cost (Pay-As-You-Go) | First 12 Months Free Tier? |
| :--- | :--- | :--- | :--- |
| **Azure PostgreSQL Flexible Server** | `Standard_B1ms` (1 vCPU, 2 GB RAM, 32 GB SSD) | ~$15 – $18 / mo | **100% Free** (750 hours/mo for 12 mos) |
| **Azure Container Apps** | 3 microservices (0.25 vCPU, 0.5 GiB RAM each) | ~$10 – $15 / mo | Includes 180k vCPU-s & 360k GiB-s free |
| **Azure Blob Storage** | Standard LRS (Hot/Cool) | ~$0.50 – $1.00 / mo | 5 GB free for 12 mos |
| **Azure Container Registry** | Basic SKU | ~$5.00 / mo | No |
| **Log Analytics** | Ingestion & 30-day retention | ~$0.50 – $1.00 / mo | 5 GB free/mo |
| **Total Estimated Cost** | | **~$30 – $35 / mo** | **~$15 / mo** *(during 1st year)* |

---

## Prerequisites

1. **Azure CLI (`az`)**: Install via [Azure CLI Installation Guide](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli).
2. **Logged In**: Authenticate using:
   ```bash
   az login
   ```
3. **Subscription Selection** (if you have multiple subscriptions):
   ```bash
   az account set --subscription "<Your-Subscription-Name-or-ID>"
   ```

---

## Quick Start: One-Command Deployment

From the root of the repository, execute:

```bash
./deploy/deploy-azure.sh
```

### What the Script Automates:

1. **Prerequisite & Provider Registration**: Automatically installs the `containerapp` CLI extension and registers `Microsoft.App`, `Microsoft.DBforPostgreSQL`, and `Microsoft.Storage`.
2. **Resource Group & Storage Discovery**: Discovers and reuses existing `rg-mybigbro` and Azure Storage Account (`stbigbro<suffix>`) if available; otherwise provisions them and ensures both `images` (public CCTV images) and `database` (rehydration SQL scripts) containers exist.
3. **PostgreSQL Flexible Server & Rehydration**:
   - Provisions a `Standard_B1ms` server with PostGIS 3.5 enabled (`azure.extensions = 'POSTGIS'`).
   - Checks for persisted database backup in the Storage Account (`database/database-backup.sql`). If present, downloads and executes it to rehydrate the entire database back to its pre-teardown state (all webcams, markers, captured images, devices, and jobs).
   - If no backup exists (first run), executes initial schema (`01-init-schema.sql`), legacy seeds (`02-seed-webcams.sql`), and 310 refreshed NZTA cameras (`03-seed-webcams-refreshed.sql`).
4. **Cloud Container Builds (ACR)**:
   - Creates an Azure Container Registry (`Basic`).
   - Runs `az acr build` to build and tag the 3 multi-stage Linux containers (`api-service`, `capture-worker`, `web-ui`) directly in the cloud (no local Docker required).
5. **Azure Container Apps Deployment**:
   - Deploys `api-service` with internal ingress on port `8080`.
   - Deploys `capture-worker` as a background worker with database & blob secrets.
   - Deploys `web-ui` with external ingress on port `80`, reverse-proxying `/api/*` and `/healthz` directly to `api-service`.
6. **Credential Security**:
   - Saves all connection strings and passwords to `deploy/azure-credentials.env` (`chmod 600`, ignored by git).

---

## Customizing Configurations

To override default resource names, locations, or database passwords, copy the template:

```bash
cp deploy/env.template deploy/.env
```

Edit `deploy/.env`:
```bash
AZURE_LOCATION="newzealandnorth"    # Auckland (default), or australiaeast, eastus, etc.
AZURE_RESOURCE_GROUP="rg-mybigbro"
CONTAINER_IMAGE_TAG="v1.0.0"
SKIP_BUILD="false"                 # Set to true if images are already in ACR
SKIP_DB_INIT="false"              # Set to true if DB is already seeded
```

Then rerun `./deploy/deploy-azure.sh`.

---

## Verifying & Testing the Deployment

Once deployment completes, the script outputs the public URL for your web app (e.g. `https://web-ui.<unique-id>.<region>.azurecontainerapps.io`).

### 1. Test Web API Health Check
```bash
curl -i https://<your-web-ui-fqdn>/healthz
```
*Expected Response:* `HTTP/1.1 200 OK` with `Healthy`.

### 2. Query Seeded NZTA Webcams
```bash
curl -s https://<your-web-ui-fqdn>/api/webcams | jq '.[0:3]'
```
*Expected Response:* JSON array of traffic cameras with PostGIS coordinates.

### 3. Record a GPS Marker & Trigger CCTV Capture
Simulate a GPS device near an Auckland traffic camera:
```bash
curl -X POST https://<your-web-ui-fqdn>/api/geomarkers \
  -H "Content-Type: application/json" \
  -d '{"MarkerDateTime":"2026-10-29T12:00:00Z","XCoord":174.746,"YCoord":-36.869,"DeviceName":"azure-paas-device"}'
```

Query captured images for that device:
```bash
curl -s "https://<your-web-ui-fqdn>/api/geomarkers/capturedimages?top=5&deviceName=azure-paas-device"
```

### 4. Northwestern Motorway (SH16) Drive Simulator
Simulate an Android device (`android-test`) driving along the Northwestern Motorway (from Auckland CBD to Westgate) and triggering all 7 cameras:
```bash
# Run against the live Azure deployment
python3 deploy/simulate-drive.py

# Custom options:
python3 deploy/simulate-drive.py --device my-phone       # Custom device name
python3 deploy/simulate-drive.py --cameras-only          # Ping only camera trigger zones
python3 deploy/simulate-drive.py --eastbound             # Drive Eastbound (Westgate -> City)
python3 deploy/simulate-drive.py --delay 0.5             # Adjust pacing (seconds)
```

### 5. View the Map UI in Your Browser
Open `https://<your-web-ui-fqdn>` in your browser. The OpenStreetMap interface will load the webcams and plot captured photos as markers move.
To filter directly for `android-test`:
`https://<your-web-ui-fqdn>/?devicename=android-test`

---

## Alternative: Declarative Bicep Deployment

If you prefer Infrastructure-as-Code via Azure Bicep:

```bash
# 1. Create Resource Group in New Zealand North
az group create --name rg-mybigbro --location newzealandnorth

# 2. Deploy Bicep template
az deployment group create \
  --resource-group rg-mybigbro \
  --template-file deploy/bicep/main.bicep \
  --parameters postgresAdminPassword="YourStrongPassword123#!"
```

---

## Cost-Saving Teardown & Database Persistence

To delete billable compute and database resources while **preserving your database data and captured CCTV photos**:

```bash
./deploy/destroy-azure.sh
```

### What `destroy-azure.sh` does:
1. **Dumps Database Contents**: Connects to the PostgreSQL Flexible Server and dumps the complete schema, tables, PostGIS geometries, markers, webcams, and captured images into a SQL rehydration script.
2. **Uploads to Storage Account**: Saves the script as `database-backup.sql` inside the `database` container of your persistent Azure Storage Account (`stbigbro<suffix>`).
3. **Selectively Tears Down Billable Services**:
   - Container Apps (`web-ui`, `api-service`, `capture-worker`)
   - Container Apps Environment
   - PostgreSQL Flexible Server
   - Azure Container Registry
   - Log Analytics Workspace
4. **Preserves Storage Account & Resource Group**: Keeps `rg-mybigbro` and `stbigbro<suffix>` active, reducing your ongoing Azure costs to storage only (**~$0.50 – $1.00 / month**).

### Re-Deploying & Rehydrating:
When you want to spin everything back up:
```bash
./deploy/deploy-azure.sh
```
`deploy-azure.sh` automatically detects the existing Storage Account in `rg-mybigbro`, reuses it, and detects `database/database-backup.sql` to fully rehydrate the PostgreSQL database back to its exact state prior to teardown.
