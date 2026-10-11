"""Run manually in CodexTest with the relevant station already open.
forge: selected direct recipe; milling: selected affordable recipe with local ingredients,
queue space, and required tools installed. Never launches or closes the game.
"""
import sys,json,time
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'TraderVoices'))
from test_live import call,root
assert call('/state',sections='world')['world']['gameName']=='CodexTest'
mode=sys.argv[1]
def tree():return call('/ui/tree')['nodes']
if mode=='forge':
 nodes=tree(); paths=' '.join(n['path'] for n in nodes)
 assert 'workstation_forge_nosmelting/' in paths,paths
 assert '/windowForgeInput/' not in paths
 assert not any('unit_' in (n.get('text') or '') for n in nodes)
elif mode=='milling':
 call('/ui/click','POST',text='CRAFT',exact=1)
 time.sleep(.5)
 nodes=tree()
 assert not any('Collecting ingredients' in (n.get('text') or '') for n in nodes)
 assert any((n.get('text') or '')=='CRAFTING' for n in nodes),'No crafting queue card appeared'
else:raise AssertionError('Use forge or milling')
(root/'_Documentation/RegressionReview_20261010'/('live_'+mode+'.json')).write_text(json.dumps(nodes,indent=2))
print('PASS',mode)
