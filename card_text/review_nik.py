import json, re, os
D = os.path.dirname(os.path.abspath(__file__))
CARDS = r'C:\Users\Gary\Desktop\WS模擬器\CardContent\Cards\Nikke'
tw = json.load(open(os.path.join(D, 'NIK_tw.json'), encoding='utf-8'))

# 小幅統一寫法
def polish(s):
    s = re.sub(r'\]\s+(?=[\u3400-\u9fff])', ']', s)                 # 費用括號後不留空格
    s = re.sub(r'(?<=[\u3400-\u9fff])X(?=[\u3400-\u9fff])', ' X ', s)  # 「傷害X次」→「傷害 X 次」
    s = re.sub(r'(?<=[\u3400-\u9fff])X(?=[ ，。])', ' X', s)
    s = s.replace('（傷害可能被取消。）', '（傷害可以被取消。）')
    return s
tw = {k: polish(v) for k, v in tw.items()}
json.dump(tw, open(os.path.join(D, 'NIK_tw.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

# 依卡片整理成對照表
out = ['# NIK 卡片效果試譯（繁體中文・台灣用語）', '',
       '英文原文在上，中文譯文在下。`<sprite …>` 在遊戲裡會顯示成圖示（例如橫置、費用、判定圖示）。', '']
for s in ('S117', 'S135'):
    out.append('## NIK/' + s)
    out.append('')
    code = name = None
    texts = []
    def flush():
        if code and texts:
            out.append('### ' + code + '　' + (name or ''))
            for t in texts:
                out.append('- EN：' + t.replace('<', '&lt;'))
                out.append('- **中：' + tw.get(t, '（沒有譯文）').replace('<', '&lt;') + '**')
            out.append('')
    for l in open(os.path.join(CARDS, s, 'CardData.txt'), encoding='utf-8'):
        l = l.rstrip('\n')
        m = re.match(r'^(Character|Event|Climax): (.+)$', l)
        if m:
            flush(); code, name, texts = m.group(2), None, []
        elif l.startswith('Name '):
            name = l[5:]
        elif l.startswith('Text '):
            texts.append(l[5:])
    flush()
open(os.path.join(D, 'NIK_試譯對照.md'), 'w', encoding='utf-8').write('\n'.join(out))
print('ok', len(tw))
