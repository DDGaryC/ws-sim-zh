# 模擬器「更新卡片」之後的翻譯檢查：找出還沒翻譯的卡片效果文字與對戰提示，翻好後裝進模擬器、產生要發佈的檔案。
#
# 用法（在 card_text 資料夾執行）：
#   python check_update.py              只檢查：列出沒翻譯的句子，寫到 pending_effects.json／pending_prompts.json
#   python check_update.py --api        檢查後用 Claude API 翻譯（一般 API，不是批次；量少時幾十秒內完成）並套用
#   python check_update.py --apply 檔名  套用手動翻好的譯文（JSON：{"英文": "中文", ...}，效果、提示、系列名可以混在一起）
#   python check_update.py --names      只處理搜尋用的資料：向官網補查新卡的日文名，重新產生 AutoTranslator\WSZH\
#
# 檢查也會列出：
#   ・沒有中文名的系列（series_zh.txt 沒有的）→ 系列欄打中文會找不到
#   ・沒有日文名的新卡（模擬器和官網卡名表都沒有，也還沒查過的）→ 日文／中文卡名搜尋找不到
#
# 套用後會：
#   ・效果文字 → card_text\U<日期>_tw.json，重新安裝到模擬器（tw／cn），並更新 repo 的 cardtext\（要另外 git push）
#   ・對戰提示 → 新的 Pxxx_src／_part.json，重新產生 CardPrompts.txt（tw／hk／cn；要發新版才會給大家）
#   ・系列中文名 → 加到 series_zh.txt（--api 的譯名請再看一下）；新卡日文名 → 向官網逐張查，查到的加到 jpnames_ws_tcg.tsv、
#     查不到的記在 jpnames_tried.txt（下次不再查）；最後重新產生 AutoTranslator\WSZH\（要發新版才會給大家）
# API key 的讀法、花費紀錄沿用 api_translate.py（環境變數 ANTHROPIC_API_KEY，不寫進任何檔案）。
import glob, json, os, re, subprocess, sys, time, urllib.parse, urllib.request
D = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, D)
import make_name_maps as NM
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
            # 縮排在效果區塊裡的 Text 也要（角色獲得那個能力後，預覽會顯示這段，例如「Auto: (Canace) When…」）
            m = re.match(r'^\s*Text:?\s+(.*)$', line.rstrip('\n'))
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


# ---------- 搜尋用的名稱（系列中文名、日文卡名）----------
TRIED = os.path.join(D, 'jpnames_tried.txt')      # 官網查不到日文名的卡號（下次不再查）
DETAIL = 'https://ws-tcg.com/manage/CardListUser/detailByNoJson?card_number='


def load_tried():
    if not os.path.exists(TRIED): return None
    return set(l.strip() for l in open(TRIED, encoding='utf-8') if l.strip() and not l.startswith('#'))


def check_names():
    ms = NM.missing_series()
    _, miss = NM.supplement()
    tried = load_tried()
    if tried is None:
        # 第一次：現有的卡在官網全部卡名表（fetch_jpnames.py）裡都找過了，直接記成查過
        with open(TRIED, 'w', encoding='utf-8', newline='\n') as f:
            f.write('# 日版官網查不到日文名的卡號（check_update.py 產生；刪掉某行就會重查）\n')
            for c in sorted(miss): f.write(c + '\n')
        tried = set(miss)
    new = {c: v for c, v in miss.items() if c not in tried}
    print('系列中文名：沒有 %d 個' % len(ms))
    for s in ms: print('  ' + s)
    print('日文卡名：新卡沒有日文名 %d 張（之前查不到的 %d 張不再列出）' % (len(new), len(miss) - len(new)))
    by = {}
    for c, folder in new.items(): by.setdefault(folder, []).append(c)
    for folder in sorted(by)[:30]: print('  %s：%d 張（例：%s）' % (folder, len(by[folder]), sorted(by[folder])[0]))
    return ms, new


def fetch_jp(new):
    # 向官網逐張查（放慢速度，不要對官網造成負擔）
    if not new: return
    found, miss = {}, []
    for i, cid in enumerate(sorted(new)):
        hit = None
        for c in NM.candidates(cid):
            try:
                req = urllib.request.Request(DETAIL + urllib.parse.quote(c), headers={'User-Agent': 'Mozilla/5.0'})
                card = json.loads(urllib.request.urlopen(req, timeout=30).read().decode('utf-8')).get('card')
            except Exception as ex:
                print('  %s 查詢失敗（%s），下次再查' % (c, ex)); hit = False; break
            time.sleep(0.3)
            if card and (card.get('card_name') or '').strip():
                hit = (c, card['card_name'].strip()); break
        if hit: found[hit[0]] = hit[1]
        elif hit is None: miss.append(cid)
        if (i + 1) % 50 == 0: print('  已查 %d / %d 張' % (i + 1, len(new)), flush=True)
    if found:
        official = NM.load_official()
        official.update(found)
        with open(NM.OFFICIAL, 'w', encoding='utf-8', newline='\n') as f:
            for no in sorted(official): f.write(no + '\t' + official[no] + '\n')
    if miss:
        with open(TRIED, 'a', encoding='utf-8', newline='\n') as f:
            for c in miss: f.write(c + '\n')
    print('官網日文卡名：查到 %d 張，查不到 %d 張' % (len(found), len(miss)))


def add_series(names):
    # names：{系列英文名: "中文名|別名"}
    names = {en: zh.strip() for en, zh in names.items() if zh and zh.strip()}
    if not names: return
    with open(NM.SERIES_ZH, 'a', encoding='utf-8', newline='\n') as f:
        for en in sorted(names): f.write(en + '\t' + names[en] + '\n')
    print('系列中文名 %d 個 → series_zh.txt' % len(names))
    for en in sorted(names): print('  %s → %s' % (en, names[en]))


def apply_names(new):
    fetch_jp(new)
    NM.build()
    print('下一步：系列中文名、日文卡名要發新版中文化套件才會給大家（build.ps1）。')


SERIES_PROMPT = ('你在幫卡牌遊戲 Weiss Schwarz（黑白雙翼）的中文化套件翻譯系列名稱。每個系列是一部動畫、遊戲、漫畫或輕小說作品。'
                 '請給出台灣玩家最常用的中文作品名（繁體中文、台灣用語），有多個常見叫法時用 | 分隔（例：「點兔|請問您今天要來點兔子嗎」）。'
                 '如果不確定這部作品的中文名，或它沒有中文名，就原樣回傳英文，不要自己編譯名。')


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
    ms, new = check_names()
    if not args: sys.exit(0)
    if args[0] == '--api':
        import api_translate as A, api_prompts as AP
        trans = api_translate(effects, A.system_prompt())
        trans.update(api_translate(prompts, AP.system_prompt()))
        if effects or prompts: apply(trans, effects, prompts)
        add_series({en: zh for en, zh in api_translate(ms, SERIES_PROMPT).items() if zh.strip() != en})
        apply_names(new)
    elif args[0] == '--apply' and len(args) > 1:
        trans = json.load(open(args[1], encoding='utf-8'))
        add_series({en: trans.pop(en) for en in ms if en in trans})
        if trans: apply(trans, effects, prompts)
        apply_names(new)
    elif args[0] == '--names':
        apply_names(new)
    else:
        print(__doc__ if __doc__ else '用法：python check_update.py [--api | --apply 檔名]')
