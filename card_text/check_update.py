# 模擬器「更新卡片」之後的翻譯檢查：找出還沒翻譯的卡片效果文字與對戰提示，翻好後裝進模擬器、產生要發佈的檔案。
#
# 用法（在 card_text 資料夾執行）：
#   python check_update.py              只檢查：列出沒翻譯的句子，寫到 pending_effects.json／pending_prompts.json
#   python check_update.py --api        檢查後用 Claude API 翻譯（一般 API，不是批次；量少時幾十秒內完成）並套用
#   python check_update.py --apply 檔名  套用手動翻好的譯文（JSON：{"英文": "中文", ...}，效果和提示可以混在一起）
#
# 套用後會：
#   ・效果文字 → card_text\U<日期>_tw.json，重新安裝到模擬器（tw／cn），並更新 repo 的 cardtext\（要另外 git push）
#   ・對戰提示 → 新的 Pxxx_src／_part.json，重新產生 CardPrompts.txt（tw／hk／cn；要發新版才會給大家）
# API key 的讀法、花費紀錄沿用 api_translate.py（環境變數 ANTHROPIC_API_KEY，不寫進任何檔案）。
import glob, json, os, re, subprocess, sys, time
D = os.path.dirname(os.path.abspath(__file__))
SIM = r'C:\Users\Gary\Desktop\WS模擬器'
BS = '\\n'
TAG = re.compile(r'<sprite[^>]*>|</?b>|</?i>')
PE, PP = os.path.join(D, 'pending_effects.json'), os.path.join(D, 'pending_prompts.json')


def norm(s):
    # 和 merge_batch.py 相同的整理規則（數字前後空格、全形 ＆、關鍵字圖示）
    s = re.sub(r'(?<=[㐀-鿿])(?=[+\-−]?\d)', ' ', s)
    s = re.sub(r'(?<=\d)(?=[㐀-鿿])', ' ', s)
    s = re.sub(r'\]\s+(?=[㐀-鿿])', ']', s)
    s = re.sub(r'(?<=[㐀-鿿])X(?=[㐀-鿿])', ' X ', s)
    s = s.replace('【REPLAY】', 'REPLAY').replace('【RESONATE】', '【共鳴】').replace('【SHIFT】', '【交替】').replace('【FUSION】', '【融合】')
    return s.replace('&', '＆')


# ---------- 檢查 ----------
def known_effects():
    sys.path.insert(0, D)
    from install_cardtext import build_files
    s = set()
    for d in build_files().values():
        for k in d:
            for part in k.split(BS): s.add(part.strip())
    return s


def card_effects():
    lines = {}
    for f in glob.glob(os.path.join(SIM, 'CardContent', 'Cards', '*', '*', 'CardData.txt')):
        folder = os.path.relpath(os.path.dirname(f), os.path.join(SIM, 'CardContent', 'Cards'))
        cur = None
        for line in open(f, encoding='utf-8', errors='ignore'):
            m = re.match(r'^(Character|Event|Climax):\s*(\S+)', line)
            if m: cur = m.group(2); continue
            m = re.match(r'^Text:?\s+(.*)$', line.rstrip('\n'))
            if m and cur:
                for part in m.group(1).split(BS):
                    part = part.strip()
                    if part and re.search(r'[A-Za-z]{3}', part): lines.setdefault(part, (cur, folder))
    return lines


