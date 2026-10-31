# Stops the cluster to save credits: the nodes are released, the disks (data) and the registry stay.
#   .\deploy\azure\stop.ps1
. (Join-Path $PSScriptRoot "settings.ps1")

Invoke-Checked { az aks stop --resource-group $ResourceGroup --name $Cluster }
Write-Host "Stopped. Start it again with .\deploy\azure\start.ps1"
