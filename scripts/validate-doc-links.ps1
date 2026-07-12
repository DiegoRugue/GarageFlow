[CmdletBinding()]
param(
    [string] $RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = [IO.Path]::GetFullPath($RepositoryRoot)
$excludedDirectories = @('.git', '.superpowers', '.terraform', 'bin', 'obj', 'node_modules')
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

function Test-IsEscaped([string] $Text, [int] $Index) {
    $backslashes = 0
    for ($cursor = $Index - 1; $cursor -ge 0 -and $Text[$cursor] -eq '\'; $cursor--) {
        $backslashes++
    }
    return ($backslashes % 2) -eq 1
}

function Get-InlineLinkTargets([string] $Line) {
    $targets = [Collections.Generic.List[string]]::new()
    $searchFrom = 0
    while ($searchFrom -lt $Line.Length) {
        $delimiter = $Line.IndexOf('](', $searchFrom, [StringComparison]::Ordinal)
        if ($delimiter -lt 0) { break }
        $labelStart = $Line.LastIndexOf('[', $delimiter)
        if ($labelStart -lt 0 -or (Test-IsEscaped $Line $delimiter)) {
            $searchFrom = $delimiter + 2
            continue
        }

        $cursor = $delimiter + 2
        while ($cursor -lt $Line.Length -and [char]::IsWhiteSpace($Line[$cursor])) { $cursor++ }
        if ($cursor -ge $Line.Length) { break }

        if ($Line[$cursor] -eq '<') {
            $targetStart = $cursor
            $cursor++
            while ($cursor -lt $Line.Length -and
                ($Line[$cursor] -ne '>' -or (Test-IsEscaped $Line $cursor))) { $cursor++ }
            if ($cursor -ge $Line.Length) {
                $searchFrom = $delimiter + 2
                continue
            }
            $targetEnd = $cursor
            $close = $Line.IndexOf(')', $cursor + 1)
            if ($close -lt 0) {
                $searchFrom = $delimiter + 2
                continue
            }
            $targets.Add($Line.Substring($targetStart, $targetEnd - $targetStart + 1))
            $searchFrom = $close + 1
            continue
        }

        $targetStart = $cursor
        $targetEnd = -1
        $depth = 0
        $linkClose = -1
        while ($cursor -lt $Line.Length) {
            $character = $Line[$cursor]
            if ($character -eq '\' -and $cursor + 1 -lt $Line.Length) {
                $cursor += 2
                continue
            }
            if ($character -eq '(') {
                $depth++
                $cursor++
                continue
            }
            if ($character -eq ')') {
                if ($depth -eq 0) {
                    $targetEnd = $cursor
                    $linkClose = $cursor
                    break
                }
                $depth--
                $cursor++
                continue
            }
            if ([char]::IsWhiteSpace($character) -and $depth -eq 0) {
                $targetEnd = $cursor
                $titleAndClose = $Line.Substring($cursor)
                $titleMatch = [regex]::Match(
                    $titleAndClose,
                    '^\s+(?:"(?:\\.|[^"])*"|''(?:\\.|[^''])*''|\((?:\\.|[^)])*\))\s*\)')
                if ($titleMatch.Success) {
                    $linkClose = $cursor + $titleMatch.Length - 1
                }
                break
            }
            $cursor++
        }

        if ($linkClose -ge 0 -and $targetEnd -gt $targetStart) {
            $targets.Add($Line.Substring($targetStart, $targetEnd - $targetStart))
            $searchFrom = $linkClose + 1
        }
        else {
            $searchFrom = $delimiter + 2
        }
    }
    return $targets
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
    $isWindowsDrivePath = $target -match '^[A-Za-z]:[\\/]'
    if (-not $isWindowsDrivePath -and $target -match '^[A-Za-z][A-Za-z0-9+.-]*:') {
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
    $candidate = if ($isWindowsDrivePath) {
        $target
    }
    elseif ($target.StartsWith('/')) {
        Join-Path $root $target.TrimStart('/', '\')
    }
    else {
        Join-Path $sourceDirectory $target
    }
    $candidate = [IO.Path]::GetFullPath($candidate)
    $relativeCandidate = [IO.Path]::GetRelativePath($root, $candidate)
    if ([IO.Path]::IsPathRooted($relativeCandidate) -or
        $relativeCandidate -eq '..' -or
        $relativeCandidate.StartsWith("..$([IO.Path]::DirectorySeparatorChar)")) {
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
    $fenceCharacter = ''
    $fenceLength = 0
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $file.FullName) {
        $lineNumber++
        if ($insideFence) {
            if ($line -match '^ {0,3}(?<fence>`+|~+)[ \t]*$') {
                $closingFence = $Matches.fence
                if ($closingFence.Substring(0, 1) -eq $fenceCharacter -and
                    $closingFence.Length -ge $fenceLength) {
                    $insideFence = $false
                    $fenceCharacter = ''
                    $fenceLength = 0
                }
            }
            continue
        }

        if ($line -match '^ {0,3}(?<fence>`{3,}|~{3,})(?<info>.*)$') {
            $openingFence = $Matches.fence
            $openingCharacter = $openingFence.Substring(0, 1)
            if ($openingCharacter -eq '~' -or $Matches.info -notmatch '`') {
                $insideFence = $true
                $fenceCharacter = $openingCharacter
                $fenceLength = $openingFence.Length
                continue
            }
        }
        if ($line -match '^(?: {4}|\t)') { continue }

        foreach ($target in Get-InlineLinkTargets $line) {
            Test-LocalTarget $target $file.FullName $lineNumber
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
