# Builds the images, pushes them to the registry and applies the Kubernetes manifests to AKS.
# Run it after create.ps1, and again after every change to the code or the manifests.
#   .\deploy\azure\deploy.ps1
. (Join-Path $PSScriptRoot "settings.ps1")

# Random secrets on the first deploy, kept in secrets.env (ignored by git) for the next ones.
$secretsFile = Join-Path $Overlay "secrets.env"
if (-not (Test-Path $secretsFile)) {
    function New-Secret([int] $length) {
        $bytes = [byte[]]::new($length)
        [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
        return [Convert]::ToBase64String($bytes).Replace("+", "A").Replace("/", "B").Replace("=", "")
    }
    @(
        "POSTGRES_USER=cropconnect"
        "POSTGRES_PASSWORD=$(New-Secret 24)"
        "RABBITMQ_USER=cropconnect"
        "RABBITMQ_PASSWORD=$(New-Secret 24)"
        "JWT_SECRET=$(New-Secret 48)"
        "INTERNAL_API_KEY=$(New-Secret 32)"
        "ADMIN_USERNAME=admin"
        "ADMIN_EMAIL=admin@cropconnect.test"
        "ADMIN_PASSWORD=Admin-$(New-Secret 12)"
    ) | Set-Content -Path $secretsFile -Encoding ascii
    Write-Host "Generated $secretsFile (keep it: it holds the admin password)."
}

Write-Host "Building the images..."
Push-Location $RepoRoot
try {
    Invoke-Checked { docker compose --profile simulation build }
}
finally {
    Pop-Location
}

Write-Host "Pushing to $Registry.azurecr.io..."
Invoke-Checked { az acr login --name $Registry }
foreach ($image in $Images) {
    $remote = "$Registry.azurecr.io/${image}:latest"
    Invoke-Checked { docker tag "cropconnect/${image}:latest" $remote }
    Invoke-Checked { docker push --quiet $remote }
}

Write-Host "Applying the manifests..."
Invoke-Checked { az aks get-credentials --resource-group $ResourceGroup --name $Cluster --overwrite-existing --only-show-errors }
Invoke-Checked { kubectl apply -k $Overlay }
# New "latest" images and config changes only reach running pods on a restart.
Invoke-Checked { kubectl -n cropconnect rollout restart deployment }
Invoke-Checked { kubectl -n cropconnect rollout status deployment --timeout=10m }

$ip = kubectl -n cropconnect get service api-gateway --output jsonpath="{.status.loadBalancer.ingress[0].ip}"
Write-Host "CropConnect is running: http://$ip/api/farms"
