# WS 模擬器 中文化套件：打包＋發佈
#
# 用法：powershell -ExecutionPolicy Bypass -File build.ps1 [-Sim 模擬器資料夾] [-NoPublish]
#
# 1. 編譯音樂設定工具、舊牌組修復工具（放進模擬器資料夾）
# 2. 把模擬器資料夾裡的中文化檔案同步到 files\，產生 manifest.txt、version.txt
#    （版本號取自「使用說明.txt」更新紀錄最上面那一條，例如 v1.1.0）
# 3. 安裝程式原始碼有變動時才重新編譯（避免檔案一直變，累積不到 Windows／防毒的信任）
# 4. 產生 dist\ 裡的兩個壓縮檔，並複製一份到桌面
# 5. 推上 GitHub（DDGaryC/ws-sim-zh），並建立／更新該版本的 Release
#    已安裝的使用者開遊戲時就會收到更新通知
param(
    [string]$Sim = (Join-Path ([Environment]::GetFolderPath('Desktop')) 'WS模擬器'),
    [switch]$NoPublish
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$Dist = Join-Path $Here 'dist'
$Files = Join-Path $Here 'files'
$Desk = [Environment]::GetFolderPath('Desktop')
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$utf8 = New-Object Text.UTF8Encoding $false
New-Item -ItemType Directory -Force $Dist | Out-Null

function Newer($target, [string[]]$sources) {
    if (-not (Test-Path $target)) { return $true }
    $t = (Get-Item $target).LastWriteTime
    foreach ($s in $sources) { if ((Get-Item (Join-Path $Here $s)).LastWriteTime -gt $t) { return $true } }
    return $false
}

# ---- 1. 工具 ----
New-Item -ItemType Directory -Force (Join-Path $Sim 'MusicTool') | Out-Null
$mtExe = Join-Path $Sim 'MusicTool\MusicTool.exe'
if (Newer $mtExe 'MusicTool.cs') {
    & $csc /nologo /target:winexe /optimize+ /codepage:65001 /nowarn:0649 "/out:$mtExe" `
        /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll (Join-Path $Here 'MusicTool.cs')
    if ($LASTEXITCODE -ne 0) { throw "音樂設定工具編譯失敗（如果工具正開著，請先關閉）" }
}
$drExe = Join-Path $Sim '舊牌組修復工具.exe'
if (Newer $drExe 'DeckRescue.cs', 'LegacyDecks.cs') {
    & $csc /nologo /target:winexe /optimize+ /codepage:65001 "/out:$drExe" `
        /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll (Join-Path $Here 'DeckRescue.cs') (Join-Path $Here 'LegacyDecks.cs')
    if ($LASTEXITCODE -ne 0) { throw "舊牌組修復工具編譯失敗" }
}

# ---- 2. 版本號、同步 files\、manifest ----
$guideFull = [IO.File]::ReadAllText((Join-Path $Here '使用說明.txt'), [Text.Encoding]::UTF8)
$m = [regex]::Match($guideFull, '(?m)^v(\d+(?:\.\d+)+)（')
if (-not $m.Success) { throw "在使用說明.txt 的更新紀錄裡找不到版本號（格式：v1.2.3（日期））" }
$Version = $m.Groups[1].Value
"version: $Version"

$items = @('WSLaunch.vbs', 'README_中文.txt', 'MusicTool\MusicTool.exe', '舊牌組修復工具.exe')
$items += Get-ChildItem (Join-Path $Sim 'AutoTranslator') -Recurse -File |
    Where-Object { $_.Name -notmatch '\.(bak\d*|orig|old)$' } |
    ForEach-Object { $_.FullName.Substring($Sim.TrimEnd('\').Length + 1) }

if (Test-Path $Files) { Remove-Item -LiteralPath $Files -Recurse -Force }
$manifest = New-Object System.Collections.Generic.List[string]
$manifest.Add("# sha256`tsize`tpath（WS 模擬器中文化套件 v$Version）")
foreach ($rel in ($items | Sort-Object)) {
    $src = Join-Path $Sim $rel
    if (-not (Test-Path -LiteralPath $src)) { throw "找不到 $src" }
    $dst = Join-Path $Files $rel
    New-Item -ItemType Directory -Force (Split-Path $dst) | Out-Null
    if ($rel -eq 'AutoTranslator\Config.ini') {
        # 發佈用的設定檔一律預設繁中（台灣）；安裝程式會依使用者選擇改寫
        $ini = [IO.File]::ReadAllText($src, [Text.Encoding]::UTF8) -replace '(?m)^Language=.*$', 'Language=tw'
        [IO.File]::WriteAllText($dst, $ini, (New-Object Text.UTF8Encoding $true))
    } else { Copy-Item -LiteralPath $src $dst }
    $hash = (Get-FileHash -LiteralPath $dst -Algorithm SHA256).Hash.ToLower()
    $manifest.Add("$hash`t$((Get-Item -LiteralPath $dst).Length)`t$($rel.Replace('\', '/'))")
}
[IO.File]::WriteAllText((Join-Path $Here 'manifest.txt'), ($manifest -join "`n") + "`n", $utf8)
[IO.File]::WriteAllText((Join-Path $Here 'version.txt'), $Version + "`n", $utf8)
"files: $($items.Count)"

# ---- 3. 安裝程式（原始碼有變動才重新編譯） ----
$Installer = Join-Path $Dist 'WS模擬器中文化套件安裝程式.exe'
if (Newer $Installer 'Installer.cs', 'Online.cs', 'LegacyDecks.cs') {
    & $csc /nologo /target:winexe /optimize+ /codepage:65001 /nowarn:0414 "/out:$Installer" `
        /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll `
        (Join-Path $Here 'Installer.cs') (Join-Path $Here 'Online.cs') (Join-Path $Here 'LegacyDecks.cs')
    if ($LASTEXITCODE -ne 0) { throw "安裝程式編譯失敗" }
    "installer rebuilt"
} else { "installer unchanged" }

# 自己這台模擬器也記錄版本並放一份更新程式
[IO.File]::WriteAllText((Join-Path $Sim 'wszh_version.txt'), $Version, $utf8)
try { Copy-Item $Installer (Join-Path $Sim 'wszh_updater.exe') -Force } catch { "（wszh_updater.exe 使用中，略過）" }

# ---- 4. 壓縮檔 ----
function New-Zip($path, [scriptblock]$fill) {
    if (Test-Path $path) { [IO.File]::Delete($path) }
    $z = [IO.Compression.ZipFile]::Open($path, 'Create')
    try { & $fill $z } finally { $z.Dispose() }
}
function Add-Text($z, $name, $text) {
    $e = $z.CreateEntry($name, 'Optimal')
    $w = New-Object IO.StreamWriter($e.Open(), (New-Object Text.UTF8Encoding $true)); $w.Write($text.TrimStart([char]0xFEFF)); $w.Dispose()
}
$Zip = Join-Path $Dist 'WS模擬器中文化套件.zip'
New-Zip $Zip {
    param($z)
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z, $Installer, 'WS模擬器中文化套件/WS模擬器中文化套件安裝程式.exe', 'Optimal')
    Add-Text $z 'WS模擬器中文化套件/使用說明.txt' $guideFull
}
$Manual = Join-Path $Dist 'WS模擬器中文化套件_手動安裝包.zip'
$manualGuide = [IO.File]::ReadAllText((Join-Path $Here '手動安裝說明.txt'), [Text.Encoding]::UTF8)
$i = $guideFull.IndexOf('更新紀錄'); $i = $guideFull.LastIndexOf('=====', $i)
if ($i -gt 0) { $i = $guideFull.LastIndexOf("`n", $i) + 1; $manualGuide = $manualGuide.TrimEnd() + "`r`n`r`n`r`n" + $guideFull.Substring($i) }
New-Zip $Manual {
    param($z)
    $root = 'WS模擬器中文化套件_手動安裝包/'
    Add-Text $z ($root + '手動安裝說明.txt') $manualGuide
    foreach ($rel in $items) {
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z, (Join-Path $Files $rel), $root + '放進模擬器資料夾/' + $rel.Replace('\', '/'), 'Optimal')
    }
}
foreach ($f in $Installer, $Zip, $Manual) {
    try { Copy-Item $f $Desk -Force } catch { "（桌面的 $(Split-Path $f -Leaf) 正在使用中，沒有更新）" }
}
"dist ready"

# ---- 5. 發佈到 GitHub ----
if ($NoPublish) { "（-NoPublish：沒有上傳）"; return }
$ErrorActionPreference = 'Continue'   # git／gh 會把進度寫到 stderr，不能當成錯誤
Push-Location $Here
try {
    git add -A
    git diff --cached --quiet
    if ($LASTEXITCODE -ne 0) {
        git commit -q -m "v$Version"
        if ($LASTEXITCODE -ne 0) { throw "git commit 失敗" }
    }
    git push -q origin HEAD
    if ($LASTEXITCODE -ne 0) { throw "git push 失敗" }

    # 這個版本的更新紀錄，當作 Release 說明
    $notes = ''
    $nm = [regex]::Match($guideFull, "(?ms)^v$([regex]::Escape($Version))（.*?(?=^v\d|\z)")
    if ($nm.Success) { $notes = $nm.Value.Trim() }
    $notesFile = Join-Path $Dist 'notes.txt'; [IO.File]::WriteAllText($notesFile, $notes, $utf8)
    gh release view "v$Version" *> $null
    if ($LASTEXITCODE -ne 0) {
        gh release create "v$Version" $Zip $Manual $Installer --title "v$Version" --notes-file $notesFile | Out-Host
    } else {
        gh release upload "v$Version" $Zip $Manual $Installer --clobber | Out-Host
        gh release edit "v$Version" --notes-file $notesFile | Out-Host
    }
    if ($LASTEXITCODE -ne 0) { throw "建立 GitHub Release 失敗" }
    "published v$Version"
} finally { Pop-Location }
