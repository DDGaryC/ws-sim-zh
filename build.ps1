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

# 編譯到暫存檔再換上：防毒軟體常在掃描剛產生的檔案時鎖住它
function Install-Built($tmp, $target) {
    for ($try = 1; ; $try++) {
        try { Move-Item -LiteralPath $tmp $target -Force -ErrorAction Stop; return }
        catch { if ($try -ge 30) { throw "$(Split-Path $target -Leaf) 被其他程式鎖住（工具還開著，或防毒軟體正在掃描），請稍後再試" }; Start-Sleep -Seconds 2 }
    }
}

# ---- 1. 工具 ----
New-Item -ItemType Directory -Force (Join-Path $Sim 'MusicTool') | Out-Null
$mtExe = Join-Path $Sim 'MusicTool\MusicTool.exe'
if (Newer $mtExe 'MusicTool.cs') {
    & $csc /nologo /target:winexe /optimize+ /codepage:65001 /nowarn:0649 "/out:$mtExe.new" `
        /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll (Join-Path $Here 'MusicTool.cs')
    if ($LASTEXITCODE -ne 0) { throw "音樂設定工具編譯失敗" }
    Install-Built "$mtExe.new" $mtExe
}
$drExe = Join-Path $Sim '舊牌組修復工具.exe'
if (Newer $drExe 'DeckRescue.cs', 'LegacyDecks.cs') {
    & $csc /nologo /target:winexe /optimize+ /codepage:65001 "/out:$drExe.new" `
        /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll (Join-Path $Here 'DeckRescue.cs') (Join-Path $Here 'LegacyDecks.cs')
    if ($LASTEXITCODE -ne 0) { throw "舊牌組修復工具編譯失敗" }
    Install-Built "$drExe.new" $drExe
}

# 主畫面音樂補丁（ReiPatcher 執行在 .NET 3.5，所以用 3.5 的編譯器）
$patchDll = Join-Path $Sim 'ReiPatcher\Patches\WSZH.MainMenuMusic.Patcher.dll'
if (Newer $patchDll 'MainMenuMusicPatch.cs') {
    $csc35 = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v3.5\csc.exe'
    if (-not (Test-Path $csc35)) { throw "找不到 .NET 3.5 的編譯器（需要在 Windows 功能裡開啟 .NET Framework 3.5）" }
    & $csc35 /nologo /target:library /optimize+ "/out:$patchDll.new" "/r:$(Join-Path $Sim 'ReiPatcher\ReiPatcher.exe')" "/r:$(Join-Path $Sim 'ReiPatcher\Mono.Cecil.dll')" /r:System.Core.dll (Join-Path $Here 'MainMenuMusicPatch.cs')
    if ($LASTEXITCODE -ne 0) { throw "主畫面音樂補丁編譯失敗" }
    Install-Built "$patchDll.new" $patchDll
}

# ---- 2. 版本號、同步 files\、manifest ----
$guideFull = [IO.File]::ReadAllText((Join-Path $Here '使用說明.txt'), [Text.Encoding]::UTF8)
$m = [regex]::Match($guideFull, '(?m)^v(\d+(?:\.\d+)+)（')
if (-not $m.Success) { throw "在使用說明.txt 的更新紀錄裡找不到版本號（格式：v1.2.3（日期））" }
$Version = $m.Groups[1].Value
"version: $Version"

# ---- 安裝程式（原始碼有變動才重新編譯）；它同時也是更新程式 wszh_updater.exe，一起放進更新清單讓它能自我更新 ----
$Installer = Join-Path $Dist 'WS模擬器中文化套件安裝程式.exe'
if (Newer $Installer 'Installer.cs', 'Online.cs', 'LegacyDecks.cs') {
    $tmpExe = Join-Path $Dist 'installer.new.exe'
    & $csc /nologo /target:winexe /optimize+ /codepage:65001 /nowarn:0414 "/out:$tmpExe" `
        /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /r:System.Web.Extensions.dll `
        (Join-Path $Here 'Installer.cs') (Join-Path $Here 'Online.cs') (Join-Path $Here 'LegacyDecks.cs')
    if ($LASTEXITCODE -ne 0) { throw "安裝程式編譯失敗" }
    # 防毒軟體常在掃描剛產生的 exe 時鎖住檔案，等它掃完再換上
    for ($try = 1; ; $try++) {
        try { Move-Item -LiteralPath $tmpExe $Installer -Force -ErrorAction Stop; break }
        catch { if ($try -ge 30) { throw "安裝程式被其他程式（通常是防毒軟體）鎖住，請稍後再試" }; Start-Sleep -Seconds 2 }
    }
    "installer rebuilt"
} else { "installer unchanged" }

