# Deletes every Azure resource of the project (cluster, registry, disks and their data). Cannot be undone.
#   .\deploy\azure\destroy.ps1
. (Join-Path $PSScriptRoot "settings.ps1")

$answer = Read-Host "Delete the resource group $ResourceGroup and everything in it? (yes/no)"
if ($answer -ne "yes") {
    Write-Host "Nothing was deleted."
    return
}

Invoke-Checked { az group delete --name $ResourceGroup --yes }
Remove-Item (Join-Path $Overlay "secrets.env") -ErrorAction SilentlyContinue
Write-Host "Deleted."
