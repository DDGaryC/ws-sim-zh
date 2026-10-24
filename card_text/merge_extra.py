# 把補翻的 {name}_tw_partN.json（對應 {name}_src2.json）併進 {name}_tw.json
# 用法：python merge_extra.py NIK 4
import json, os, sys, re
D = os.path.dirname(os.path.abspath(__file__))
name, part = sys.argv[1], sys.argv[2]
src = json.load(open(os.path.join(D, name + '_src2.json'), encoding='utf-8'))
tw_path = os.path.join(D, name + '_tw.json')
tw = json.load(open(tw_path, encoding='utf-8'))
add = json.load(open(os.path.join(D, '%s_tw_part%s.json' % (name, part)), encoding='utf-8'))
assert len(add) == len(src)
def norm(s):
    s = re.sub(r'(?<=[㐀-鿿])(?=[+\-−]?\d)', ' ', s)
    s = re.sub(r'(?<=\d)(?=[㐀-鿿])', ' ', s)
    s = re.sub(r'\]\s+(?=[㐀-鿿])', ']', s)
    return s.replace('&', '＆')
for e in add:
    tw[src[e['i']]] = norm(e['zh'])
json.dump(tw, open(tw_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print(name, 'total', len(tw))