# 自己這台模擬器也記錄版本並放一份更新程式
[IO.File]::WriteAllText((Join-Path $Sim 'wszh_version.txt'), $Version, $utf8)
Copy-Item $Installer (Join-Path $Sim 'wszh_updater.exe') -Force   # 要放進更新清單，複製失敗就停止

$items = @('WSLaunch.vbs', 'README_中文.txt', 'MusicTool\MusicTool.exe', '舊牌組修復工具.exe', 'wszh_updater.exe', 'ReiPatcher\Patches\WSZH.MainMenuMusic.Patcher.dll')
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
# ---- 4b. 發佈前檢查（任何一項失敗就不上傳） ----
"checking..."
$problems = New-Object System.Collections.Generic.List[string]

# (1) 翻譯檔格式：正規表示式要能解析、不能含未跳脫的 =
foreach ($tf in Get-ChildItem (Join-Path $Files 'AutoTranslator\Translation') -Recurse -Filter '_AutoGeneratedTranslations.txt') {
    $lang = $tf.Directory.Parent.Name; $ln = 0
    foreach ($line in [IO.File]::ReadAllLines($tf.FullName, [Text.Encoding]::UTF8)) {
        $ln++
        if ($line -eq '' -or $line.StartsWith('//')) { continue }
        $mm = [regex]::Match($line, '^((?:\\.|[^=\\])*)=(.*)$')
        if (-not $mm.Success) { continue }
        $key = $mm.Groups[1].Value
        if ($key.StartsWith('r:')) {
            if (-not ($key.StartsWith('r:"') -and $key.EndsWith('"') -and $key.Length -gt 4)) { $problems.Add("翻譯檔 $lang 第 $ln 行：規則格式錯誤（可能含有未跳脫的 =）"); continue }
            try { [void](New-Object regex ($key.Substring(3, $key.Length - 4).Replace('\n', "`n"))) }
            catch { $problems.Add("翻譯檔 $lang 第 $ln 行：規則無法解析") }
        }
    }
}

