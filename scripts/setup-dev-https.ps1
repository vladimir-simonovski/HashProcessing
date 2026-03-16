#!/usr/bin/env pwsh
#Requires -Version 7.4

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$CertDir = $env:CERT_DIR ?? '.aspnet/https'
$CertName = $env:CERT_NAME ?? 'hashprocessing.api.pfx'
$CertPassword = $env:HTTPS_CERT_PASSWORD ?? 'devpassword'
$CertPath = Join-Path $CertDir $CertName

New-Item -ItemType Directory -Path $CertDir -Force | Out-Null

if (Test-Path $CertPath) {
    Write-Host "HTTPS certificate already exists at $CertPath"
    exit 0
}

Write-Host 'Generating ASP.NET Core development certificate...'
dotnet dev-certs https | Out-Null

Write-Host "Exporting certificate to $CertPath"
dotnet dev-certs https -ep $CertPath -p $CertPassword | Out-Null

Write-Host 'Trusting the development certificate...'
dotnet dev-certs https --trust

Write-Host 'Done. Certificate exported and trusted for Docker Compose.'
