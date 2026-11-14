<#
.SYNOPSIS
    Fetches the curated verification corpus into .corpus/ for the LintMark verifier.

.DESCRIPTION
    Reads corpus-sources.json (next to this script) and, for every source, makes a shallow, blob-less clone
    (--depth 1 --filter=blob:none) with a sparse checkout that downloads only the files that are used. The files
    that are used are chosen from the tree listing before any blob is downloaded: the per-source cap is applied
    deterministically (sorted, evenly spread), so a capped source costs only its cap in downloads.

    The result of a source is written to .corpus/<category>/<name>/ (and below a <owner>__<repo> folder for
    sources that have several members):

        files/      whole files that were selected (named *.md; other files get '.md' appended)
        examples/   one file per extracted example, named <NNN>-<slug>.md
        SOURCE.txt  the repository, branch, resolved commit SHA and the counts

    The clones themselves are kept in .corpus/.clones/ (a folder the verifier never searches) so that a source can
    be processed again, with other caps, without the network (-Reprocess).

    Extractors (the 'extractor' field of a source):

        commonmark-examples, markdig-examples
            The spec example format: a fence of backticks followed by 'example', the Markdown, a line with a single
            '.', the expected output and the closing fence. The tab arrow is converted to a real tab, as the spec
            test suites do.
        fixture-dots
            Blocks of a title line, '.', the input, '.', the output and '.' (markdown-it, mdformat and their
            plugins). The input is kept.
        pandoc-cmd
            Pandoc command tests: a fenced block whose first line is '% pandoc <arguments>' and whose standard input
            runs up to a '^D' line. Only the blocks that read Markdown (markdown, commonmark, gfm and variants, or
            no reader option) are kept; the input is kept.

    Files that are empty, that are larger than 2 MB, that contain NUL characters or that are not valid UTF-8 are
    skipped. Text is copied byte for byte, so line endings (CRLF included) and BOMs are preserved. A source that
    fails is reported and skipped; the others go on.

    The corpus is read-only test input and is never committed (.corpus/ is ignored by Git, markdownlint and the
    spell checker).

.PARAMETER Root
    The folder that receives the corpus. Default: .corpus in the root of this repository.

.PARAMETER Manifest
    The manifest. Default: corpus-sources.json next to this script.

.PARAMETER Only
    Names of the sources to process; default: all.

.PARAMETER Force
    Delete and fetch again the sources that are already there.

.PARAMETER Reprocess
    Rebuild the output of the sources from their existing clones (for example after a cap changed); sources
    without a clone are cloned.

.EXAMPLE
    pwsh tools/LintMark.Verify/fetch-corpus.ps1

.EXAMPLE
    pwsh tools/LintMark.Verify/fetch-corpus.ps1 -Only commonmark-spec, readmes -Force

.EXAMPLE
    dotnet run --project tools/LintMark.Verify -- .corpus --dema
#>
[CmdletBinding()]
param(
    [string]$Root = (Join-Path $PSScriptRoot '..\..\.corpus'),
    [string]$Manifest = (Join-Path $PSScriptRoot 'corpus-sources.json'),
    [string[]]$Only,
    [switch]$Force,
    [switch]$Reprocess
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Git output and the file names it lists are UTF-8
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$script:StrictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
$script:Utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$script:MaxFileBytes = 2MB
$script:TabArrow = [string][char]0x2192

# --- Git ---------------------------------------------------------------------------------------------------------

function Invoke-Git {
    # Runs git (in a folder when given) and returns its standard output; throws when git fails.
    param([string]$Directory, [string[]]$Arguments, [string[]]$InputLines)
    $all = @('-c', 'core.autocrlf=false', '-c', 'core.longpaths=true', '-c', 'core.quotepath=false')
    if ($Directory) { $all += @('-C', $Directory) }
    $all += $Arguments
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        if ($InputLines) { $output = $InputLines | & git @all 2>&1 } else { $output = & git @all 2>&1 }
        $code = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $previous }
    if ($code -ne 0) {
        throw "git $($Arguments -join ' ') failed ($code): $(($output | Out-String).Trim())"
    }
    return @($output | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] })
}