def check():
    sys.path.insert(0, D)
    import extract_prompts as ep
    ep.SIM = SIM
    have = known_effects()
    eff = {t: v for t, v in card_effects().items() if t not in have}
    cnt = ep.prompts()
    hv = ep.existing()
    pro = [t for t, n in cnt.most_common() if t not in hv]
    json.dump(sorted(eff), open(PE, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    json.dump(pro, open(PP, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('卡片效果：沒翻譯 %d 句' % len(eff))
    for t, (cid, folder) in list(eff.items())[:30]: print('  %s（%s）%s' % (cid, folder, t[:80]))
    print('對戰提示：沒翻譯 %d 句' % len(pro))
    for t in pro[:30]: print('  ' + t[:90])
    return list(eff), pro


# ---------- API 翻譯 ----------
def api_translate(texts, system):
    if not texts: return {}
    sys.path.insert(0, D)
    import api_translate as A
    c = A.client()
    st = A.load_state()
    out = {}
    for b in range(0, len(texts), 60):
        chunk = texts[b:b + 60]
        body = '\n'.join('%d\t%s' % (i, t.replace('\n', '\\n')) for i, t in enumerate(chunk))
        msg = c.messages.create(
            model=A.MODEL, max_tokens=16000,
            system=[{'type': 'text', 'text': system, 'cache_control': {'type': 'ephemeral'}}],
            output_config={'effort': 'low', 'format': {'type': 'json_schema', 'schema': A.SCHEMA}},
            messages=[{'role': 'user', 'content': '請翻譯以下 %d 條（每行「編號<Tab>英文」），回傳 JSON：\n\n%s' % (len(chunk), body)}])
        u = msg.usage
        # 一般 API 是批次價的兩倍
        st['spent'] += 2 * ((u.input_tokens or 0) * A.PRICE['in'] + (u.output_tokens or 0) * A.PRICE['out']
                            + (getattr(u, 'cache_read_input_tokens', 0) or 0) * A.PRICE['cache_read']
                            + (getattr(u, 'cache_creation_input_tokens', 0) or 0) * A.PRICE['cache_write']) / 1e6
        text = next((x.text for x in msg.content if x.type == 'text'), '')
        try: items = {e['i']: e['zh'] for e in json.loads(text)['items']}
        except Exception: items = {}
        for i, t in enumerate(chunk):
            if items.get(i): out[t] = items[i]
    A.save_state(st)
    print('API 翻譯 %d / %d 句，累計花費約 %.2f 美元' % (len(out), len(texts), st['spent']))
    return out


# ---------- 套用 ----------
def apply(trans, effects, prompts):
    eff_set, pro_set = set(effects), set(prompts)
    e_out, p_out, bad = {}, {}, []
    for en, zh in trans.items():
        zh = norm(zh.strip())
        if sorted(TAG.findall(en)) != sorted(TAG.findall(zh)): bad.append(en); continue
        if en in eff_set: e_out[en] = zh
        elif en in pro_set: p_out[en] = zh
        else: e_out[en] = zh            # 不在清單裡的當成效果文字（手動補翻舊卡）
    if bad: print('標籤（sprite／b／i）和英文不一致、沒有套用：%d 句' % len(bad)); [print('  ' + b[:90]) for b in bad]
    if e_out:
        p = os.path.join(D, 'U%s_tw.json' % time.strftime('%Y%m%d'))
        cur = json.load(open(p, encoding='utf-8')) if os.path.exists(p) else {}
        cur.update(e_out)
        json.dump(cur, open(p, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
        print('效果文字 %d 句 → %s' % (len(e_out), os.path.basename(p)))
    if p_out:
        n = 1 + max([int(os.path.basename(f)[1:4]) for f in glob.glob(os.path.join(D, 'P[0-9][0-9][0-9]_src.json'))] or [0])
        name = 'P%03d' % n
        src = list(p_out)
        json.dump(src, open(os.path.join(D, name + '_src.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
        json.dump([{'i': i, 'zh': p_out[t]} for i, t in enumerate(src)], open(os.path.join(D, name + '_part.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
        print('對戰提示 %d 句 → %s' % (len(p_out), name))
    py = sys.executable
    if p_out: subprocess.run([py, os.path.join(D, 'merge_prompts.py')], check=True)
    if e_out:
        subprocess.run([py, os.path.join(D, 'install_cardtext.py')], check=True)
        subprocess.run([py, os.path.join(D, 'publish_cardtext.py')], check=True)
    print()
    if e_out: print('下一步：卡片翻譯要推上 GitHub（repo 的 cardtext\\），大家按「更新卡片翻譯」就會拿到。')
    if p_out: print('下一步：對戰提示要發新版中文化套件才會給大家（build.ps1）。')


if __name__ == '__main__':
    args = sys.argv[1:]
    effects, prompts = check()
    if not args: sys.exit(0)
    if args[0] == '--api':
        sys.path.insert(0, D)
        import api_translate as A, api_prompts as AP
        trans = api_translate(effects, A.system_prompt())
        trans.update(api_translate(prompts, AP.system_prompt()))
        apply(trans, effects, prompts)
    elif args[0] == '--apply' and len(args) > 1:
        apply(json.load(open(args[1], encoding='utf-8')), effects, prompts)
    else:
        print(__doc__ if __doc__ else '用法：python check_update.py [--api | --apply 檔名]')
