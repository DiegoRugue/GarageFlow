[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = [IO.Path]::GetFullPath($RepositoryRoot)
$excludedDirectories = @('.git', '.superpowers', '.terraform', 'bin', 'obj', 'node_modules')
$inlineLinkPattern = [regex]'!?\[[^\]]*\]\(\s*(?<target><[^>]+>|(?:\\.|[^\s)])+)(?:\s+["''][^"'']*["''])?\s*\)'
$referenceLinkPattern = [regex]'^\s*\[[^\]]+\]:\s*(?<target><[^>]+>|\S+)'
$errors = [Collections.Generic.List[string]]::new()
$checked = 0

function Test-IsExcluded([string] $Path) {
    $relative = [IO.Path]::GetRelativePath($root, $Path)
    $segments = $relative -split '[\\/]'
    return @($segments | Where-Object { $excludedDirectories -contains $_ }).Count -gt 0
}

function Remove-MarkdownEscapes([string] $Value) {
    return [regex]::Replace($Value, '\\([\\`*_{}\[\]()#+\-.! ])', '$1')
}

function Test-LocalTarget([string] $RawTarget, [string] $SourcePath, [int] $LineNumber) {
    $target = $RawTarget.Trim()
    if ($target.StartsWith('<') -and $target.EndsWith('>')) {
        $target = $target[1..($target.Length - 2)] -join ''
    }
    $target = Remove-MarkdownEscapes $target
    if ([string]::IsNullOrWhiteSpace($target) -or $target.StartsWith('#') -or $target.StartsWith('//')) {
        return
    }
    if ($target -match '^[A-Za-z][A-Za-z0-9+.-]*:') {
        return
    }

    $separatorIndex = $target.IndexOfAny([char[]]@('?', '#'))
    if ($separatorIndex -ge 0) {
        $target = $target.Substring(0, $separatorIndex)
    }
    if ([string]::IsNullOrWhiteSpace($target)) {
        return
    }

    try {
        $target = [Uri]::UnescapeDataString($target)
    }
    catch {
        $errors.Add("$SourcePath`:$LineNumber has invalid URI encoding: $RawTarget")
        return
    }

    $sourceDirectory = Split-Path -Parent $SourcePath
    $candidate = if ($target.StartsWith('/')) {
        Join-Path $root $target.TrimStart('/', '\')
    }
    else {
        Join-Path $sourceDirectory $target
    }
    $candidate = [IO.Path]::GetFullPath($candidate)
    $relativeCandidate = [IO.Path]::GetRelativePath($root, $candidate)
    if ($relativeCandidate -eq '..' -or $relativeCandidate.StartsWith("..$([IO.Path]::DirectorySeparatorChar)")) {
        $errors.Add("$SourcePath`:$LineNumber escapes the repository: $RawTarget")
        return
    }

    if (-not (Test-Path -LiteralPath $candidate)) {
        if ($target -match '^(?<path>.+):\d+$') {
            $withoutLine = if ($Matches.path.StartsWith('/')) {
                Join-Path $root $Matches.path.TrimStart('/', '\')
            }
            else {
                Join-Path $sourceDirectory $Matches.path
            }
            $withoutLine = [IO.Path]::GetFullPath($withoutLine)
            if (Test-Path -LiteralPath $withoutLine) {
                $script:checked++
                return
            }
        }
        $errors.Add("$SourcePath`:$LineNumber target does not exist: $RawTarget")
        return
    }
    $script:checked++
}

$markdownFiles = Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.md' |
    Where-Object { -not (Test-IsExcluded $_.FullName) }

foreach ($file in $markdownFiles) {
    $insideFence = $false
    $fenceMarker = ''
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $file.FullName) {
        $lineNumber++
        if ($line -match '^\s*(?<fence>`{3,}|~{3,})') {
            $marker = $Matches.fence.Substring(0, 1)
            if (-not $insideFence) {
                $insideFence = $true
                $fenceMarker = $marker
            }
            elseif ($marker -eq $fenceMarker) {
                $insideFence = $false
                $fenceMarker = ''
            }
            continue
        }
        if ($insideFence) { continue }

        foreach ($match in $inlineLinkPattern.Matches($line)) {
            Test-LocalTarget $match.Groups['target'].Value $file.FullName $lineNumber
        }
        $referenceMatch = $referenceLinkPattern.Match($line)
        if ($referenceMatch.Success) {
            Test-LocalTarget $referenceMatch.Groups['target'].Value $file.FullName $lineNumber
        }
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    throw "Documentation link validation failed with $($errors.Count) error(s)."
}

Write-Host "Documentation links PASS: $checked local target(s) across $($markdownFiles.Count) Markdown file(s)."
