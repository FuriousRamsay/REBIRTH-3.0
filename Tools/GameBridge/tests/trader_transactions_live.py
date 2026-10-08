"""Real mouse transactions in disposable CodexTest; start with the trader window open."""
from pathlib import Path
import sys,json,re,time
ROOT=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(ROOT/'Tools/TraderVoices'))
from test_live import call
results=[]
trade_name=sys.argv[1] if len(sys.argv)>1 else 'Shotgun Shell'
trade_item=sys.argv[2] if len(sys.argv)>2 else 'ammoShotgunShell'
sell_item=sys.argv[3] if len(sys.argv)>3 else trade_item
def record(label,data):results.append({'check':label,'data':data})
def ui():return call('/ui/tree',window='trader')['nodes']
def click(**args):return call('/ui/click','POST',window='trader',**args)
def state():return call('/state',sections='inventory')
def count(s,name):return sum(i['count'] for k in ['backpack','toolbelt'] for i in s.get(k,[]) if i['name']==name)
def text(id):return next(n['text'] for n in ui() if n.get('id')==id)
start=call('/ping')['lastLogSeq']
assert 'trader' in call('/ui/tree')['openWindows']
# Quantity defaults and all four arrows. The fixture has a 495-unit animal-fat stack.
click(item='resourceAnimalFat');time.sleep(.25)
def quantity():return int(next(n['input'] for n in ui() if n.get('id')=='count_input'))
assert quantity()==1
click(id='countUp');assert quantity()==2
click(id='countDown');assert quantity()==1
click(id='countMax');assert quantity()>1
click(id='countMin');assert quantity()==1
call('/ui/hover','POST',item='resourceCloth');assert quantity()==1
record('minimum default and min/decrease/increase/max controls','PASS')
# Search uses the native text input and a real stock selection.
call('/ui/type','POST',id='searchInput',value=trade_name);time.sleep(.5)
click(path='trader/rebirthTraderRoot/traderBody/windowTrader/content/items/0')
time.sleep(.25)
assert trade_name in text('selectedName')
buy=int(re.search(r'(\d+)',text('transactionPrice'))[1])
before=state();click(text='Buy');time.sleep(.3);after=state()
print('BUY',count(before,trade_item),count(after,trade_item),count(before,'casinoCoin'),count(after,'casinoCoin'),buy)
assert count(after,trade_item)==count(before,trade_item)+1
assert count(after,'casinoCoin')==count(before,'casinoCoin')-buy
record('buy exact inventory and currency delta',{'price':buy,'before':before,'after':after})
click(item=sell_item);time.sleep(.3);sell=int(re.search(r'(\d+)',text('transactionPrice'))[1])
before=state();quantitySold=quantity();assert quantitySold==1
click(text='Sell');time.sleep(.3);after=state()
assert count(after,sell_item)==count(before,sell_item)-1
assert count(after,'casinoCoin')==count(before,'casinoCoin')+sell
record('sell minimum quantity exact inventory and currency delta',{'quantity':quantitySold,'price':sell,'before':before,'after':after})
call('/ui/type','POST',id='searchInput',value='');time.sleep(.5)
click(id='pageUp');assert any(n.get('text')=='2' and '/pager/' in n['path'] for n in ui())
click(id='pageDown');assert any(n.get('text')=='1' and '/pager/' in n['path'] for n in ui())
record('search, paging forward/back','PASS')
click(item='rebirthCookingCardCvegstew')
assert text('selectedHeading')=='SELECTED ITEM'
assert 'Vegetable Stew' in text('selectedName')
assert not any('/parts/' in n['path'] for n in ui())
record('recipe card has no modification slots',call('/screenshot','POST',name='trader_final_recipe'))
for selected in ['drinkJarBoiledWater','rebirthCookingCardCvegstew']:
    click(item=selected)
    for hovered in list(dict.fromkeys(n['item']['name'] for n in ui() if n.get('item')))[:5]:
        call('/ui/hover','POST',item=hovered)
        assert not any('/parts/' in n['path'] for n in ui()),(selected,hovered)
record('ten hover transitions across water and recipe selection without modification cells','PASS')
click(id='btnRebirthCraftingTabCrafting');opened=call('/ui/tree')['openWindows']
assert 'trader' not in opened and 'crafting' in opened,opened
record('crafting tab leaves trading cleanly',opened)
errors=call('/log',since=start,level='error');assert errors['count']==0;record('errors',errors)
out=ROOT/'_Documentation/Trader Workspace/transaction_results.json'
out.write_text(json.dumps(results,indent=2))
print('PASS: buy, sell, exact currency/item changes, search, paging, card presentation, crafting navigation')





