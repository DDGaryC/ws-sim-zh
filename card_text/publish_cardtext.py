# 產生要發佈到 GitHub 的卡片翻譯：
#   <repo>\cardtext\tw\*.json ＋ cardtext\manifest.txt      （台灣版；香港版在遊戲裡由台灣版轉換）
#   <repo>\cardtext\cn\*.json ＋ cardtext\manifest_cn.txt   （简体中文版，由台灣版轉換）
# 遊戲裡的「更新卡片翻譯」按鈕會依目前語言讀對應的 manifest，只下載有變動的檔案。
import json, os, glob, hashlib, shutil, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
D = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(D)
OUT = os.path.join(REPO, 'cardtext')
from install_cardtext import build_files, build_cn   # 與本機安裝共用同一套內容（含引號裡拆出的能力）

files = build_files()
for lang, fs, manifest in (('tw', files, 'manifest.txt'), ('cn', build_cn(files), 'manifest_cn.txt')):
    d = os.path.join(OUT, lang)
    if os.path.isdir(d): shutil.rmtree(d)
    os.makedirs(d)
    lines = []
    for name in sorted(fs):
        data = json.dumps(fs[name], ensure_ascii=False, indent=0).encode('utf-8')
        open(os.path.join(d, name + '.json'), 'wb').write(data)
        lines.append('%s\t%d\t%s.json' % (hashlib.sha256(data).hexdigest(), len(data), name))
    open(os.path.join(OUT, manifest), 'w', encoding='utf-8', newline='\n').write(
        '# sha256\tsize\tfile（WS 模擬器中文化套件：卡片效果翻譯 %s）\n' % lang + '\n'.join(lines) + '\n')
print('cardtext files', len(files), 'lines', sum(len(v) for v in files.values()), '(tw + cn)')
