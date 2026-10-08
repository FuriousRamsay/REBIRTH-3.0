from test_capacity_live import *
old=json.loads((out/'capacity_initial.json').read_text())
for a in ('strength','constitution'):
 v=old['gearAttributes'][a];call('/gearfixture','POST',confirm='CodexTest',attribute=a,value=v['Current'],potential=v['Potential'])
call('/ui','POST',action='open',window='rebirthSurvivorCharacter')
before=count(state(),'rebirthGearExpandedDaypack');equip('rebirthGearExpandedDaypack');assert not state()['survivorGear'].get('backpack');assert count(state(),'rebirthGearExpandedDaypack')==before
(out/'requirement_rejection.json').write_text(json.dumps({'passed':True,'item':'rebirthGearExpandedDaypack','attributes':state()['gearAttributes'],'itemConserved':True},indent=2))
print('PASS requirement rejection; original attributes restored')
call('/console','POST',cmd='debuff god');call('/key','POST',name='Escape')
