# 2026-10-30 修正：MEMORY 和 RECOLLECTION 原本都翻成【回想】，memory（區域）翻成「回想區」，三個詞撞在一起。
# 改成（和日文版對應）：
#   MEMORY（記憶）       → 【記憶】
#   RECOLLECTION（回想） → 【回想】（Stella Sora 起的新關鍵字）
#   memory（思い出）     → 思出區（不用回憶區：和回想太像）
# 處理 *_tw.json（效果文字）和 P*_part.json（對戰提示）。可以重複執行。用法：python fix_memory.py
import glob, json, os, re
D = os.path.dirname(os.path.abspath(__file__))
KW = re.compile(r'【回想】|「回想」|(?<=】)\s*回想\s*(?=[\[［如你這對<「（]|$)')


def fix(en, zh):
    zh = zh.replace('回想區', '思出區').replace('回憶區', '思出區')
    order = re.findall(r'\b(MEMORY|Memory|RECOLLECTION)\b', en)
    rec = [w == 'RECOLLECTION' for w in order]
    aligned = len(rec) == len(KW.findall(zh))
    i = [0]

    def sub(m):
        k = i[0]; i[0] += 1
        # 英文有 RECOLLECTION 的：個數對得上就照出現順序對應，對不上就保留
        if 'RECOLLECTION' in en and not (aligned and not rec[k]): return m.group(0)
        return '「記憶」' if m.group(0).startswith('「') else '【記憶】'
    return KW.sub(sub, zh)


def fix_text(en, zh):
    out = fix(en, zh)
    left = re.sub(r'【回想】|回想（RECOLLECTION）|回想效果', '', out)
    return out, ('回想' in left and 'RECOLLECTION' not in en)


changed, odd = 0, []
for f in glob.glob(os.path.join(D, '*_tw.json')):
    d = json.load(open(f, encoding='utf-8'))
    n = 0
    for en, zh in list(d.items()):
        z, bad = fix_text(en, zh)
        if bad: odd.append((os.path.basename(f), en, z))
        if z != zh: d[en] = z; n += 1
    if n:
        json.dump(d, open(f, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
        changed += n
print('效果文字改了 %d 句' % changed)

changed = 0
for f in glob.glob(os.path.join(D, 'P[0-9][0-9][0-9]_part.json')):
    # 這些檔案的排版不一，直接在原始文字裡換掉譯文字串，不重新排版
    src = json.load(open(f.replace('_part.json', '_src.json'), encoding='utf-8'))
    raw = open(f, encoding='utf-8').read()
    new = {}
    for e in json.loads(raw):
        en = src[e['i']] if e['i'] < len(src) else ''
        z, bad = fix_text(en, e['zh'])
        if bad: odd.append((os.path.basename(f), en, z))
        if z != e['zh']:
            if new.get(e['zh'], z) != z: odd.append((os.path.basename(f), en, '同一句譯文有兩種改法，沒有改：' + e['zh']))
            new[e['zh']] = z
    n = 0
    for old, z in new.items():
        o, s = json.dumps(old, ensure_ascii=False), json.dumps(z, ensure_ascii=False)
        n += raw.count(o); raw = raw.replace(o, s)
    if n:
        open(f, 'w', encoding='utf-8', newline='').write(raw)
        changed += n
print('對戰提示改了 %d 句' % changed)
print('改完還有「回想」、英文卻沒有 RECOLLECTION 的：%d 句' % len(odd))
for name, en, z in odd[:40]: print('  [%s] %s\n      %s' % (name, en[:120], z[:120]))
