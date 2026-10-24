# 用 Claude API（Message Batches，五折）翻譯卡片效果文字
# 從待翻清單 queue.json 的「尾端」（最舊的系列）往前取，和子代理（從前端往後）不重疊。
#
# 用法：
#   python api_translate.py submit <請求數> [每個請求的條數=60]   送出一個批次
#   python api_translate.py collect                              收回已完成的批次、檢查、存成 Axxx_tw.json
#   python api_translate.py status                               看進度與累計花費
#
# API key：從環境變數 ANTHROPIC_API_KEY 讀，沒有的話讀 Windows 使用者設定（setx 設的）。
#          只放在記憶體裡，不寫進任何檔案、也不印出來。
# 花費上限：累計超過 BUDGET_USD 就不再送新批次。
import json, os, sys, re, glob, time
import anthropic
from anthropic.types.message_create_params import MessageCreateParamsNonStreaming
from anthropic.types.messages.batch_create_params import Request

D = os.path.dirname(os.path.abspath(__file__))
WORK = os.path.join(D, 'api_work')          # 已列入 .gitignore
os.makedirs(WORK, exist_ok=True)
BS = chr(92) + 'n'
MODEL = 'claude-sonnet-5-5'
BUDGET_USD = 15.0
# Claude Sonnet 5.5 批次價格（每百萬 token，已打五折）
PRICE = {'in': 1.0, 'out': 5.0, 'cache_read': 0.10, 'cache_write': 1.25}
TAG = re.compile(r'<sprite[^>]*>|</?b>|</?i>')


def api_key():
    k = os.environ.get('ANTHROPIC_API_KEY')
    if k: return k
    try:
        import winreg
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, 'Environment') as h:
            return winreg.QueryValueEx(h, 'ANTHROPIC_API_KEY')[0]
    except Exception:
        return None


def client():
    k = api_key()
    if not k: sys.exit('找不到 ANTHROPIC_API_KEY（請先用 setx 設定）')
    return anthropic.Anthropic(api_key=k)


def load_state():
    p = os.path.join(WORK, 'state.json')
    return json.load(open(p, encoding='utf-8')) if os.path.exists(p) else {'batches': {}, 'spent': 0.0, 'next': 1}


