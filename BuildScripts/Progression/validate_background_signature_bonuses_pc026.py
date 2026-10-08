#!/usr/bin/env python3
from pathlib import Path
import xml.etree.ElementTree as ET
import re,sys,csv,io
ROOT=Path(__file__).resolve().parents[2]
checks=[]
def chk(name, cond): checks.append((name,bool(cond))); print(('PASS ' if cond else 'FAIL ')+name)
def txt(rel): return (ROOT/rel).read_text(encoding='utf-8',errors='replace')
def parse(rel): return ET.parse(ROOT/rel).getroot()

prog=parse('Config/_Survivor/progression.xml'); bgs=parse('Config/_Survivor/backgrounds.xml'); bonuses=parse('Config/_Survivor/background_bonuses.xml')
skills={x.get('id'):x for x in prog.findall('./skills/skill') if x.get('id','').startswith('skill.')}
t=skills.get('skill.teaching')
chk('Teaching skill exists',t is not None)
chk('Teaching is normal with signed runtime bounds',t is not None and t.get('min')=='-50' and t.get('max')=='100' and t.get('advanced') not in ('true','1'))
chk('Teaching LBD requires completed teaching',t is not None and 'completed' in (t.get('lbd_source') or '').lower() and 'teaching' in (t.get('lbd_source') or '').lower())
chk('48 current Skills authored',len(skills)>=48)

teacher=bgs.find(".//background[@id='background.teacher']")
start={x.get('id'):float(x.get('value','0')) for x in teacher.findall('./starting_skills/skill')} if teacher is not None else {}
theory={x.get('id'):float(x.get('value','0')) for x in teacher.findall('./starting_skill_knowledge/skill')} if teacher is not None else {}
chk('Teacher starts Teaching +40',start.get('skill.teaching')==40)
chk('Teacher Mechanics weakness retained',start.get('skill.mechanics')==-10)
chk('Teacher no longer uses Bartering as primary start',start.get('skill.bartering',0)<=0)
chk('Teacher Teaching Knowledge +45',theory.get('skill.teaching')==45)
chk('Teacher starting experience does not auto grant specialist Knowledge','no automatic specialist Knowledge' in (teacher.find('./authoring').get('starting_experience') if teacher is not None and teacher.find('./authoring') is not None else ''))
bonus=bonuses.find(".//bonus[@id='background_bonus.scholar_and_mentor']")
tuning={x.get('key'):x for x in bonus.findall('./tuning')} if bonus is not None else {}
chk('Scholar and Mentor bonus exists',bonus is not None and bonus.get('background_id')=='background.teacher')
chk('Teacher solo study multiplier 0.8 unlocked',tuning.get('solo_study_time_multiplier') is not None and tuning['solo_study_time_multiplier'].get('value')=='0.8' and tuning['solo_study_time_multiplier'].get('locked')=='false')
chk('Lasting Lessons gain 1.25 unlocked',tuning.get('lasting_lessons_gain_multiplier') is not None and tuning['lasting_lessons_gain_multiplier'].get('value')=='1.25' and tuning['lasting_lessons_gain_multiplier'].get('locked')=='false')
chk('Lasting Lessons duration 1800 unlocked',tuning.get('lasting_lessons_active_seconds') is not None and tuning['lasting_lessons_active_seconds'].get('value')=='1800' and tuning['lasting_lessons_active_seconds'].get('locked')=='false')

ids=txt('Scripts/Survivor/Domain/RebirthSurvivorIds.cs'); policy=txt('Scripts/Survivor/Persistence/RebirthSurvivorSkillMigrationPolicy.cs')
chk('Teaching stable ID constant','SkillTeaching = "skill.teaching"' in ids)
chk('Skill allowlist contains Teaching','"skill.teaching"' in policy)
allow=set(re.findall(r'"(skill\.[^"]+)"',policy))
chk('current Skill allowlist has 48+',len(allow)>=48)
legacy_aliases={'skill.bladed_melee','skill.blunt_melee','skill.handguns','skill.rifles'}
chk('current Skill allowlist contains progression plus only known legacy aliases',set(skills).issubset(allow) and (allow-set(skills)).issubset(legacy_aliases))

