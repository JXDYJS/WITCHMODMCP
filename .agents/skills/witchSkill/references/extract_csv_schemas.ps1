# 重新生成 csv-schemas.md（官方 CSV 表头参考）
# 用途：游戏版本更新后，从官方模板仓库重新提取表头，刷新 references/csv-schemas.md。
#
# 用法：
#   param(
#     [Parameter(Mandatory=$true)][string]$TemplateRoot,  # 模板仓库 DataConfigs 根目录（ModTemplate/Scripts/Lib/DataConfigs）
#     [string]$Output = (Join-Path $PSScriptRoot "csv-schemas.md")
#   )
#   & .\extract_csv_schemas.ps1 -TemplateRoot "<clone>\ModTemplate\Scripts\Lib\DataConfigs"
#
# 模板仓库（MIT）：git clone https://github.com/meowalive/apocalyptic-journey-mod-tutorial.git

param(
    [Parameter(Mandatory = $true)][string]$TemplateRoot,
    [string]$Output = (Join-Path $PSScriptRoot "csv-schemas.md")
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path -LiteralPath $TemplateRoot)) { throw "TemplateRoot 不存在: $TemplateRoot" }

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# 官方 CSV 模板表头参考")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("> **来源**：官方 Mod 模板仓库 `meowalive/apocalyptic-journey-mod-tutorial`（MIT License, (c) 2026 MeowAlive）的 `ModTemplate/Scripts/Lib/DataConfigs/`。")
[void]$sb.AppendLine("> 每个表只提取「表头行 + 中文注释行」；需要完整数据或刷新时，`git clone https://github.com/meowalive/apocalyptic-journey-mod-tutorial.git` 查看。")
[void]$sb.AppendLine("> 若游戏版本更新，重新运行本脚本（extract_csv_schemas.ps1）即可刷新本文件。")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("> 注意：游戏按**列名**（非列序）读取 CSV。写 Mod 的 CSV 时列名必须与下表完全一致；不要臆造 `Cost / CardType / Damage / Defend / Magic / Heal / Buff / Exhaust / MaxLayer` 等列。")
[void]$sb.AppendLine("")

$files = @()
foreach ($section in @("Data", "Text")) {
    [void]$sb.AppendLine("---")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("## $section/")
    [void]$sb.AppendLine("")
    $sectionFiles = Get-ChildItem (Join-Path $TemplateRoot $section) -Recurse -File -Filter "*.csv" -ErrorAction Stop | Sort-Object FullName
    $files += $sectionFiles
    foreach ($f in $sectionFiles) {
        $rel   = ($f.FullName.Substring($TemplateRoot.Length + 1)).Replace('\', '/')
        $lines = [System.IO.File]::ReadAllLines($f.FullName, [System.Text.Encoding]::UTF8)
        $hdr   = if ($lines.Length -ge 1) { $lines[0] } else { "" }
        $cmt   = if ($lines.Length -ge 2) { $lines[1] } else { "" }
        [void]$sb.AppendLine("### $rel")
        [void]$sb.AppendLine('```')
        [void]$sb.AppendLine($hdr)
        if (-not [string]::IsNullOrWhiteSpace($cmt)) { [void]$sb.AppendLine($cmt) }
        [void]$sb.AppendLine('```')
        [void]$sb.AppendLine("")
    }
}

$outDir = Split-Path -Parent $Output
if ($outDir -and -not (Test-Path -LiteralPath $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }
[System.IO.File]::WriteAllText($Output, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
Write-Output ("已生成 {0} 个表 -> {1}" -f $files.Count, $Output)
