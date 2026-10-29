# 產生中文搜尋用的資料（遊戲裡 Names.cs 讀取）：
#   AutoTranslator\WSZH\series.txt  系列英文名<Tab>中文別名（| 分隔，含簡體）── 系列欄可以打中文
#   AutoTranslator\WSZH\cjkmap.txt  漢字對照（一行「字<Tab>統一後的字」）── 日文卡名用的漢字（黒、姫、桜…）、
#                                    簡體字都統一成繁體，打中文也能找到日文卡名
# 寫到模擬器與 files\ 兩處。用法：python make_name_maps.py
import glob, os, re
import opencc
D = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(D)
SIM = r'C:\Users\Gary\Desktop\WS模擬器'
jp2t, s2t, t2s = opencc.OpenCC('jp2t'), opencc.OpenCC('s2t'), opencc.OpenCC('t2s')
# OpenCC 沒有換掉、但中文慣用寫法不同的字
EXTRA = {'姫': '姬', '鬭': '鬥', '闘': '鬥', '嬢': '孃', '縁': '緣', '銃': '銃', '渋': '澀', '亜': '亞', '巻': '卷', '弐': '貳', '桜': '櫻'}

# 系列中文名
series = []
for line in open(os.path.join(D, 'series_zh.txt'), encoding='utf-8'):
    if line.startswith('#') or '\t' not in line: continue
    en, zh = line.rstrip('\n').split('\t', 1)
    names = [n.strip() for n in zh.split('|') if n.strip()]
    names += [t2s.convert(n) for n in names if t2s.convert(n) != n]
    series.append((en, names))

# 日文卡名裡出現的字
chars = set()
cards = os.path.join(SIM, 'CardContent', 'Cards')
for f in glob.glob(os.path.join(cards, '*', '*', 'JPNames.txt')):
    for line in open(f, encoding='utf-8', errors='ignore'): chars.update(line)
for f in glob.glob(os.path.join(cards, '*', '*', 'CardData.txt')):
    for line in open(f, encoding='utf-8', errors='ignore'):
        if line.startswith('JPName'): chars.update(line)
for _, names in series:
    for n in names: chars.update(n)

# 補充日文卡名：模擬器沒有日文名的卡，用日版官網的資料（fetch_jpnames.py 抓的 jpnames_ws_tcg.tsv）補
def jp_number(cid):
    head, no = cid.rsplit('-', 1)
    for a, b in (('TE', 'T'), ('PE', 'P')):
        if no.startswith(a): return head + '-' + b + no[2:]
    return head + '-' + (no[1:] if no.startswith('E') else no)

official = {}
tsv = os.path.join(D, 'jpnames_ws_tcg.tsv')
if os.path.exists(tsv):
    for line in open(tsv, encoding='utf-8'):
        p = line.rstrip('\n').split('\t')
        if len(p) == 2: official[p[0]] = p[1]
has_jp = set()
for f in glob.glob(os.path.join(cards, '*', '*', 'JPNames.txt')):
    for line in open(f, encoding='utf-8', errors='ignore'):
        if ' ' in line: has_jp.add(line.split(' ', 1)[0].strip())
supplement = {}
for f in glob.glob(os.path.join(cards, '*', '*', 'CardData.txt')):
    cur, jpname = None, False
    for line in list(open(f, encoding='utf-8', errors='ignore')) + ['Character: <end>']:
        m = re.match(r'^(Character|Event|Climax):\s*(\S+)', line)
        if m:
            if cur and not jpname and cur not in has_jp and '/' in cur and '-' in cur:
                jp = jp_number(cur)
                cands = [jp]
                mm = re.match(r'^(.*-[A-Z]*)(\d{1,2})$', jp)
                if mm: cands += [mm.group(1) + mm.group(2).zfill(3), mm.group(1) + 'P' + mm.group(2).zfill(2)]
                for c in cands:
                    if c in official: supplement[cur] = official[c]; break
            cur, jpname = m.group(2), False
        elif line.startswith('JPName'): jpname = True
for n in supplement.values(): chars.update(n)
cjk = [c for c in chars if '\u3400' <= c <= '\u9fff' or '\uf900' <= c <= '\ufaff']

m = {}
for c in cjk:
    t = EXTRA.get(c) or jp2t.convert(c)
    t = EXTRA.get(t, t)
    if t != c: m[c] = t
canon = set(m.get(c, c) for c in cjk)
for t in canon:                                   # 簡體 → 繁體（打簡體也找得到）
    s = t2s.convert(t)
    if s != t and s not in m: m[s] = t
    j = s2t.convert(t)
    if j != t and t not in m: m[t] = j

for root in (os.path.join(SIM, 'AutoTranslator'), os.path.join(REPO, 'files', 'AutoTranslator')):
    d = os.path.join(root, 'WSZH')
    os.makedirs(d, exist_ok=True)
    with open(os.path.join(d, 'series.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('# 系列中文名（由 card_text\\make_name_maps.py 產生）\n')
        for en, names in series: f.write(en + '\t' + '|'.join(names) + '\n')
    with open(os.path.join(d, 'jpnames.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('# 補充日文卡名：模擬器沒有日文名的卡（卡號<Tab>日文卡名，資料來自日版官網；由 card_text\\make_name_maps.py 產生）\n')
        for k in sorted(supplement): f.write(k + '\t' + supplement[k] + '\n')
    with open(os.path.join(d, 'cjkmap.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('# 漢字對照：日文漢字、簡體字 → 繁體（由 card_text\\make_name_maps.py 產生）\n')
        for k in sorted(m): f.write(k + '\t' + m[k] + '\n')
print('series', len(series), 'cjk chars', len(cjk), 'map', len(m), 'official names', len(official), 'supplement', len(supplement))