function Get-EscapedSparsePath {
    # Escapes a path for a gitignore-style sparse-checkout pattern and anchors it at the root.
    param([string]$Path)
    $escaped = $Path -replace '([\\*?\[\]!#])', '\$1'
    $escaped = $escaped -replace '^ ', '\ ' -replace ' $', '\ '
    return "/$escaped"
}

# --- Selection ---------------------------------------------------------------------------------------------------

function Test-PathPrefix {
    # True when the path is below one of the prefixes (a prefix ending in '/') or is one of the exact files.
    param([string]$Path, [string[]]$Prefixes)
    foreach ($prefix in $Prefixes) {
        if ($prefix -eq '' -or $Path -ceq $prefix -or ($prefix.EndsWith('/') -and $Path.StartsWith($prefix, [StringComparison]::Ordinal))) {
            return $true
        }
    }
    return $false
}

function Select-Evenly {
    # Picks at most $Cap items, evenly spread over the (already ordered) list; the same input gives the same pick.
    param([object[]]$Items, [int]$Cap)
    $list = @($Items)
    if ($Cap -le 0 -or $list.Count -le $Cap) { return $list }
    $picked = for ($i = 0; $i -lt $Cap; $i++) { $list[[int][Math]::Floor($i * $list.Count / $Cap)] }
    return @($picked)
}

function Get-Property {
    # Reads an optional property of a manifest object.
    param($Object, [string]$Name, $Default = $null)
    if ($Object.PSObject.Properties.Name -contains $Name) { return $Object.$Name }
    return $Default
}

function Get-Slug {
    param([string]$Text, [int]$MaxLength = 40)
    $slug = ($Text.ToLowerInvariant() -replace '[^a-z0-9]+', '-').Trim('-')
    if ($slug.Length -gt $MaxLength) { $slug = $slug.Substring(0, $MaxLength).Trim('-') }
    if ($slug.Length -eq 0) { return 'example' }
    return $slug
}

function Read-Utf8 {
    # Returns the text of a file, or $null when it is empty, too large, binary or not UTF-8.
    param([string]$Path)
    $info = [System.IO.FileInfo]::new($Path)
    if (-not $info.Exists -or $info.Length -eq 0 -or $info.Length -gt $script:MaxFileBytes) { return $null }
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ([Array]::IndexOf($bytes, [byte]0) -ge 0) { return $null }
    try { return $script:StrictUtf8.GetString($bytes) } catch { return $null }
}

function Write-Text {
    param([string]$Path, [string]$Text)
    [void][System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($Path))
    [System.IO.File]::WriteAllText($Path, $Text, $script:Utf8NoBom)
}

function Get-Hash {
    param([string]$Text)
    return [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($script:Utf8NoBom.GetBytes($Text)))
}

# --- Extractors --------------------------------------------------------------------------------------------------
# Every extractor yields objects { Ordinal; Slug; Text }: the text is the exact Markdown of one example.

function Split-Lines {
    # Splits on LF only, so that a CR stays part of its line and is preserved in the output.
    param([string]$Text)
    return $Text -split "`n"
}

function Get-Marker {
    # The line without a trailing CR, for comparing it with markers.
    param([string]$Line)
    return $Line.TrimEnd("`r")
}

function Join-Body {
    param($Lines)
    if ($Lines.Count -eq 0) { return '' }
    return ($Lines -join "`n") + "`n"
}

