# WeissSim AI 腳本說明（中文版）

> 原文：`Weiss Schwarz_Data\StreamingAssets\AIData\AI_Scripting_Guide.md`
> 翻譯原則：函式名稱、指令、語法、範例程式碼、卡名一律保留原文（腳本裡就是要這樣寫）；說明文字改寫成中文，盡量好懂而不是逐字硬翻。
> 用語：休息室＝Waiting Room、能量區＝Stock、傷害區＝Clock、名場面＝Climax（CX）、再演＝Encore、判定＝Trigger、正置／橫置／倒置＝Stand／Rest／Reverse、舞台前列＝Center Stage、舞台後列＝Backstage、助太刀＝Backup、集中＝Brainstorm、回想區＝Memory、洗牌重置＝Refresh、開機＝Standby、木門＝Door、城門＝Gate、箭頭＝Choice。

這份文件說明 `AIVersion6`（V6）和 `AIVersion7`（V7）讀取的 AI 文字腳本：檔案結構、運算式、各種區段、卡片標籤、動作指令，以及共用的狀態／機率函式。

「預估資源」、「戰鬥規劃」和「再演」相關的更新都屬於 V7，並不是另一個「V8」格式。下面標註 **V7** 的功能需要新版的 `AIVersion7` 才有，不要假設舊版 V7 或 V6 也支援。

最好的範例就是 `AIData` 資料夾裡現成的腳本。`AIList.txt` 負責把牌組對應到腳本檔和 AI 版本。這份文件**不**涵蓋 AI Version 5 那套不同的腳本系統。

程式實作參考（模擬器原始碼，發佈版裡沒有）：`AIVersion6.cs`、`AIVersion7.cs`、`AI.cs`、`Enums.cs`。

---

## 1. 選擇 AI 腳本

`AIList.txt` 的每一筆長這樣：

```text
Deck AI_Emilia Book
Name Re:Zero Blue Emilia
File Emilia Book AI
Version 6
```

- `File`：腳本檔名（不含副檔名），例如上面會讀 `AIData\Emilia Book AI.txt`。
- `Version`：決定用哪一套解析器和決策程式。本文件的語法要用 6 或 7。

## 2. 檔案結構

AI 腳本由一連串具名的「區段（Section）」組成。區段名稱不縮排、不巢狀；大括號 `{ }` 只用在動作區段和問題區段內的程式區塊。

```text
Section Header
ColorPriority Red Blue
Experience 0
ActMode Manual
BackstageMode Auto
ClimaxMode Auto

Section FirstTurnDiscard
KeepIf IsCharacter && CardLevel = 0
KeepCopies(1) [Example Character]

Section SearchValue
Default(100)
Priority(500) [Example Character] && Level >= 1

Section [Example Character]
AutoIf(1) Level >= 1 && Stock >= 1
```

- 以 `//` 開頭的行是註解，載入時會被移除。建議整行寫註解，不要寫在行尾。
- 空白行會被忽略。
- 名稱和列舉型指令大致不分大小寫；但 V7 比對卡名比 V6 嚴格。

### Header（標頭）

| 項目 | 意義 |
| --- | --- |
| `ColorPriority` | AI 在做顏色／升級選擇時偏好的顏色順序。 |
| `Experience` | AI 選升級卡時想達到的經驗值目標。 |
| `ActMode` | `Auto`＝AI 自動使用起動能力（ACT）；`Manual`＝交給腳本的動作指令處理。 |
| `BackstageMode` | `Auto`＝自動放後列。 |
| `ClimaxMode` | `Auto`＝自動選名場面。 |

V7 另外支援 `NeverPlayFromHandTags: TagA, TagB`：有任一列出標籤的卡，AI 一般從手牌打出時不會選它。注意這不會禁止透過其他效果或動作把它放上場。

## 3. 卡片引用與卡片區段

卡片區段寫成 `Section [卡名]`，裡面放這張卡的設定：

```text
Section [Example Character]
AutoIf(1) Stock >= 1
AutoTarget(1,Standing && If(InLocation CenterStage))
Tags: Mill, StandbyTarget
```

- 卡名要和遊戲裡顯示的**完全一樣**（英文卡名）。中括號內的逗號算卡名的一部分。
- 在運算式中寫 `[卡名]`，意思是「目前判斷的這張卡是不是這張」。
- V6 的中括號可以寫卡名或卡號，也會統一 `《》` 和 `«»`。V7 的卡片區段和簡寫標籤只認卡名，要兩版都能用就寫卡名。
- V6 有拼字檢查，牌組裡每張卡都應該有自己的區段。V7 用卡片區段的設定來讀標籤和特殊標記。AI 初始化時，遊戲可能會回報缺少區段或找不到卡名。

## 4. 運算式語法

條件會回傳數值，**大於 0 就算成立**；比較運算回傳 true/false。

```text
Level >= 2
Stock > 0 && HandContains(1, IsClimax)
Not Have(1, [Example Character])
ChanceToHit(4, IsClimax) >= 0.5
```

- 比較：`=`、`==`、`!=`、`<`、`<=`、`>`、`>=`（`==` 會被當成 `=`）。
- 邏輯：`&&`、`||`、`&&&`、`|||`；否定可寫 `Not 運算式` 或 `!運算式`。
- 解析器會**先**找 `|||`、`&&&`，再找 `||`、`&&`。所以當外層要包住「帶參數的卡片條件」（特別是丟給卡片效果解析器的 `If(...)`）時，用三個字元的版本當外層，就不會被誤切：

```text
If(LocationContains WaitingRoom 1 IsCharacter &&& Level >= 2)
```

- 函式參數用逗號分隔，但小括號、中括號內的逗號不會切開參數，所以這樣是合法的：

```text
CountCards(Stage, [Example Name, Character])
```

- 數值支援數字、正負號、`+`、`-`、`*`。**沒有除法**。不要依賴複雜的運算優先順序，用簡單的項或巢狀函式。
- `If(運算式)`：V7 如果認得是 AI 語法（包括自訂標籤）就直接用 AI 語法計算，否則丟給遊戲的「卡片效果條件解析器」。V6 一律丟給卡片效果解析器。要跨版本相容，請優先用下面列的 AI 條件函式。
- 只有卡片效果才有的判斷（例如 `HasMarker`）必須個別用 `If` 包起來：要選「沒有標記的卡」寫 `Not If(HasMarker)`。直接寫 `HasMarker` 不是合法的 AI 條件。

## 5. 條件函式

以下函式名稱不分大小寫。除非特別說明，都可以當條件用，也可以當數值比較。

### 基本狀態與數量

| 函式 | 結果 |
| --- | --- |
| `True`、`False` | 常數 1 或 0。 |
| `GoingFirst` | 只有在「整場第一回合、且 AI 是先攻」時為 1。 |
| `MyTurn`、`OpponentsTurn` | 現在是不是 AI／對手的回合。 |
| `Level`、`OpponentLevel` | 等級（`OpponentsLevel` 是別名）。 |
| `Clock`、`OpponentClock` | 傷害區張數。 |
| `Stock`、`OpponentStock` | 能量區張數。 |
| `FreeStock` | V7：扣掉 `ReserveStock` 保留量後，可用來出卡的能量。 |
| `RefreshCount` | V7：從開局到現在完成幾次洗牌重置（含規則重置和把休息室洗回牌庫的效果）；0＝還在第一副牌庫。 |
| `FirstTurnAtLevel(N)` | V7：自己第一次在等級 `N` 的準備回合（包含在抽牌、傷害、主要階段升級）。如果是在對手回合或自己主要階段之後才升到該等級，就從下個自己回合開始算，所以被戰鬥／再演打上去不會吃掉第一次出卡的機會。N 必須是非負整數。 |
| `AttackTarget` | V7：本主要階段的攻擊數目標，上限 3（受引擎、開局規則、腳本的出卡／攻擊限制影響）。不在主要階段時，不會沿用上回合腳本設的開局限制。 |
| `AttackersAvailable(條件[, 預計花費能量])` | V7：估計可攻擊的角色數＝符合條件、可行動的場上角色＋付得起的手牌角色，上限 3。**不含目前判斷的這張卡**（和 `Have` 一樣，連它佔的手牌位置也排除），所以可以在傷害／丟牌優先度裡用它保護「湊三攻需要的身體」。手牌候選共用同一份能量預算，並遵守主要階段的手牌／能量保留。第二個參數可以先扣掉某個效果要付的能量。不在主要階段時，把橫置的存活角色當成下回合的身體，並用等級／顏色／能量＋AI 出卡限制估計手牌能不能出。不計算未來的檢索、替換或以橫置狀態放上場的角色。 |
| `OpenStageForNormalPlay` | V7：有空的舞台位置可一般登場時為真（遵守腳本設的主要階段前列上限）。已有角色的位置不算。 |
| `Deck`、`OpponentDeck` | 牌庫張數。 |
| `Hand`、`OpponentHand` | 手牌張數。 |
| `OpenBackstage`、`OpenCenterStage` | 空位數（前列的倒置位置也算空位）。 |
| `PlayableCharactersInHand` | 照費用由低到高，大約能從手牌出幾隻角色。新版 V7 會共用預估的能量／手牌預算，主要階段時還會考慮可登場的位置數，並遵守 AI 出卡限制。**不是**「各自付得起的卡有幾張」。 |
| `UnwantedCardsInHand` | 依 SearchValue 和 WRValue 的差距算出的「不想要的手牌」張數。 |
| `AttackingCharacters` | 目前預定的攻擊數。 |
| `ClimaxIsPlanned` | AI 已經預定好要打名場面時為 1。 |

### 預估資源與下回合預測（V7）

這些都是估計值，不保證未知的抽牌或未來效果。本回合的預估用目前的規劃狀態；下回合預估用目前已知的手牌，並依下表的假設。

| 函式 | 結果 |
| --- | --- |
| `ProjectedHand` | 手牌數減去「保護的最低手牌數」（最低 0）。保護量包含 `ReserveHand`，以及手上有預定名場面時至少保留 1 張。不會扣掉所有未來的選擇性丟牌。 |
| `ProjectedStock` | 能量減去 `ReserveStock`（最低 0），和 `FreeStock` 相同。不會自動扣掉所有預定的 ACT/AUTO 費用。 |
| `ProjectedPlayableCharacters` | 和新版 `PlayableCharactersInHand` 同一套估算：考慮預估能量、手牌保留、AI 出卡限制、主要階段登場位置後，累計付得起的手牌角色數。 |
| `PlayableCharactersNextTurn` | 估計下回合能「一起」出的手牌角色數（費用由低到高）。用預估等級、目前的顏色條件、AI 永不出卡規則，以及「目前能量＋預估剩餘判定數」的共用能量預算。不考慮未來的舞台空位和手牌保留。 |
| `ExpectedLevelNextTurn` | 目前等級，若傷害區 ≥ 5 就 +1，上限 3。只是粗估，不是真的預測傷害或傷害區決策。 |
| `ExpectedStockPaymentsNextTurn(角色條件, 一能量ACT次數)` | 下回合最多能付幾次能量（上限 3）：符合條件的手牌角色＋你給定次數的「花 1 能量的 ACT」。用目前能量、預估等級、目前顏色、AI 永不出卡規則。 |
| `ProjectedDeckSizeAfterAttacks` | 剩下的判定都翻完後，自己牌庫大約剩幾張。如果判定數超過牌庫，會模擬一次用目前休息室洗回的重置，並扣掉一般的重置懲罰（除非被忽略）。不預測其他效果造成的磨牌、抽牌或重置內容變化。 |

```text
Section SearchValue
Priority(5000) IsCharacter && CanDeployCharacter && ProjectedPlayableCharacters < 3

Section [Optional Filter]
AutoIfAndOnlyIf(1) ProjectedHand >= 4 && ProjectedStock >= 1
```

`ExpectedStockPaymentsNextTurn` 一定要給兩個參數：

1. 卡片條件，會對手牌每張候選卡分別判斷；寫 `True` 表示其他條件符合的角色都算。
2. 數值運算式，代表「**花 1 能量的 ACT 次數**」（非負整數）——不是效果編號，也不是任意 ACT 的次數。參數數量錯或次數不合法會回報 AI 檔案錯誤。

ACT 的預測由腳本自己負責：只計入下回合真的有機會付得起能量和其他費用（橫置、丟牌等要分開算資源）的啟動次數。這個函式不會自己去找 ACT，也不會驗證你給的次數是否存在。超過 3 會被截到 3，因為它本來就是用在「三能量再演清能量」的比較。

