"""Parent-run actual predicate extraction, no production writes."""
from pathlib import Path
import hashlib
here = Path(__file__).resolve().parent
source = here.parents[1] / 'Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyDeviceAdapter.cs'
text = source.read_text(encoding='utf-8-sig')

def extract(signature):
    start = text.index(signature)
    brace = text.index('{', start)
    semicolon = text.index(';', start)
    if '=>' in text[start:min(brace,semicolon)]: return text[start:semicolon + 1]
    depth = 0
    for pos in range(brace,len(text)):
        if text[pos]=='{': depth+=1
        elif text[pos]=='}':
            depth-=1
            if depth==0: return text[start:pos+1]
    raise ValueError(signature)

names=['private static bool Eligible(', 'private static bool MatchesExpected(',
       'private static bool DeviceOwnerAuthorized(', 'private static bool PoiOwnerAuthorized(',
       'private static bool Inside(', 'private static bool EnemyNear(']
(here / 'ActualPredicates.generated.cs').write_text('using System;\nusing System.Linq;\npartial class Adapter\n{\n'+'\n'.join(extract(n) for n in names)+'\n}\n',encoding='utf-8')
(here / 'source_hash.txt').write_text(hashlib.sha256(source.read_bytes()).hexdigest()+'\n',encoding='utf-8')