function Get-ExampleFenceExamples {
    # Spec format: a fence of backticks and ' example', the input, '.', the expected output, the closing fence.
    param([string]$Text)
    $lines = Split-Lines $Text
    $heading = ''
    $ordinal = 0
    $i = 0
    while ($i -lt $lines.Count) {
        $line = Get-Marker $lines[$i]
        if ($line -match '^(`{3,}) example(?:\s.*)?$') {
            $fence = $Matches[1]
            $ordinal++
            $content = [System.Collections.Generic.List[string]]::new()
            $i++
            while ($i -lt $lines.Count -and (Get-Marker $lines[$i]) -ne '.' -and (Get-Marker $lines[$i]) -ne $fence) {
                $content.Add($lines[$i])
                $i++
            }

            # Skip the expected output
            while ($i -lt $lines.Count -and (Get-Marker $lines[$i]) -ne $fence) { $i++ }
            $body = (Join-Body $content).Replace($script:TabArrow, "`t")
            [pscustomobject]@{ Ordinal = $ordinal; Slug = (Get-Slug $heading); Text = $body }
        }
        elseif ($line -match '^#{1,6}\s+(.+?)\s*#*\s*$') {
            $heading = $Matches[1]
        }

        $i++
    }
}

function Get-DotFixtureExamples {
    # Fixture format: title, '.', input, '.', output, '.'.
    param([string]$Text)
    $state = 0
    $title = ''
    $content = [System.Collections.Generic.List[string]]::new()
    $ordinal = 0
    foreach ($raw in (Split-Lines $Text)) {
        $line = Get-Marker $raw
        if ($state -eq 0) {
            if ($line -eq '.') { $state = 1; $content = [System.Collections.Generic.List[string]]::new() }
            elseif ($line.Trim().Length -gt 0) { $title = $line }
        }
        elseif ($state -eq 1) {
            if ($line -eq '.') { $state = 2 } else { $content.Add($raw) }
        }
        elseif ($line -eq '.') {
            $state = 0
            $ordinal++
            [pscustomobject]@{ Ordinal = $ordinal; Slug = (Get-Slug $title); Text = (Join-Body $content) }
            $title = ''
        }
    }
}

function Get-PandocCommandExamples {
    # Cram format: fenced blocks of '% pandoc <args>', the standard input, '^D' and the expected output.
    param([string]$Text)
    $lines = Split-Lines $Text
    $ordinal = 0
    $i = 0
    while ($i -lt $lines.Count) {
        if ((Get-Marker $lines[$i]) -notmatch '^(`{3,})[^`]*$') { $i++; continue }
        $closing = '^`{{{0},}}\s*$' -f $Matches[1].Length
        $block = [System.Collections.Generic.List[string]]::new()
        $i++
        while ($i -lt $lines.Count -and (Get-Marker $lines[$i]) -notmatch $closing) {
            $block.Add($lines[$i])
            $i++
        }

        $i++
        if ($block.Count -eq 0 -or (Get-Marker $block[0]) -notmatch '^%\s*pandoc\b(.*)$') { continue }
        $arguments = $Matches[1]
        $end = -1
        for ($k = 1; $k -lt $block.Count; $k++) { if ((Get-Marker $block[$k]) -eq '^D') { $end = $k; break } }
        if ($end -lt 2) { continue }

        # Only a Markdown reader: markdown, commonmark, gfm and variants, or no reader option (Markdown is the default)
        $reader = 'markdown'
        if ($arguments -match '(?:^|\s)(?:-f|-r|--from|--read)(?:\s+|=)(\S+)') { $reader = $Matches[1] }
        if ($reader -notmatch '^(markdown|commonmark|gfm)') { continue }

        $ordinal++
        [pscustomobject]@{ Ordinal = $ordinal; Slug = (Get-Slug $reader); Text = (Join-Body @($block[1..($end - 1)])) }
    }
}

