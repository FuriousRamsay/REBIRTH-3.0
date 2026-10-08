"""Replace two misleading native-icon stand-ins and schedule their matching cards."""
import sys
import shutil
from pathlib import Path
import assets

manifest = assets.init()
for key, source in zip(('skillet','pickles'),sys.argv[1:]):
    icon = 'rebirthCookingFood'+key.title()
    task = next((t for t in manifest['tasks'] if t['key']=='existing-'+key),None)
    if task is None:
        task = dict(key='existing-'+key, category='food',
            title='Mushroom Skillet' if key=='skillet' else 'Pickled Vegetables',
            icon=icon,path=str(assets.ICONS/(icon+'.png')),master=str(assets.MASTERS/(icon+'.png')),
            prompt='Correct the existing food illustration to match its actual ingredients.',refs=[],status='generated')
        manifest['tasks'].append(task)
    task['source']=source
    shutil.copy2(source,task['master'])
    assets.export(source,task['path'])
    card = next(t for t in manifest['tasks'] if t['key']=='C-'+key)
    card['refs'][1]=task['master']
    card['status']='pending'
assets.write(manifest)
print('Corrected food images saved; two matching recipe cards scheduled for regeneration.')
