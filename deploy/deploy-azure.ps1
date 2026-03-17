param(
    [Parameter(Mandatory = $false)]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $false)]
    [string]$Location,

    [Parameter(Mandatory = $false)]
    [string]$WebAppName,

    [Parameter(Mandatory = $false)]
    [string]$AppServicePlanName,

    [Parameter(Mandatory = $false)]
    [string]$SqlServerName,

    [Parameter(Mandatory = $false)]
    [string]$SqlDatabaseName,

    [Parameter(Mandatory = $false)]
    [string]$SqlAdminUser,

    [Parameter(Mandatory = $false)]
    [string]$SqlAdminPassword,

    [Parameter(Mandatory = $false)]
    [string]$SurveyLinkSecretKey = "",

    [Parameter(Mandatory = $false)]
    [int]$SurveyLinkExpiryHours = 72,

    [Parameter(Mandatory = $false)]
    [string]$SurveySupportEmail = "",

    [Parameter(Mandatory = $false)]
    [string]$ConfigPath = (Join-Path $PSScriptRoot "deploy-azure.settings.json"),

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

$config = $null
if (-not [string]::IsNullOrWhiteSpace($ConfigPath) -and (Test-Path $ConfigPath)) {
    Write-Host "Loading deployment settings from $ConfigPath"
    $config = Get-Content -Path $ConfigPath -Raw | ConvertFrom-Json
}

function Resolve-Setting {
    param(
        [string]$CliValue,
        $ConfigValue,
        [string]$DefaultValue = ""
    )

    if (-not [string]::IsNullOrWhiteSpace($CliValue)) {
        return $CliValue
    }

    if ($null -ne $ConfigValue -and -not [string]::IsNullOrWhiteSpace([string]$ConfigValue)) {
        return [string]$ConfigValue
    }

    return $DefaultValue
}

$ResourceGroup = Resolve-Setting -CliValue $ResourceGroup -ConfigValue $config.ResourceGroup
$Location = Resolve-Setting -CliValue $Location -ConfigValue $config.Location
$WebAppName = Resolve-Setting -CliValue $WebAppName -ConfigValue $config.WebAppName
$AppServicePlanName = Resolve-Setting -CliValue $AppServicePlanName -ConfigValue $config.AppServicePlanName
$SqlServerName = Resolve-Setting -CliValue $SqlServerName -ConfigValue $config.SqlServerName
$SqlDatabaseName = Resolve-Setting -CliValue $SqlDatabaseName -ConfigValue $config.SqlDatabaseName
$SqlAdminUser = Resolve-Setting -CliValue $SqlAdminUser -ConfigValue $config.SqlAdminUser
$SqlAdminPassword = Resolve-Setting -CliValue $SqlAdminPassword -ConfigValue $config.SqlAdminPassword -DefaultValue $env:AZURE_SQL_ADMIN_PASSWORD
$SurveyLinkSecretKey = Resolve-Setting -CliValue $SurveyLinkSecretKey -ConfigValue $config.SurveyLinkSecretKey
$SurveySupportEmail = Resolve-Setting -CliValue $SurveySupportEmail -ConfigValue $config.SurveySupportEmail

if ($SurveyLinkExpiryHours -eq 72 -and $null -ne $config.SurveyLinkExpiryHours) {
    $SurveyLinkExpiryHours = [int]$config.SurveyLinkExpiryHours
}

$requiredValues = @{
    ResourceGroup = $ResourceGroup
    Location = $Location
    WebAppName = $WebAppName
    AppServicePlanName = $AppServicePlanName
    SqlServerName = $SqlServerName
    SqlDatabaseName = $SqlDatabaseName
    SqlAdminUser = $SqlAdminUser
    SqlAdminPassword = $SqlAdminPassword
}

$missing = @()
foreach ($key in $requiredValues.Keys) {
    if ([string]::IsNullOrWhiteSpace($requiredValues[$key])) {
        $missing += $key
    }
}

if ($missing.Count -gt 0) {
    throw "Missing required deployment settings: $($missing -join ', '). Provide them via script parameters, $ConfigPath, or AZURE_SQL_ADMIN_PASSWORD for SqlAdminPassword."
}

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
$surveyBaseUrl = "https://$WebAppName.azurewebsites.net"

$appSettings = @(
    "ASPNETCORE_ENVIRONMENT=Production",
    "ASPNETCORE_HTTPS_PORT=443",
    "SurveyLinkSettings__BaseUrl=$surveyBaseUrl",
    "SurveyLinkSettings__ExpiryHours=$SurveyLinkExpiryHours"
)

if (-not [string]::IsNullOrWhiteSpace($SurveySupportEmail)) {
    $appSettings += "SurveyLinkSettings__SupportEmail=$SurveySupportEmail"
}

if (-not [string]::IsNullOrWhiteSpace($SurveyLinkSecretKey)) {
    $appSettings += "SurveyLinkSettings__SecretKey=$SurveyLinkSecretKey"
} else {
    Write-Warning "SurveyLinkSecretKey was not provided. Existing Azure setting will be retained."
}

az webapp config appsettings set `
    --resource-group $ResourceGroup `
    --name $WebAppName `
    --settings $appSettings | Out-Null

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