def save_state(st):
    json.dump(st, open(os.path.join(WORK, 'state.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)


def taken_lines():
    s = set()
    for f in glob.glob(os.path.join(D, '*_tw.json')):
        for k in json.load(open(f, encoding='utf-8')):
            for part in k.split(BS): s.add(part.strip())
    for f in glob.glob(os.path.join(D, 'B*_src.json')) + glob.glob(os.path.join(WORK, 'A*_src.json')):
        for t in json.load(open(f, encoding='utf-8')): s.add(t)
    return s


def system_prompt():
    glossary = open(os.path.join(D, '用語表.md'), encoding='utf-8').read()
    ex = list(json.load(open(os.path.join(D, 'NIK_tw.json'), encoding='utf-8')).items())
    pick = ex[:25] + ex[330:345]
    examples = '\n'.join('EN: %s\nZH: %s' % (a, b) for a, b in pick)
    return (
        '你是 Weiss Schwarz 卡牌遊戲的專業譯者，把英文卡片效果文字翻成繁體中文（台灣用語），'
        '讓台灣玩家一看就懂，用語和中文化介面一致。規則意思必須和英文完全一致，不可增減條件、次數、數量、對象。\n\n'
        '格式重點：\n'
        '- <sprite name="..."/>、<b>、</b>、<i>、</i> 標籤原樣保留，種類和數量必須和英文完全相同。\n'
        '- 卡名（英文 "..." 括起來的）保留英文，改用「」括起來。特徵 <XXX> 改成《XXX》。\n'
        '- Auto: → 【自】、Cont: → 【永】、Act: → 【起】、CxCombo → 【名場面連動】，其他關鍵字照用語表。\n'
        '- 數字和中文之間留一個空格；費用括號 ] 後面不留空格。\n'
        '- 判定圖示開頭的句子（<sprite name="mini_xxx"/> When this card is triggered, ...）不加【自】，翻成「這張卡判定時，……」。\n'
        '- 英文裡的字面 \\n（反斜線加 n）照原樣保留。英文有文法小錯時照合理意思翻。\n'
        '- 每一條都要翻，輸出的編號要和輸入一一對應。\n\n'
        '=== 用語表 ===\n' + glossary + '\n\n=== 譯文範例（照這個風格） ===\n' + examples
    )


SCHEMA = {
    'type': 'object',
    'properties': {'items': {'type': 'array', 'items': {
        'type': 'object', 'properties': {'i': {'type': 'integer'}, 'zh': {'type': 'string'}},
        'required': ['i', 'zh'], 'additionalProperties': False}}},
    'required': ['items'], 'additionalProperties': False,
}


def norm(s):
    s = re.sub(r'(?<=[㐀-鿿])(?=[+\-−]?\d)', ' ', s)
    s = re.sub(r'(?<=\d)(?=[㐀-鿿])', ' ', s)
    s = re.sub(r'\]\s+(?=[㐀-鿿])', ']', s)
    s = re.sub(r'(?<=[㐀-鿿])X(?=[㐀-鿿])', ' X ', s)
    s = s.replace('【REPLAY】', 'REPLAY').replace('【RESONATE】', '【共鳴】').replace('【SHIFT】', '【交替】')
    return s.replace('&', '＆')


def submit(nreq, per):
    st = load_state()
    if st['spent'] >= BUDGET_USD: sys.exit('已達花費上限 %.2f 美元，不再送出' % BUDGET_USD)
    taken = taken_lines()
    queue = [q['t'] for q in json.load(open(os.path.join(D, 'queue.json'), encoding='utf-8')) if q['t'] not in taken]
    queue.reverse()                       # 從最舊的系列開始
    sysp = [{'type': 'text', 'text': system_prompt(), 'cache_control': {'type': 'ephemeral'}}]
    reqs, files = [], []
    for r in range(nreq):
        chunk = queue[r * per:(r + 1) * per]
        if not chunk: break
        name = 'A%04d' % st['next']; st['next'] += 1
        json.dump(chunk, open(os.path.join(WORK, name + '_src.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
        body = '\n'.join('%d\t%s' % (i, t) for i, t in enumerate(chunk))
        reqs.append(Request(custom_id=name, params=MessageCreateParamsNonStreaming(
            model=MODEL, max_tokens=32000, system=sysp,
            output_config={'effort': 'low', 'format': {'type': 'json_schema', 'schema': SCHEMA}},
            messages=[{'role': 'user', 'content': '請翻譯以下 %d 條（每行「編號<Tab>英文」），回傳 JSON：\n\n%s' % (len(chunk), body)}])))
        files.append(name)
    if not reqs: sys.exit('沒有可以翻的內容了')
    b = client().messages.batches.create(requests=reqs)
    st['batches'][b.id] = {'names': files, 'status': 'submitted', 'time': time.strftime('%Y-%m-%d %H:%M')}
    save_state(st)
    print('送出批次，%d 個請求、%d 條' % (len(reqs), sum(len(json.load(open(os.path.join(WORK, n + '_src.json'), encoding='utf-8'))) for n in files)))


def collect():
    st = load_state()
    c = client()
    for bid, info in st['batches'].items():
        if info['status'] == 'done': continue
        b = c.messages.batches.retrieve(bid)
        if b.processing_status != 'ended':
            print('還在處理：%s（完成 %d / 共 %d）' % (info['time'], b.request_counts.succeeded, len(info['names']))); continue
        good = bad = 0
        for res in c.messages.batches.results(bid):
            name = res.custom_id
            srcp = os.path.join(WORK, name + '_src.json')
            src = json.load(open(srcp, encoding='utf-8'))
            if res.result.type != 'succeeded':
                os.remove(srcp); bad += len(src); continue     # 刪掉來源檔＝這些句子回到待翻清單
            msg = res.result.message
            u = msg.usage
            st['spent'] += ((u.input_tokens or 0) * PRICE['in'] + (u.output_tokens or 0) * PRICE['out']
                            + (getattr(u, 'cache_read_input_tokens', 0) or 0) * PRICE['cache_read']
                            + (getattr(u, 'cache_creation_input_tokens', 0) or 0) * PRICE['cache_write']) / 1e6
            text = next((x.text for x in msg.content if x.type == 'text'), '')
            try: items = {e['i']: e['zh'] for e in json.loads(text)['items']}
            except Exception: items = {}
            out = {}
            for i, t in enumerate(src):
                z = items.get(i)
                if not z: continue
                z = norm(z)
                if sorted(TAG.findall(t)) != sorted(TAG.findall(z)): continue
                out[t] = z
            json.dump(out, open(os.path.join(D, name + '_tw.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
            os.remove(srcp)                                  # 沒通過檢查的句子會回到待翻清單
            good += len(out); bad += len(src) - len(out)
        info['status'] = 'done'
        save_state(st)
        print('收回批次（%s）：通過 %d 條、退回 %d 條' % (info['time'], good, bad))
    print('累計花費約 %.2f 美元（上限 %.0f）' % (st['spent'], BUDGET_USD))


def status():
    st = load_state()
    n = sum(len(json.load(open(f, encoding='utf-8'))) for f in glob.glob(os.path.join(D, 'A*_tw.json')))
    pend = [i for i in st['batches'].values() if i['status'] != 'done']
    print('API 已翻 %d 條；處理中的批次 %d 個；累計花費約 %.2f 美元（上限 %.0f）' % (n, len(pend), st['spent'], BUDGET_USD))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'status'
    if cmd == 'submit': submit(int(sys.argv[2]), int(sys.argv[3]) if len(sys.argv) > 3 else 60)
    elif cmd == 'collect': collect()
    else: status()