```text
// 假設下回合可以用一次符合條件的 1 能量 ACT。
ExpectedStockPaymentsNextTurn(Not Backup,1)

// 每隻存活、符合條件的引擎各算一次啟動（假設每隻都橫置自己）。
ExpectedStockPaymentsNextTurn(True,CountCards(Stage,[One-Stock Brainstorm] && Not Reversed && Not If(HasAppliedEffect CannotUseOwnAct)))
```

付得起的組合共用同一份能量：有 3 能量時，兩隻 2 費角色只算 2 次付款，不是 4 次；一隻 2 費角色加一次 1 能量 ACT 可以算 3 次。結果是 3 表示「至少能付 3 次」，不一定剛好花掉 3 能量。

這個預測不保留舞台位置或手牌、不預測新抽到或檢索到的卡、不驗證所有未來出卡限制、不模擬對手干擾，也允許需要覆蓋登場的估計。它不計算自動的能量輪替（例如 Laplace），需要的話在腳本裡另外加。和 `PlayableCharactersNextTurn` 不同，它不會加上攻擊判定預期得到的能量。**需要「下回合一定合法」的規則不要用這些估計值。**

### 卡片與區域判斷

| 函式 | 語法與結果 |
| --- | --- |
| `Have(數量, 條件)` | 手牌或場上（非倒置）至少有這麼多張符合。不算目前這張卡，也不算名場面區。 |
| `HandContains(數量, 條件)` | 手牌至少有這麼多張符合。 |
| `WRContains(數量, 條件)` | 休息室至少有這麼多張符合。 |
| `StageContains(數量, 條件)` | 自己舞台至少有這麼多張符合。 |
| `OpponentStageContains(數量, 條件)` | 對手舞台至少有這麼多張符合。 |
| `OpponentCenterStageContains(數量, 條件)` | 對手前列至少有這麼多張符合。 |
| `DeckContains(數量, 條件)` | 自己目前牌庫裡至少有這麼多張符合。 |
| `CountCards(區域, 條件)` | 指定區域（`GameLocation`）中符合條件的張數。 |
| `DeckIncludes(數量, 條件)` | 原始牌表裡是否至少有這麼多張符合。 |
| `NumberIncludedInDeck(條件)` | 原始牌表中符合條件的張數。 |
| `HasLabel(標籤)` | 目前這張卡有沒有某個 AI `CardLabel`。 |
| `HasAutoWithLabel(標籤)` / `HasAutoLabel(標籤)` | 目前這張卡有沒有某個時機標籤的 AUTO 效果。V7 不分大小寫、接受 `HasAutoLabel` 別名、會驗證卡片情境，且不會偷看對手未公開的卡。 |
| `HasEffectCommand(類別, 指令[, 參數開頭])` | V7：檢查目前的 `Auto`、`Act`、`Act_Backup` 或 `Cont` 能力裡有沒有某個引擎指令（可再比對參數開頭）。包含目前被賦予的能力；不含尚未生效的巢狀 `GainEffect`/`GainChosenEffect` 區塊；不看對手未公開的卡或失去能力的卡。 |
| `Facing(條件)` | 和目前這張卡正面相對的角色是否符合條件。 |
| `FacingCharactersPower` | 正面相對角色的戰力，沒有則為 0。 |
| `OnStage`、`Standing`、`Rested`、`Reversed` | 目前這張卡在場上／正置／橫置／倒置。 |
| `IsCharacter`、`IsEvent`、`IsClimax` | 卡片種類。 |
| `CardLevel`、`CardCost`、`CardPower`、`CardSoul` | 這張卡的等級、費用、戰力、魂。 |
| `ColorIs(顏色)` | 這張卡是不是該顏色。 |
| `HaveColor(顏色)` | 玩家是否滿足該顏色的出卡條件。要寫列舉名稱，如 `HaveColor(Blue)`、`Red`、`Yellow`、`Green`、`Purple`，不要寫卡資料的縮寫 `B`。 |
| `CanPlayFromHand` | 引擎的原始合法性檢查（這張手牌能不能出）。V7 的 AI 決策另外還會套用 `NeverPlayFromHand`。 |
| `AttackingCharacter` | V7：候選卡就是它主人目前正在攻擊的角色時為真（包括已橫置、攻擊還在結算中的）。 |
| `CanDeployCharacter` | V7：這張角色能不能從手牌或回收區域登場。主要階段時檢查引擎合法性、顏色、費用、AI 出卡限制和資源保留（回收的卡會多佔一格手牌）；不在主要階段時用等級／顏色／能量估計。不預測檢索費用、未來的顏色變化或未來的提前登場條件。 |
| `WonAllCenterLanes` | V7：看「目前存活的場面」：自己前列三格都有非倒置角色，且對面三格都是空的或倒置。這不是戰鬥歷史；對手有角色再演回來就會變成 false。 |
| `CanWinUncoveredLane(戰力)` | V7：給定的預估戰力，能不能嚴格贏過「其他正置前列角色都打不贏的那一格對手」。依目前場面估計，不會窮舉所有手牌出法。 |
| `CanStandbyToCenterStage` | 開機能不能放到前列。 |
| `CanRun`、`CanBounce` | 對已知的移動／彈回效果做的粗略判斷，不是完整規則模擬。 |

### 戰鬥、優先度與比例

| 函式 | 結果 |
| --- | --- |
| `CanReverseOpponent` | 目前這張卡符合資格，且預估戰力達到對面角色的戰力時為 1。 |
| `WillBeReversed` | 新版 V7：用預估的攻擊方戰力和雙方的炸彈效果檢查預定的正面攻擊。直接攻擊和已知會側面攻擊的計畫不算會被倒置；沒有對手的會被忽略。不預測所有未來的回應。 |
| `OpponentLowestPower` | 對手前列中、還沒被指定為攻擊目標的最低戰力；受 Great Performance 影響。 |
| `OpponentUnbeatableCharacters` | 舊的條件項目，使用前請確認目前的實作行為。 |
| `GetPriority(區段)` | V7：對目前這張卡計算某個內建或自訂優先度區段。區段名稱不分大小寫；找不到自訂區段或遞迴計算會回報 AI 檔案錯誤。 |
| `ClimaxRatio`、`OpponentClimaxRatio` | 估計牌庫中的名場面比例（受底層計算的可見度限制）。 |
| `EstimatedClimaxRatio` | V7：只根據「已知資訊」估計自己名場面密度（含已知牌庫頂和未知的牌庫／能量池）；和舊的 `ClimaxRatio` 不同，不會用到未公開的牌庫內容。 |
| `RemainingTriggerChecks` | V7：前列正置角色＋目前這次攻擊（還沒判定時）剩下幾次判定。會算入 `TriggerChecks` 設定和生效中的雙判定效果。 |
| `AttacksRemaining` | 新版 V7：剩下幾次合法攻擊＝min(3, 腳本攻擊上限, 遊戲最大值) − 已攻擊次數（最低 0）。算的是攻擊次數，不是判定次數。 |
| `WillAttackLast` | 新版 V7：目前這張卡是不是引擎排定的待攻擊角色中的最後一個。順序和攻擊階段的宣告順序相同；沒有卡片情境或沒有待攻擊角色時為 false。不要在 `AttackOrder` 裡用（會變成順序依賴自己）。 |
| `OwnClimaxesTriggered` | 新版 V7：本回合／上個自己回合判定出幾張名場面。對手回合期間會保留，到自己下回合開始才歸零，所以對手回合的再演可以參考。算的是判定出的名場面張數（含額外判定），不是雙魂圖示，也不代表那些名場面還在能量區。 |
| `MyBattlingCharactersPower`、`OpponentBattlingCharactersPower` | 目前戰鬥雙方的戰力，沒有則為 0。 |

### 能量區與牌庫頂的已知資訊

這些函式用的是「看過或推理出來的資訊」，不會把所有隱藏卡都當成已知。

| 函式 | 結果 |
| --- | --- |
| `StockCxDepth`、`OpponentStockCxDepth` | 能量區中最近一張「已知名場面」要付幾次能量才會付到；不知道有名場面則為 0。 |
| `KnownCxInTopStock(N)` | 新版 V7：最近的已知名場面在第 1～N 次付款的深度內。例如 `KnownCxInTopStock(3)` 表示正常付 3 能量會付到已知名場面；更深或不確定的不算。 |
| `KnownCxInStock`、`OpponentKnownCxInStock` | 能量區中已確認的名場面張數。 |
| `ConfirmedCleanStockDepth`、`OpponentConfirmedCleanStockDepth` | 能量區頂端連續幾張確定不是名場面。如果所有名場面都已知在能量區以外，未知的能量也可推定為乾淨。 |
| `KnownTopDeckCount`、`OpponentKnownTopDeckCount` | 牌庫頂連續已知的張數。 |
| `KnownTopCardIs(條件)`、`OpponentKnownTopCardIs(條件)` | 只有在牌庫頂那張確定已知且符合時為 1，否則為 0。 |

牌庫頂記憶會在支援的公開／重排／預覽和已知卡移動時更新；抽牌、磨牌、判定會把已知的部分消耗掉，洗牌會清空。它不是「所有看過的卡」的完整紀錄。

### 機率函式

| 函式 | 語法與結果 |
| --- | --- |
| `ChanceToHit(N, 條件)` | 接下來 N 張中至少有一張符合的機率。 |
| `ExpectedHits(N, 條件)` | 接下來 N 張中符合的期望張數。 |
| `ChanceNextCard(條件)` | 下一張符合的機率；牌庫頂已知時是精確值。 |
| `OpponentChanceToHit(N, 條件)` | 對手版本。目前只有 `IsClimax` 能估計未知卡；其他條件只看已知的牌庫頂，未知部分沒有意義。 |
| `OpponentExpectedHits(N, 條件)` | 同上限制。 |
| `OpponentChanceNextCard(條件)` | 同上限制。 |
| `MillChance(N, 條件)` | 舊別名，等同自己牌庫的 `ChanceToHit(N, 條件)`。 |

自己的估計用「已知牌表」和「牌庫外的已知卡」。對手的估計不能偷看隱藏卡。除了名場面以外，對手的估計請當成未知，除非相關的牌庫頂已經公開過。

### 效果使用紀錄

`N` 是卡上的 AUTO 或 ACT 效果編號，各自從 1 開始數。

| 函式 | 結果 |
| --- | --- |
| `UsedAuto(N)` | 本回合這張卡的 AUTO N 已結算過則為 1。 |
| `TimesUsedAuto(N)` | 本回合 AUTO N 結算了幾次。 |
| `UsedAct(N)` | 等同 `TimesUsedAct(N) >= 1`。 |
| `TimesUsedAct(N)` | 本回合 ACT N 結算了幾次。 |

這些只算「已完成」的效果，排隊中、預定中、打算要用的都不算。換回合時歸零。

### 效果可用性（V7）

兩個函式都需要目前卡片情境，以及一個正整數效果編號（各類別從 1 開始）。

| 函式 | 結果 |
| --- | --- |
| `CanUseAuto(N)` | AUTO N 存在、這張卡沒被禁止使用自己的 AUTO、且目前的關鍵字／腳本規則允許。會參考 `AutoIf`、`AutoIfAndOnlyIf`、`DontUseAuto`。**不**代表時機已經觸發、選擇性費用付得起或有合法目標。 |
| `CanUseAct(N)` | ACT N 目前是合法的 `UseAct` 動作，且沒被卡片或關鍵字規則擋住。這是「現在這個階段」的合法性，不是下回合預測；不在可以用 ACT 的時機時通常是 false。 |

```text
Section [Example Character]
AutoIfAndOnlyIf(2) CanUseAuto(1) && AttacksRemaining >= 1
```

不要在同一個 AUTO 自己的規則裡寫 `CanUseAuto(N)`——計算可用性時會去讀它的規則，造成遞迴。用來檢查「另一個」效果，或放在別的決策情境。

### 讀取能力結構（V7）

一般的「倒置對手時」連動用時機標籤判斷；防禦型能力（相關效果可能還沒生效）用指令結構判斷：

```text
HasAutoWithLabel(OnReverseOpponent)
HasEffectCommand(Cont,GainSpecialEffect,ReceiveNoDamageFromAutoEffects)
HasEffectCommand(Cont,LoseSoul,2)
```

指令讀取只是「結構上有沒有這個能力」的檢查，不是效果模擬。例如「`If InBattle` 之後接傷害護盾」的 CONT，在戰鬥前就會被認出來，但不會計算或預測條件。不保證費用、使用次數限制或指令目標。未來 `GainEffect` 區塊裡的指令，要等能力真的被賦予後才算。複雜或牌組特有的分類請用自訂標籤。

