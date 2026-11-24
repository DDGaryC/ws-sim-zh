# 把 card_text\*_tw.json 裝到模擬器的 AutoTranslator\Translation\tw\CardText\（本機測試用）
# 香港版在遊戲裡由台灣版轉換，不另外存檔。發佈到 GitHub 用 publish_cardtext.py。
# 另外把「獲得以下能力"Auto: ..."」引號裡的能力拆出來另存一份（角色實際獲得能力後會單獨顯示那一段）
import json, os, glob, sys, re, shutil
D = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, D)
EN_Q = re.compile(r'"((?:Auto|Cont|Act): .+?)"(?=\s*(?:$|[.,]|and |After| "|\(|to |until|Then|If |$))')
ZH_Q = re.compile(r'「(【(?:自|永|起)】.*?)」(?=$|[。，、]|並|，|「|之後|直到|（|$)')

def quoted_pairs(en, zh):
    a = [m.group(1) for m in EN_Q.finditer(en)]
    b = [m.group(1) for m in ZH_Q.finditer(zh)]
    if a and len(a) == len(b): return list(zip(a, b))
    return []

# 「以任意順序執行以下 2 個效果」「選擇以下 1 個效果」：遊戲會把各效果單獨列成「1. …」讓玩家選，拆出每一段
EN_FOLLOW = re.compile(r'following (?:\d+ |one |two |three )?effects?|in any order\.')
ZH_FOLLOW = re.compile(r'以下\s*\d*\s*個效果')

def zh_pieces(s):
    out, depth, start = [], 0, 0
    for i, c in enumerate(s):
        if c == '「':
            if depth == 0: start = i + 1
            depth += 1
        elif c == '」' and depth:
            depth -= 1
            if depth == 0: out.append(s[start:i])
    return out

def effect_pairs(en, zh):
    m, n = EN_FOLLOW.search(en), ZH_FOLLOW.search(zh)
    if not m or not n: return []
    a = re.findall(r'"([^"]+)"', en[m.end():])
    b = zh_pieces(zh[n.end():])
    if len(a) < 2 or len(a) != len(b) or en[m.end():].count('"') != 2 * len(a): return []
    return list(zip(a, b))

# 對戰提示（MultipleChoiceQuestion 等）裡「\n1. …\n2. …」的選項：遊戲會先 EnrichenText 再顯示，XUnity 整段對不到，
# 由 CardText 逐行翻（「N. 」後面那段查表），所以把 CardPrompts 裡的選項逐行拆出來
PROMPTS = os.path.join(os.path.dirname(D), 'files', 'AutoTranslator', 'Translation', 'tw', 'Text', 'CardPrompts.txt')
OPT = re.compile(r'^\s*(\d{1,2})\.\s+(.+?)\s*$')
ENRICH = re.compile(r'CxCombo|CXCOMBO|Auto:|Act:|Cont:|REPLAY|BOND/|《|\b(?:ENCORE|BRAINSTORM|ALARM|SHIFT|BACKUP|GREAT PERFORMANCE|ACCELERATE|FORCE|SUNDER|MERGE|FUSION|INHERITANCE|RESONATE|MEMORY|EXPERIENCE|CHANGE|ASSIST|INSPIRE|BOARDING|RECOLLECTION)\b')

def prompt_option_pairs():
    out = []
    if not os.path.exists(PROMPTS): return out
    for line in open(PROMPTS, encoding='utf-8-sig'):
        line = line.rstrip('\r\n')
        if line.startswith('//'): continue
        m = re.search(r'(?<!\\)=', line)
        if not m: continue
        en, zh = line[:m.start()].replace('\\=', '='), line[m.end():].replace('\\=', '=')
        if '\\n' not in en:
            # 單行提示裡有 EnrichenText 會換掉的字（Auto: → 圖示、ENCORE → 粗體…），XUnity 整段對不到，整句放進來
            # 引號卡名若是名場面連動對象會被加粗（<b>"X"</b>），也一樣對不到
            if (ENRICH.search(en) or '"' in en) and '«' not in en and en.strip() != zh.strip(): out.append((en.strip(), zh.strip()))
            continue
        a, b = en.split('\\n'), zh.split('\\n')
        if len(a) != len(b): continue
        for x, y in zip(a, b):
            x, y = x.strip(), y.strip()
            if not x or not y or x == y: continue
            mx, my = OPT.match(x), OPT.match(y)
            if mx and my and mx.group(1) == my.group(1):
                out.append((mx.group(2), my.group(2)))
            # 整行也放（提問句「Choose an effect:」、「1: …」「1.Search …」這類非標準編號的選項）
            out.append((x, y))
    return out

def build_files():
    files = {}
    for f in glob.glob(os.path.join(D, '*_tw.json')):
        files[os.path.basename(f)[:-len('_tw.json')]] = json.load(open(f, encoding='utf-8'))
    known = set(k for d in files.values() for k in d)
    quoted = {}
    for d in files.values():
        for en, zh in d.items():
            for a, b in quoted_pairs(en, zh):
                a = a.strip()
                if a not in known and a not in quoted: quoted[a] = b.strip()
            for a, b in effect_pairs(en, zh):
                a = a.strip()
                if a not in known and a not in quoted: quoted[a] = b.strip()
    # 遊戲把這些固定句接在效果文字後面（同一段文字），XUnity 整段對不到，所以在效果翻譯這裡逐行處理
    quoted['Choose order to resolve effects:'] = '請選擇效果處理的順序：'
    quoted['Use this effect now?'] = '現在要使用這個效果嗎？'
    quoted['Pay the cost and use this effect?'] = '要支付費用並使用這個效果嗎？'
    # 遊戲內建的再演 3（CardLibrary.encore3，所有角色都有），再演詢問和效果說明列會顯示
    quoted['Auto: Encore [<sprite name="3cost"/>]'] = '【自】【再演】[<sprite name="3cost"/>]'
    for a, b in prompt_option_pairs():
        if a not in known and a not in quoted: quoted[a] = b
    files['zz_quoted'] = quoted
    return files

# 简体中文版：由台灣版轉換（zhcn.py）
def build_cn(files):
    from zhcn import to_cn
    return {name: {en: to_cn(zh) for en, zh in d.items()} for name, d in files.items()}

if __name__ == '__main__':
    SIM = sys.argv[1] if len(sys.argv) > 1 else r'C:\Users\Gary\Desktop\WS模擬器'
    files = build_files()
    for lang, fs in (('tw', files), ('cn', build_cn(files))):
        out = os.path.join(SIM, 'AutoTranslator', 'Translation', lang, 'CardText')
        os.makedirs(out, exist_ok=True)
        for name, data in fs.items():
            # 和 publish_cardtext.py 寫法一致（LF 換行），本機檔案才會和雲端 sha256 相同，遊戲的更新提醒不會誤報
            open(os.path.join(out, name + '.json'), 'wb').write(json.dumps(data, ensure_ascii=False, indent=0).encode('utf-8'))
    hk = os.path.join(SIM, 'AutoTranslator', 'Translation', 'hk', 'CardText')
    if os.path.isdir(hk): shutil.rmtree(hk)          # 舊版另存的香港版，已改成遊戲裡轉換
    print('files', len(files), 'lines', sum(len(d) for d in files.values()), 'quoted abilities', len(files['zz_quoted']))
