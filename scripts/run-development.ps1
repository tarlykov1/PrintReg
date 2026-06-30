$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path -Parent $PSScriptRoot
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Не найден dotnet SDK. Установите .NET SDK 8.'
}
Push-Location $root
try {
    dotnet run --project (Join-Path $root 'src/GSPLabelPrinter/GSPLabelPrinter.csproj') -p:Platform=x64
}
finally {
    Pop-Location
}