svc=txt('Scripts/Survivor/Progression/RebirthTeachingService.cs'); net=txt('Scripts/Survivor/Progression/RebirthTeachingNetPackages.cs'); ui=txt('Scripts/Survivor/UI/XUiC_RebirthTeaching.cs')
for name,needle in [
 ('server authority gate','IsServerAuthority()'),('explicit participant validation','TryValidateParticipants'),('distinct players','instructor == student'),
 ('student acceptance path','ProcessOfferResponse'),('offer expires','OfferLifetimeSeconds'),('timed session','SessionDurationSeconds'),('continuous proximity','MaximumDistance * MaximumDistance'),
 ('death interruption','instructor.IsDead() || student.IsDead()'),('creation-hold interruption','RebirthCharacterCreationHoldService.IsHeld'),
 ('instructor practical competence','MinimumInstructorSkill'),('instructor Knowledge competence','MinimumInstructorKnowledge'),('meaningful knowledge gap','MinimumKnowledgeGap'),
 ('pair subject cooldown persisted','PairSubjectCooldownSeconds'),('history key includes student and subject','BuildHistoryKey(studentStorageKey, skillId)'),
 ('advanced subjects excluded','def.Advanced'),('Teaching cannot teach itself','RebirthSurvivorIds.SkillTeaching'),('legacy Bartering excluded','"skill.bartering"'),
 ('direct transfer bounded below instructor','instructorKnowledge - studentKnowledge - 0.01f'),('direct transfer max three','3f'),
 ('Teaching LBD only after applied outcome','RebirthSkillAwardService.TryAward(instructor, RebirthSurvivorIds.SkillTeaching'),
 ('Teacher Lasting Lessons applied after lesson','sr.Progression.TeachingLessons[session.SkillId]'),('Lasting Lessons persisted source instructor','InstructorStorageKey = ii.StorageKey'),
 ('history capped','TeachingHistory.Count <= 128'),
]: chk(name,needle in svc)
chk('no party requirement injected','IsInParty' not in svc and '.Party' not in svc)
chk('completed lesson requires positive real outcome','if (applied <= 0.0001f)' in svc)
chk('lesson itself does not call generic TryStudy','RebirthSkillKnowledgeService.TryStudy' not in svc)

skillaward=txt('Scripts/Survivor/Progression/RebirthSkillAwardService.cs'); sknow=txt('Scripts/Survivor/Progression/RebirthSkillKnowledgeService.cs')
chk('Lasting Lessons boosts legitimate practical LBD','GetSubjectLearningMultiplier(player,skillId)' in skillaward)
chk('Lasting Lessons boosts direct Skill Knowledge study',sknow.count('GetSubjectLearningMultiplier(player,skillId)')>=2)
lit=txt('Scripts/Survivor/Progression/RebirthLiteratureStudySessionService.cs'); audio=txt('Scripts/Survivor/Progression/RebirthAudiobookListeningSessionService.cs')
chk('Teacher solo bonus speeds physical literature','GetSoloStudyTimeMultiplier(player)' in lit)
chk('Teacher solo bonus speeds audiobooks','GetSoloStudyTimeMultiplier(player)' in audio)

installer=txt('Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs'); attr=txt('Scripts/Survivor/Progression/RebirthAttributeProgressionService.cs')
chk('Teaching service installed','RebirthTeachingService.Install()' in installer)
chk('Teaching trains an Attribute','case "skill.teaching"' in attr)

for cls,direction,sender in [
 ('NetPackageRebirthTeachingSubjectsRequest','ToServer','ValidEntityIdForSender(instructorId)'),
 ('NetPackageRebirthTeachingSubjectsResponse','ToClient',None),
 ('NetPackageRebirthTeachingOfferRequest','ToServer','ValidEntityIdForSender(instructorId)'),
 ('NetPackageRebirthTeachingOffer','ToClient',None),
 ('NetPackageRebirthTeachingOfferResponse','ToServer','ValidEntityIdForSender(studentId)')]:
 start=net.find('class '+cls+' : NetPackage')
 block=net[start:] if start>=0 else ''
 block=block[:block.find('\n[Preserve]',1) if block.find('\n[Preserve]',1)>=0 else len(block)]
 chk(cls+' direction '+direction,('NetPackageDirection.'+direction) in block)
 if sender: chk(cls+' sender validated',sender in block)
chk('subject response list bounded 64','Math.Min(64' in net and 'i<64' in net)

players=txt('Scripts/UI/Players/XUiC_RebirthPlayers.cs'); xui=txt('Config/XUi_InGame/xui.xml'); windows=txt('Config/XUi_InGame/windows.xml')
chk('Players detail has Teach button','btnRebirthTeachSelected' in players and 'btnRebirthTeachSelected' in windows)
chk('Teach requires selected online nonlocal player','online && !isLocal' in players)
chk('Players button proximity precheck','sqrMagnitude <= 64f' in players)
chk('Teaching window group Rebirth-only','<if cond="character_progression(\'Rebirth\')"><append xpath="/xui">' in xui and 'name="rebirthTeaching"' in xui)
chk('Teaching offer group Rebirth-only','name="rebirthTeachingOffer"' in xui)
chk('Teaching subject list uses native scrollbar','name="teachingSubjectScroll"' in windows and '<defaultscrollbar/>' in windows and '<scrollview name="teachingSubjectScrollView"' in windows)
chk('48 subject rows authored',len(re.findall(r'name="teachSubjectRow\d\d"',windows))==48)
chk('subject rows have clickable controls',len(re.findall(r'name="teachSubjectButton\d\d"',windows))==48)
chk('student has explicit Accept and Decline','btnTeachingOfferAccept' in windows and 'btnTeachingOfferDecline' in windows)
chk('offer UI sends explicit response', 'NetPackageRebirthTeachingOfferResponse' in ui and 'Respond(true)' in ui and 'Respond(false)' in ui)
chk('ESC/close becomes decline','if(!responded&&offerId>0L)Send(false)' in ui)

