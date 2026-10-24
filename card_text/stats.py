# 統計遊戲匯出的卡片文字：不重複的效果行數、字數、各系列行數
import collections
BS = chr(92) + 'n'
lines = collections.Counter(); per = collections.defaultdict(set)
for l in open(r'C:\Users\Gary\Desktop\WS模擬器\CardText_dump.tsv', encoding='utf-8'):
    cid, series, text = l.rstrip('\n').split('\t', 2)
    for t in text.split(BS):
        t = t.strip()
        if t: lines[t] += 1; per[series].add(t)
print('unique lines', len(lines), 'chars', sum(len(t) for t in lines))
shared = sum(1 for t, n in lines.items() if n >= 5)
print('lines used by 5+ cards', shared)
print('series', len(per))
