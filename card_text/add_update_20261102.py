# 2026-11-02 模擬器 0.7.9.6 新增的介面（牌組資料夾／標籤、起手模擬、對戰聊天）
# 寫進 files\AutoTranslator\Translation\{tw,hk,cn}\Text\WSZH_Translations.txt 的最後一段，並同步到模擬器
# 再跑一次會先移除舊的這一段再重寫
import os, sys
D = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, D)
from zhcn import to_cn
BASE = os.path.join(D, '..', 'files', 'AutoTranslator', 'Translation')
SIM = r'C:\Users\Gary\Desktop\WS模擬器\AutoTranslator\Translation'
HEAD = '// ===== 2026-11-02 模擬器 0.7.9.6 新介面 ====='

# 起手模擬右邊的卡片資訊「Character | Level 0 | Cost 0 / Green | None」（實際是換行）
MULL_DETAIL = []
for cat, cz in (('Character', '角色'), ('Event', '事件'), ('Climax', '名場面')):
    for col, colz in (('Yellow', '黃'), ('Green', '綠'), ('Red', '紅'), ('Blue', '藍'), ('Purple', '紫')):
        MULL_DETAIL.append(('r:"^%s [|] Level ([0-9]+) [|] Cost ([0-9]+)\\n%s [|] None$"' % (cat, col), '%s｜等級 $1｜費用 $2\\n%s｜無判定' % (cz, colz)))
        MULL_DETAIL.append(('r:"^%s [|] Level ([0-9]+) [|] Cost ([0-9]+)\\n%s [|] (.+)$"' % (cat, col), '%s｜等級 $1｜費用 $2\\n%s｜判定 $3' % (cz, colz)))