# 測試用的假模擬器資料夾
$chk = Join-Path $env:TEMP ("wszh_check_" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force (Join-Path $chk 'Weiss Schwarz_Data\Managed'), (Join-Path $chk 'Weiss Schwarz_Data\StreamingAssets'), (Join-Path $chk 'AutoTranslator'), (Join-Path $chk 'ReiPatcher') | Out-Null
Copy-Item (Join-Path $Sim 'Weiss Schwarz.exe') $chk
[IO.File]::WriteAllText((Join-Path $chk 'Weiss Schwarz_Data\Managed\XUnity.AutoTranslator.Plugin.Core.dll'), 'placeholder')
[IO.File]::WriteAllText((Join-Path $chk 'Weiss Schwarz_Data\StreamingAssets\Banlist.txt'), "Format Test`r`n`r`n// comment`r`nBanned AAA/W00-000`r`n")
foreach ($f in 'arialuni_sdf_u2019', 'SetupReiPatcherAndAutoTranslator.exe') { Copy-Item (Join-Path $Sim $f) $chk }
[IO.File]::WriteAllText((Join-Path $chk 'AutoTranslator\Config.ini'), "[General]`r`nLanguage=hk`r`n", (New-Object Text.UTF8Encoding $true))
[IO.File]::WriteAllText((Join-Path $chk 'wszh_version.txt'), '0.0.1', $utf8)

# (2) 啟動腳本：在假資料夾實際跑一次（不真的開遊戲）
$vbsText = [IO.File]::ReadAllText((Join-Path $Files 'WSLaunch.vbs'), [Text.Encoding]::Unicode)
$launchLine = 'sh.Run """" & dir & "\ReiPatcher\ReiPatcher.exe"" -c ""Weiss Schwarz.ini""", 0, False'
if (-not $vbsText.Contains($launchLine)) { $problems.Add("啟動腳本：找不到啟動遊戲的那一行") }
[IO.File]::WriteAllText((Join-Path $chk 'launch_test.vbs'), $vbsText.Replace($launchLine, 'WScript.Echo "LAUNCH-OK"'), [Text.Encoding]::Unicode)
$ErrorActionPreference = 'Continue'
$out = & cscript.exe //nologo (Join-Path $chk 'launch_test.vbs') 2>&1 | Out-String
$vbsExit = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
if ($vbsExit -ne 0 -or $out -notmatch 'LAUNCH-OK') { $problems.Add("啟動腳本執行失敗：" + $out.Trim()) }
$bl = [IO.File]::ReadAllText((Join-Path $chk 'Weiss Schwarz_Data\StreamingAssets\Banlist.txt'))
if ($bl -match "(?m)^[ \t]*\r?\n" -or $bl -match '(?m)^// comment') { $problems.Add("啟動腳本：沒有正確清理禁限卡表的空白行") }

# (3) 更新與還原演練（用本機 files\，不連網）
$env:WSZH_BASE = 'file:///' + $Here.Replace('\', '/') + '/'
$logFile = Join-Path $chk '中文化安裝紀錄.txt'
$p = Start-Process $Installer -ArgumentList '/update-silent', "`"$chk`"" -PassThru -Wait
$log = if (Test-Path $logFile) { [IO.File]::ReadAllText($logFile) } else { '' }
if ($p.ExitCode -ne 0 -or $log -notmatch [regex]::Escape("已更新到 v$Version")) { $problems.Add("更新演練失敗：" + $log.Trim()) }
elseif ([IO.File]::ReadAllText((Join-Path $chk 'AutoTranslator\Config.ini')) -notmatch '(?m)^Language=hk') { $problems.Add("更新演練：沒有保留使用者的語言設定") }
elseif (-not (Test-Path (Join-Path $chk 'wszh_backup\version.txt'))) { $problems.Add("更新演練：沒有產生備份") }
else {
    $p = Start-Process $Installer -ArgumentList '/rollback-silent', "`"$chk`"" -PassThru -Wait
    $ver = [IO.File]::ReadAllText((Join-Path $chk 'wszh_version.txt')).Trim()
    if ($p.ExitCode -ne 0 -or $ver -ne '0.0.1' -or (Test-Path (Join-Path $chk 'README_中文.txt'))) { $problems.Add("還原演練失敗：" + [IO.File]::ReadAllText($logFile).Trim()) }
}
$env:WSZH_BASE = $null
try { [IO.Directory]::Delete($chk, $true) } catch { }

if ($problems.Count -gt 0) {
    "發佈前檢查沒有通過，這次不會上傳："
    $problems | ForEach-Object { "  ✘ $_" }
    throw "發佈前檢查失敗"
}
"checks passed"

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
    # GitHub 會把檔名裡的中文去掉，所以上傳用英文檔名，另外加上中文顯示名稱
    $up = Join-Path $Dist 'upload'; New-Item -ItemType Directory -Force $up | Out-Null
    Copy-Item $Zip (Join-Path $up 'ws-sim-zh.zip') -Force
    Copy-Item $Manual (Join-Path $up 'ws-sim-zh-manual.zip') -Force
    Copy-Item $Installer (Join-Path $up 'ws-sim-zh-setup.exe') -Force
    $assets = @(
        "$(Join-Path $up 'ws-sim-zh.zip')#WS模擬器中文化套件（安裝程式＋使用說明）.zip",
        "$(Join-Path $up 'ws-sim-zh-manual.zip')#WS模擬器中文化套件_手動安裝包.zip",
        "$(Join-Path $up 'ws-sim-zh-setup.exe')#WS模擬器中文化套件安裝程式（單檔）.exe"
    )
    gh release view "v$Version" *> $null
    if ($LASTEXITCODE -ne 0) {
        gh release create "v$Version" @assets --title "v$Version" --notes-file $notesFile | Out-Host
    } else {
        gh release upload "v$Version" @assets --clobber | Out-Host
        gh release edit "v$Version" --notes-file $notesFile | Out-Host
    }
    if ($LASTEXITCODE -ne 0) { throw "建立 GitHub Release 失敗" }
    "published v$Version"
} finally { Pop-Location }
