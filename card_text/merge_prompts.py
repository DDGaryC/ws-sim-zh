# 合併 Pxxx_part.json（對戰提示文字的譯文）→ AutoTranslator 的 CardPrompts.txt
# 寫到模擬器與 files\ 兩處；香港版用台灣版轉換區域名稱（休息室→控制室、能量區→費用區）
# 用法：python merge_prompts.py [模擬器路徑]
import glob, json, os, re, sys
D = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(D)
SIM = sys.argv[1] if len(sys.argv) > 1 else r'C:\Users\Gary\Desktop\WS模擬器'
TAG = re.compile(r'<sprite[^>]*>|</?b>|</?i>')
HK = [('休息室', '控制室'), ('能量區', '費用區')]


def norm(s):
    s = re.sub(r'(?<=[㐀-鿿])(?=[+\-−]?\d)', ' ', s)
    s = re.sub(r'(?<=\d)(?=[㐀-鿿])', ' ', s)
    return s.replace('&', '＆')


def esc(s):
    return s.replace('\r', '').replace('\n', '\\n').replace('=', '\\=')


pairs, bad = {}, []
for src_p in sorted(glob.glob(os.path.join(D, 'P*_src.json'))):
    name = os.path.basename(src_p)[:-len('_src.json')]
    part = os.path.join(D, name + '_part.json')
    if not os.path.exists(part): print(name, 'no part yet'); continue
    src = json.load(open(src_p, encoding='utf-8'))
    got = {r['i']: r['zh'] for r in json.load(open(part, encoding='utf-8'))}
    for i, t in enumerate(src):
        z = got.get(i)
        if not z or sorted(TAG.findall(t)) != sorted(TAG.findall(z)): bad.append((name, i)); continue
        pairs[t] = norm(z.strip())

head = '// WS 模擬器中文化套件：卡片腳本的對戰提示文字（由 card_text\\merge_prompts.py 產生，請勿手動修改）\n'
for lang, conv in (('tw', []), ('hk', HK)):
    lines = []
    for en, zh in pairs.items():
        for a, b in conv: zh = zh.replace(a, b)
        lines.append(esc(en) + '=' + esc(zh))
    data = head + '\n'.join(lines) + '\n'
    for root in (os.path.join(SIM, 'AutoTranslator'), os.path.join(REPO, 'files', 'AutoTranslator')):
        d = os.path.join(root, 'Translation', lang, 'Text')
        if os.path.isdir(d):
            open(os.path.join(d, 'CardPrompts.txt'), 'w', encoding='utf-8', newline='\n').write(data)
print('prompts', len(pairs), 'bad', len(bad), bad[:20])
