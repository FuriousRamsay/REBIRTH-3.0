"""Bounded checks through real UI input; always quit the disposable game afterward."""
from test_live import call,console,root
import time,json
results=[]
def record(name,value):
    results.append({'check':name,'result':value});print(name,flush=True);return value
def click(**args):
    r=call('/ui/click','POST',**args);time.sleep(.35);return r
def key(name):
    r=call('/key','POST',name=name);time.sleep(1);return r
def screenshot(name):return record(name,call('/screenshot','POST',name=name))
try:
    assert call('/ping')['state']=='ingame'
    call('/surroundings');call('/cleararea','POST',radius=40);call('/restore','POST')
    start=call('/ping')['lastLogSeq']
    record('preferences_survived_reload',console('rbvoices status'))
    opened=call('/ui/tree')['openWindows']
    if 'crafting' not in opened:
        if 'ingameMenu' in opened:click(text='Resume')
        key('Tab')
    record('starting_ui',call('/ui/tree'))
    click(id='rebirthCraftingTabChallenges');time.sleep(1);screenshot('curriculum_final_top')
    tree=call('/ui/tree')
    record('challenge_tree_top',tree)
    assert 'challenges' in tree['openWindows']
    # Review lower sections using the actual scroll host, not a coordinate over the tooltip.
    call('/ui/scroll','POST',path='challenges/windowChallengeList/content/rebirthChallengeGroupScroll',delta=-7)
    time.sleep(.35);screenshot('curriculum_final_lower')
    key('Escape')
    console('rbvoices set Hugh 2');console('rbvoices log on')
    call('/lookat','POST',entity=193);call('/activate','POST');time.sleep(.4)
    click(text='May I see your inventory?')
    voice=record('opening_trade_does_not_say_farewell',console('rbvoices status'))
    assert '_browse_' in json.dumps(voice)
    screenshot('trader_browse_final')
    call('/give','POST',item='resourceSilverNugget',count=1)
    click(item='resourceSilverNugget')
    tree=call('/ui/tree');record('sale_selection',tree)
    sell=[n for n in tree['nodes'] if n.get('text','').lower()=='sell']
    if sell:
        click(path=sell[0]['path'])
        voice=record('confirmed_sale',console('rbvoices status'))
        assert '_sale_' in json.dumps(voice)
    else:record('sale_not_verified','Sell control absent after selection; no success claimed')
    key('Escape')
    record('trade_close_farewell',console('rbvoices status'))
    record('hydration_supply_persistence',console('rbmet slot'))
    record('new_errors',call('/errors',since=start))
finally:
    for trader in ('Rekt','Hugh','Jen','Joel','Bob'):
        try:console('rbvoices set '+trader+' 0')
        except Exception:pass
    try:console('rbvoices log off')
    except Exception:pass
    (root/'_Documentation/Trader Voicesets/final_runtime_checks.json').write_text(json.dumps(results,indent=2))
    try:call('/quit','POST')
    except Exception:pass
