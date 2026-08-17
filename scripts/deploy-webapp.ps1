<#
.SYNOPSIS
  One-command code-only redeploy of the Voice Live Avatar app to an existing Azure App Service.
  No azd, no manual zip. Builds the frontend, publishes the app, packages a Kudu-valid zip
  (forward-slash entries — works on Windows PowerShell 5.1 AND PowerShell 7), forces a
  pre-built deploy, and checks health.

.EXAMPLE
  pwsh -File scripts/deploy-webapp.ps1 -App wa-d-ai-avatars-01 -ResourceGroup rg-d-sharedservices

.NOTES
  Prereqs: .NET 10 SDK, Node.js, Azure CLI (az login done). Run from the repository root.
#>
param(
  [Parameter(Mandatory)] [string]$App,
  [Parameter(Mandatory)] [string]$ResourceGroup,
  [string]$Project = 'web/src/VoiceLive.Web'
)
$ErrorActionPreference = 'Stop'

$publishDir = Join-Path $PWD 'tmp/publish'
$zipPath    = Join-Path $PWD 'tmp/app.zip'

Write-Host "==> Matching publish flavour to the App Service platform..." -ForegroundColor Cyan
$linuxFx = az webapp config show --name $App --resource-group $ResourceGroup --query linuxFxVersion -o tsv
Write-Host "    linuxFxVersion = '$linuxFx'"

Write-Host "==> Forcing pre-built deploy (no Oryx build on Kudu)..." -ForegroundColor Cyan
az webapp config appsettings set --name $App --resource-group $ResourceGroup `
  --settings SCM_DO_BUILD_DURING_DEPLOYMENT=false ENABLE_ORYX_BUILD=false | Out-Null

Write-Host "==> Publishing $Project ..." -ForegroundColor Cyan
if ([string]::IsNullOrWhiteSpace($linuxFx)) {
  # empty platform runtime => self-contained
  dotnet publish $Project -c Release -r linux-x64 --self-contained true -o $publishDir
} else {
  # DOTNETCORE|10.0 (default) => framework-dependent
  dotnet publish $Project -c Release -o $publishDir
}

if (-not (Test-Path (Join-Path $publishDir 'VoiceLive.Web.dll'))) {
  throw "Publish output is missing VoiceLive.Web.dll at the root of $publishDir. Aborting before deploy."
}

Write-Host "==> Packaging Kudu-valid zip (forced forward-slash entries)..." -ForegroundColor Cyan
Remove-Item $zipPath -ErrorAction SilentlyContinue
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
# Build the archive entry-by-entry so separators are ALWAYS '/'. This is required on
# Windows PowerShell 5.1, whose .NET Framework ZipFile writes '\' entries that Kudu's
# Linux rsync rejects with "failed to stat ...: Invalid argument (22)".
$fs = [System.IO.File]::Open($zipPath, [System.IO.FileMode]::Create)
$archive = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Create)
try {
  $root = (Resolve-Path $publishDir).Path.TrimEnd('\', '/')
  Get-ChildItem -Path $publishDir -Recurse -File | ForEach-Object {
    $relative = $_.FullName.Substring($root.Length + 1).Replace('\', '/')
    $entry = $archive.CreateEntry($relative, [System.IO.Compression.CompressionLevel]::Optimal)
    $entryStream = $entry.Open()
    try {
      $bytes = [System.IO.File]::ReadAllBytes($_.FullName)
      $entryStream.Write($bytes, 0, $bytes.Length)
    } finally {
      $entryStream.Dispose()
    }
  }
} finally {
  $archive.Dispose()
  $fs.Dispose()
}

# Fail fast if any entry still has a backslash (would break the Linux deploy).
$bad = [System.IO.Compression.ZipFile]::OpenRead($zipPath).Entries |
  Where-Object { $_.FullName -match '\\' } | Select-Object -First 1
if ($bad) { throw "Zip contains backslash entries (e.g. '$($bad.FullName)'). Aborting." }

Write-Host "==> Deploying to $App ..." -ForegroundColor Cyan
az webapp deploy --name $App --resource-group $ResourceGroup --src-path $zipPath --type zip

Write-Host "==> Health check..." -ForegroundColor Cyan
$health = (curl.exe -s "https://$App.azurewebsites.net/api/health")
Write-Host "    /api/health -> $health"
if ($health -match 'Healthy') {
  Write-Host "SUCCESS: app is Healthy. Artifact kept at $zipPath for rollback." -ForegroundColor Green
} else {
  Write-Warning "Health did not report Healthy. If 503, config failed to load (see runbook section 10)."
  Write-Warning "For the real deploy reason run: az webapp log deployment show --name $App --resource-group $ResourceGroup"
}
