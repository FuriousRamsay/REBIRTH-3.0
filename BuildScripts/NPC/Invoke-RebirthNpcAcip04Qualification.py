#!/usr/bin/env python3
from pathlib import Path
import sys
root=Path(__file__).resolve().parents[2]
paths={
'advanced':root/'Scripts/Rebirth/NPC/Progression/RebirthNpcAdvancedProgression.cs',
'qualification':root/'Scripts/Rebirth/NPC/Progression/RebirthNpcAdvancedProgressionQualification.cs',
'profession':root/'Scripts/Rebirth/NPC/Progression/RebirthNpcProfessionProgression.cs',
'lifecycle':root/'Scripts/Rebirth/NPC/Foundation/RebirthNpcLifecycle.cs',
'persistence':root/'Scripts/Rebirth/NPC/Persistence/RebirthNpcPersistenceCoordinator.cs',
'admin':root/'Scripts/Rebirth/NPC/Administration/RebirthNpcAdministrationSupport.cs'}
text={k:p.read_text(encoding='utf-8') for k,p in paths.items()}
checks=[]
def check(name,value): checks.append((name,bool(value)))
for k,s in text.items():check(k+'.braces',s.count('{')==s.count('}'));check(k+'.parentheses',s.count('(')==s.count(')'))
a=text['advanced'];p=text['profession'];l=text['lifecycle'];c=text['persistence'];d=text['admin']
check('certification.graph.cycle',':cycle' in a and 'Visit(id,visiting,visited,errors)' in a)
check('certification.graph.missing','missing-prerequisite' in a)
check('certification.idempotent','r.Certifications.Contains(d.Id)' in a)
check('mentorship.eligibility','mentor.Level<5' in a and 'mentor.Level<=trainee.Level' in a)
check('mentorship.validity',all(x in a for x in ['expired','participant-not-present','out-of-range']))
check('mentorship.replay','CreditedOutcomeIds.Contains(outcomeId)' in a and 'CreditedOutcomeIds.Add(outcomeId)' in a)
check('mentorship.commit.after.award',p.find('if(result.Accepted)')<p.find('CommitMentorshipCredit')<p.find('OnProfessionAwardAccepted'))
check('specialization.deterministic','StableHash' in a and 'OrderByDescending' in a and 'ThenBy' in a)
check('specialization.consumer','GetSpecialization(npcId,profession)' in p)
check('work.eligibility','IsWorkEligible' in a)
check('persistence.versioned','rebirthNpcAdvancedProgression' in a and 'schema","1"' in a)
check('persistence.atomic','File.Copy(p,bak,true)' in a and 'File.Move(tmp,p)' in a)
check('restore.revalidation','ValidateSessionNoLock(c,now,out reason)' in a)
check('lifecycle.initialize','RebirthNpcAdvancedProgressionService.EnsureInitialized();' in l)
check('lifecycle.reset','RebirthNpcAdvancedProgressionService.ResetForWorldChange();' in l)
check('coordinator.save','RebirthNpcAdvancedProgressionPersistenceStore.Save();' in c)
check('admin.inspect','operation == "advancedprogression"' in d)
check('admin.qualify','operation == "advancedprogressionqualify"' in d)
for n,v in checks:print(('PASS' if v else 'FAIL')+' '+n)
failed=[n for n,v in checks if not v]
print('RESULT: '+('PASS' if not failed else 'FAIL')+' checks='+str(len(checks))+' failed='+str(len(failed)))
sys.exit(0 if not failed else 2)
