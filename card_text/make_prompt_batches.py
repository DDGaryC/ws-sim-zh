# 把 prompts_queue.json 切成 Pxxx_src.txt / Pxxx_src.json（每批 PER 條），給子代理翻譯
# 用法：python make_prompt_batches.py [每批條數=200]
import json, os, sys
D = os.path.dirname(os.path.abspath(__file__))
PER = int(sys.argv[1]) if len(sys.argv) > 1 else 200
q = json.load(open(os.path.join(D, 'prompts_queue.json'), encoding='utf-8'))
done = set()
for n in os.listdir(D):
    if n.startswith('P') and n.endswith('_src.json'):
        done.update(json.load(open(os.path.join(D, n), encoding='utf-8')))
q = [t for t in q if t not in done]
start = 1 + sum(1 for n in os.listdir(D) if n.startswith('P') and n.endswith('_src.json'))
names = []
for b in range(0, len(q), PER):
    name = 'P%03d' % (start + len(names))
    chunk = q[b:b + PER]
    json.dump(chunk, open(os.path.join(D, name + '_src.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
    open(os.path.join(D, name + '_src.txt'), 'w', encoding='utf-8').write(
        ''.join('%d\t%s\n' % (i, t.replace('\n', '\\n')) for i, t in enumerate(chunk)))
    names.append(name)
print(' '.join(names))
