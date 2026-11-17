param(
    [Parameter(Mandatory = $true)]
    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$target = [System.IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $target) {
    throw "O arquivo de destino já existe: $target"
}

$directory = Split-Path -Parent $target
if (-not (Test-Path -LiteralPath $directory)) {
    New-Item -ItemType Directory -Path $directory | Out-Null
}

Push-Location $repository
try {
    dotnet ef migrations script --idempotent `
        --project server/src/Infraestrutura `
        --startup-project server/src/Api `
        --context SomniaDbContext `
        --configuration Release `
        --output $target
    if ($LASTEXITCODE -ne 0) {
        throw 'Falha ao gerar o script de migrations.'
    }
    Write-Output "Script idempotente gerado em: $target"
}
finally {
    Pop-Location
}