指令名稱和參數不分大小寫。`LoseSoul,2` 不會匹配 `LoseSoul 20`，`GainPower` 也不會匹配其他名字的指令。第三個參數省略＝任何參數都符合；或給開頭序列，例如 `ReceiveNoDamageFromAutoEffects` 來比對特定的特殊效果。未知的類別、指令或格式錯誤的參數會回報 AI 檔案錯誤。只有「指令解析」會被快取，能力清單每次都重新查詢，所以被賦予／移除的能力會即時反映。

### 對位與戰力預測

| 函式 | 語法與結果 |
| --- | --- |
| `PredictedPowerIfPlayed` | 在目前可用的前列格子中，最佳的預估戰力。 |
| `PredictedPowerInLane(Left)` | 指定格子（`Left`、`Middle`、`Right`）的預估戰力。 |
| `CanWinLane(格子)` | 預估戰力**嚴格大於**對面時為 1。平手不算贏。 |
| `WouldTieLane(格子)` | 預估戰力相等時為 1。 |
| `WouldLoseLane(格子)` | 自己預估戰力較低時為 1。 |

預測包含引擎現有的戰力估計和支援的「預定加攻」。只是規劃用的預測，不是任意效果或對手未來回應的完整模擬。

### 洗牌重置與傷害預測

| 函式 | 結果 |
| --- | --- |
| `CardsUntilRefresh`、`OpponentCardsUntilRefresh` | 目前牌庫張數。 |
| `WouldRefreshAfterDraw(N)` | N（正數）≥ 目前牌庫張數時為 1。 |
| `WouldRefreshAfterMill(N)` | 同上，用於磨牌。 |
| `WouldRefreshAfterTriggers(N)` | 同上，用於判定。 |
| `OpponentWouldRefreshAfterDraw(N)` / `OpponentWouldRefreshAfterTriggers(N)` | 對手版本。 |
| `KnownCxBeforeRefresh`、`OpponentKnownCxBeforeRefresh` | 在牌庫翻完前、已知牌庫頂裡有幾張名場面。 |
| `ChanceDamageCancels(N)` | 自己受到一般 N 點傷害時被取消的機率。 |
| `ExpectedDamage(N)` | `N ×（1 − 取消機率）`，一般「全有或全無」的傷害模型。 |
| `OpponentChanceDamageCancels(N)` | 對手受傷被取消的機率。 |
| `OpponentExpectedDamage(N)` | 對手預期受到的未取消傷害。 |

傷害預測有考慮重置的邊界和重置懲罰那一張，但**沒有**考慮所有傷害修正、防傷、不能取消、或特殊燒傷的取消規則。當作一般傷害的估計就好，不是規則保證。

## 6. 可比較數值（Comparable Values）

`TestValue` 也接受遊戲 `ComparableValue` 列舉裡的名稱，這些是引擎本身的數值，例如：`ThisCardsLevel`、`ThatCardsPower`、`CardsInDeck`、`CardsInWaitingRoom`、`TargetCount`、`X`、`Experience`、`Answer`、`CancelsThisTurn`、`ActsUsedThisTurn`、`CardsAddedByEffect`、`CardsFlippedDuringLastCancel`、`AI_FreeStock`、`AI_FreeRest`。

完整清單在原始碼 `Enums.cs` 的 `ComparableValue`。依賴目標、戰鬥或目前效果的數值會隨情境變化，請在 AI 實際使用的同一個效果／目標情境下測試。

## 7. 優先度區段（Priority Sections）

優先度區段在特定決策情境下替卡片打分數。常見的有：

| 區段 | 用途 |
| --- | --- |
| `GoingFirst_Attacker`、`GoingFirst_ExtraCenterStage`、`GoingFirst_Backstage` | 先攻第一回合的攻擊手／額外前列／後列選擇 |
| `AttackerValue` | 當攻擊手的價值 |
| `ReverseValue` | 倒置對手角色的價值 |
| `DefenderValue` | 當防守方的價值 |
| `BackstageValue` | 放後列的價值 |
| `SearchValue` | 檢索／回收時的價值（越高越想拿） |
| `ProtectValue` | 保護的價值 |
| `WRValue` | 丟進休息室的價值（越高越願意丟） |
| `AttackOrder` | 攻擊順序 |
| `ClockValue` | 放傷害區的價值 |
| `LevelValue` | 升級時選它的價值 |
| `DirectAttackValue` | 直接攻擊的價值 |
| `OverplayValue` | 被覆蓋登場時的可犧牲程度 |
| `StandbyValue` | 開機目標的價值 |
| `TopDeckValue` | 放牌庫頂的價值 |

每行一個指令：

```text
Section AttackerValue
Default(100)
Priority(500) [Preferred Attacker] && Level >= 1
PriorityBonus(50) HasLabel(Backup)
DuplicatePriority(-200) [Unique Engine]
```

- `Default(n)`、`Priority(n)`：設定目前分數。所有符合的行**依檔案順序**都會計算，所以**最後一個符合的設定會勝出**。
- `PriorityBonus(n)`：在目前分數上加分。
- `DuplicatePriority(n)`：只有當 `Have(1,[這張卡])` 已經數到一張同名卡時才設定。

V7 也允許自訂名稱的優先度表，用 `GetPriority(名稱)` 查詢。自訂表用相同指令和相同順序規則，但**不會**得到內建區段自動加的策略關鍵字加分。不要寫出循環的 `GetPriority` 依賴。

```text
Section OpponentBurnShields
Default(0)
Priority(1) HasTag(BurnShield)

Section WRValue
PriorityBonus(100) Facing(GetPriority(OpponentBurnShields) > 0)
```

V7 每載入一次 AI 檔會建立一次區段索引。重複的區段名稱仍是「第一個勝出」；卡名優先於卡號。被快取的是解析後的指令、參數和條件結構，**不是結果**：條件每次仍會讀取目前的卡和遊戲狀態，也保留短路計算。重新載入 AI 檔會清除快取。

**`CostSacrificeValue`**（V7，選用）：替「犧牲費用」要選的場上角色排序（越高越可犧牲）。這和 `OverplayValue` 不同：新版 V7 一般覆蓋登場時，`OverplayValue` 越高越可犧牲，並會和該角色的其他策略角色權衡；以橫置狀態開機覆蓋時，也是選允許位置中 `OverplayValue` 最高的。但部分舊的「非橫置效果放置」路徑仍是低分優先，不要假設所有放置路徑排序都一樣。沒寫 `CostSacrificeValue` 時，犧牲行為維持原樣。

**對位與攻擊順序**：新版 V7 會對已選好的攻擊手一起最佳化格子和攻擊順序。預設第一優先是「嚴格贏下最多個有角色的對手格子」；牌組偏好只用來打破平手，不會為了偏好放棄能贏的格子。例外是選用的 `AvoidFacing` 標籤：會先把「面對指定對手」的次數降到最低，再追求倒置數。`AttackOrder` 一般是高分先攻，但「攻擊時加攻」的來源必須排在依賴它的角色前面。其他時機／戰鬥風險規則再打破平手，最後以由左到右當空間上的平手判定。

`ReverseValue`、`DirectAttackValue`、`AttackerValue` 會影響對位分數。`DefenderValue`、`ProtectValue` 影響戰鬥保留；`BackstageValue`、`OverplayValue` 影響舞台保留／替換。**`ProtectValue` 不會啟用內建的三能量再演。** 一般登場可以先把可移動的角色移開再覆蓋；狀態變化後，計畫修補會重新檢查攻擊手和未來加攻承諾。這些是引擎行為，不是新的腳本動作指令。

主要階段重新規劃時，只保留 `StartMainPhase` 中還存活的攻擊指派。準備階段的指派會在初始快照前修補；之後因犧牲、替換或修補而被移除的指派，會從快照中永久刪除再重建。例如 Class Visit Ruby 可以犧牲先前預定的 Taiki 來回收城門，而不會一直重新恢復又移除 Taiki 過時的指派；空出來的格子仍可給規劃器使用。

**`AfterTriggersTopDeckValue`**（V7，選用）：在自己的主要、名場面、攻擊階段啟用完整的牌庫頂排序。AI 先讓「待判定那幾格」的 `TopDeckValue` 總和最大，再讓判定完後留在最上面那張的 `AfterTriggersTopDeckValue` 最大。TokyoBlade 的設定是乾淨判定優先於保留一張等級 2 以上角色給對手抽牌時的彈回。只有公開過的卡會重排；之後的磨牌、連動或重置可能讓先前的安排失效。

## 8. 起手換牌區段（FirstTurnDiscard）

`Section FirstTurnDiscard` 控制開局換牌。每張候選卡依序比對各行。支援的指令：

| 指令 | 用途 |
| --- | --- |
| `KeepIf 條件` | 符合條件就保留。 |
| `KeepCopies(n) [卡名]` | 指定卡最多保留 n 張。 |
| `KeepSecond [卡名]` | 只有在已經丟掉一張同名卡、且適用張數平衡規則時，才保留第二張。 |
| `DiscardIfKeeping(n) [卡名]` | 依「是否保留指定卡」和保留數量設定丟牌規則。 |
| `DiscardIfKeepingLessThan(n) [卡名]` | 保留數低於門檻時設定丟牌規則。 |
| `Emote 表情熱鍵` | 顯示其中一個表情。 |

```text
Section FirstTurnDiscard
KeepIf IsCharacter && CardLevel = 0
KeepCopies(1) [Example Climax]
```

沒寫這個區段時，預設會保留等級 0 角色、其他全換。

## 9. 動作區段（Action Sections）

動作區段在主要階段或特定事件時，依序執行行為。區塊以 `{` 開始、`}` 結束。最常用的規劃掛勾是 `StartMainPhase` 和 `EndMainPhase`；先攻第一回合**不會**執行 `StartMainPhase`，但結束階段的處理照常執行。

```text
Section StartMainPhase
If(HandContains(1, [Example Brainstormer]) && Stock >= 1)
{
    UseAct(1, [Example Brainstormer])
    IfSo
    {
        PlanClimax([Example Climax])
    }
}
```

### 動作指令

| 指令 | 用途 |
| --- | --- |
| `If(條件)` | 條件成立才執行區塊。 |
| `IfSo` / `IfNotSo` | 依上一個動作是否成功分支。 |
| `Else` | 前一個 `If` 的另一個分支。 |
| `PlayCharacter(條件)` | 打出優先度最高的符合角色。 |
| `PlayBackstage(條件, 選用優先度區段)` | 把符合的角色放到後列。V7 可用指定的優先度區段排序候選（預設 BackstageValue）。 |
| `PlayEvent(條件)` | 打出符合的事件卡。 |
| `UseAct(N, [卡名])` | 使用指定卡的 ACT N（可用的話）。 |
| `UseNextAuto(N, [卡名])` | 預定在該 AUTO 詢問費用時接受。 |
| `PlanClimax(條件)` | 把手上符合的名場面標記為預定要打的。 |
| `SearchTarget(條件)` | 設定之後檢索／回收時偏好的卡。 |
| `CostTarget(條件)` | 付費用時偏好選符合的卡。 |
| `PrioritizeEffectTargets(條件)` | 下一次效果選目標時偏好符合的卡。 |
| `MoveBackstage(條件)` | 把符合、可移動的前列角色移到後列。 |
| `MoveBackstageForwardToMakeRoom()` | 把沒有被指派任務的後列角色往前移，騰出空間。 |
| `ForcePlayAsAttacker(條件)` | 強制把符合、可出的角色放到可用的攻擊格。 |
| `ReplaceAttacker(條件[, 原角色條件])` | V7：明確地覆蓋登場，替換價值最低的可移動前列角色（包含已指派攻擊的格子）。第二個條件限制哪些有角色的格子可以被替換（空格仍然可用），用來在後期修補時保護已準備好的終結者和引擎。只給一個參數時維持原行為。 |
| `FillAttackers(n, 條件)` | V7：把缺的正置前列攻擊格補到 n 個（受引擎／腳本上限限制）。先用符合的後列正置角色，再用合法手牌；優先考慮「一起付得起的攻擊數」再考慮個別價值；可以覆蓋可移動的橫置／倒置角色；**永不**替換現有的正置前列攻擊手。只限緊急使用的卡要等一般候選用完才會考慮；明確寫出的條件可以釋放被保留的後列引擎。嘗試次數有上限，避免登場失敗時一直重試。 |
| `FillStage(n, 條件)` | V7：從符合條件的合法手牌角色補空位，直到場上符合條件的角色達到 n 隻。優先放後列，再放允許的前列；永不覆蓋或移動現有角色。每次登場後重新檢查合法性／資源和數量，偏好低費用和 BackstageValue 高的卡。遵守能量／手牌保留和登場限制。無法達成時會記錄未達數量和手牌角色狀態；`IfSo` 回報是否達到目標數。 |
| `SetAttackLimit(n)` | V7：限制本回合實際攻擊次數（包括已在場上的存活角色），每個主要階段重設成遊戲合法上限。沒設定的牌組仍保留重新正置帶來的額外攻擊。這和只限制登場的 `SetCenterStageLimit` 不同。 |
| `ReserveStock(n)` | V7：本主要階段保留能量，不拿來一般出角色／事件或預估登場規劃。剩餘量看 `FreeStock` 和 `ProjectedStock`。腳本明確使用的 ACT/AUTO 費用仍要自己擋。 |
| `ReserveHand(運算式)` | V7：主要階段保護這麼多張手牌，不拿來一般出角色／事件。每次登場前都會重新計算，所以 `CountCards(CenterStage,FinisherSetup) + 1` 會隨場面增加。預估付款／張數共用這個下限；預定名場面的保留會把它拉到至少 1。名場面和明確的 ACT/AUTO 費用不受影響。每個主要階段重設；`ReserveHand()` 會清除明確的下限，但不會清除剩下的預定名場面保留。 |
| `Emote(表情熱鍵)` | 顯示其中一個表情。 |
| `Return(條件)` | 在問題區段中回傳布林結果。 |

