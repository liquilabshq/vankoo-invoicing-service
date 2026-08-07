#!/usr/bin/env pwsh
# Verificacion estandar del repo. Ver docs/verification.md.
$ErrorActionPreference = "Stop"

Write-Host "==> dotnet restore"
dotnet restore LiquiLabs.Vankoo.Invoicing.sln
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "==> dotnet build"
dotnet build LiquiLabs.Vankoo.Invoicing.sln --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "==> dotnet test"
dotnet test LiquiLabs.Vankoo.Invoicing.sln --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "==> verify.ps1: OK"
exit 0
