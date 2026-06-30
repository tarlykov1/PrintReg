$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'output/GSPLabelPrinter-win-x64'
$project = Join-Path $root 'src/GSPLabelPrinter/GSPLabelPrinter.csproj'
$solution = Join-Path $root 'GSPLabelPrinter.sln'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Не найден dotnet SDK. Установите .NET SDK 8 и повторите публикацию.'
}

Write-Host 'Restoring...'
dotnet restore $solution
Write-Host 'Building...'
dotnet build $solution --configuration Release -p:Platform=x64 --no-restore
Write-Host 'Testing...'
dotnet test $solution --configuration Release -p:Platform=x64 --no-build

if (Test-Path $out) {
    Get-ChildItem $out -Exclude 'data','backup','logs','config.json' | Remove-Item -Recurse -Force
} else {
    New-Item -ItemType Directory -Path $out | Out-Null
}

Write-Host 'Publishing...'
dotnet publish $project --configuration Release --runtime win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $out

if (-not (Test-Path (Join-Path $out 'config.json'))) {
    Copy-Item (Join-Path $root 'config.example.json') (Join-Path $out 'config.json') -Force
}
New-Item -ItemType Directory -Force -Path (Join-Path $out 'data'), (Join-Path $out 'backup'), (Join-Path $out 'logs') | Out-Null
if (-not (Test-Path (Join-Path $out 'data/employees.csv'))) {
    Copy-Item (Join-Path $root 'data/employees.csv') (Join-Path $out 'data/employees.csv') -Force
}
Copy-Item (Join-Path $root 'src/GSPLabelPrinter/wwwroot') (Join-Path $out 'wwwroot') -Recurse -Force

$required = @(
    'GSPLabelPrinter.exe',
    'config.json',
    'data/employees.csv',
    'backup',
    'logs',
    'wwwroot/index.html',
    'wwwroot/settings.html',
    'wwwroot/css/app.css',
    'wwwroot/js/app.js',
    'wwwroot/js/settings.js'
)
foreach ($item in $required) {
    $path = Join-Path $out $item
    if (-not (Test-Path $path)) { throw "После публикации отсутствует обязательный элемент: $item" }
}

$exe = Join-Path $out 'GSPLabelPrinter.exe'
Write-Host "Published EXE: $exe"
