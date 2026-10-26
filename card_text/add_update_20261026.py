# 2026-10-26 模擬器卡片資料更新後新增、還沒翻譯的句子（4 句效果文字＋1 句對戰提示）
import json, os
D = os.path.dirname(os.path.abspath(__file__))
effects = {
    'Auto: CxCombo When this card\'s battle opponent becomes reversed, if "Pagan Tower" is in your climax area, and you have 3 or more other <RPG> characters, perform the following effects 1 time each. "Choose 1 <RPG> character in your waiting room, and return it to your hand." "Choose 1 card in your hand and 1 <RPG> character in your level, you may exchange them." "You may put the top card of your stock into your hand. If you do, choose 1 card in your hand, and put it into your stock."':
        '【自】【名場面連動】這張卡的戰鬥對手變成倒置時，如果「Pagan Tower」在你的名場面區，且你有 3 張以上其他的《RPG》角色，以下效果各執行 1 次。「選擇你休息室的 1 張《RPG》角色，返回手牌。」「選擇你的 1 張手牌與你等級區的 1 張《RPG》角色，你可以將它們交換。」「你可以將你能量區最上面的 1 張卡加入手牌。加入的話，選擇你的 1 張手牌，放置到能量區。」',
    'Auto: CxCombo [Put 1 "Gen Narumi\'s Exclusive Weapon, GS-3305" from your waiting room face-up underneath this card as a marker, send "And to the Strongest Unit" from your climax area to waiting room] At the start of your attack phase, you may pay the cost. If you do, <sprite name="stand"/> this card, look at your opponent\'s hand then choose up to 1 card put into your opponent\'s memory, at the end of your turn, your opponent returns that card to their hand.':
        '【自】【名場面連動】[將你休息室的 1 張「Gen Narumi\'s Exclusive Weapon, GS-3305」正面朝上放置到這張卡下方作為標記＆將你名場面區的「And to the Strongest Unit」放置到休息室]你的攻擊階段開始時，你可以支付費用。支付的話，將這張卡<sprite name="stand"/>，查看對手的手牌，選擇其中最多 1 張放置到對手的回想區，你的回合結束時，對手將那張卡返回手牌。',
    'Auto: [Put 1 <Apocalyptic World> character from your hand into your waiting room] When this card is placed on the stage from your hand, you may pay the cost. If you do, search your deck for up to 1 ""My Other Self", Ai & Ai Tokiwa" or ""Stage", Ai" or "Vision Eye", reveal it to your opponent, put it into your hand, and shuffle your deck.':
        '【自】[將你手牌中 1 張《Apocalyptic World》角色放置到休息室]這張卡從手牌放置到舞台時，你可以支付費用。支付的話，從你的牌庫搜尋最多 1 張「"My Other Self", Ai ＆ Ai Tokiwa」或「"Stage", Ai」或「Vision Eye」，給對手看之後加入手牌，然後將牌庫洗牌。',
    'Choose a "Mysterious Being, Alone" in your waiting room and put it into any position on the stage. Put this card into memory.':
        '選擇你休息室的 1 張「Mysterious Being, Alone」，放置到舞台的任意位置。將這張卡放置到回想區。',
}
json.dump(effects, open(os.path.join(D, 'U20261026_tw.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

prompts = ['Exchange a card in your hand with a 1 <RPG> Character in your level?']
json.dump(prompts, open(os.path.join(D, 'P026_src.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
json.dump([{'i': 0, 'zh': '要將你的 1 張手牌與等級區的 1 張《RPG》角色交換嗎？'}],
          open(os.path.join(D, 'P026_part.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
print('ok')
