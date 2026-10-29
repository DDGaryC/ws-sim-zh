# 產生中文搜尋用的資料（遊戲裡 Names.cs 讀取）：
#   AutoTranslator\WSZH\series.txt  系列英文名<Tab>中文別名（| 分隔，含簡體）── 系列欄可以打中文
#   AutoTranslator\WSZH\cjkmap.txt  漢字對照（一行「字<Tab>統一後的字」）── 日文卡名用的漢字（黒、姫、桜…）、
#                                    簡體字都統一成繁體，打中文也能找到日文卡名
#   AutoTranslator\WSZH\jpnames.txt 補充日文卡名（模擬器沒有日文名的卡，用日版官網的資料）
# 寫到模擬器與 files\ 兩處。用法：python make_name_maps.py
# check_update.py 也會用這裡的函式檢查「沒有中文名的系列」「沒有日文名的卡」。
import glob, os, re
D = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(D)
SIM = r'C:\Users\Gary\Desktop\WS模擬器'
CARDS = os.path.join(SIM, 'CardContent', 'Cards')
SERIES_ZH = os.path.join(D, 'series_zh.txt')
OFFICIAL = os.path.join(D, 'jpnames_ws_tcg.tsv')          # fetch_jpnames.py 抓的全部官網卡名＋check_update.py 補查的
# OpenCC 沒有換掉、但中文慣用寫法不同的字
EXTRA = {'姫': '姬', '鬭': '鬥', '闘': '鬥', '嬢': '孃', '縁': '緣', '銃': '銃', '渋': '澀', '亜': '亞', '巻': '卷', '弐': '貳', '桜': '櫻'}


def load_series_zh():
    out = []
    for line in open(SERIES_ZH, encoding='utf-8'):
        if line.startswith('#') or '\t' not in line: continue
        en, zh = line.rstrip('\n').split('\t', 1)
        out.append((en.strip(), [n.strip() for n in zh.split('|') if n.strip()]))
    return out


def sim_series():
    # 模擬器的系列英文名（牌組編輯系列欄用的就是這個）
    s = set()
    for f in glob.glob(os.path.join(CARDS, '*', 'SingleSetData.txt')):
        for line in open(f, encoding='utf-8', errors='ignore'):
            if line.startswith('Series:'): s.add(line.split(':', 1)[1].strip())
    return s


def missing_series():
    have = set(en for en, _ in load_series_zh())
    return sorted(s for s in sim_series() if s and s not in have)


def load_official():
    official = {}
    if os.path.exists(OFFICIAL):
        for line in open(OFFICIAL, encoding='utf-8'):
            p = line.rstrip('\n').split('\t')
            if len(p) == 2: official[p[0]] = p[1]
    return official


def jp_number(cid):
    head, no = cid.rsplit('-', 1)
    for a, b in (('TE', 'T'), ('PE', 'P')):
        if no.startswith(a): return head + '-' + b + no[2:]
    return head + '-' + (no[1:] if no.startswith('E') else no)


def candidates(cid):
    # 模擬器卡號 → 官網可能的卡號（英文版 E／TE／PE 前綴、補零、P 開頭）
    jp = jp_number(cid)
    cands = [jp]
    mm = re.match(r'^(.*-[A-Z]*)(\d{1,2})$', jp)
    if mm: cands += [mm.group(1) + mm.group(2).zfill(3), mm.group(1) + 'P' + mm.group(2).zfill(2)]
    return cands


def cards_without_jp():
    # 模擬器裡沒有日文名的卡（卡號 → 資料夾）
    has_jp = set()
    for f in glob.glob(os.path.join(CARDS, '*', '*', 'JPNames.txt')):
        for line in open(f, encoding='utf-8', errors='ignore'):
            if ' ' in line: has_jp.add(line.split(' ', 1)[0].strip())
    out = {}
    for f in glob.glob(os.path.join(CARDS, '*', '*', 'CardData.txt')):
        folder = os.path.relpath(os.path.dirname(f), CARDS)
        cur, jpname = None, False
        for line in list(open(f, encoding='utf-8', errors='ignore')) + ['Character: <end>']:
            m = re.match(r'^(Character|Event|Climax):\s*(\S+)', line)
            if m:
                if cur and not jpname and cur not in has_jp and '/' in cur and '-' in cur: out[cur] = folder
                cur, jpname = m.group(2), False
            elif line.startswith('JPName'): jpname = True
    return out


def supplement(official=None):
    official = load_official() if official is None else official
    sup, miss = {}, {}
    for cid, folder in cards_without_jp().items():
        hit = next((official[c] for c in candidates(cid) if c in official), None)
        if hit: sup[cid] = hit
        else: miss[cid] = folder
    return sup, miss


def build():
    import opencc
    jp2t, s2t, t2s = opencc.OpenCC('jp2t'), opencc.OpenCC('s2t'), opencc.OpenCC('t2s')
    series = []
    for en, names in load_series_zh():
        names = names + [t2s.convert(n) for n in names if t2s.convert(n) != n]
        series.append((en, names))

    # 日文卡名裡出現的字
    chars = set()
    for f in glob.glob(os.path.join(CARDS, '*', '*', 'JPNames.txt')):
        for line in open(f, encoding='utf-8', errors='ignore'): chars.update(line)
    for f in glob.glob(os.path.join(CARDS, '*', '*', 'CardData.txt')):
        for line in open(f, encoding='utf-8', errors='ignore'):
            if line.startswith('JPName'): chars.update(line)
    for _, names in series:
        for n in names: chars.update(n)
    official = load_official()
    sup, miss = supplement(official)
    for n in sup.values(): chars.update(n)
    cjk = sorted(c for c in chars if '\u3400' <= c <= '\u9fff' or '\uf900' <= c <= '\ufaff')

    m = {}
    for c in cjk:
        t = EXTRA.get(c) or jp2t.convert(c)
        t = EXTRA.get(t, t)
        if t != c: m[c] = t
    canon = set(m.get(c, c) for c in cjk)
    for t in sorted(canon):                           # 簡體 → 繁體（打簡體也找得到）；排序讓每次結果一樣
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
            for k in sorted(sup): f.write(k + '\t' + sup[k] + '\n')
        with open(os.path.join(d, 'cjkmap.txt'), 'w', encoding='utf-8', newline='\n') as f:
            f.write('# 漢字對照：日文漢字、簡體字 → 繁體（由 card_text\\make_name_maps.py 產生）\n')
            for k in sorted(m): f.write(k + '\t' + m[k] + '\n')
    print('series', len(series), 'cjk chars', len(cjk), 'map', len(m), 'official names', len(official),
          'supplement', len(sup), 'still missing', len(miss))


if __name__ == '__main__':
    build()