models=txt('Scripts/Survivor/Domain/RebirthSurvivorRuntimeModels.cs'); migration=txt('Scripts/Survivor/Persistence/RebirthWorldCharacterMigration.cs'); repo=txt('Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs')
m_schema=re.search(r'CurrentSchemaVersion\s*=\s*(\d+)',models); chk('world-character schema 15+',m_schema is not None and int(m_schema.group(1))>=15)
chk('14 to 15 migration registered','Register(14, Migrate14To15)' in migration)
chk('migration backfills Teaching skill','EnsureSkillEntry(os,RebirthSurvivorIds.SkillTeaching,teacher?40f:0f' in migration)
chk('migration backfills Teaching theory','EnsureSkillTheoryEntry(ot,RebirthSurvivorIds.SkillTeaching,teacher?45f:0f' in migration)
chk('migration initializes teaching state','new XElement("teaching",new XElement("lessons"),new XElement("history"))' in migration)
chk('migration refreshes definition authority','origin.SetAttributeValue("definitionHash"' in migration and 'Migrate14To15' in migration)
chk('runtime has persistent Lasting Lessons','TeachingLessons' in models and 'RebirthTeachingLessonRuntimeState' in models)
chk('runtime has persistent teaching history','TeachingHistory' in models and 'RebirthTeachingHistoryRuntimeState' in models)
chk('repository serializes Lasting Lessons','new XElement("lesson"' in repo and 'remainingActiveSeconds' in repo and 'gainMultiplier' in repo)
chk('repository serializes cooldown history','new XElement("completed"' in repo and 'lastCompletedUtcTicks' in repo and 'studentStorageKey' in repo)
chk('repository deserializes teaching state','state.TeachingLessons[skill]' in repo and 'state.TeachingHistory[key]' in repo)

release=txt('Scripts/Survivor/Debug/RebirthSurvivorReleaseAcceptance.cs')
chk('release acceptance expects 48 Skills',('GetCurrentSkillIds().Length' in release) or 'expected=48' in release or 'expected = 48' in release or 'ExpectedSkillCount = 48' in release)
harness=txt('Scripts/Survivor/Progression/RebirthTeachingVectorHarness.cs')
for n in ['weak-instructor-gap-rejected','novice-teacher-bounded','expert-teacher-bounded','gap-half-cap','pair-subject-cooldown','lasting-lessons-bounded']: chk('vector '+n,n in harness)

loc=txt('Config/Localization.csv'); needed=['xuiRebirthSkillTeaching','xuiRebirthTeach','xuiRebirthTeachingTitle','xuiRebirthTeachingOfferTitle','xuiRebirthTeachingAccept','xuiRebirthTeachingDecline']
for k in needed: chk('localization '+k,('\n'+k+',') in ('\n'+loc))
keys=[]
for line in loc.splitlines():
    if not line.strip(): continue
    keys.append(line.split(',',1)[0].strip())
chk('localization duplicate keys zero',len(keys)==len(set(keys)))

xmls=list(ROOT.rglob('*.xml')); bad=[]
for p in xmls:
    try: ET.parse(p)
    except Exception as e: bad.append((p,e))
chk('all project XML parses',not bad)
for rel in ['Scripts/Survivor/Progression/RebirthTeachingService.cs','Scripts/Survivor/Progression/RebirthTeachingNetPackages.cs','Scripts/Survivor/UI/XUiC_RebirthTeaching.cs','Scripts/Survivor/Domain/RebirthSurvivorRuntimeModels.cs','Scripts/Survivor/Persistence/RebirthWorldCharacterMigration.cs','Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs']:
    s=txt(rel);chk('delimiter '+Path(rel).name,s.count('{')==s.count('}'))

passed=sum(v for _,v in checks); failed=len(checks)-passed
print(f'PC026_STATIC_CHECKS={len(checks)} PASSED={passed} FAILED={failed} XML={len(xmls)}')
if bad:
    for p,e in bad: print('XML_ERROR',p,e)
sys.exit(1 if failed else 0)
