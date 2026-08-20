# publish-tools.ps1 — build WitchModMCP.Contracts.dll and push to GitHub Pages.
#
# Usage:
#   pwsh scripts/publish-tools.ps1 -PagesRepo <path> -Version 1.2.3
#
# -PagesRepo is any local clone of the GitHub repo that has Pages enabled.
# This project reuses its own source repo for Pages, so any clone works.
#
# After first run the repo will contain:
#   .nojekyll
#   manifest.json
#   WitchModMCP.Contracts.dll

param(
    [Parameter(Mandatory = $true)] [string] $PagesRepo,
    [Parameter(Mandatory = $true)] [string] $Version,
    [string] $BaseUrl,
    [string] $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot ".."))
)

$ErrorActionPreference = "Stop"

Set-Location $RepoRoot

$dllSrc = Join-Path $RepoRoot "WitchModMCP/Scripts/WitchModMCP.Contracts.dll"
$pagesRoot = (Resolve-Path $PagesRepo).Path
$dllDst = Join-Path $pagesRoot "WitchModMCP.Contracts.dll"
$manifestPath = Join-Path $pagesRoot "manifest.json"

# 1. Build only the Tools part (fast — does not rebuild the framework).
Write-Host "[publish] building Tools part..." -ForegroundColor Cyan
& dotnet build "$RepoRoot/WitchModMCP.csproj" -p:BuildPart=Tools -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)" }

if (-not (Test-Path $dllSrc)) { throw "built DLL not found at $dllSrc" }

# 2. Copy DLL into the Pages repo.
Write-Host "[publish] copying DLL to $dllDst" -ForegroundColor Cyan
Copy-Item -LiteralPath $dllSrc -Destination $dllDst -Force

# 3. Compute SHA256 + size, write manifest.json.
$bytes = (Get-Item $dllDst).Length
$hash = (Get-FileHash $dllDst -Algorithm SHA256).Hash.ToLowerInvariant()
if (-not $BaseUrl) {
    $BaseUrl = (Read-Host "Pages base URL (e.g. https://JXDYJS.github.io/WITCHMODMCP)").TrimEnd('/')
} else {
    $BaseUrl = $BaseUrl.TrimEnd('/')
}
$manifest = @{
    version = $Version
    tools   = @{
        url    = "$BaseUrl/WitchModMCP.Contracts.dll"
        sha256 = $hash
        size   = $bytes
    }
} | ConvertTo-Json -Depth 5
$manifest | Set-Content -Path $manifestPath -Encoding UTF8 -NoNewline
Write-Host "[publish] wrote manifest.json ($bytes bytes, sha256=$($hash.Substring(0,8))...)" -ForegroundColor Green

# 4. Git commit + push inside the Pages repo.
Set-Location $pagesRoot

# First run only: drop .nojekyll so GitHub Pages serves raw files (not Jekyll-rendered).
if (-not (Test-Path ".nojekyll")) {
    "" | Set-Content -Path ".nojekyll" -NoNewline
    Write-Host "[publish] created .nojekyll (one-time)" -ForegroundColor Yellow
}

& git add WitchModMCP.Contracts.dll manifest.json .nojekyll
& git commit -m "tools: $Version"
if ($LASTEXITCODE -ne 0) { throw "git commit failed" }
& git push
if ($LASTEXITCODE -ne 0) { throw "git push failed" }

Write-Host "[publish] done. Published $Version." -ForegroundColor Green
