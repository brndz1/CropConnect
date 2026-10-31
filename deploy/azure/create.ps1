# Creates the Azure resources once: resource group, container registry, AKS cluster and a budget alert.
#   az login
#   .\deploy\azure\create.ps1
. (Join-Path $PSScriptRoot "settings.ps1")

Write-Host "Registering the resource providers (first time only)..."
foreach ($provider in "Microsoft.ContainerService", "Microsoft.ContainerRegistry", "Microsoft.Network", "Microsoft.Compute") {
    Invoke-Checked { az provider register --namespace $provider --wait --output none }
}

Write-Host "Resource group $ResourceGroup in $Location..."
Invoke-Checked { az group create --name $ResourceGroup --location $Location --output none }

Write-Host "Container registry $Registry (Basic)..."
Invoke-Checked { az acr create --resource-group $ResourceGroup --name $Registry --sku Basic --output none }

# Free control plane; one B2ms node with a small OS disk; the cluster autoscaler adds a second node only when
# pods no longer fit. The cluster pulls from the registry with its managed identity (--attach-acr).
Write-Host "AKS cluster $Cluster (takes about 5 minutes)..."
Invoke-Checked {
    az aks create `
        --resource-group $ResourceGroup `
        --name $Cluster `
        --location $Location `
        --tier free `
        --node-count $MinNodes `
        --node-vm-size $NodeSize `
        --node-osdisk-size 30 `
        --enable-cluster-autoscaler `
        --min-count $MinNodes `
        --max-count $MaxNodes `
        --attach-acr $Registry `
        --no-ssh-key `
        --output none
}

Invoke-Checked { az aks get-credentials --resource-group $ResourceGroup --name $Cluster --overwrite-existing }

# Budget with email alerts to the signed-in account. Some subscription offers do not support budgets, so a
# failure here only prints a warning.
Write-Host "Budget alert of $BudgetAmount USD..."
$subscription = az account show --query id --output tsv
$email = az account show --query user.name --output tsv
$start = (Get-Date -Day 1).ToString("yyyy-MM-01")
$budget = @{
    properties = @{
        category   = "Cost"
        amount     = $BudgetAmount
        timeGrain  = "Monthly"
        timePeriod = @{ startDate = $start; endDate = (Get-Date).AddYears(1).ToString("yyyy-MM-01") }
        filter     = @{ dimensions = @{ name = "ResourceGroupName"; operator = "In"; values = @($ResourceGroup) } }
        notifications = @{
            Actual50 = @{ enabled = $true; operator = "GreaterThan"; threshold = 50; contactEmails = @($email) }
            Actual80 = @{ enabled = $true; operator = "GreaterThan"; threshold = 80; contactEmails = @($email) }
        }
    }
} | ConvertTo-Json -Depth 6 -Compress
$budgetFile = New-TemporaryFile
# Without a byte order mark, which az rest would reject (Windows PowerShell writes one with -Encoding utf8).
[IO.File]::WriteAllText($budgetFile, $budget)
az rest --method put `
    --url "https://management.azure.com/subscriptions/$subscription/providers/Microsoft.Consumption/budgets/cropconnect?api-version=2023-05-01" `
    --body "@$budgetFile" --output none
if ($LASTEXITCODE -ne 0) {
    Write-Warning "The budget could not be created; check the costs in the Azure portal (Cost Management)."
}
Remove-Item $budgetFile

Write-Host "Done. Next: .\deploy\azure\deploy.ps1"
