from pathlib import Path
import re, sys
ROOT = Path(__file__).resolve().parents[2]
loader = (ROOT/'Scripts/Survivor/Definitions/RebirthSurvivorDefinitionLoader.cs').read_text(encoding='utf-8')
models = (ROOT/'Scripts/Survivor/Domain/RebirthSurvivorDefinitionModels.cs').read_text(encoding='utf-8')
checks=[]
def ck(name, cond): checks.append((name,bool(cond)))
ck('model exposes Knowledge', 'ReadOnlyCollection<RebirthKnowledgeDefinition> Knowledge' in models)
ck('constructor requires knowledge parameter', 'IList<RebirthKnowledgeDefinition> knowledge)' in models)
ck('constructor assigns Knowledge', 'Knowledge=new ReadOnlyCollection<RebirthKnowledgeDefinition>' in models)
ck('loader allocates knowledge list', 'List<RebirthKnowledgeDefinition> knowledge = new List<RebirthKnowledgeDefinition>();' in loader)
ck('loader reads knowledge node', 'Child(root,"knowledge",true)' in loader)
ck('loader adds knowledge definitions', 'knowledge.Add(new RebirthKnowledgeDefinition' in loader)
call = re.search(r'new\s+RebirthProgressionDefinition\s*\(([^;]+)\);', loader, re.S)
ck('loader constructor call exists', call is not None)
if call:
    args=[a.strip() for a in call.group(1).replace('\n',' ').split(',')]
    ck('loader passes 12 constructor arguments', len(args)==12)
    ck('loader final constructor argument is knowledge', args[-1]=='knowledge')
    ck('loader passes maxNegativeTraitRefund', len(args)>2 and args[2]=='maxNegativeTraitRefund')
else:
    ck('loader passes 12 constructor arguments', False)
    ck('loader final constructor argument is knowledge', False)
    ck('loader passes maxNegativeTraitRefund', False)
# Guard against the exact stale 11-argument form that generated CS7036.
ck('no stale skills-only constructor tail', 'attributes,tiers,skills);' not in loader.replace(' ',''))
failed=[n for n,v in checks if not v]
for n,v in checks: print(('PASS' if v else 'FAIL')+': '+n)
print(f'REBIRTH Survivor Compile Fix 03 vectors: {"PASS" if not failed else "FAIL"}')
print(f'passed={len(checks)-len(failed)}/{len(checks)}')
print('compile_validation_claimed=False')
sys.exit(1 if failed else 0)