V7 另外支援 `SetCenterStageLimit(n)`（規劃主要階段時限制前列佔用數）和 `EndMainPhaseNow()`（停止剩下的主要階段規劃）。這些限制會在主要階段流程中重設，請當作規劃控制，不是永久的場面規則。

`IfSo` 只有在前一個指令會回報成功與否時才有用。請把它緊接在會設定狀態的動作後面，不要假設每個指令都會更新。

`DigForClimax` 已從新版 V7 移除；V6 的舊處理是空的，根本不會挖名場面。新腳本不要用，請改用明確的合法動作和牌組自己的檢索優先度。

### 讓升級先於開機結算（V7）

打名場面同時觸發「放傷害區的 AUTO」和開機時，用編號的 `ResolutionPriority(效果編號,值)` 標籤讓放傷害區的 AUTO 先結算，值越大越先。以 Fujimibot 在等級 0／傷害 6 為例：主要階段先讓 Hanbei 上場，打出 Date A Playback，先結算 Hanbei 的傷害費用與升級，再用新的等級結算開機。Teatime 的正置連動在攻擊階段開始時結算，在以橫置狀態登場之後。

開機判定也會用 `StandbyValue`；如果高價值目標在攻擊判定時也該被選，它的優先度就不要只限名場面階段。
`StandbyPosition(CenterStage)` 或 `StandbyPosition(Backstage)` 要有括號；用空白隔開的區域會被當成條件，而不是放置參數。像這樣的條件規則：
`StandbyPosition(CenterStage) If(Phase Climax) || CanStandbyToCenterStage`
在名場面連動時要求放前列，但在攻擊判定沒有安全的前列格時，允許先把目標放到後列存著。

Fujimibot 在等級 1 偏好 Teatime，但在等級 2～3 只要有 Kotori 就一定優先選她，甚至優先於後期的 Kurimu 開機路線。Kotori 開機是放後列，所以主要階段規劃不會為她保留空的攻擊格。主要階段結束前會先重新確認休息室的可用卡，再做最後的攻擊手修補。沒有 Kotori 時，Teatime 的格子保留和後期 Kurimu 優先度仍然有效。

Fujimibot 會保護它的 Rias 集中角色不被放傷害區，更不會拿去升級。它一般角色的 `LevelValue` 是 500，而 Rias 是負值：V7 會在顏色偏好之前先考慮 ≥500 的值，所以只要有一般的替代卡，顏色規劃就不會蓋過這個保護。

### 付「犧牲場上角色」費用時保住攻擊數（V7）

`AutoTarget` 控制的是**效果目標**，不是拿來付犧牲費用的卡。
- 用 `AutoIfAndOnlyIf` 拒絕「會少掉必要攻擊」的選擇性犧牲；
- 用 `CostSacrificeValue` 偏好選安全的費用卡。

`AttackersAvailable` 會排除正在判斷的卡，所以放在 `StageContains` 裡，就能算出「拿掉每個候選後還剩多少攻擊力」：

```text
Section [Example Sacrifice Healer]
AutoIfAndOnlyIf(1) Clock > 0 && StageContains(1,IsCharacter && If(Not Standing || AttackersAvailable(Not Backup) >= AttackTarget))

Section CostSacrificeValue
Default(1000)
Priority(2000) Rested
Priority(3000) Reversed
Priority(-20000) Standing && AttackersAvailable(Not Backup) < AttackTarget
```

這是保守地用目前的能量計算，不假設選擇性效果會產生資源。如果是名場面開機的回合，要先設好前列上限再測這個條件，讓 `AttackTarget` 留出預定的格子。最後再用 `FillAttackers(AttackTarget,Not Backup)` 補缺的格子，它不會替換正置的攻擊手，也不會超過上限。

## 10. 問題區段（Question Sections）

`ShouldClock`（要不要放傷害區）和 `ShouldEncore3`（要不要三能量再演）是布林問題區段。它們用動作區段那樣的 `If`/`Else` 區塊和 `Return(條件)`。**第一個執行到的 `Return` 就結束**；都沒執行到 `Return` 時答案是 false。

```text
Section ShouldClock
If(Level < 3 && HandContains(1, [Needed Color]))
{
    Return(True)
}
Return(False)
```

- **`SkipClock`**（V7，選用）：結果為 true 時**強制不放傷害區**，連 V7 通用的「攻擊手不足就放傷害區」也會被否決。省略或 false 時維持原本決策。只影響傷害階段的決定，不影響卡片效果強制選傷害區的卡；啟動時會記錄手牌、重置次數、估計密度和資源。
- **`SkipTriggerSalvage`**（V7，選用）：在選擇性的木門或城門判定圖示回收選卡之前，以觸發的卡為情境來詢問（包括只有一張合法卡時的「全選」捷徑）。true＝放棄回收；省略或 false 時照 `DoorTriggerValue`、`GateTriggerValue` 和舊有選擇。TokyoBlade 的用法：名場面區是 Wound-shifting Scabbard、有一隻正置且還沒攻擊的 On a Starry Night Akane、且手牌已有 5 張以上時拒絕回收，因為第 6 張手牌會讓那隻 Akane「手牌 5 張以下」的連動條件失效。

### 內建三能量再演（V7）

新版 V7 把 `Section ShouldEncore3` 當成內建三能量再演的**唯一**策略判斷。只在以下情況詢問：對手回合的再演階段、以「被倒置死掉的角色」為目前卡片、卡在休息室、原本的位置是空的、且至少有 3 能量。沒寫這個區段會記錄並回傳 false。

引擎**不再**自動加上 `ProtectValue`、傷害區、卡片等級、替補攻擊手、或 5/7 能量的策略門檻。想要這些限制請自己寫在這個區段。這個判斷用的是原始能量是否付得起，不看 `ReserveStock`；`ProtectValue` 和 `EncoreValue` 仍分別用於保護和其他再演效果。

```text
Section ShouldEncore3
If(Not KnownCxInTopStock(3))
{
    Return(False)
}
If(Stock >= 8)
{
    Return(True)
}
// 8 能量以下：在牌庫很小、快要重置前，把卡在能量區的多張判定名場面清掉。
// 這個範例沒有提供預期的 1 能量 ACT 次數；有的話請加上牌組自己的預測。
Return(Stock >= 3 && Deck < 10 && OwnClimaxesTriggered >= 2 && StockCxDepth > ExpectedStockPaymentsNextTurn(Not Backup,0))
```

各腳本的作法：

- **TokyoBlade**、**ALL Door**：任何符合資格的死亡角色都用上面這種「清能量」策略，搭配各自牌組的 ACT 預測；ALL Door 還計入 Riri 的 Laplace 輪替。兩個分支都要求能量區前 3 張內有已知名場面。小牌庫分支可以在 8 能量以下運作，但仍要付得起 3 能量。
- **Memchobot**：只讓 L2 Topic 再演，條件是能量 ≥10，或 ≥7 且 3 次付款內有已知名場面；並保留它原本的傷害區／卡片等級／替補攻擊手門檻。
- **FujimiBot**：重現原本的「受保護卡」策略，包括 5/7 能量門檻。
- **SakiTenmaAI**：保留原本的 `ProtectValue` 為正、傷害區 6、替補數量和 5/7 能量的保護，但拿掉「卡片等級」門檻，讓它等級 1 的連動角色在玩家等級較高時也能再演。替補數量用現有的 `PlayableCharactersInHand` 和場上非倒置、`AttackerValue` 為正的角色數，和舊策略一致。

這些都是腳本（TXT）層級的策略，不是引擎預設。

## 11. 卡片標籤（Card Metadata Labels）

卡片區段裡可以放引擎認得的標籤，用來調整特定的 AI 行為。它們是「設定掛勾」，不是通用的腳本指令。參數和行為依標籤而定，而且**只有在程式有實作處理時才會有作用**。

### 一般效果控制

