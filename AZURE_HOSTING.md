# Azure Hosting Guide (App Service + Azure SQL)

This guide deploys the app to your Azure account and connects it to Azure SQL.

## 1) Prerequisites

- Azure subscription
- Azure CLI installed (`az --version`)
- .NET 9 SDK installed (`dotnet --version`)
- Logged into Azure CLI:

```powershell
az login
az account set --subscription "<YOUR_SUBSCRIPTION_ID_OR_NAME>"
```

## 2) Run the deployment script

From the workspace root:

> Important: Do not commit publish artifacts (for example `deploy/publish` or zip outputs). The repository is configured to ignore these build outputs.

```powershell
Set-Location .\deploy
.\deploy-azure.ps1 `
  -ResourceGroup "rg-tin200-prod" `
  -Location "eastus" `
  -WebAppName "tin200-app-prod-001" `
  -AppServicePlanName "asp-tin200-prod" `
  -SqlServerName "tin200sqlprod001" `
  -SqlDatabaseName "tin200db" `
  -SqlAdminUser "tinadmin" `
  -SqlAdminPassword "<STRONG_PASSWORD>" `
  -SurveyLinkSecretKey "<STABLE_STRONG_SECRET>" `
  -SurveyLinkExpiryHours 72 `
  -SurveySupportEmail "support@yourcompany.com" `
  -AllowMyIp
```

What the script does:
- Creates resource group, App Service plan, and Web App
- Creates Azure SQL logical server + database
- Configures SQL firewall rules
- Sets `ASPNETCORE_ENVIRONMENT=Production`
- Sets `ASPNETCORE_HTTPS_PORT=443`
- Sets `SurveyLinkSettings__BaseUrl=https://<webapp-name>.azurewebsites.net`
- Sets `SurveyLinkSettings__ExpiryHours` and optional `SurveyLinkSettings__SupportEmail`
- Sets `SurveyLinkSettings__SecretKey` when provided (recommended)
- Sets `DefaultConnection` connection string in App Service
- Publishes and deploys the app

Important for survey links:
- `SurveyLinkSettings__SecretKey` must be stable and non-empty in Azure.
- If it changes, previously sent links become invalid.

Note: Azure App Service terminates TLS at the front end. Setting `ASPNETCORE_HTTPS_PORT=443` ensures ASP.NET Core generates HTTPS redirects and URLs with the expected public port.

## 3) Create/import database schema and data

Your app expects the `TIN200` table and existing mapped column names.

Use one of these options:
- **Best for existing data:** Export/import a BACPAC from your current SQL Server into Azure SQL.
- **Script-based:** Run your table creation/data scripts in Azure SQL Query Editor.

After import, verify the table exists:

```sql
SELECT TOP 10 * FROM TIN200;
```

## 4) Verify app in Azure

- App URL: `https://<webapp-name>.azurewebsites.net`
- Health endpoint: `https://<webapp-name>.azurewebsites.net/health`

If health is OK, open `/Tin200/Index` and confirm records load.

## 5) Recommended production hardening

- Move SQL admin password out of scripts and use secure secret handling.
- Add custom domain + TLS certificate.
- Enable App Service diagnostics and Application Insights.
- Restrict SQL firewall to only required addresses/services.

## 6) Optional: Managed Identity for SQL (passwordless)

You can switch from SQL user/password to managed identity later:
- Enable system-assigned managed identity on Web App
- Create contained user in Azure SQL for that identity
- Grant least-privilege roles
- Replace connection string with AAD-based authentication

---

If you want, I can also add a GitHub Actions workflow to auto-deploy to Azure on each push.

## 7) Optional: GitHub Actions auto-deploy

This repository now includes:
- [Build and Deploy workflow](.github/workflows/azure-webapp-deploy.yml)

It runs on pushes to `main` (and manual trigger via Actions tab), then builds and deploys to Azure Web App.

Configure these in GitHub:

- **Repository Variable**
  - `AZURE_WEBAPP_NAME` = your Azure Web App name (e.g. `tin200-app-prod-001`)

- **Repository Secret (choose one auth option)**
  - `AZURE_WEBAPP_PUBLISH_PROFILE` = publish profile XML from Azure Portal
  - or `AZURE_CREDENTIALS` = Azure service principal JSON for `azure/login`

How to get publish profile:
1. Azure Portal → your Web App
2. Overview → **Get publish profile**
3. Copy the file contents into the GitHub secret

How to get AZURE_CREDENTIALS JSON (optional alternative):
1. Create a service principal with access to your resource group
2. Capture JSON in this format: `{"clientId":"...","clientSecret":"...","subscriptionId":"...","tenantId":"..."}`
3. Save it as `AZURE_CREDENTIALS` GitHub secret

After that, pushing to `main` will deploy automatically.
