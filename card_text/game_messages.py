# 遊戲程式裡「拼接後才顯示」的訊息（卡名、數字在中間），用 regex 規則翻譯
# 產生 Translation\{tw,hk,cn}\Text\GameMessages.txt（模擬器與 files\ 兩處）
# 清單來源：反編譯 Assembly-CSharp 後用 audit_concat.py 找出翻譯檔沒有涵蓋的拼接訊息（只收玩家看得到的，不收卡片腳本的除錯訊息）
import os
SIM = r'C:\Users\Gary\Desktop\WS模擬器'
REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
N = r'([0-9]+)'

# (規則, 台灣, 簡體)；香港版由台灣版轉換
RULES = [
    (r'^Attack (.+) on backstage\. \(your left\)$', '攻擊後列的「$1」（你的左邊）', '攻击后列的「$1」（你的左边）'),
    (r'^Attack (.+) on backstage\. \(your right\)$', '攻擊後列的「$1」（你的右邊）', '攻击后列的「$1」（你的右边）'),
    (r'^Attack (.+) on center stage\. \(middle\)$', '攻擊前列的「$1」（中間）', '攻击前列的「$1」（中间）'),
    (r'^Attack (.+) on center stage\. \(your left\)$', '攻擊前列的「$1」（你的左邊）', '攻击前列的「$1」（你的左边）'),
    (r'^Attack (.+) on center stage\. \(your right\)$', '攻擊前列的「$1」（你的右邊）', '攻击前列的「$1」（你的右边）'),
    (r'^Choose %s cards from your waiting room to send$' % N, '請從休息室選擇 $1 張卡（支付費用）', '请从控室选择 $1 张卡（支付费用）'),
    (r'^Choose %s cards from your waiting room to shuffle into your deck$' % N, '請從休息室選擇 $1 張卡洗回牌庫', '请从控室选择 $1 张卡洗回卡组'),
    (r'^Choose %s characters from your waiting room to shuffle into your deck$' % N, '請從休息室選擇 $1 張角色洗回牌庫', '请从控室选择 $1 张角色洗回卡组'),
    (r'^Choose %s non-climax cards from your waiting room to shuffle into your deck$' % N, '請從休息室選擇 $1 張名場面以外的卡洗回牌庫', '请从控室选择 $1 张高潮以外的卡洗回卡组'),
    (r'^Choose %s cards to put under (.+) as a marker\.$' % N, '請選擇 $1 張卡，放置到「$2」下方作為標記。', '请选择 $1 张卡，放置到「$2」下方作为标记。'),
    (r'^Choose %s characters to put under (.+) as a marker\.$' % N, '請選擇 $1 張角色，放置到「$2」下方作為標記。', '请选择 $1 张角色，放置到「$2」下方作为标记。'),
    (r'^Choose a characters to put (.+) under as a marker\.$', '請選擇 1 張角色，將「$1」放置到它下方作為標記。', '请选择 1 张角色，将「$1」放置到它下方作为标记。'),
    (r'^Choose %s cards to reveal$' % N, '請選擇 $1 張卡公開', '请选择 $1 张卡公开'),
    (r'^Choose %s cards to send from memory to waiting room\.$' % N, '請選擇回想區的 $1 張卡放置到休息室。', '请选择回想区的 $1 张卡放置到控室。'),
    (r'^Choose %s cards to send\.$' % N, '請選擇 $1 張卡（支付費用）。', '请选择 $1 张卡（支付费用）。'),
    (r'^Choose %s characters to send\.$' % N, '請選擇 $1 張角色（支付費用）。', '请选择 $1 张角色（支付费用）。'),
    (r'^Send %s cards\.$' % N, '選擇 $1 張卡（支付費用）。', '选择 $1 张卡（支付费用）。'),
    (r'^Choose cards? to flip face-down$', '請選擇要翻成背面朝上的卡', '请选择要翻成背面朝上的卡'),
    (r'^Choose cards? to flip face-up$', '請選擇要翻成正面朝上的卡', '请选择要翻成正面朝上的卡'),
    (r'^Send (.+) to the waiting room to return (.+) to the stage rested\?$', '要將「$1」放置到休息室，讓「$2」以橫置狀態回到舞台嗎？', '要将「$1」放置到控室，让「$2」以横置状态回到舞台吗？'),
    (r'^Opponent chose <(.+)>$', '對手選擇了《$1》', '对手选择了《$1》'),
    (r'^Opponent chose (.+)\.$', '對手選擇了「$1」。', '对手选择了「$1」。'),
    (r'^Opponent must discard down to %s cards\.$' % N, '對手必須把手牌丟棄到剩 $1 張。', '对手必须把手牌丢弃到剩 $1 张。'),
    (r'^You Lose(.*)$', '你輸了$1', '你输了$1'),
    (r'^Player ([12]) wins$', '玩家 $1 獲勝', '玩家 $1 获胜'),
    (r'^Replay$', '重播', '重播'),
    # 模擬器 0.7.9.5 新增
    (r'^Emote Pack$', '表情包', '表情包'),
    (r'^Default$', '預設', '默认'),
    (r'^Rematch$', '再來一場', '再来一局'),
    (r'^Inspect Board$', '查看場面', '查看场面'),
    (r'^Flip Stock$', '翻開能量區', '翻开能量区'),
    (r"^Flip Opponent's Stock$", '翻開對手的能量區', '翻开对手的能量区'),
    (r'^View My Deck$', '查看我的牌庫', '查看我的卡组'),
    (r"^View Opponent's Deck$", '查看對手的牌庫', '查看对手的卡组'),
    (r'^Viewing your remaining deck\.$', '正在查看你剩下的牌庫。', '正在查看你剩下的卡组。'),
    (r"^Viewing your opponent's remaining deck\.$", '正在查看對手剩下的牌庫。', '正在查看对手剩下的卡组。'),
    (r'^Post-game inspection is only available after the match ends\.$', '對戰結束後才能查看場面。', '对战结束后才能查看场面。'),
    (r'^Stock can only be flipped for inspection after the match ends\.$', '對戰結束後才能翻開能量區查看。', '对战结束后才能翻开能量区查看。'),
    (r'^Opponent stock inspection is only available in local matches\.$', '只有本機對戰可以查看對手的能量區。', '只有本机对战可以查看对手的能量区。'),
    (r'^Online stock inspection must be requested from the server\.$', '線上對戰要由主機查看能量區。', '线上对战要由主机查看能量区。'),
    (r'^The deck name contains (.+): (.+)\s*Please remove (it|them) and try again\.$', '牌組名稱含有不能用的字元：$2\\n請刪除後再試一次。', '卡组名称含有不能用的字符：$2\\n请删除后再试一次。'),
    (r'^(.+) Please choose a different deck\.$', '$1 請換一副牌組。', '$1 请换一副卡组。'),
    (r'^Deck is not legal for format (.+) \((.*)\)\.$', '牌組不符合「$1」賽制的規定（$2）。', '卡组不符合「$1」赛制的规定（$2）。'),
    (r'^Contains more than max \(%s\) copies of (.+)$' % N, '「$2」超過上限 $1 張', '「$2」超过上限 $1 张'),
    (r'^A deck named (.+) already exists\.$', '已經有名為「$1」的牌組。', '已经有名为「$1」的卡组。'),
    (r'^(.+) could not be found\.$', '找不到「$1」。', '找不到「$1」。'),
    (r'^Card not found: (.+)$', '找不到卡片：$1', '找不到卡片：$1'),
    (r'^Illegal deck\. (.*)$', '牌組不合規定。$1', '卡组不合规定。$1'),
    (r'^Error loading deck\. (.*)$', '讀取牌組時發生錯誤。$1', '读取卡组时发生错误。$1'),
    (r'^Problem with code in card (.+)$', '卡片腳本有問題：$1', '卡片脚本有问题：$1'),
    (r'^BGM changed for (.+) \(unsaved\)$', '已變更「$1」的背景音樂（尚未儲存）', '已变更「$1」的背景音乐（尚未保存）'),
    (r'^SFX changed for (.+) \(unsaved\)$', '已變更「$1」的音效（尚未儲存）', '已变更「$1」的音效（尚未保存）'),
    (r'^Preview failed: (.*)$', '試聽失敗：$1', '试听失败：$1'),
    (r'^Preview file not found: (.*)$', '找不到試聽檔案：$1', '找不到试听文件：$1'),
    (r'^Save failed: (.*)$', '儲存失敗：$1', '保存失败：$1'),
    (r'^Failed to read catalog: (.*)$', '讀取清單失敗：$1', '读取清单失败：$1'),
    (r'^Could not start the simulator updater: (.*)$', '無法啟動模擬器更新程式：$1', '无法启动模拟器更新程序：$1'),
    (r'^Received bad sleeve data from opponent\. %s bytes$' % N, '收到對手錯誤的卡套資料（$1 位元組）', '收到对手错误的卡套数据（$1 字节）'),
]
HK = [('休息室', '控制室'), ('能量區', '費用區')]

def esc(s): return s.replace('=', '\\=')

head = '// WS 模擬器中文化套件：遊戲拼接後顯示的訊息（由 card_text\\game_messages.py 產生，請勿手動修改）\n'
for lang in ('tw', 'hk', 'cn'):
    lines = []
    for rx, tw, cn in RULES:
        zh = cn if lang == 'cn' else tw
        if lang == 'hk':
            for a, b in HK: zh = zh.replace(a, b)
        lines.append('r:"%s"=%s' % (esc(rx), esc(zh)))
    data = head + '\n'.join(lines) + '\n'
    for root in (os.path.join(SIM, 'AutoTranslator'), os.path.join(REPO, 'files', 'AutoTranslator')):
        d = os.path.join(root, 'Translation', lang, 'Text')
        if os.path.isdir(d):
            open(os.path.join(d, 'GameMessages.txt'), 'w', encoding='utf-8', newline='\n').write(data)
print('rules', len(RULES))