| 標籤 | 用途 |
| --- | --- |
| `AutoIf(N) 條件` | 條件成立時「偏好」使用 AUTO N。 |
| `AutoIfAndOnlyIf(N) 條件` | **只有**條件成立時才允許使用 AUTO N。 |
| `DontUseAuto(N)` | 在標籤條件下拒絕 AUTO N。 |
| `AutoTarget(N, 條件)` / `ActTarget(N, 條件)` | V7：把該效果的目標限制在符合條件的卡。未分類（`Undefined`）的選擇中，較前面的目標行排名較前（在有效的預定目標之後）。有分類的 `AIChoice` 選擇在符合範圍內維持各自情境的排序。符合範圍為空時：選擇性選擇會放棄；強制選擇則記錄不符並改用引擎合法選項（AI 策略不能跳過卡片腳本規定的選擇）。箭頭判定也接受 `AutoTarget(1, 條件)`。 |
| `AutoTargetFallback(N, 條件)` / `ActTargetFallback(N, 條件)` | 選用的「備案目標」：只有主要目標都不在合法選項中時才使用。寫法和對應的主要目標標籤相同。未分類選擇的備案依序偏好：預定承諾 → 較前面的備案行 → `SearchValue` 最高。有分類的選擇維持情境排序。箭頭判定備案時用一般的手牌／能量獎勵排序。`PrioritizeEffectTargets` 會蓋過主要和備案規則。沒寫這些標籤時，嚴格目標行為不變。 |
| `EventTarget(條件1, 條件2, ...)` | V7：事件卡目標限制在這些條件的聯集，越前面的條件越優先。這個限制在「全選」捷徑之前也會套用。和其他目標規則一樣，空的範圍可以放棄選擇性選擇，但不能跳過有合法選項的強制選擇。 |
| `AutoAssign(N, 運算式)` / `ActAssign(N, 運算式)` | 設定該效果要求的分配值。 |
| `AutoAnswer(N, 答案)` / `ActAnswer(N, 答案)` | 為 AUTO/ACT 的問題選答案（從 0 開始），包括對手卡片提出、而你的 AI 檔裡有該卡區段的問題。箭頭判定用 `AutoAnswer(1, 答案)`（`0`＝手牌、`1`＝能量）。超出範圍的答案會回報 AI 檔案錯誤。 |
| `AutoTimingAnswer(時機, 條件)` | V7：依引擎時機回答 AUTO 的是／否問題，適用效果編號不固定的被賦予能力。在獲得能力的角色上計算。 |
| `StopLookingWhen(條件)` | V7：「看最多 N 張／公開最多 N 張」的效果中，翻到符合的卡就停。標籤後面的條件只在開始看之前算一次；還沒翻到的卡絕不偷看。 |
| `OnAttackMill(N, 上限, 合格條件, 壓縮門檻)` | V7：讓 AUTO N 在攻擊宣告時參與「自己牌庫看最多 N 張」的反應式預算。上限是正整數、要和效果的看牌張數一致；門檻是 0～1 的數字。規則見下方。 |
| `LookAtUpToDeckFloor(N, 最少保留) [條件]` | V7：把 AUTO N 的「看／公開最多 N 張」限制在最多 `牌庫張數 − 最少保留` 張（最低 0）。任何階段都適用，包括戰鬥後或再演時的「死亡時」效果。可以和既有的平均分配／輕翻條件一起運作，不改變獎勵或後續效果指令。 |
| `TriggerChecks(n)` | V7：宣告每次攻擊固定判定幾次，用於重排牌庫頂和攻擊時磨牌預算（例如 Beyond the Darkness, Aqua 寫 `TriggerChecks(2)`）。雙判定和額外判定效果也會被計算。 |
| `ForceSideAttack [條件]` | V7：這隻角色被選去攻擊且條件成立時，強制合法的側面攻擊。會蓋過 `DontSideAttack`、ReverseValue、戰力、魂／斬殺等攻擊方式偏好，即使側攻魂是 0。若不能側攻或對面沒有角色，記錄後用一般合法的攻擊選擇。省略條件＝永遠。 |
| `PreferSideAttack(未來魂) [條件]` | V7：偏好「判定前至少 1 魂」的合法側面攻擊，不管對面戰力或正面傷害。主要階段會比戰力對位更早安排這些攻擊手，並偏好能得到最多側攻魂的有角色格子；主要階段結束時重新檢查。`未來魂`是只在主要階段（沒有名場面在場時）用於擺位的預估魂加成，不用於攻擊選擇。會遵守移動限制、必須中間、禁止攻擊、Great Performance、免費側攻和禁止傷害。沒有划算的格子時就照一般擺位／攻擊。 |
| `DefensiveFrontalBounce(N, 能量費用, 目標條件, 選用優先度區段)` | V7：讓「場上送進回想區」的犧牲來源參與側邊格子的保護。AUTO N 必須是有效的 `OnOtherFrontalAttacked`（其他角色被正面攻擊時）、把被攻擊角色回手的能力。能量費用是非負數；條件指定可被保護的其他角色。選用區段搭配 `HasHigherPriorityDefensiveTarget` 使用。規則見下方。 |
| `EventAnswer(運算式)` | 為事件卡的問題選答案。 |
| `NeverPlayFromHand [條件]` | V7：卡片層級禁止一般從手牌打出；省略條件＝永遠。 |
| `PlayForAutoOrEmergency(N)` | V7：一般從手牌登場時，要求它的 `OnPlay`（登場時）AUTO N 可用、且符合對應的 `AutoIfAndOnlyIf(N)` 條件。請為這個 AUTO 寫一條權威規則，包含費用和目標是否存在。條件不成立時，只有最後的 `FillAttackers` 在所有一般候選都失敗後才能讓它上場；緊急許可會一直保持到放置完成，只略過這個 AUTO 準備條件，不會略過卡片合法性、資源保留或其他登場限制。它的 AUTO 仍遵守同一條付款規則。不會阻止已在場上的同名卡攻擊。被標註的 AUTO 裡的 `SearchDeck` 也會保留 AUTO 編號／類別，讓 `AutoTarget` 規則生效；沒標註的牌庫檢索維持舊行為。同一個 AUTO 的費用和獎勵規則請用區域區分。 |
| `OnlyPlayToOpenStage [條件]` | V7：條件成立時，一般登場必須放在空的、AI 允許的位置；也禁止覆蓋指定的有角色位置。 |
| `RequiresFullWinningBoard(後列區段, 滿場戰力, 替換保護條件)` | V7：一般從手牌登場時，要求兩側格子都有正置且會贏的角色，並付得起兩張有意義的後列支援（依指定區段排序）。先放支援、重新檢查場面／資源，再把角色放到中間，不擠掉兩側的贏家。滿場戰力＝印刷戰力＋滿場中間加成；會取代既有的中間位置預估加成而不是重複計算。條件用來保護角色不被替換。 |
| `EmergencyAttackerOnly(選用目標數) [條件]` | V7：把這張卡排除在一般攻擊手規劃之外（包括第一回合和清場輔助），主要階段開始時把已在後列的同名卡固定在後列。明確的 `FillAttackers` 只在一般候選之後才考慮它。沒給目標數時，只有在前列正置攻擊手為 0 時才能被釋放；給了目標（1～3 或像 `AttackTarget` 的運算式）則在正置攻擊手少於該數時可作最後補救。請把它的明確修補放在所有其他回收／資源釋放手段之後。 |
| `RequiresMiddlePosition [條件]` | V7：攻擊手擺位後和主要階段結束時，把已登場的前列角色調整到中間（和可移動的角色交換，並更新攻擊計畫的格子／目標）。已經有相同標籤的角色在中間就保留給它。仍受移動限制；會蓋過一般的對位偏好。 |
| `AvoidFacing(對手條件) [條件]` | V7：偏好讓對面「實際對手」不符合條件的前列格子。後面的條件在攻擊方身上算；對手條件在對手身上算（包含 Great Performance 強制的目標）。整體主要階段規劃會先把「面對符合者」的次數降到最低，再追求倒置數，單一攻擊手也一樣。移動限制、被封鎖位置、必須中間／強制側攻等仍然優先。如果所有可行計畫都還是會面對，就用衝突最少的合法計畫並記錄，而不是放棄攻擊。效果放置維持既有的空位、舞台角色和替換保護，這個提示只在同一層級內排序。 |
| `GainPowerBeforeAttacking(數值)` | V7：只在主要階段，把預估的「攻擊階段才會得到的加攻」加進對位規劃（手牌和場上角色都適用）。只用於目前戰力還沒包含的加成，並讓估計和攻擊順序一致。攻擊時不會再加一次。 |

**答案標籤的規則**：解析器用「該效果編號**最後一個**符合且有效的標籤」。所以先寫安全的預設答案，再往後寫較具體的條件。查詢標籤時會先依標籤名稱和效果編號過濾，再計算條件；登場或攻擊手標籤檢查時，不相關的 AUTO/ACT 條件不會被計算。

`AutoAnswer`／`ActAnswer` 的答案如果要用布林運算式，請用 `If(...)` 包起來，例如 `AutoAnswer(1,If(Hand < 5))`。`AutoTarget`／`ActTarget` 後面的條件在效果持有者身上計算；目標條件則在每個候選身上計算。

目標規則會套用到該編號能力的**每一次**選擇，包括 `PayCost 0` 裡腳本化的費用選擇。費用和獎勵選的是不同卡時，請用區域區分條件。例：ALL Door 的 Noble Lily, Kaede 明確指定「前列、沒有標記的 "Pajama Party" Riri」作為必要的標記宿主，和它的牌庫檢索目標分開寫。卡片腳本本身不變，會先把 Kaede 放到選中的 Riri 下面再檢索；接受 0 能量的費用並不會跳過放標記這一步。

**對手效果的預設**：對手的未分類效果、且所有合法選項都是你自己的舞台角色時，V7 預設先選前列再選後列，然後選 `ProtectValue` 最低的（平手時倒置的優先）。如果是單純「加正戰力」的效果，則改選偏好位置中 `ProtectValue` 最高的。這個「只加戰力」的判斷僅限於只含目標位置／選擇指令、正整數 `GainPower` 和大括號的效果；複雜效果維持保守的保護預設。明確的目標規則和分類的 `AIChoice` 情境優先。

#### 箭頭判定（Choice，V7）

箭頭規則寫在「觸發的那張名場面」的卡片區段。引擎內部的判定編號是 0，但為了和既有腳本相容，V7 腳本介面刻意用 `AutoTarget(1, ...)` 和 `AutoAnswer(1, ...)`。這只適用於真正的箭頭判定效果，不適用於有箭頭圖示的卡上的其他能力。

```text
Section [Example Choice Climax]
AutoAnswer(1,0) Hand < 7
AutoAnswer(1,1) Hand >= 7
AutoTarget(1,IsCharacter && If(InLocation WaitingRoom) && If(HasTrigger Soul))
```

符合的目標行會限制引擎的合法獎勵，並依檔案順序排序；同分時維持引擎的選項順序。沒有明確目標排序時，選用的 `ChoiceTriggerValue` 優先度區段可以選分數最高的正值獎勵。否則：回手牌選 `SearchValue` 最高的，放能量選最低的。沒有腳本答案時：手牌 6 張以下放手牌，否則放能量。箭頭回收不會消耗預定的未來檢索目標。

Memchobot 的箭頭名場面用 `AutoTargetFallback(1,True)`，所以缺少偏好的 Kana/Topic 時也不會放棄合法的獎勵。它的答案規則依序是：預設回手 → 手牌滿時放能量 → 缺連動卡時回手的例外，遵守「最後符合者勝出」。

Spring Day 用 `ActTargetFallback(1,True)`：沒翻到偏好的卡時，拿公開卡中最好的獎勵。它在等級 3 的動作分支把隨機獎勵選擇交給卡片區段的優先層級，而不是硬性過濾個別的組件；新找到的 First Stage 可以在第二次集中之前檢索 Beyond。只有它「選擇性的獎勵挖掘」會輕翻；一開始強制的四張集中不變。至少重置過一次、且估計名場面密度 ≥25% 時：等級 0～2 翻到 `SearchValue` 為正的名場面，或付得起且等級 ≤ 目前等級的角色就停；等級 2 另外翻到等級 3 的 B-KOMACHI Kana 或 3/0 First Stage 事件也停。等級 3 會一直翻到 Kana 或 First Stage 出現，或翻滿四張。Kana 和 First Stage 在等級 2～3 有明確的獎勵優先度。還沒重置過或密度低於 25% 時，照完整挖掘。決定何時停時不會看還沒翻開的卡。

`AutoAddedToHand(N)`：AUTO N 是否「真的」把自己的一張卡加入了這隻角色當前實體的手牌。拒付費用、什麼都沒選、或移動被擋都不算。和 `UsedAuto(N)` 不同，只要這個實體還在場上，它會跨回合保持；離場再上場就是新的實體，需要重新成功過濾一次。

#### 避開特定對位（V7）

```text
Section OpponentBurnShields
Priority(1) HasEffectCommand(Cont,GainSpecialEffect,ReceiveNoDamageFromAutoEffects)

Section [Example Burn Finisher]
AvoidFacing(GetPriority(OpponentBurnShields) > 0)
```

這是明確的策略偏好，不是新的卡片合法性限制。它可能為了讓燒傷終結者避開護盾而放棄一次倒置，但不能移動不可移動的角色、略過必須中間的規定、移除強制目標，也不能保證穿過全域防傷。沒標註的牌組維持一般的「優先贏下倒置」策略。

#### 強制側面攻擊（V7）

把指令寫在該角色的卡片區段：

```text
Section [Example Character]
ForceSideAttack
```

要有條件就寫成例如 `ForceSideAttack OpponentLevel >= 3`。每次這隻角色攻擊前都會重新計算條件。它只決定攻擊方式：不會讓角色登場、不改攻擊順序、不繞過攻擊限制、也不抑制它的攻擊時效果。對面是空格時仍用合法的直接攻擊；Great Performance 或其他禁止側攻的效果照一般合法選擇。TokyoBlade 沒有卡使用這個指令。

ALL Door 的設定：Count the Important Things 在場時，強制 Pure White Shining Lily, Yuyu 側攻，即使魂不是正數（空格仍直接攻擊）。Plunge Forward Lily, Riri 用 `PreferSideAttack(0)`，當那張名場面在手牌時，主要階段預估提升為 1 魂。Riri 通常比 Yuyu 先攻擊；在前列有 Yuyu 的連動回合，當對手休息室名場面 ≤2 張，或名場面 ≥3 張且非名場面張數嚴格大於 `4 ×（名場面 − 2）` 時，Riri 有明確的優先度。剛好 4:1 不滿足後者。這些順序規則在每次攻擊前重新計算。

#### 攻擊時「看最多 N 張」的反應式預算（V7）

`OnAttackMill` 要自己開啟，和卡名無關。後面的條件宣告攻擊時磨牌「何時有效」（例如對應的名場面和必要的舞台特徵）。每隻角色只在一個攻擊時 AUTO 裡標註一個「看最多 N 張」指令；如果雙判定攻擊手的額外判定是在自己的攻擊中才獲得，要給它 `TriggerChecks` 宣告。AUTO 編號限定了預算範圍，所以同一隻角色的其他能力維持一般行為。沒標註的效果和其他 AI 版本仍用 `StopLookingWhen`；人類玩家的選擇不受影響。

計畫在「看牌開始前、本次攻擊判定前」計算，用的是目前牌庫張數，而不是回合開始時的快取。會檢查剩下的前列正置攻擊手能否合法攻擊、本回合剩下的攻擊額度；目前已橫置的攻擊手連同它尚未進行的判定也會算進去。只有目前有效的 `OnAttackMill` 標籤會貢獻選擇性磨牌量。

