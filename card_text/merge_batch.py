# 把子代理翻好的 Bxxx_part.json 檢查後轉成 Bxxx_tw.json（{英文: 中文}）
# 用法：python merge_batch.py B001 B002 ...
import json, os, sys, re
D = os.path.dirname(os.path.abspath(__file__))
TAG = re.compile(r'<sprite[^>]*>|</?b>|</?i>')

def norm(s):
    s = re.sub(r'(?<=[㐀-鿿])(?=[+\-−]?\d)', ' ', s)
    s = re.sub(r'(?<=\d)(?=[㐀-鿿])', ' ', s)
    s = re.sub(r'\]\s+(?=[㐀-鿿])', ']', s)
    s = re.sub(r'(?<=[㐀-鿿])X(?=[㐀-鿿])', ' X ', s)
    s = s.replace('【REPLAY】', 'REPLAY').replace('【RESONATE】', '【共鳴】').replace('【SHIFT】', '【交替】').replace('【FUSION】', '【融合】')   # 遊戲會把 REPLAY 畫成圖示
    return s.replace('&', '＆')

for name in sys.argv[1:]:
    src = json.load(open(os.path.join(D, name + '_src.json'), encoding='utf-8'))
    part = os.path.join(D, name + '_part.json')
    if not os.path.exists(part): print(name, 'MISSING part file'); continue
    rows = json.load(open(part, encoding='utf-8'))
    got = {r['i']: r['zh'] for r in rows}
    out, bad = {}, []
    for i, t in enumerate(src):
        z = got.get(i)
        if not z: bad.append(i); continue
        z = norm(z)
        if sorted(TAG.findall(t)) != sorted(TAG.findall(z)): bad.append(i); continue
        out[t] = z
    json.dump(out, open(os.path.join(D, name + '_tw.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print(name, 'ok', len(out), 'of', len(src), 'bad', bad)
