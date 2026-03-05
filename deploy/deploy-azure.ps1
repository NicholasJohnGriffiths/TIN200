param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string]$Location,

    [Parameter(Mandatory = $true)]
    [string]$WebAppName,

    [Parameter(Mandatory = $true)]
    [string]$AppServicePlanName,

    [Parameter(Mandatory = $true)]
    [string]$SqlServerName,

    [Parameter(Mandatory = $true)]
    [string]$SqlDatabaseName,

    [Parameter(Mandatory = $true)]
    [string]$SqlAdminUser,

    [Parameter(Mandatory = $true)]
    [string]$SqlAdminPassword,

    [switch]$AllowMyIp
)

$ErrorActionPreference = "Stop"

$azureCliWbin = "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin"
if (-not (Get-Command az -ErrorAction SilentlyContinue) -and (Test-Path (Join-Path $azureCliWbin "az.cmd"))) {
    $env:Path = "$azureCliWbin;$env:Path"
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI not found. Install Azure CLI or add az to PATH."
}

$scriptRoot = $PSScriptRoot
$projectRoot = Split-Path -Parent $scriptRoot
$publishDir = Join-Path $scriptRoot "publish"
$publishZip = Join-Path $scriptRoot "publish.zip"

Write-Host "Checking Azure CLI login..."
az account show | Out-Null

Write-Host "Creating resource group..."
az group create --name $ResourceGroup --location $Location | Out-Null

Write-Host "Creating Azure SQL logical server..."
az sql server create `
    --name $SqlServerName `
    --resource-group $ResourceGroup `
    --location $Location `
    --admin-user $SqlAdminUser `
    --admin-password $SqlAdminPassword | Out-Null

Write-Host "Creating Azure SQL database..."
az sql db create `
    --resource-group $ResourceGroup `
    --server $SqlServerName `
    --name $SqlDatabaseName `
    --service-objective Basic | Out-Null

Write-Host "Allowing Azure services to access SQL server..."
az sql server firewall-rule create `
    --resource-group $ResourceGroup `
    --server $SqlServerName `
    --name AllowAzureServices `
    --start-ip-address 0.0.0.0 `
    --end-ip-address 0.0.0.0 | Out-Null

if ($AllowMyIp) {
    $myIp = (Invoke-RestMethod -Uri "https://api.ipify.org")
    Write-Host "Allowing your current IP $myIp on SQL firewall..."
    az sql server firewall-rule create `
        --resource-group $ResourceGroup `
        --server $SqlServerName `
        --name AllowMyCurrentIp `
        --start-ip-address $myIp `
        --end-ip-address $myIp | Out-Null
}

Write-Host "Creating App Service plan..."
az appservice plan create `
    --name $AppServicePlanName `
    --resource-group $ResourceGroup `
    --location $Location `
    --sku B1 | Out-Null

Write-Host "Creating Web App..."
az webapp create `
    --name $WebAppName `
    --resource-group $ResourceGroup `
    --plan $AppServicePlanName `
    --runtime "dotnet:9" | Out-Null

$connectionString = "Server=tcp:$SqlServerName.database.windows.net,1433;Initial Catalog=$SqlDatabaseName;Persist Security Info=False;User ID=$SqlAdminUser;Password=$SqlAdminPassword;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

Write-Host "Configuring app settings and SQL connection string..."
az webapp config appsettings set `
    --resource-group $ResourceGroup `
    --name $WebAppName `
    --settings ASPNETCORE_ENVIRONMENT=Production | Out-Null

az webapp config connection-string set `
    --resource-group $ResourceGroup `
    --name $WebAppName `
    --connection-string-type SQLAzure `
    --settings DefaultConnection="$connectionString" | Out-Null

Write-Host "Publishing application..."
dotnet publish (Join-Path $projectRoot "TINWeb.csproj") -c Release -o $publishDir

if (Test-Path $publishZip) {
    Remove-Item $publishZip -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $publishZip -Force

Write-Host "Deploying package to Azure Web App..."
az webapp deploy `
    --resource-group $ResourceGroup `
    --name $WebAppName `
    --src-path $publishZip `
    --type zip | Out-Null

$webUrl = "https://$WebAppName.azurewebsites.net"
Write-Host "Deployment complete."
Write-Host "App URL: $webUrl"
Write-Host "Health URL: $webUrl/health"
Write-Host "Next: import/create TIN200 schema and data in Azure SQL database if not already present."