- 設 `D`＝目前牌庫張數、`T`＝剩下的判定數、`M`＝剩下的選擇性磨牌總量。當 `0 <= D - 1 - T <= M` 時，可以達成「剛好剩 1 張」。
- 可以達成時，把 `D - 1 - T` 張盡量平均分給剩下的磨牌效果（不超過各自上限）。餘數給較前面的；上限相同的效果之間最多差 1 張。只鎖定目前這個效果的配額，下一次磨牌再重新計算。如果光靠判定就會剩 1 張，看 0 張也是合法的。
- 無法達成時，重新檢查估計名場面密度：≥ 門檻就一張一張翻，翻到第一張合格的卡就停（輕翻）；低於門檻就完整翻。
- 至少重置過一次後，只要密度 ≥ 門檻就**一律輕翻**，即使可以達成剩 1 張。壓縮度和重置次數在每次攻擊時磨牌都重新檢查，不是整個攻擊階段只算一次。

例：兩隻 Kana、牌庫 10 張、剩 3 次單判定 → 選擇性預算是 6，分成 **3 + 3**，不是 4 + 2。如果剩下有一隻雙判定的 Aqua，就算 4 次判定，5 張分成 **3 + 2**。判斷只看公開的卡，絕不會去看隱藏的卡來決定何時停。既有的獎勵選擇和之後的「前三張重排」各自獨立、不變。

留 1 張牌庫，是為了讓一般的重置延後到名場面離開名場面區之後。這是預測，不保證能避開計畫外的額外抽牌、能量魂判定、強制的牌庫移動、之後取消的攻擊或重新正置。這些變化會在下一次標註的磨牌時納入；未來的重新正置和未知的額外消耗牌庫效果不會被假設。AI 紀錄會寫下牌庫張數、判定預算、平均配額或輕翻／完整翻模式，方便實測。

#### 選擇性「看最多 N 張」的牌庫下限（V7）

攻擊宣告以外的選擇性磨牌，可以用 `LookAtUpToDeckFloor` 保護目前的牌庫。TokyoBlade 的死亡時 Ai 宣告 `LookAtUpToDeckFloor(2,1) CountCards(Climax,IsClimax) >= 1`：名場面區還有名場面時，牌庫剩 4/3/2/1 張，原本看 4 張就變成最多 3/2/1/0 張；牌庫 5 張以上時仍可看滿 4 張。這條規則在名場面還在場時，也適用於對手回合／再演時的結算，保留 1 張牌庫，避免這個選擇性效果提早觸發重置、把名場面排除在外。既有的丟牌決定、公開獎勵偏好和 Ai 之後的回想區步驟不變；沒有名場面在場時不設下限。這不會阻止其他效果、傷害或之後的判定造成的重置。

#### 防守時保護側邊格子（V7）

`DefensiveFrontalBounce` 要自己開啟。來源必須留在場上、沒被倒置、擁有宣告的 AUTO，且被允許使用該 AUTO 並進入回想區。目標必須是其他沒被倒置、符合條件、可以回手的側邊角色。`HasDefensiveSideTarget` 回報是否存在這樣的目標和足夠資金；它不會替效果任意選目標。

- `CanUseDefensiveBounce`：確認宣告的來源和目前戰鬥目標都有效、合格、付得起。
- `HasOtherDefensiveTarget`：除了目前戰鬥中的角色，還有沒有其他合格、非倒置的前列角色。
- `HasHigherPriorityDefensiveTarget`：另外要求那隻角色在宣告的優先度區段中排名高於目前目標。

兩者都遵守來源的能力／回想區限制、資金、目標特徵和回手限制；都不會替固定的卡片效果改選別的目標。後列角色不算未來會被正面攻擊的目標。

每次攻擊前：一個會輸或會相殺的雙判定來源，在「另一個合格的側邊角色會贏、已經在攻擊中存活、或能用正數魂合法側攻」時，會選擇側攻。正數側攻魂一般就符合；判定前 0 魂在對手等級 ≤1、或等級 2 且有名場面在場、且魂判定有效時也符合。兩次判定只是提供傷害機會，不保證有傷害。會輸／相殺、受保護的側邊攻擊手，只有在判定前魂為正、且有預期能在自己攻擊中存活的來源時才側攻。免費側攻和實際攻擊限制會被遵守。這些保護情況會蓋過 `DontSideAttack` 和一般的攻擊偏好，但不改變攻擊時連動的結算。

資金用目前的能量；1 能量的彈回也可以預期自己還沒進行的判定會補能量。這是反應式預測，不保證未來的能量消耗效果、被移除、或對手先攻擊來源。正面攻擊會輸或撞到炸彈的來源，不會被假設會存活。沒標註的 Bot 維持原本的決策。

### TokyoBlade（V7）

> 以下是作者針對 `TokyoBlade.txt`（【推しの子】牌組）的設計說明，可以當作「一個完整 V7 腳本怎麼思考」的案例。

**Girlfriend Akane**：用 `EmergencyAttackerOnly(AttackTarget)`。一般規劃和早期修補都把她留在後列。只有在其他身體／回收嘗試和等級 3 保留釋放都做完後，最後一次後列專用修補才可以把她拉上來補缺的攻擊。如果她在上次緊急攻擊中存活，下次主要階段會回到空的後列位置。即使引擎已經在場上，也優先用其他合法的手牌身體。Happy New Year「只有 0 攻擊手時才出」的規則不變。

**Talking About Breaking Up Aqua**：場上沒有 Happy New Year 時，優先從休息室回收它。這次選擇會關掉「攻擊修補目標」規則，避免排除引擎或和引擎專用的硬性目標衝突。缺引擎的回收，即使沒有手牌／攻擊不足，也足以讓它使用選擇性過濾；既有的後期城門手牌保護仍有效。主要階段有安全多餘的可丟卡時，會嘗試回收並立刻把回收的集中角色放到後列，主要階段結束時再檢查一次後來才回收到的引擎。休息室沒有集中角色時，維持一般回收優先度。

**開局**：`AI_TokyoBlade` 讀 `TokyoBlade.txt`。保留等級 0 身體和一張 Superstar's Acting；當起手有至少兩張 0 費等級 0 角色（不含後列引擎和助太刀）時，再保留一張 Aquarium Date, Kana Arima。Happy New Year 單獨不算進攻擊手下限。Bot 會把 Happy New Year 放在後列。先攻開局維持引擎的一攻規則；後攻開局在等級 0 目標兩攻，有 Beyond the Darkness 時三攻。第 3 回合起即使還在等級 0 也目標三攻，取代原本等級 0 兩攻的策略。

**攻擊優先**：沿用 Mushoku 的「攻擊優先」層級：確定性的 Miyazaki 回收先於隨機挖牌；即使牌庫壓縮良好，集中也可以修補攻擊不足。Coffee 現在只限早期的 Kana／集中準備，不再隨便回收身體。修補會避免等級 3 自殺式重置，以及把開機 Saya 的替換目標洗掉。檢索加分放最後，讓本回合便宜的身體（特別是能贏的）排在準備卡之前；等級 0～1 之後保留下回合便宜的身體，等級 2 以上回到終結套件優先。傷害區決策即使手牌看起來健康，也能辨識出攻擊不足。容量計算排除受保護的引擎／助太刀，並考慮共用能量，而不是把「三張各自付得起」當成「三張一起付得起」。

**補攻擊手**：一般和後期連動規劃後，`FillAttackers` 修補缺的格子，不覆蓋正置的終結者。先用已在後列的身體，再花手牌／能量；加攻引擎只在緊急時才當攻擊手。後列處理是 Manual。Happy New Year 用 `EmergencyAttackerOnly` 保護，並明確排除在三攻修補之外：Bot 寧可接受兩攻，也不把集中角色拉上來當第三攻。只有在其他合法修補都失敗、前列正置攻擊手為 0 時，最後的單攻擊手修補才可以拉它上來。選擇性的集中不會橫置唯一可用的緊急攻擊手，必須還有另一個付得起／可用的身體。除此之外，三攻優先於保留每張終結者的丟牌或助太刀。等級 3 時 Bot 先保留一張城門的丟牌，如果格子還缺，最後才釋放登場保留；這可能刻意犧牲連動可行性換第三攻。最後的名場面檢查仍會檢查城門的手牌／能量／場面條件，必要時改打木門施壓。這是盡力修補，不保證在合法卡、可移動格子或資源不足時一定三攻。

**本等級調整**：也適用於等級 1 的 0 費攻擊手。檢索／回收在木門有效時偏好可出的 Kana；傷害區／丟牌保護本回合攻擊下限需要的身體；多餘的未來卡比本等級攻擊手先當丟牌。Talking 和集中的攻擊修補獎勵用「預期登場合法性」判斷；Coffee 的明確目標刻意包含下回合的 Kana。前列補滿後預估手牌偏少，可以在前期觸發集中。用 Living/Miyazaki 回收終結者，保留免能量登場路線，而不要求終結者從手牌付得起。

**跳過放傷害區**：至少重置過一次後，若抽完牌後手牌 ≥6、估計名場面密度 ≥20%（平均五張一張）、有 1 能量和休息室角色、且 Happy New Year 在場上正置或在手牌且後列有空位，就不放傷害區。牌庫的兩種名場面都算集中命中。集中的兩攻下限、等級 3 能量／助太刀保留、安全重置／替換目標保護也都適用；不會為了主要階段策略本來就會拒絕的集中而跳過。這個否決優先於緊急放傷害區。等級 2／傷害 6、手上有城門且有藍色時，以下任一情況可以放傷害區升到等級 3：三能量可以出手牌終結者（或手牌 Saya 配休息室終結者）；或一能量＋六張手牌、且 Living 在手牌／前列，可免能量登場手牌終結者。兩條路線都會保留一次連動付款的能量。沒有準備好時，仍套用重置後的集中跳過規則。

**Kana 的場面條件**：主要階段結束前所有攻擊手修補和支援出牌完成後，若前列有 Aquarium Date Kana 且手上有木門，必要時觸發 `FillStage(4,IsCharacter && If(HasTrait Show Business))`。Kana 需要三隻「**其他**」Show Business 角色，所以兩攻擊手＋兩張符合的後列就夠；只有三攻擊手不夠。這次最後修補用合法的手牌補空位（包括可出的引擎），不會把已建立的身體拉下來，也不會為了攻擊犧牲集中。如果還是湊不到四隻，木門的 `NeverPlayFromHand` 條件會把它留給之後的連動，而不是拿來普通施壓。打名場面時會重新檢查這個限制（包括明確預定的木門）；前列沒有 Kana 時，原本的木門施壓策略不變。

**除錯紀錄**：每次切換回合階段前，所有 Bot 都會把 `Bot phase snapshot` 寫進 Unity 除錯紀錄，內容包括：切出／切入的階段、回合、攻擊／防守方、Bot 實作、等級／傷害／能量／牌庫張數、每張手牌的名稱／卡號／等級／費用／顏色符合、舞台位置／戰鬥位置。這只是除錯用，不是公開聊天或網路訊息。搭配失敗的舞台修補診斷，可以分辨「真的沒有合法身體」和「錯過登場機會」，而不用從手牌張數去推測手牌內容。

**連動組件**：沿用 Mushoku 和 Memchobot「攻擊優先、補缺的連動半邊」層級：本回合攻擊確定後，等級 0 的檢索目標是湊到手上兩張 Kana（已有木門時三張）。有 Kana 但缺木門時，優先於奢侈的引擎；等級 1～2 有多餘名場面和可犧牲角色時，名場面交換角色也是有用的橋樑。從等級 0 到 2，傷害區和丟牌決策會保留最多三張手牌／前列的 Kana。Coffee 用 `PlayForAutoOrEmergency(1)`：只有在等級 0 或自己第一個等級 1 回合、有空閒能量、傷害區 <6、休息室有合法的非終結者 Show Business 角色可當傷害費用、且牌庫真的有 Kana／集中目標時，才一般出它來付檢索。費用是把休息室的角色放傷害區，不是 Coffee 本身。它的 AUTO 目標規則把費用限定在休息室、獎勵限定在牌庫，所以獎勵偏好不會擋住或誤導付款。優先回收缺的 Happy New Year，否則在手上 Kana 少於三張時拿 Aquarium Date Kana。互斥的條件獎勵規則不會強迫拿不存在的目標。之後的 Coffee 只當最後手段的攻擊手，超出那個時間窗就不再付傷害／檢索費用。例外是刻意的 0-6 升級：傷害費用會在檢索前完成升級，所以在等級 0、傷害 6、手上有木門、Kana 在手牌或牌庫、能湊到四隻 Show Business、且有紅色卡可以升級（從傷害區，或把休息室紅色身體放傷害區，費用目標會優先選它）時，Coffee 可以付費。這時如果有木門沒有 Kana，檢索會選 Kana 而不是 Happy New Year。

