# 從 queue.json 取出還沒翻的效果文字，切成幾個批次給子代理翻譯
# 用法：python make_batch.py 起始批號 批次數 每批條數
#   例：python make_batch.py 1 4 120  → 產生 B001..B004_src.json / .txt
import json, os, sys, glob
D = os.path.dirname(os.path.abspath(__file__))
BS = chr(92) + 'n'
start, count, size = int(sys.argv[1]), int(sys.argv[2]), int(sys.argv[3])

done = set()
for f in glob.glob(os.path.join(D, '*_tw.json')):
    for k in json.load(open(f, encoding='utf-8')):
        for part in k.split(BS): done.add(part.strip())
# 已經切出去、還在翻的批次也要跳過
for f in glob.glob(os.path.join(D, 'B*_src.json')) + glob.glob(os.path.join(D, 'api_work', 'A*_src.json')):
    for t in json.load(open(f, encoding='utf-8')): done.add(t)

queue = [q for q in json.load(open(os.path.join(D, 'queue.json'), encoding='utf-8')) if q['t'] not in done]
pos = 0
for b in range(start, start + count):
    chunk = queue[pos:pos + size]; pos += size
    if not chunk: break
    name = 'B%03d' % b
    src = [q['t'] for q in chunk]
    json.dump(src, open(os.path.join(D, name + '_src.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
    open(os.path.join(D, name + '_src.txt'), 'w', encoding='utf-8').write('\n'.join('%d\t%s' % (i, t) for i, t in enumerate(src)))
    series = sorted(set(q['s'] for q in chunk), key=[q['s'] for q in chunk].index)
    print(name, len(src), ' / '.join(series))
print('remaining after these', len(queue) - pos)
