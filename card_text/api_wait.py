# 等到 API 批次全部處理完（每 60 秒檢查一次，最多等 3 小時），然後結束
import time, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import api_translate as A
c = A.client()
for _ in range(180):
    st = A.load_state()
    pend = [bid for bid, i in st['batches'].items() if i['status'] != 'done']
    if not pend: print('no pending'); break
    if all(c.messages.batches.retrieve(b).processing_status == 'ended' for b in pend):
        print('ended'); break
    time.sleep(60)
