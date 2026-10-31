# Names and sizes shared by the Azure scripts. The registry name must match deploy/k8s/overlays/azure.
$ResourceGroup = "cropconnect-rg"
# Azure for Students only allows a few regions; Belgium Central offers a 2 vCPU size to this subscription.
$Location = "belgiumcentral"
$Cluster = "cropconnect-aks"
$Registry = "cropconnect16641"
# 2 vCPU, 8 GB: the whole platform fits on one node; the autoscaler adds a second one under load.
$NodeSize = "Standard_D2as_v4"
$MinNodes = 1
$MaxNodes = 2
# Monthly budget in USD; Azure emails the account at 50 % and 80 % of it.
$BudgetAmount = 60

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$Overlay = Join-Path $RepoRoot "deploy\k8s\overlays\azure"
$Images = @(
    "auth-service", "users-service", "farms-service", "orders-service",
    "sensors-service", "notifications-service", "api-gateway", "sensor-simulator", "web"
)

$ErrorActionPreference = "Stop"

# Stops the script when an az, docker or kubectl command fails (they do not throw on their own).
function Invoke-Checked {
    param([scriptblock] $Command)
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $Command"
    }
}
