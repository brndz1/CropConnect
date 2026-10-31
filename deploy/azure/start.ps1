# Starts the stopped cluster (about 5 minutes); the pods and their data come back as they were.
#   .\deploy\azure\start.ps1
. (Join-Path $PSScriptRoot "settings.ps1")

Invoke-Checked { az aks start --resource-group $ResourceGroup --name $Cluster }
Invoke-Checked { az aks get-credentials --resource-group $ResourceGroup --name $Cluster --overwrite-existing --only-show-errors }
Invoke-Checked { kubectl -n cropconnect rollout status deployment --timeout=10m }

$ip = kubectl -n cropconnect get service web --output jsonpath="{.status.loadBalancer.ingress[0].ip}"
Write-Host "CropConnect is running: http://$ip (API at http://$ip/api)"
