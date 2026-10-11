"""Presentation-only live acceptance for Purge teaching windows in CodexTest."""
import json,sys,time,urllib.error
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"TraderVoices"))
from test_live import call,root
assert call("/state",sections="world")["world"]["gameName"]=="CodexTest"
call("/surroundings")
call("/cleararea","POST",radius=40)
call("/restore","POST")
results=[]
for topic in ("discovery","supplies"):
    results.append(call("/purgeinformationpreview","POST",confirm="CodexTest",topic=topic))
    time.sleep(.5)
    tree=call("/ui/tree",window="rebirthPurgeInformation")
    nodes=tree["nodes"]
    assert any(n.get("id")=="purgeInformationBody" and n.get("text") for n in nodes),tree
    results.append(tree)
    results.append(call("/screenshot","POST",name="purge_information_"+topic))
    call("/ui/click","POST",id="purgeInformationClose")
    time.sleep(.3)
    assert "rebirthPurgeInformation" not in call("/ui")["openWindows"]
(root/"_Documentation/PurgeReaudit_20261010/LIVE_INFORMATION_PREVIEW.json").write_text(json.dumps(results,indent=2))
print(json.dumps([r for r in results if "path" in r]))
print("PASS real-input close and populated bodies; screenshots require visual review. Reward triggers NOT tested.")