function Get-Examples {
    # Runs the extractor on the files; yields { Name; Text } in a deterministic order, without duplicates.
    param([string]$Extractor, [string]$CloneDirectory, [string[]]$Files, [System.Collections.Generic.HashSet[string]]$Seen)
    $multiple = $Files.Count -gt 1
    foreach ($file in $Files) {
        $text = Read-Utf8 (Join-Path $CloneDirectory $file)
        if ($null -eq $text) { continue }
        $found = switch ($Extractor) {
            { $_ -in 'commonmark-examples', 'markdig-examples' } { Get-ExampleFenceExamples $text }
            'fixture-dots' { Get-DotFixtureExamples $text }
            'pandoc-cmd' { Get-PandocCommandExamples $text }
            default { throw "Unknown extractor '$Extractor'." }
        }

        $stem = Get-Slug ([System.IO.Path]::ChangeExtension($file, $null)) 60
        foreach ($example in $found) {
            if ($example.Text.Trim().Length -eq 0) { continue }
            if (-not $Seen.Add((Get-Hash $example.Text))) { continue }
            $number = '{0:D3}' -f $example.Ordinal
            $name = if ($multiple) { "$stem-$number-$($example.Slug)" } else { "$number-$($example.Slug)" }
            [pscustomobject]@{ Name = $name; Text = $example.Text }
        }
    }
}

# --- Sources -----------------------------------------------------------------------------------------------------

function Get-Members {
    # Expands a source into its members: one repository each, with the fields of the source as defaults.
    param($Source)
    $members = Get-Property $Source 'members'
    if (-not $members) {
        return @([pscustomobject]@{ Folder = ''; Settings = $Source })
    }

    foreach ($member in $members) {
        $merged = [ordered]@{}
        foreach ($property in $Source.PSObject.Properties) { if ($property.Name -ne 'members') { $merged[$property.Name] = $property.Value } }
        foreach ($property in $member.PSObject.Properties) { $merged[$property.Name] = $property.Value }
        [pscustomobject]@{ Folder = ($member.repo -replace '/', '__'); Settings = [pscustomobject]$merged }
    }
}

function Add-ExistingExampleHashes {
    # Adds the hashes of the examples that earlier sources produced, for the dedupeAgainst field.
    param([string]$CorpusRoot, $Sources, [string[]]$Names, [System.Collections.Generic.HashSet[string]]$Seen)
    foreach ($name in $Names) {
        $other = $Sources | Where-Object { $_.name -eq $name } | Select-Object -First 1
        if (-not $other) { Write-Warning "dedupeAgainst: unknown source '$name'"; continue }
        $directory = Join-Path $CorpusRoot "$($other.category)/$name"
        if (-not (Test-Path $directory)) { Write-Warning "dedupeAgainst: '$name' has not been fetched"; continue }
        $files = Get-ChildItem -Path $directory -Recurse -Filter '*.md' -File | Where-Object { $_.FullName -match '[\\/]examples[\\/]' }
        foreach ($file in $files) {
            $text = Read-Utf8 $file.FullName
            if ($null -ne $text) { [void]$Seen.Add((Get-Hash $text)) }
        }
    }
}

