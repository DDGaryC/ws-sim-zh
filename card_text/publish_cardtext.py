# 產生要發佈到 GitHub 的卡片翻譯：<repo>\cardtext\tw\*.json ＋ cardtext\manifest.txt
# 遊戲裡的「更新卡片翻譯」按鈕會讀 manifest.txt，只下載有變動的檔案。
# 只放台灣版；香港版在遊戲裡由台灣版轉換。
import json, os, glob, hashlib, shutil, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
D = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(D)
OUT = os.path.join(REPO, 'cardtext')
from install_cardtext import build_files   # 與本機安裝共用同一套內容（含引號裡拆出的能力）

files = build_files()
tw_dir = os.path.join(OUT, 'tw')
if os.path.isdir(tw_dir): shutil.rmtree(tw_dir)
os.makedirs(tw_dir)
lines = []
for name in sorted(files):
    data = json.dumps(files[name], ensure_ascii=False, indent=0).encode('utf-8')
    open(os.path.join(tw_dir, name + '.json'), 'wb').write(data)
    lines.append('%s\t%d\t%s.json' % (hashlib.sha256(data).hexdigest(), len(data), name))
open(os.path.join(OUT, 'manifest.txt'), 'w', encoding='utf-8', newline='\n').write(
    '# sha256\tsize\tfile（WS 模擬器中文化套件：卡片效果翻譯）\n' + '\n'.join(lines) + '\n')
print('cardtext files', len(files), 'lines', sum(len(v) for v in files.values()))