**Miyazaki**：等級 0／傷害 4 以上可以回收已知的 Kana 準備下回合；等級 1～2 有木門時也可以。要求：已在場上時手牌 4 張（從手牌登場前 5 張）、有安全多餘／錯的名場面或非終結者未來角色可丟、犧牲 Miyazaki 後仍有足夠其他攻擊手維持本回合攻擊下限。Kana 本身絕不會被指定為丟牌。選擇性磨牌不會把休息室已知的 Kana 洗掉。Kana 的連動獎勵偏好公開的、合格的等級 2 以上 Show Business 角色，以解鎖「前三張重排」，不管玩家等級或壓縮度。沒有公開到時，照一般 SearchValue。反應式磨牌數、壓縮牌庫輕翻和重排演算法不變；後期終結者登場／保留規則不變。

**Beyond the Darkness（Aqua）的彈回**：當有其他角色被正面攻擊時，付費彈回保留給存活中、合格的 Aquarium Date Kana。`DefensiveBounceValue` 排序：Kana ＞ 其他前列角色 ＞ Ai 最後。只要還有其他合格的前列目標，被正面攻擊的 Ai 不會被彈回，以保留它的炸彈相殺。沒有其他有效目標時，Ai 還是可以被救。即使還有低優先度的側邊目標，中間的 Kana 也可以被救；其他目標維持原本的側邊優先。

Beyond the Darkness 在被考慮為後列手牌修剪之前會先攻擊。有空的後列位置時，手牌超過 7 張，或打木門後預估 Kana 連動獎勵會超過 7 張，就可以出多的那張。它的雙判定側攻會保留自己去救另一隻側邊 Show Business 角色；當夥伴能造成正數判定前魂、且預期 Aqua 仍可用時，會輸的側邊夥伴也側攻。對手抽牌時，中間的 Aqua 在有付得起的合格側邊目標時，會拒絕 Happy New Year 賦予的彈回。Aqua 在還有其他合格側邊目標時，把付費彈回留給側邊，沒有時才用在中間；最高優先度的等級 1 Kana 例外，在中間也可以救。卡片效果把目標固定為「目前被攻擊的角色」：Bot 無法改選別處的 Kana、救自己，也無法保證對手先攻擊別的角色時能救到 Kana。

**Taiki**：不再使用「滿場全贏」登場門檻。等級 2 時有很高的攻擊手優先度（低於有效的 Kana 連動），並保留 `RequiresMiddlePosition`。等級 2 登場不會因為即將重置或格子不完整／會輸而被擋。後期重新檢查可以替換一般攻擊手，但不會替換 Kana、準備好的終結者、引擎或助太刀。

攻擊手配置後，若 Taiki 已在場，會提示最多兩張付得起的支援卡放進空的後列，提升他在中間的戰力。`FullBoardBackstageValue` 排序：Happy New Year Kana、Girlfriend Akane、Living Akane、Beyond the Darkness Aqua，然後**只有在 AUTO 1 真的回收到卡之後**才是 Talking About Breaking Up Aqua。既有的後列角色保留，能量／手牌保留仍有效。缺支援不會阻止 Taiki 登場。`PlayBackstage(條件, 選用優先度區段)` 用給定的區段排序手牌候選；省略就用一般的 BackstageValue。

**清能量**：只針對已知的判定名場面。一般付費登場自然會清能量；一般規劃後，若本回合攻擊已經擺好，正置的集中角色即使牌庫壓縮良好也會付掉第一深度的名場面。兩隻正置集中角色可以付到第二深度，第二次付款前會重新確認它現在是能量最上面。Coffee 早期的準備檢索可能順便清能量，但單純清能量不再足以讓它付傷害／檢索費用。清能量會保留一次連動能量和手牌終結者的登場費用（若已有終結者在場且 Raida 讓防守有效，則改保留兩能量），永不把開機 Saya 的目標洗掉，並避免等級 3 自殺式重置。未知／更深的能量不會盲目追。

#### 卡片效果決策策略

已對照 CardContent 檢查過登錄牌組的所有卡。強制效果照常結算；選擇性效果依下表策略。刻意拒付選擇性費用，不代表要跳過之後的強制效果。

| 卡片 | 結算策略 |
| --- | --- |
| Beyond the Darkness, Aqua | 強制的雙判定／能量頂付款；用側攻保留自己，優先救有效的等級 1 Kana（包括中間）。還有其他有效前列目標時，不救被正面攻擊的 Ai；否則維持側邊優先、中間為備案。 |
| Coffee in One Hand, Aqua | 只在等級 0 或自己第一個等級 1 回合、費用合法且牌庫有目標時，為缺的集中／Kana 準備而出；否則只當最後手段攻擊手、不付 AUTO。避免傷害到 6，除非在 0-6 付費升級進入有效的木門／Kana 連動；保護能量保留。 |
| Living on the Stage, Akane Kurokawa | 手動 ACT，走免費終結者／Saya 路線；需要真的目標、安全的丟牌，以及登場後足夠的連動燃料。 |
| Happy New Year, Kana Arima | 手動集中：用於修補、預估手牌回補、第一副牌庫的磨牌、有限的已知能量清理，或密度 ≥20%。被賦予的前列彈回預設接受；例外：Aqua 有付得起的合格側邊目標、Starry Night 戰力超過 11000、或滿場／滿戰力的 Taiki 已贏下所有前列格。 |
| Meetup on Christmas Eve, Ai | 第一副牌庫或壓縮度低時走丟牌／挖牌／回想區路線；名場面在場時保留 1 張牌庫；有本等級獎勵就拿，否則拿有用的未來卡。 |
| From Now On, Akane Kurokawa | 只有在要把多餘／錯的名場面換成本回合的木門／城門組件時，才犧牲非必要的身體。 |
| Sightseeing in Miyazaki, Aqua & Akane Kurokawa | 第一副牌庫或低壓縮時磨牌，不洗掉已知的 Kana 或預留的終結者回收。ACT 在等級 0／傷害 4 以上準備 Kana，或在等級 1～2 有安全丟牌且其他攻擊手已確保時擴大木門回合；等級 3 的 Starry Night／城門回收維持六張門檻。不再用 ACT 回收緊急攻擊手或集中角色。 |
| "Blade" Taiki Himekawa | 高優先度的等級 2 攻擊手，放中間、不需滿場門檻；之後有餘裕時用排序好的支援補後列。對手抽牌時，若滿五隻、戰力 ≥12000 且贏下所有前列格就保留。一律結算「公開最多四張」和必要的獎勵丟牌；中期偏好木門、等級 3 偏好城門。 |
| Aquarium Date, Kana Arima | 一律結算合法的連動；反應式平衡看牌數，盡量留 1 張牌庫；做不到就在密度 ≥25% 時輕翻，否則完整翻。任何重置後，≥25% 一律輕翻。獎勵和重排優先度分開。 |
| Talking About Breaking Up, Aqua | 休息室有 Happy New Year 且場上沒有時回收它；否則過濾不想要／過多的手牌或修補缺的攻擊，保留城門燃料。強制的攻擊加攻偏好連動攻擊手。 |
| As the "Girlfriend". Akane Kurokawa | 一律結算名場面加攻；偏好 Starry Night（有三張時給側邊），否則給側邊。判定回收把錯的／多的名場面換成有用的身體，從等級 2 起保留唯一的木門和唯一的城門。 |
| During Practice, Kana Arima | 很少登場：只有在付完後能量低於對手（會退還），或攻擊最後的成長能嚴格贏下其他正置前列攻擊手都贏不了的格子時才合法。對位規劃會計入這個未來成長（上限：四隻其他角色、兩隻先攻的攻擊手）。接受選擇性的盲放能量；強制的攻擊成長自動處理。 |
| Class Visit, Ruby | 等級 2/3 缺城門時，偏好用一張來回收（包括木門判定），讓下回合可以回收。只有休息室有缺的城門時才犧牲多餘的非終結者。被動的回想區輔助自動處理。 |
| Acting of a Genius, Akane Kurokawa -Princess Saya- | 安全的選擇性抽牌、強制的戰力目標；有合格的城門時一律換成可用的 Starry Night。替換在 Girlfriend 加攻之前結算。 |
| On a Starry Night, Akane Kurokawa | 付合格的城門連動；接受每次選擇性回復。保護前列大型角色不被抽牌階段賦予的彈回影響。 |
| Guided Twin Stars, Aqua & Ruby | 只在等級 2／傷害 4 以上提早出；接受選擇性的登場回復和被賦予的倒置時彈回（依時機判斷，而不是猜效果編號）。 |
| For the Success of the Performance, Sumiaki Raida | 終結者登場後才保留／使用助太刀；有安全的非終結者可犧牲時，付額外的減魂費用，目標為目前的戰鬥。 |
| Superstar's Acting / Wound-shifting Scabbard | 手動打名場面並檢查組件合法性。木門的角色回收用分層的手牌優先度；城門的名場面回收在等級 0/1 偏好木門、等級 2/3 偏好城門。 |

`GateTriggerValue`：V7 選用的優先度區段，只用於城門判定的名場面回收。引擎會用 `AddToHand` 的選擇情境初始化真正的城門判定效果，所以會走回收策略，而不是未定義選擇的隨機備案。分類只限於判定效果本身，不影響印在有城門圖示的卡上的其他能力。腳本給的正分獎勵會略過通用的「拒絕選擇性回收」，且不會消耗給未來效果用的 `SearchTarget` 或 `plannedSearchTarget`。沒寫區段或沒有正分選項時，維持舊的城門選擇。TokyoBlade 在等級 0～1 偏好木門、等級 2～3 偏好城門（即使手上已有一張，讓 Girlfriend 可以過濾多的那張）。偏好的名場面不在時，另一張合格的仍是備案。

`DoorTriggerValue`：木門判定的角色回收對應區段。木門同樣用 `AddToHand` 情境，而不是隨機備案。正分獎勵用這張表、不消耗之後的檢索承諾；沒寫或沒有正分時，退回一般的回手優先度。TokyoBlade 在等級 0～1 排序：Aquarium Date Kana、Talking About Breaking Up Aqua、Beyond the Darkness Aqua。等級 2～3 時兩張等級 3 Akane 為最高層，接著 Living Akane 和 Class Visit Ruby。其他合格角色是備案。

**From Now On** 即使連動角色還不在手上，也會修正缺的本等級名場面；既有的提前 Taiki／檢索／等級 2 城門優先度不變。**Miyazaki** 的手動 ACT 在等級 1～2 補缺的 Kana、等級 3 補未分配的 Starry Night（包括已在場上的 Miyazaki 的後期檢查），條件是手上已有對應名場面、啟動前手牌至少 6 張。從手牌出 Miyazaki 前要至少 7 張，登場／登場時效果後會再檢查一次。需要休息室有連動目標和安全的丟牌候選；開機 Saya 承諾的目標受保護。緊急攻擊手和集中回收不再用這個 ACT。第一副牌庫或估計密度低於 25% 時仍可磨牌，並有終結套件和致命重置保護。

**例行的 Happy New Year 集中**：估計名場面密度 **≥20%** 時可用（平均五張一張，不是保證命中）。保留付費終結者的能量、有效的 Raida 防守、付完後另一個緊急攻擊手，以及重置安全。它的橫置費用自然讓每張一回合只能用一次。

**Kana 的 `OnAttackMill(1,4,IsCharacter && If(HasTrait Show Business),0.25)`**：在木門和四隻 Show Business 角色在場時有效。每次連動結算都會依剩下的判定平衡看牌數，盡量剩 1 張牌庫，除非重置後的高壓縮優先。做不到時，任何等級下密度 ≥25% 就輕翻，否則完整翻。獎勵偏好公開的、合格的等級 2 以上角色；三張重排仍偏好乾淨判定，再來是給前列彈回用的等級 2 以上卡。

**主要階段計畫**：確定性回收排在隨機挖牌之前，區分本回合攻擊下限和未來連動組件，並在一般規劃後重新檢查可取得性。Coffee 只在有限的早期時間窗檢索缺的集中角色或 Aquarium Date Kana。Miyazaki 可以在不犧牲攻擊下限的情況下回收引擎。From Now On 在等級 1～2 把名場面換成木門、等級 3 換成城門。Kana 登場時盡量包含便宜的第四隻舞台角色，因為連動需要三隻**其他**角色。等級 2 時，壓縮牌庫保護和已知能量清理補強了約定好的輕翻規則。

