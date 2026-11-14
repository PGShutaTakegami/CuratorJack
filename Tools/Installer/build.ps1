# CJSetup.exe をビルドして Builds フォルダへコピーする
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

dotnet build (Join-Path $root 'CJInstaller.csproj') -c Release -nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$output = Join-Path $root '..\..\Builds'
New-Item -ItemType Directory -Force $output | Out-Null
Copy-Item (Join-Path $root 'bin\Release\net48\CJSetup.exe') (Join-Path $output 'CJSetup.exe') -Force
Write-Host "Builds\CJSetup.exe を作成しました"
