<#
.SYNOPSIS
    Fetches a corpus of public Markdown documentation into .corpus/ for the LintMark verifier.

.DESCRIPTION
    Each repository is cloned shallow, without blobs, with a sparse checkout limited to the Markdown files of the
    chosen sub-trees, so a few thousand files per repository are downloaded rather than whole repositories. The
    corpus is read-only test input for tools/LintMark.Verify and is never committed (.corpus/ is ignored by Git, by
    markdownlint and by the spell checker). The script is idempotent: a repository that is already there is left
    alone unless -Update is given. A repository that fails to clone is reported and skipped.

.PARAMETER Root
    The folder that receives the repositories. Default: .corpus in the root of this repository.

.PARAMETER Only
    Names of the repositories to fetch (see the table below); default: all.

.PARAMETER Update
    Fetch the latest commit of repositories that are already present.

.PARAMETER Jobs
    Number of repositories fetched at the same time. Default: 4.

.EXAMPLE
    pwsh tools/LintMark.Verify/fetch-corpus.ps1 -Only github-docs, rust-book

.EXAMPLE
    dotnet run --project tools/LintMark.Verify -- .corpus --sample 2000 --matrix
#>
[CmdletBinding()]
param(
    [string]$Root = (Join-Path $PSScriptRoot '..\..\.corpus'),
    [string[]]$Only,
    [switch]$Update,
    [int]$Jobs = 4
)

$ErrorActionPreference = 'Stop'

# The corpus: name, URL and the sparse-checkout patterns (gitignore syntax) that select the Markdown files
$repositories = @(
    @{ Name = 'github-docs'; Url = 'https://github.com/github/docs.git'; Patterns = @('/content/**/*.md', '/data/**/*.md') }
    @{ Name = 'azure-docs'; Url = 'https://github.com/MicrosoftDocs/azure-docs.git'; Patterns = @(
            'app-service', 'storage', 'virtual-machines', 'azure-functions', 'aks', 'cosmos-db', 'sql', 'active-directory',
            'azure-monitor', 'api-management', 'event-hubs', 'key-vault', 'container-apps', 'load-balancer', 'virtual-network' |
            ForEach-Object { "/articles/$_/**/*.md" }) }
    @{ Name = 'rust-book'; Url = 'https://github.com/rust-lang/book.git'; Patterns = @('/src/**/*.md', '/listings/**/*.md') }
    @{ Name = 'kubernetes-website'; Url = 'https://github.com/kubernetes/website.git'; Patterns = @('/content/en/**/*.md') }
    @{ Name = 'mdn-content'; Url = 'https://github.com/mdn/content.git'; Patterns = @(
            'html', 'css', 'javascript', 'api' | ForEach-Object { "/files/en-us/web/$_/**/*.md" }) }
    @{ Name = 'docusaurus'; Url = 'https://github.com/facebook/docusaurus.git'; Patterns = @('**/*.md') }
    @{ Name = 'nextjs'; Url = 'https://github.com/vercel/next.js.git'; Patterns = @('/docs/**/*.md') }
    @{ Name = 'vscode-docs'; Url = 'https://github.com/microsoft/vscode-docs.git'; Patterns = @('/docs/**/*.md', '/api/**/*.md', '/blogs/**/*.md', '/release-notes/**/*.md') }
    @{ Name = 'dotnet-docs'; Url = 'https://github.com/dotnet/docs.git'; Patterns = @(
            'csharp', 'standard', 'core', 'fundamentals' | ForEach-Object { "/docs/$_/**/*.md" }) }
    @{ Name = 'jekyll'; Url = 'https://github.com/jekyll/jekyll.git'; Patterns = @('/docs/**/*.md', '/README.md') }
    # The CommonMark specification is spec.txt; it is copied to spec.md after the checkout (stress input)
    @{ Name = 'commonmark-spec'; Url = 'https://github.com/commonmark/commonmark-spec.git'; Patterns = @('/spec.txt'); Copy = @{ From = 'spec.txt'; To = 'spec.md' } }
)

if ($Only) {
    $unknown = $Only | Where-Object { $_ -notin $repositories.Name }
    if ($unknown) { throw "Unknown repository name(s): $($unknown -join ', '). Known: $($repositories.Name -join ', ')" }
    $repositories = $repositories | Where-Object { $_.Name -in $Only }
}

New-Item -ItemType Directory -Path $Root -Force | Out-Null
$Root = (Resolve-Path $Root).Path

# The work of one repository, as text so that it can run in a parallel runspace
$work = @'
param($Repository, $Root, $Update)
$directory = Join-Path $Root $Repository.Name
$git = @('-c', 'core.autocrlf=false', '-c', 'core.longpaths=true', '-c', 'core.symlinks=false')
$result = [ordered]@{ Name = $Repository.Name; Status = 'ok'; Files = 0 }
try {
    if (-not (Test-Path (Join-Path $directory '.git'))) {
        # Shallow, blob-less, sparse: only the selected Markdown blobs are downloaded by the checkout
        $output = & git @git clone --depth 1 --filter=blob:none --sparse $Repository.Url $directory 2>&1
        if ($LASTEXITCODE -ne 0) { throw "clone failed: $($output | Select-Object -Last 3)" }
        $output = & git @git -C $directory sparse-checkout set --no-cone @($Repository.Patterns) 2>&1
        if ($LASTEXITCODE -ne 0) { throw "sparse-checkout failed: $($output | Select-Object -Last 3)" }
        $output = & git @git -C $directory checkout 2>&1
        if ($LASTEXITCODE -ne 0) { throw "checkout failed: $($output | Select-Object -Last 3)" }
        $result.Status = 'cloned'
    }
    elseif ($Update) {
        $output = & git @git -C $directory fetch --depth 1 origin 2>&1
        if ($LASTEXITCODE -ne 0) { throw "fetch failed: $($output | Select-Object -Last 3)" }
        $output = & git @git -C $directory reset --hard FETCH_HEAD 2>&1
        if ($LASTEXITCODE -ne 0) { throw "reset failed: $($output | Select-Object -Last 3)" }
        $result.Status = 'updated'
    }
    else {
        $result.Status = 'present'
    }

    if ($Repository.Copy) {
        Copy-Item (Join-Path $directory $Repository.Copy.From) (Join-Path $directory $Repository.Copy.To) -Force
    }

    $result.Files = @(Get-ChildItem -Path $directory -Recurse -File -Filter '*.md' -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '[\\/]\.git[\\/]' }).Count
}
catch {
    $result.Status = "FAILED: $($_.Exception.Message)"
}
[pscustomobject]$result
'@

$results = $repositories | ForEach-Object -ThrottleLimit $Jobs -Parallel {
    $block = [scriptblock]::Create($using:work)
    & $block $_ $using:Root $using:Update
}

$results | Format-Table Name, Status, Files -AutoSize
$total = ($results | Measure-Object -Property Files -Sum).Sum
Write-Host "Total Markdown files in the corpus: $total"
if ($results | Where-Object { $_.Status -like 'FAILED*' }) {
    Write-Warning 'Some repositories failed; the corpus is incomplete (re-run to retry them).'
}
