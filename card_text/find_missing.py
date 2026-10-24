# 從遊戲匯出的 CardText_dump.tsv 找出某系列還沒有譯文的效果文字
# 用法：python find_missing.py NIK
import json, sys, os, glob
D = os.path.dirname(os.path.abspath(__file__))
prefix = sys.argv[1] + '/'
BS = chr(92) + 'n'   # 字面的「\n」

keys = set()
for f in glob.glob(os.path.join(D, '*_tw.json')):
    for k in json.load(open(f, encoding='utf-8')):
        for part in k.split(BS): keys.add(part)

miss = {}; ncard = 0
for l in open(r'C:\Users\Gary\Desktop\WS模擬器\CardText_dump.tsv', encoding='utf-8'):
    cid, series, text = l.rstrip('\n').split('\t', 2)
    if not cid.startswith(prefix): continue
    ncard += 1
    for line in text.split(BS):
        if line and line not in keys and line.rstrip() not in keys:
            miss.setdefault(line, []).append(cid)

name = sys.argv[1]
json.dump(list(miss.keys()), open(os.path.join(D, name + '_src2.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
open(os.path.join(D, name + '_src2.txt'), 'w', encoding='utf-8').write('\n'.join('%d\t%s' % (i, t) for i, t in enumerate(miss)))
print('cards', ncard, 'missing lines', len(miss))
for i, (t, c) in enumerate(list(miss.items())[:40]): print(i, c[:2], t[:140])
