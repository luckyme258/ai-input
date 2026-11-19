# Register / unregister the AiIme TSF text service (administrator required).
# Usage (elevated PowerShell):
#   .\tools\Install-Tsf.ps1 -Action Register
#   .\tools\Install-Tsf.ps1 -Action Unregister

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Register", "Unregister")]
    [string]$Action
)

$ErrorActionPreference = "Stop"

$isAdmin = ([Security.Principal.WindowsPrincipal] `
    [Security.Principal.WindowsIdentity]::GetCurrent()
).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Error "Please run this script as administrator."
    exit 1
}

# Prefer the published Release directory; fall back to Debug build output.
$repoRoot = Split-Path -Parent $PSScriptRoot
$toolCandidates = @(
    "dist\tsf-win-x64\AiIme.Tsf.RegisterTool.exe",
    "src\AiIme.Tsf.RegisterTool\bin\Release\net8.0-windows\win-x64\AiIme.Tsf.RegisterTool.exe",
    "src\AiIme.Tsf.RegisterTool\bin\Debug\net8.0-windows\win-x64\AiIme.Tsf.RegisterTool.exe"
)

$tool = $null
foreach ($rel in $toolCandidates) {
    $candidate = Join-Path $repoRoot $rel
    if (Test-Path -LiteralPath $candidate) { $tool = $candidate; break }
}

if (-not $tool) {
    Write-Error "AiIme.Tsf.RegisterTool.exe not found. Run dotnet build/publish first."
    exit 2
}

Write-Host "Using register tool: $tool"
$verb = if ($Action -eq "Register") { "register" } else { "unregister" }
& $tool $verb
exit $LASTEXITCODE
