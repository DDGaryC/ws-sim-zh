# 從日版官網（ws-tcg.com）的卡片搜尋 API 抓所有卡的日文卡名 → card_text\jpnames_ws_tcg.tsv（卡號<Tab>日文卡名）
# 用來補模擬器沒有日文卡名的系列（例如 MARVEL）。可以中斷後繼續（已抓的頁會跳過）。
# 用法：python fetch_jpnames.py
import json, os, time, urllib.request
D = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(D, 'jpnames_ws_tcg.tsv')
CACHE = os.path.join(D, 'jpnames_pages')          # 每頁原始結果（中斷後繼續用；已列入 .gitignore）
URL = 'https://ws-tcg.com/manage/CardListUser/searchJson?show_page_count=120&page=%d'
os.makedirs(CACHE, exist_ok=True)

def get(page):
    p = os.path.join(CACHE, '%04d.json' % page)
    if os.path.exists(p): return json.load(open(p, encoding='utf-8'))
    for attempt in range(5):
        try:
            req = urllib.request.Request(URL % page, headers={'User-Agent': 'Mozilla/5.0'})
            data = json.loads(urllib.request.urlopen(req, timeout=60).read().decode('utf-8'))
            json.dump(data, open(p, 'w', encoding='utf-8'), ensure_ascii=False)
            time.sleep(0.4)                         # 放慢速度，不要對官網造成負擔
            return data
        except Exception as ex:
            print('page', page, 'retry', attempt + 1, ex, flush=True)
            time.sleep(5 * (attempt + 1))
    raise SystemExit('page %d failed' % page)

first = get(1)
pages = first['page_count']
names = {}
for page in range(1, pages + 1):
    d = first if page == 1 else get(page)
    for it in d.get('items', []):
        no, name = (it.get('card_number') or '').strip(), (it.get('card_name') or '').strip()
        if no and name: names[no] = name
    if page % 50 == 0: print('page', page, '/', pages, 'cards', len(names), flush=True)
with open(OUT, 'w', encoding='utf-8', newline='\n') as f:
    for no in sorted(names): f.write(no + '\t' + names[no] + '\n')
print('done', len(names), 'cards ->', OUT)