**等級 3 計畫**：Living on the Stage 先於付費終結者登場，並保護它的目標和唯一的城門不被當丟牌費用。前列已有 `N` 隻終結者時：Living 從手牌出需要手牌 ≥ `N + 5`；Living 已在場上需要 ≥ `N + 4`。這個門檻計入了打城門和每隻預定終結者保留一張丟牌。另外要有安全的丟牌候選。動態的 `ReserveHand` 防止一般規劃之後把這些燃料花在填充卡上。付費的 Akane 登場同樣保留丟牌燃料；Princess Saya 的選擇性抽牌可以補燃料，但打城門前不超過六張。Saya 的張數受休息室可用 Akane 數限制；會讓替換路線失效的重置時，選擇性抽牌／磨牌會被縮減。初始準備時，加攻引擎不能拿走付費終結者需要的能量。

沒有直接的 Akane 在手上、且休息室有未被保留的 Akane 時，Living 也可以免費出 Saya。Miyazaki 的回收不能拿已經承諾給場上 Saya 的 Akane。

連續的 Akane 連動只需要**一點**初始能量：每次攻擊在判定前付款，判定再補能量給下一次攻擊。登場後，有的話保護兩能量給 Raida，而不是太早為了保留助太刀犧牲終結者。城門打出前需要 2～6 張手牌，打完後至少留 1 張丟牌、最多 5 張。後期回收和手牌修剪不會多出準備好的終結者；城門回合不合法時，仍有木門施壓的備案。攻擊順序會避免在五張手牌的 Akane 之前吃到不必要的木門判定。只有在 Akane 戰力嚴格高於 11000 時，才拒絕對手抽牌時被賦予的彈回。分層的檢索、傷害區、丟牌、犧牲和保護優先度，會保留下一次城門／終結者組件，並避免犧牲目前正在防守戰鬥的角色。

這些都是考慮資源的經驗法則，不保證在資源或對手戰力不允許時仍能三隻終結者、三次成功連動或三格全贏。解析器／規則情境測試不能取代完整的 Unity 實機測試。

**Meetup on Christmas Eve, Ai**：只要付得起丟牌，且 `RefreshCount = 0` 或估計名場面密度 < 25%，就付丟牌並結算「最多看四張」，不管玩家等級或手上有沒有不要的卡。名場面在場時，`LookAtUpToDeckFloor(2,1)` 會限制看牌數留 1 張牌庫，但不跳過之後的回想區步驟。它的目標依區域區分：回想區從休息室選 Ai，公開的獎勵來自 `ScryArea`。等級 0～2 時，獎勵優先拿下回合用的等級 1 Aquarium Date, Kana Arima（包括還在等級 0 時就準備等級 1 連動）。沒有那張 Kana 或在等級 3 時，偏好等級 ≤ 目前等級的合格卡，都沒有就退而拿任何合法的等級 1 以上獎勵。同層內用 SearchValue 排序；目前的能量、顏色和攻擊不足不會阻止拿下回合的獎勵。重置後密度 ≥25% 時，拒絕這個選擇性挖牌以保留壓縮。

**`ShouldSearchForCost0`**（V7 選用問題區段）：控制通用的「能量少時優先檢索 0 費卡」偏好，這個偏好原本優先於 `SearchValue`。省略就維持原行為。TokyoBlade 用 `Return(Level < 2)`：開局檢索仍拿便宜身體，之後的檢索用牌組的組件優先度，而不是自動拿 0 費角色取代 Akane、城門或助太刀。明確的檢索／效果目標仍是最高優先。

### Saki Tenma 8 Choice（V7）

`SakiTenmaAI.txt` 用自訂的 `OpponentReverseThreats`、`OpponentGrowingThreats`、`OpponentBurnShields`、`OpponentSoulReducers`、`OpponentCancelHelpers`、`CookieBounceTargets` 表，而不是借用木門／城門判定區段。倒置威脅用即時的 AUTO 標籤，而不是自動產生的卡名清單。成長戰力清單和明確的對手標籤保留當備案，因為任意條件的戰力和防守行為無法可靠推斷。

等級 3 的 Saki 終結者對燒傷護盾表開啟 `AvoidFacing`。Cookie Cheer 維持只在等級 3 登場／彈回的策略，目標是減魂、終結回合護盾和取消輔助（用新的能力表判斷）。See You 保留加攻和第一個攻擊的優先度。後列保護、重建，以及原本的等級 1 檢索／丟牌和等級 3 名場面門檻不變。

兩張箭頭名場面保留 `AutoAnswer(1,0) Hand < 7` / `AutoAnswer(1,1) Hand >= 7`。舊的互斥目標區間寫法已移除：現在原生的箭頭策略會從引擎合法獎勵中，回手牌選 `SearchValue` 最高、放能量選最低。

### 其他已認得的特殊標籤

- **No Coming in Between**：`EventTarget(AttackingCharacter, CenterStage && Standing)` 先選目前的攻擊手，再選還沒攻擊的前列攻擊手（如果有）。已攻擊過的和後列角色會被排除，即使合法目標少於兩個。
- **選擇性的助太刀費用**和使用加攻助太刀是分開的。要付「防提前登場」費用前，應該用 `AutoIfAndOnlyIf` 檢查有沒有合法、可選的提前登場目標和足夠的丟牌數；只用 `AutoIf` 失敗時可能會落到預設接受。
- **避免多餘的功能卡登場**：用 `OpenStageForNormalPlay` 限制主動過濾。Erna 另外用 `OnlyPlayToOpenStage CountCards(CenterStage,Standing) >= AttackTarget`，在攻擊仍需修補時保留替換死掉格子的能力。
- V7 的出卡失敗、覆蓋選擇、選擇性 AUTO、效果目標診斷紀錄都包含卡名和卡號。出卡失敗還會記錄階段、等級、能量／費用、手牌保留、手牌位置、顏色和空位狀況。

目前列舉中還有這些特殊掛勾。請用完全正確的拼法，使用前先確認對應的 AI 處理程式或參考現有腳本：

```text
OnPlayPay2ToSearch  OnPlayHandClockToSearch  DropSearch  OnPlayPay1ClockToSearch
OnPlayTradeClimaxForCharacter  SacrificeDiscardToSalvage  DiscardToCheck4
OnPlayPeekOpponent  OnPlayTradeWR  OnPlayGivePower  ClimaxExchange
RestSelfSalvageBrainstorm  RestSelfSearchBrainstorm  Pay1ToTriggerTwice
DontSideAttack  DontAttackWithOnlyCopy  AlwaysUseGateTrigger  PlayCounterEvent
PlayBackup  AntiEarlyBackup  UseBackup  DontBackupForPower
OnPlayDiscardRevealSalvageLevel  AccelerateForOnReverseEffect  AutoGivesSelfPower
OnAttackCombo  OnReverseCombo  ResolutionPriority  Backup  EventSearchThenDiscard
OnPlayDraw2Discard1  OnPlayCheck3  OnPlayCheckX  Healer  StockHealer
StandbyClimax  BasicClimax  StockSoulClimax  OnClimaxPlayBounceToGivePower
PrefersMiddlePosition  EarlyPlay2OrLessClimax  EarlyPlayInClock  EarlyPlay4OrMore
EarlyPlayInLevel  ComboLevel  ContSelfPower  ContSelfPowerForEachOther
GlobalPower  GlobalPowerOnCenterStage  Assist  Level3Assist  LevelAssist
BackstageCategory  ActRestSelfToGivePower  ComboRequiresFullStage  CleanCut
OffTrait  SacrificeToSave  OnPlayGainPower  OnAttackGivePower  OnActGainPower
OnEventGainPower  GainPowerWhenAttackingLevel2  GainPowerWhenAttackingLevel3
GainPowerInMiddlePosition  NeverAttackBombs
```

目前原始碼中已知的注意事項：`ClimaxExchange` 被標記為運作不正常；`RestSelfSalvageBrainstorm` 和 `RestSelfSearchBrainstorm` 標記為尚未實作；`PrefersMiddlePosition` 只在少數擺位路徑中有效。**列舉裡有這個名字，不代表行為完整。**

## 12. V7 自訂標籤（Coder-Defined Tags）

V7 的卡片區段可以定義任意標籤（不分大小寫）：

```text
Section [Example Card]
Tags: StandbyTarget, ClockCard, Filter
```

標籤可以直接當布林運算式、用 `HasTag(標籤)`，或放進有參數的條件：

```text
HasTag(Filter)
HandContains(1, StandbyTarget)
StageContains(1, ClockCard)
```

標籤只是普通字串，不是 `CardLabel` 列舉成員。V6 不支援。

## 13. 函式與版本差異

兩個版本共用上面列的能量／牌庫頂知識、機率、效果紀錄、對位、重置、傷害和判定函式。

**只有 V7 有的功能：**

- `ProjectedHand`、`ProjectedStock`、`ProjectedPlayableCharacters`。
- `PlayableCharactersNextTurn`、`ExpectedLevelNextTurn`、`ExpectedStockPaymentsNextTurn(條件, 一能量ACT次數)`。
- `AttacksRemaining`、`WillAttackLast`、`OwnClimaxesTriggered`、`ProjectedDeckSizeAfterAttacks`。
- `KnownCxInTopStock(N)`、`CanUseAuto(N)`、`CanUseAct(N)`。
- 新版 `ShouldEncore3` 全權負責內建三能量再演策略；V6 保留舊的經驗法則。
- `HasTag(標籤)` 和卡片區段的 `Tags: ...`。
- `HasAutoLabel(標籤)` 別名和 `HasEffectCommand(類別, 指令[, 參數開頭])`。
- `AvoidFacing(對手條件)` 和自訂名稱的 `GetPriority` 表。
- `BackstageAvailability` / `AvailableBackstage`、`CenterStageAvailability` / `AvailableCenterStage`、`StandbyCenterStageAvailability`。
- `NeverPlayFromHand` 和全域的 `NeverPlayFromHandTags:` 標頭。
- `EncoreValue` 優先度區段。
- `SetCenterStageLimit(n)` 和 `EndMainPhaseNow()` 動作指令。
- 被認得為 AI 語法的巢狀 `If(...)` 可以直接計算 V7 標籤。

**V6 的相容優勢：**

- 卡片區段標頭和中括號引用可以用卡名或卡號；V7 請用卡名。

## 14. 已知限制與安全注意事項

- 對手隱藏的牌庫和手牌內容，**永遠不能**拿來算任意條件的機率。對手未知池的機率只對 `IsClimax` 有意義；其他條件只看已知的牌庫頂。
- `TopDeckIs(條件)`、`TopStockIs(條件)` 是舊的直接判斷，**不是**有追蹤已知資訊的版本。它們會直接看目前區域的卡，沒有做「已知前綴」檢查；腳本不能用隱藏資訊時請避免使用，改用 `KnownTopCardIs(...)`、`OpponentKnownTopCardIs(...)` 和能量記憶函式。
- 重置輔助函式預測的是牌庫張數邊界。傷害函式估計一般傷害，不考慮所有防傷、不能取消、減傷或特殊燒傷規則。
- 對位預測包含目前的預測器和支援的預定加攻，不是所有可能的效果或未來回應。
- 效果編號從 1 開始，AUTO 和 ACT 分開編號。`UsedAuto` 是布林值；`TimesUsedAuto`、`TimesUsedAct` 是完成結算的次數。
- **例外**：V6 的 `DropSearch(N, 條件)` 取得標籤直接用 `GetAutoEffects()[N]` 索引，所以第一個 AUTO 要寫 `0`。費用詢問標籤（如 `AutoIfAndOnlyIf(1)`）和動作（如 `UseAct(1, [卡名])`）仍從 1 開始。
- 腳本解析和函式支援會隨程式碼改變。新增函式、標籤或動作時，請確認它在所選 AI 版本中**既有宣告、也有處理**。

## 15. 撰寫檢查清單

1. 確認 `AIList.txt` 指向正確的腳本檔名和版本。
2. 用 `Section Header`，每個規則區塊用正確登錄的區段名稱。
3. 卡名要完全正確（V7 尤其嚴格），每個中括號引用都要確認。
4. 每行一個優先度指令，由一般到具體排列（後面的會覆蓋前面的）。
5. 外層邏輯可能和巢狀卡片效果條件混淆時，用 `&&&` / `|||`。
6. 動作指令放在支援的動作區段內，大括號要一致。
7. 在卡片區段加標籤前，先確認該標籤有實作。
8. 在遊戲中實測，並查看 AI 紀錄有沒有解析錯誤、無效區段或意外的目標選擇。