ENTRIES = [
    '// --- 牌組選擇：資料夾／標籤 ---',
    ('Search deck names or tags...', '搜尋牌組名稱或標籤...'),
    ('New Folder', '新增資料夾'),
    ('Move Deck', '移動牌組'),
    ('Edit Tags', '編輯標籤'),
    ('Deck Library', '牌組資料夾'),
    ('Create Folder', '建立資料夾'),
    ('New folder name', '新資料夾名稱'),
    ('Deck name', '牌組名稱'),
    ('Tag 1', '標籤 1'), ('Tag 2', '標籤 2'), ('Tag 3', '標籤 3'), ('Tag 4', '標籤 4'),
    ('Tags (optional, up to 4; 24 characters each)', '標籤（可不填，最多 4 個，每個最多 24 字）'),
    ('Location: Decks', '位置：Decks'),
    ('r:"^Location: Decks(.+)$"', '位置：Decks$1'),          # 路徑分隔是反斜線，XUnity 會吃掉規則裡的反斜線，所以不比對它
    ('Save deck to folder', '儲存牌組到資料夾'),
    ('Create folders in Decks', '在 Decks 裡建立資料夾'),
    ('Edit deck tags (up to 4)', '編輯牌組標籤（最多 4 個）'),
    ('Save Deck', '儲存牌組'),
    ('Use Folder', '使用這個資料夾'),
    ('Save Tags', '儲存標籤'),
    ('Move Here', '移到這裡'),
    ('Choose a location inside Decks.', '請選擇 Decks 裡的位置。'),
    ('Linked folders are not supported in the deck library.', '牌組資料夾不支援連結資料夾。'),
    ('Linked deck files are not supported.', '不支援連結的牌組檔案。'),
    ('Enter a name of 1-100 characters, without leading dots or trailing dots/spaces.', '請輸入 1～100 字的名稱，開頭不能是句點，結尾不能是句點或空白。'),
    ('That name is reserved by the deck library or operating system.', '這個名稱被牌組資料夾或作業系統保留，不能使用。'),
    ('The selected folder no longer exists.', '選擇的資料夾已經不存在。'),
    ('A folder or file with that name already exists.', '已經有同名的資料夾或檔案。'),
    ('Built-in AI decks cannot be moved because they are maintained by the updater.', '內建的 AI 牌組由更新程式維護，不能移動。'),
    ('Built-in AI decks cannot be renamed.', '內建的 AI 牌組不能重新命名。'),
    ('A deck can have at most 4 tags.', '一副牌組最多 4 個標籤。'),
    ('Each tag must be at most 24 characters and contain no control characters.', '每個標籤最多 24 字，且不能有控制字元。'),
    ('The selected deck no longer exists.', '選擇的牌組已經不存在。'),
    ('r:"^Could not save deck: (.+)$"', '無法儲存牌組：$1'),
    ('r:"^Deck moved, but updating the default deck list failed: (.+)$"', '牌組已移動，但更新預設牌組清單失敗：$1'),
    ('r:"^The deck could not be renamed[.]\\n(.+)$"', '無法重新命名牌組。\\n$1'),
    ('r:"^(.+) already exists[.] Are you sure you want to overwrite it[?]\\nYour deck has fewer than 8 climax[.] While this is not against the rules, it is very ill advised[.] Save anyway[?]$"',
     '$1 已經存在，確定要覆蓋嗎？\\n你的牌組名場面卡少於 8 張。雖然不違反規則，但非常不建議。仍要儲存嗎？'),
    '// --- 牌組編輯：起手模擬（Mulligan） ---',
    ('Mulligan', '起手模擬'),
    ('MULLIGAN LAB', '起手模擬'),
    ('CARD PREVIEW', '卡片預覽'),
    ('SELECT DISCARDS  /  CONFIRM EACH HAND  /  RESET FOR FRESH SAMPLES', '選擇要換掉的牌　／　逐手確認　／　按「全部重發」重新抽樣'),
    ('r:"^HAND ([0-9]+)$"', '第 $1 手'),
    ('Add at least 10 cards to sample a five-card hand and up to five replacements.', '牌組至少要有 10 張卡，才能模擬 5 張起手和最多 5 張換牌。'),
    ('Reset all hands', '全部重發'),
    ('Inspect a card', '查看卡片'),
    ('Hover or focus a card to see its artwork.\\n\\nClick cards to mark discards, then confirm each hand separately.\\n\\nReplacements come from that hand\'s remaining deck. Discards stay out of the deck.\\n\\nReset deals five fresh hands. Your edited deck is never changed.',
     '滑鼠移到卡片上可以看卡圖。\\n\\n點卡片標記要換掉的牌，再分別確認每一手。\\n\\n換進來的牌從那一手剩下的牌庫抽，換掉的牌不會回到牌庫。\\n\\n「全部重發」會重新發 5 手牌。不會改動你正在編輯的牌組。'),
    ('Keep hand', '保留這手'),
    ('Dealing...', '發牌中...'),
    ('Choose discards or keep all five', '選擇要換掉的牌，或 5 張全部保留'),
    ('r:"^([0-9]+) to discard [|] ([0-9]+) to keep$"', '換掉 $1 張｜保留 $2 張'),
    ('r:"^Mulligan ([0-9]+)$"', '換掉 $1 張'),
    ('r:"^Discarding ([0-9]+)[.][.][.]$"', '正在換掉 $1 張...'),
    ('r:"^Drawing ([0-9]+) replacements?[.][.][.]$"', '正在抽 $1 張補牌...'),
    ('Complete', '完成'),
    ('r:"^Final hand [|] Kept ([0-9]+) / Drew ([0-9]+)\\n([0-9]+) in deck [|] ([0-9]+) in waiting room$"', '最後手牌｜保留 $1 張／補抽 $2 張\\n牌庫 $3 張｜休息室 $4 張'),
    ('r:"^([0-9]+) cards [|] Five independent shuffled decks$"', '$1 張｜5 副各自洗好的牌庫'),
    ('r:"^([0-9]+) cards [|] Five independent shuffled decks [|] Partial deck - not a standard 50-card sample$"', '$1 張｜5 副各自洗好的牌庫｜牌組不滿 50 張，不是標準的抽樣'),
    *MULL_DETAIL,
    ('DISCARD', '換掉'),
    ('KEEP', '保留'),
    ('NEW', '補抽'),
    '// --- 對戰聊天 ---',
    ('Player Chat', '玩家聊天'),
    ('Connecting...', '連線中...'),
    ('Connected', '已連線'),
    ('Not connected - your draft is kept', '未連線，已輸入的內容會保留'),
    ('Say hello to your opponent.\\nMessages are kept for this duel (latest 300).', '跟對手打聲招呼吧。\\n訊息會保留到這場對戰結束（最新 300 則）。'),
    ('Type a message...', '輸入訊息...'),
    ('Send', '送出'),
    ('Enter: send   |   Shift+Enter: new line', 'Enter：送出　｜　Shift+Enter：換行'),
    ('Copy history', '複製紀錄'),
    ('Conversation copied to clipboard', '已複製聊天紀錄'),
    ('No messages to copy yet', '還沒有訊息可以複製'),
    ('Sounds: on', '音效：開'),
    ('Sounds: off', '音效：關'),
    ('Type a message before sending', '請先輸入訊息'),
    ('Cannot send: not connected. Your draft is kept.', '無法送出：未連線。已輸入的內容會保留。'),
    ('r:"^You  -  ([0-9]+:[0-9]+)$"', '你  -  $1'),
    ('r:"^Opponent  -  ([0-9]+:[0-9]+)$"', '對手  -  $1'),
    ('r:"^Server  -  ([0-9]+:[0-9]+)$"', '系統  -  $1'),
    '// --- 戰紀錄 ---',
    ('Turn Order', '先後攻'),
]


def esc(s):
    return s if s.startswith('r:') else s.replace('=', '\\=')


def section(lang):
    out = [HEAD]
    for e in ENTRIES:
        if isinstance(e, str): out.append(e); continue
        k, v = e
        if lang == 'cn': v = to_cn(v)
        out.append(esc(k) + '=' + v)
    return out


for lang in ('tw', 'hk', 'cn'):
    p = os.path.join(BASE, lang, 'Text', 'WSZH_Translations.txt')
    raw = open(p, 'rb').read(); bom = raw.startswith(b'\xef\xbb\xbf')
    s = raw.decode('utf-8-sig'); nl = '\r\n' if '\r\n' in s else '\n'
    lines = s.split(nl)
    if HEAD in lines: lines = lines[:lines.index(HEAD)]
    while lines and lines[-1] == '': lines.pop()
    keys = {l.split('=', 1)[0] for l in lines if '=' in l and not l.startswith('//')}
    new = [l for l in section(lang) if l.startswith('//') or l.split('=', 1)[0] not in keys]
    out = (b'\xef\xbb\xbf' if bom else b'') + (nl.join(lines + new) + nl).encode('utf-8')
    open(p, 'wb').write(out)
    sp = os.path.join(SIM, lang, 'Text', 'WSZH_Translations.txt')
    if os.path.isdir(os.path.dirname(sp)): open(sp, 'wb').write(out)
    print(lang, len([l for l in new if not l.startswith('//')]), 'entries')