function Invoke-Member {
    # Fetches and processes one repository of a source; returns the counts.
    param($Source, $Member, [string]$CorpusRoot, $AllSources)
    $settings = $Member.Settings
    $repo = $settings.repo
    $category = $Source.category
    $ref = Get-Property $settings 'ref'
    $extractor = Get-Property $settings 'extractor' 'none'
    $paths = @(Get-Property $settings 'paths' @(''))
    $keepFiles = [bool](Get-Property $settings 'keepFiles' ($extractor -eq 'none'))
    $include = Get-Property $settings 'include' '\.(md|markdown)$'
    $exclude = @(Get-Property $settings 'exclude' @())
    $cap = [int](Get-Property $settings 'cap' 0)
    $exampleCap = [int](Get-Property $settings 'exampleCap' 0)
    $extractFrom = Get-Property $settings 'extractFrom' $include
    $suffix = if ($Member.Folder) { "/$($Member.Folder)" } else { '' }

    $output = Join-Path $CorpusRoot "$category/$($Source.name)$suffix"
    $clone = Join-Path $CorpusRoot ".clones/$($Source.name)$suffix"

    # Clone: shallow, without blobs, without a checkout
    if (-not (Test-Path (Join-Path $clone '.git'))) {
        if (Test-Path $clone) { Remove-Item $clone -Recurse -Force }
        [void][System.IO.Directory]::CreateDirectory((Split-Path $clone))
        $cloneArguments = @('clone', '--depth', '1', '--filter=blob:none', '--no-checkout', '--quiet')
        if ($ref) { $cloneArguments += @('--branch', $ref) }
        Invoke-Git -Arguments ($cloneArguments + @("https://github.com/$repo.git", $clone)) | Out-Null
        Invoke-Git -Directory $clone -Arguments @('config', 'core.autocrlf', 'false') | Out-Null
        Invoke-Git -Directory $clone -Arguments @('config', 'core.longpaths', 'true') | Out-Null
    }

    $sha = (Invoke-Git -Directory $clone -Arguments @('rev-parse', 'HEAD') | Select-Object -First 1).Trim()
    $branch = (Invoke-Git -Directory $clone -Arguments @('rev-parse', '--abbrev-ref', 'HEAD') | Select-Object -First 1).Trim()

    # List the tree (no blobs needed) and choose the files before anything is downloaded
    $listing = (Invoke-Git -Directory $clone -Arguments @('ls-tree', '-r', '-z', '--name-only', 'HEAD')) -join ''
    $names = [string[]]@($listing -split "`0" | Where-Object { $_ -and $_ -notmatch '[<>:"|?*\x00-\x1f]|[. ]$|[. ]/' })
    [Array]::Sort($names, [StringComparer]::Ordinal)
    $candidates = @($names | Where-Object { Test-PathPrefix $_ $paths })
    foreach ($pattern in $exclude) { $candidates = @($candidates | Where-Object { $_ -notmatch $pattern }) }

    $whole = @()
    if ($keepFiles) { $whole = @(Select-Evenly @($candidates | Where-Object { $_ -match $include }) $cap) }
    $extractInputs = @()
    if ($extractor -ne 'none') { $extractInputs = @($candidates | Where-Object { $_ -match $extractFrom }) }

    # Download exactly those files
    $wanted = @($whole) + @($extractInputs) | Select-Object -Unique
    if (@($wanted).Count -eq 0) { throw "no files of $repo match the manifest (paths: $($paths -join ', '))" }
    $patterns = @($wanted | ForEach-Object { Get-EscapedSparsePath $_ })
    Invoke-Git -Directory $clone -Arguments @('sparse-checkout', 'set', '--no-cone', '--stdin') -InputLines $patterns | Out-Null
    Invoke-Git -Directory $clone -Arguments @('checkout', '--quiet', $branch) | Out-Null

    # Build the output from scratch
    if (Test-Path $output) { Remove-Item $output -Recurse -Force }
    [void][System.IO.Directory]::CreateDirectory($output)

    $files = 0
    $skipped = 0
    foreach ($relative in $whole) {
        $sourceFile = Join-Path $clone $relative
        if ($null -eq (Read-Utf8 $sourceFile)) { $skipped++; continue }
        $target = if ($relative -match '\.md$') { $relative } else { "$relative.md" }

        # The bytes are copied unchanged: no re-encoding, no line ending conversion
        $destination = Join-Path $output "files/$target"
        [void][System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($destination))
        [System.IO.File]::WriteAllBytes($destination, [System.IO.File]::ReadAllBytes($sourceFile))
        $files++
    }

    $examples = 0
    $extracted = 0
    if ($extractor -ne 'none') {
        $seen = [System.Collections.Generic.HashSet[string]]::new()
        $against = @(Get-Property $settings 'dedupeAgainst' @())
        if ($against.Count -gt 0) { Add-ExistingExampleHashes $CorpusRoot $AllSources $against $seen }
        $all = @(Get-Examples $extractor $clone $extractInputs $seen)
        $extracted = $all.Count
        foreach ($example in @(Select-Evenly $all $exampleCap)) {
            Write-Text (Join-Path $output "examples/$($example.Name).md") $example.Text
            $examples++
        }
    }

    @(
        "source:     $($Source.name)"
        "category:   $category"
        "repository: https://github.com/$repo"
        "branch:     $branch"
        "commit:     $sha"
        "fetched:    $([DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ'))"
        "files:      $files (skipped unreadable: $skipped)"
        "examples:   $examples (of $extracted extracted)"
    ) | Set-Content -Path (Join-Path $output 'SOURCE.txt') -Encoding utf8NoBOM

    return [pscustomobject]@{ Files = $files; Examples = $examples }
}

