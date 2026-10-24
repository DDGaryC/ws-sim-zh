# 用 Claude API（Message Batches）翻譯對戰提示文字（Pxxx_src.json → Pxxx_part.json）
# 已經有譯文的編號會跳過，只送還沒翻的。
# 用法：
#   python api_prompts.py submit    送出所有還沒翻的條目
#   python api_prompts.py collect   收回結果，寫進 Pxxx_part.json
# API key 的讀法與花費記錄沿用 api_translate.py（同一份 state.json）；這個工作另有自己的上限 PROMPT_BUDGET_USD。
import glob, json, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from api_translate import D, WORK, MODEL, PRICE, SCHEMA, client, load_state, save_state
from anthropic.types.message_create_params import MessageCreateParamsNonStreaming
from anthropic.types.messages.batch_create_params import Request

PROMPT_BUDGET_USD = 5.0
PER = 100


def system_prompt():
    glossary = open(os.path.join(D, '用語表.md'), encoding='utf-8').read()
    guide = open(os.path.join(D, '提示翻譯指示.md'), encoding='utf-8').read()
    guide = guide.split('輸出：', 1)[0]          # 輸出與自我檢查的部分是給子代理的，API 不需要
    return ('你是 Weiss Schwarz 卡牌遊戲的專業譯者。以下是工作說明與用語表，照著翻譯，每一條都要翻，輸出的編號要和輸入一一對應。\n\n'
            + guide + '\n=== 用語表 ===\n' + glossary)


def part_path(name): return os.path.join(D, name + '_part.json')


def load_part(name):
    p = part_path(name)
    if not os.path.exists(p): return {}
    try: return {r['i']: r['zh'] for r in json.load(open(p, encoding='utf-8'))}
    except Exception: return {}


def save_part(name, got):
    json.dump([{'i': i, 'zh': got[i]} for i in sorted(got)], open(part_path(name), 'w', encoding='utf-8'), ensure_ascii=False, indent=0)


def submit():
    st = load_state()
    if st.get('prompt_spent', 0.0) >= PROMPT_BUDGET_USD: sys.exit('已達提示翻譯的花費上限')
    todo = []                                     # (批號, 編號, 英文)
    for sp in sorted(glob.glob(os.path.join(D, 'P*_src.json'))):
        name = os.path.basename(sp)[:-len('_src.json')]
        got = load_part(name)
        for i, t in enumerate(json.load(open(sp, encoding='utf-8'))):
            if not got.get(i): todo.append((name, i, t))
    if not todo: sys.exit('沒有要翻的了')
    sysp = [{'type': 'text', 'text': system_prompt(), 'cache_control': {'type': 'ephemeral'}}]
    reqs, plan = [], {}
    for r in range(0, len(todo), PER):
        chunk = todo[r:r + PER]
        cid = 'Q%04d' % (r // PER)
        plan[cid] = [[n, i] for n, i, _ in chunk]
        body = '\n'.join('%d\t%s' % (k, t.replace('\n', '\\n')) for k, (_, _, t) in enumerate(chunk))
        reqs.append(Request(custom_id=cid, params=MessageCreateParamsNonStreaming(
            model=MODEL, max_tokens=32000, system=sysp,
            output_config={'effort': 'low', 'format': {'type': 'json_schema', 'schema': SCHEMA}},
            messages=[{'role': 'user', 'content': '請翻譯以下 %d 條（每行「編號<Tab>英文」），回傳 JSON：\n\n%s' % (len(chunk), body)}])))
    b = client().messages.batches.create(requests=reqs)
    json.dump(plan, open(os.path.join(WORK, 'prompts_%s.json' % b.id), 'w', encoding='utf-8'))
    st.setdefault('prompt_batches', {})[b.id] = {'status': 'submitted', 'time': time.strftime('%Y-%m-%d %H:%M')}
    save_state(st)
    print('送出批次：%d 個請求、%d 條' % (len(reqs), len(todo)))


def collect():
    st = load_state()
    c = client()
    for bid, info in st.get('prompt_batches', {}).items():
        if info['status'] == 'done': continue
        b = c.messages.batches.retrieve(bid)
        if b.processing_status != 'ended':
            print('還在處理（完成 %d 個請求）' % b.request_counts.succeeded); continue
        plan = json.load(open(os.path.join(WORK, 'prompts_%s.json' % bid), encoding='utf-8'))
        parts, good = {}, 0
        for res in c.messages.batches.results(bid):
            if res.result.type != 'succeeded': continue
            msg = res.result.message
            u = msg.usage
            cost = ((u.input_tokens or 0) * PRICE['in'] + (u.output_tokens or 0) * PRICE['out']
                    + (getattr(u, 'cache_read_input_tokens', 0) or 0) * PRICE['cache_read']
                    + (getattr(u, 'cache_creation_input_tokens', 0) or 0) * PRICE['cache_write']) / 1e6
            st['spent'] += cost
            st['prompt_spent'] = st.get('prompt_spent', 0.0) + cost
            text = next((x.text for x in msg.content if x.type == 'text'), '')
            try: items = {e['i']: e['zh'] for e in json.loads(text)['items']}
            except Exception: continue
            for k, (name, i) in enumerate(plan[res.custom_id]):
                z = items.get(k)
                if not z: continue
                if name not in parts: parts[name] = load_part(name)
                parts[name][i] = z; good += 1
        for name, got in parts.items(): save_part(name, got)
        info['status'] = 'done'
        save_state(st)
        print('收回：%d 條' % good)
    print('提示翻譯花費約 %.2f 美元（上限 %.0f）；全部累計 %.2f 美元' % (st.get('prompt_spent', 0.0), PROMPT_BUDGET_USD, st['spent']))


if __name__ == '__main__':
    {'submit': submit, 'collect': collect}[sys.argv[1] if len(sys.argv) > 1 else 'collect']()
