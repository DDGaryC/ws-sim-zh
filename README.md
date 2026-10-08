# WS 模擬器 中文化套件（ws-sim-zh）

Blake Thoennes 的 Weiss Schwarz 模擬器（Weiss Schwarz Simulator）非官方中文化套件。

- 介面翻譯：繁體中文（台灣）／繁體中文（香港）／简体中文
- 中文版啟動捷徑：模擬器更新後自動恢復中文；中文化有新版本時，開遊戲會詢問是否更新
- 音樂設定工具：替換對戰音樂、牌組編輯器音樂、音效，設定循環點
- 舊牌組修復工具：用正確的中文名稱還原舊版模擬器（登錄檔）的牌組

卡片名稱與卡片效果文字不翻譯。

## 下載

到 [Releases](../../releases/latest) 下載（或直接用固定連結：[ws-sim-zh.zip](https://github.com/DDGaryC/ws-sim-zh/releases/latest/download/ws-sim-zh.zip)）：

| 檔案 | 說明 |
|---|---|
| `ws-sim-zh.zip` | 一般使用者：安裝程式＋使用說明。安裝時會從網路下載最新的中文化檔案 |
| `ws-sim-zh-manual.zip` | 手動安裝包：電腦開著 Windows「智慧型應用程式控制」、或安裝程式被防毒擋住時使用 |

安裝程式沒有數位簽章，第一次執行時 Windows 可能會出現警告，按「其他資訊」→「仍要執行」即可。

## 內容來源

- 中文化檔案：本儲存庫的 [`files/`](files/)
- 翻譯插件：[XUnity.AutoTranslator](https://github.com/bbepis/XUnity.AutoTranslator)（MIT），安裝時從官方 Releases 下載
- 中文字型 `arialuni_sdf_u2019`：XUnity.AutoTranslator 官方提供的 TextMeshPro 字型包，安裝時從官方 Releases 下載

本專案與 Bushiroad、模擬器作者無關。Weiß Schwarz 為 Bushiroad 所有。

## 原始碼

| 檔案 | 內容 |
|---|---|
| `Installer.cs`、`Online.cs` | 安裝／更新程式 |
| `MusicTool.cs` | 音樂設定工具 |
| `DeckRescue.cs`、`LegacyDecks.cs` | 舊牌組修復工具 |
| `build.ps1` | 打包與發佈腳本 |
| `使用說明.txt`、`手動安裝說明.txt` | 使用者說明（含更新紀錄） |