# --- Main --------------------------------------------------------------------------------------------------------

if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'git is required.' }
$corpusRoot = [System.IO.Path]::GetFullPath($Root)
$manifestData = Get-Content -Raw -Path $Manifest -Encoding utf8 | ConvertFrom-Json
$sources = @($manifestData.sources)
[void][System.IO.Directory]::CreateDirectory($corpusRoot)

$report = [System.Collections.Generic.List[object]]::new()
foreach ($source in $sources) {
    if ($Only -and $source.name -notin $Only) { continue }
    if ($source.category -eq 'dema') {
        Write-Host ('{0,-10} {1,-24} verified in place with --dema; not fetched' -f $source.category, $source.name)
        continue
    }

    $category = $source.category
    $existing = Join-Path $corpusRoot "$category/$($source.name)"
    $isDone = (Test-Path $existing) -and @(Get-ChildItem -Path $existing -Recurse -Filter SOURCE.txt -File).Count -gt 0
    if ($isDone -and -not $Force -and -not $Reprocess) {
        $count = @(Get-ChildItem -Path $existing -Recurse -Filter '*.md' -File).Count
        Write-Host ('{0,-10} {1,-24} {2,6} files (exists; -Force fetches again)' -f $category, $source.name, $count)
        $report.Add([pscustomobject]@{ Category = $category; Name = $source.name; Files = $count; Failed = $false })
        continue
    }

    try {
        if ($Force) {
            foreach ($path in @($existing, (Join-Path $corpusRoot ".clones/$($source.name)"))) {
                if (Test-Path $path) { Remove-Item $path -Recurse -Force }
            }
        }

        $whole = 0
        $extractedCount = 0
        $failedMembers = 0
        foreach ($member in @(Get-Members $source)) {
            try {
                $result = Invoke-Member $source $member $corpusRoot $sources
                $whole += $result.Files
                $extractedCount += $result.Examples
            }
            catch {
                # A failing member does not stop the others
                Write-Warning "$($source.name): $($member.Settings.repo): $($_.Exception.Message)"
                $failedMembers++
            }
        }

        $total = $whole + $extractedCount
        $note = if ($failedMembers -gt 0) { " ($failedMembers member(s) FAILED)" } else { '' }
        Write-Host ('{0,-10} {1,-24} {2,6} files ({3} whole, {4} extracted){5}' -f $category, $source.name, $total, $whole, $extractedCount, $note)
        $report.Add([pscustomobject]@{ Category = $category; Name = $source.name; Files = $total; Failed = ($total -eq 0 -or $failedMembers -gt 0) })
    }
    catch {
        Write-Warning "$($source.name): $($_.Exception.Message)"
        $report.Add([pscustomobject]@{ Category = $category; Name = $source.name; Files = 0; Failed = $true })
    }
}

# Totals
Write-Host ''
foreach ($group in ($report | Group-Object Category)) {
    Write-Host ('{0,-10} {1,6} files' -f $group.Name, ($group.Group | Measure-Object Files -Sum).Sum)
}

Write-Host ('{0,-10} {1,6} files in {2} sources' -f 'TOTAL', ($report | Measure-Object Files -Sum).Sum, $report.Count)
$failures = @($report | Where-Object Failed)
if ($failures.Count -gt 0) {
    Write-Warning "Sources with failures: $(($failures | ForEach-Object Name) -join ', ')"
}
