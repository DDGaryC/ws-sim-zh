import json, re, os
D = os.path.dirname(os.path.abspath(__file__))
src = json.load(open(os.path.join(D, 'NIK_src.json'), encoding='utf-8'))
zh = {}
for n in (1, 2, 3):
    for e in json.load(open(os.path.join(D, 'NIK_tw_part%d.json' % n), encoding='utf-8')):
        zh[e['i']] = e['zh']
assert sorted(zh) == list(range(len(src))), 'missing entries'

CJK = r'[㐀-鿿（）「」『』《》【】，。：；？！、]'
def norm(s):
    # 數字（含 +/- 號）和中文之間統一留一個空格
    s = re.sub(r'(?<=[㐀-鿿])(?=[+\-−]?\d)', ' ', s)
    s = re.sub(r'(?<=\d)(?=[㐀-鿿])', ' ', s)
    s = s.replace('&', '＆')
    return s

out = {src[i]: norm(zh[i]) for i in range(len(src))}
json.dump(out, open(os.path.join(D, 'NIK_tw.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

# 檢查 sprite 標籤一致
bad = [i for i in range(len(src)) if sorted(re.findall(r'<sprite[^>]*>', src[i])) != sorted(re.findall(r'<sprite[^>]*>', out[src[i]]))]
print('entries', len(out), 'sprite mismatch', bad)
