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
            json.dump(data, open(os.path.join(out, name + '.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
    hk = os.path.join(SIM, 'AutoTranslator', 'Translation', 'hk', 'CardText')
    if os.path.isdir(hk): shutil.rmtree(hk)          # 舊版另存的香港版，已改成遊戲裡轉換
    print('files', len(files), 'lines', sum(len(d) for d in files.values()), 'quoted abilities', len(files['zz_quoted']))
