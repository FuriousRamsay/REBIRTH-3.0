from pathlib import Path
import hashlib

here = Path(__file__).resolve().parent
root = here.parents[1]
source = root / 'Scripts/Vehicles/Camper/RebirthCamperTransferCommit.cs'
text = source.read_text(encoding='utf-8-sig')
start = text.index('    internal static void AssertCargoPolicy(')
# This is the final production method. Retain its full exact body, removing only
# the enclosing production class's final brace. Fail if its boundary changes.
body = text[start:].rstrip()
assert body.endswith('}\n}') or body.endswith('}\r\n}'), 'Review changed method boundary'
body = body[:body.rfind('}')].rstrip()
(here / 'ActualPolicy.generated.cs').write_text('using System;\npartial class RebirthCamperTransferCommit\n{\n' + body + '\n}\n', encoding='utf-8')
(here / 'source_hash.txt').write_text(hashlib.sha256(source.read_bytes()).hexdigest() + '\n', encoding='utf-8')
