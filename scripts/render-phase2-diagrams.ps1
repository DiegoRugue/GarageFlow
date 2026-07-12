[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$configPath = Join-Path $repositoryRoot 'docs/architecture/diagrams/mermaid-config.json'
$imageRoot = Join-Path $repositoryRoot 'docs/architecture/diagrams/images'
$dddImageRoot = Join-Path $repositoryRoot 'docs/ddd/diagrams/images'

$diagrams = @(
    @{ Name = 'application'; Source = 'docs/architecture/diagrams/source/application.mmd'; OutputDirectory = $imageRoot },
    @{ Name = 'aws-academy'; Source = 'docs/architecture/diagrams/source/aws-academy.mmd'; OutputDirectory = $imageRoot },
    @{ Name = 'kubernetes'; Source = 'docs/architecture/diagrams/source/kubernetes.mmd'; OutputDirectory = $imageRoot },
    @{ Name = 'database-reliability'; Source = 'docs/architecture/diagrams/source/database-reliability.mmd'; OutputDirectory = $imageRoot },
    @{ Name = 'deployment-flow'; Source = 'docs/architecture/diagrams/source/deployment-flow.mmd'; OutputDirectory = $imageRoot },
    @{ Name = 'work-order-state-machine'; Source = 'docs/ddd/diagrams/source/work-order-state-machine.mmd'; OutputDirectory = $dddImageRoot }
)

if ($diagrams.Count -ne 6) {
    throw "Expected exactly six Mermaid diagrams, found $($diagrams.Count)."
}

function Assert-SourceContract {
    param(
        [Parameter(Mandatory)] [string] $RelativePath,
        [Parameter(Mandatory)] [string[]] $Required,
        [string[]] $Forbidden = @()
    )

    $content = Get-Content -LiteralPath (Join-Path $repositoryRoot $RelativePath) -Raw
    foreach ($pattern in $Required) {
        if ($content -notmatch $pattern) {
            throw "Required diagram contract '$pattern' is missing from $RelativePath."
        }
    }
    foreach ($pattern in $Forbidden) {
        if ($content -match $pattern) {
            throw "Forbidden diagram claim '$pattern' was found in $RelativePath."
        }
    }
}

Assert-SourceContract -RelativePath 'docs/architecture/diagrams/source/kubernetes.mmd' `
    -Required @('--migrate-only', 'Database__AutoMigrate=false', 'Service\s+-->', 'HPA\s+-->') `
    -Forbidden @('DatabaseMigration__Enabled', 'Pod\w*\s+-->\s+Service')
Assert-SourceContract -RelativePath 'docs/architecture/diagrams/source/aws-academy.mmd' `
    -Required @('distribuição por AZ não é garantida', 'posicionamento por AZ não é garantido', 'Actions\s+-->', 'Secrets\s+-->\s+K8sSecret', 'K8sSecret\s+-->.*Replicas') `
    -Forbidden @('NodeA|NodeB|PodA|PodB|deploy-demo-destroy')
Assert-SourceContract -RelativePath 'docs/architecture/diagrams/source/database-reliability.mmd' `
    -Required @('Command comum', 'Webhook HMAC válido', 'aggregate \+ outbox', 'aggregate \+ inbox \+ outbox', 'UPDATE condicional MarkProcessed', 'UPDATE condicional Reschedule', 'OwnershipLost')
Assert-SourceContract -RelativePath 'docs/architecture/diagrams/source/deployment-flow.mmd' `
    -Required @('deploy-aws-academy\.yml', 'Demonstração manual', 'destroy-aws-academy\.yml') `
    -Forbidden @('deploy-demo-destroy')

if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
    throw "Mermaid configuration was not found: $configPath"
}

New-Item -ItemType Directory -Force -Path $imageRoot, $dddImageRoot | Out-Null

function Invoke-MermaidRender {
    param(
        [Parameter(Mandatory)] [string] $Source,
        [Parameter(Mandatory)] [string] $Output,
        [switch] $Png
    )

    $arguments = @(
        '--yes',
        '--package', '@mermaid-js/mermaid-cli@11.16.0',
        'mmdc',
        '--configFile', $configPath,
        '--input', $Source,
        '--output', $Output,
        '--backgroundColor', 'white'
    )
    if ($Png) {
        $arguments += @('--width', '2200')
    }

    & npx @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Mermaid CLI failed with exit code $LASTEXITCODE while rendering $Source."
    }

    if (-not (Test-Path -LiteralPath $Output -PathType Leaf)) {
        throw "Mermaid CLI did not create expected output: $Output"
    }

    $outputSize = (Get-Item -LiteralPath $Output).Length
    if ($outputSize -le 10KB) {
        throw "Rendered output must be larger than 10 KB: $Output ($outputSize bytes)."
    }

    if ($Png) {
        $pngBytes = [System.IO.File]::ReadAllBytes($Output)
        if ($pngBytes.Length -lt 24 -or
            $pngBytes[0] -ne 0x89 -or $pngBytes[1] -ne 0x50 -or
            $pngBytes[2] -ne 0x4E -or $pngBytes[3] -ne 0x47) {
            throw "Rendered output does not have a valid PNG signature: $Output"
        }
        $pngWidth =
            ($pngBytes[16] * 16777216) +
            ($pngBytes[17] * 65536) +
            ($pngBytes[18] * 256) +
            $pngBytes[19]
        if ($pngWidth -gt 1600) {
            throw "PNG natural width must not exceed 1600px for 900px readability: $Output ($($pngWidth)px)."
        }
    }
}

foreach ($diagram in $diagrams) {
    $sourcePath = Join-Path $repositoryRoot $diagram.Source
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Mermaid source was not found: $sourcePath"
    }

    $svgPath = Join-Path $diagram.OutputDirectory "$($diagram.Name).svg"
    $pngPath = Join-Path $diagram.OutputDirectory "$($diagram.Name).png"
    Invoke-MermaidRender -Source $sourcePath -Output $svgPath
    Invoke-MermaidRender -Source $sourcePath -Output $pngPath -Png
}

Write-Host 'Rendered exactly six Phase 2 diagrams as SVG and 2200px PNG.'
