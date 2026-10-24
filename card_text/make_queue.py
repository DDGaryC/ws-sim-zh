# 依「系列新舊」排出還沒翻譯的效果文字（新的系列在前），寫成 queue.json
# 系列新舊用該系列卡號裡最大的彈號判斷（例如 NIK/S135 → 135）
import json, os, re, glob, collections
D = os.path.dirname(os.path.abspath(__file__))
BS = chr(92) + 'n'
DUMP = r'C:\Users\Gary\Desktop\WS模擬器\CardText_dump.tsv'

done = set()
for f in glob.glob(os.path.join(D, '*_tw.json')):
    for k in json.load(open(f, encoding='utf-8')):
        for part in k.split(BS): done.add(part.strip())

newest = collections.defaultdict(int)
lines_of = collections.OrderedDict()
for l in open(DUMP, encoding='utf-8'):
    cid, series, text = l.rstrip('\n').split('\t', 2)
    m = re.search(r'/[A-Za-z]+(\d+)', cid)
    if m: newest[series] = max(newest[series], int(m.group(1)))
    lines_of.setdefault(series, [])
    for t in text.split(BS):
        t = t.strip()
        if t: lines_of[series].append(t)

order = sorted(lines_of, key=lambda s: -newest[s])
queue, seen = [], set(done)
for s in order:
    for t in lines_of[s]:
        if t not in seen:
            seen.add(t); queue.append({'s': s, 't': t})
json.dump(queue, open(os.path.join(D, 'queue.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
print('queue', len(queue))
cnt = collections.Counter(q['s'] for q in queue)
for s in order[:25]: print(newest[s], s, cnt.get(s, 0))
