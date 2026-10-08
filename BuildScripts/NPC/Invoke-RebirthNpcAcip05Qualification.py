#!/usr/bin/env python3
from pathlib import Path
import sys,xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
files={k:root/v for k,v in {
"replication":"Scripts/Rebirth/NPC/Progression/RebirthNpcProgressionReplication.cs",
"ui":"Scripts/Rebirth/NPC/Progression/RebirthNpcProgressionXUi.cs",
"admin":"Scripts/Rebirth/NPC/Progression/RebirthNpcProgressionAdministration.cs",
"service":"Scripts/Rebirth/NPC/Progression/RebirthNpcProgressionService.cs",
"advanced":"Scripts/Rebirth/NPC/Progression/RebirthNpcAdvancedProgression.cs",
"lifecycle":"Scripts/Rebirth/NPC/Foundation/RebirthNpcLifecycle.cs",
"commands":"Scripts/Rebirth/NPC/Administration/RebirthNpcAdministrationSupport.cs",
"xui":"Config/XUi_InGame/xui.xml",
"windows":"Config/XUi_InGame/windows.xml"}.items()}
t={k:v.read_text(encoding="utf-8") for k,v in files.items()}
checks=[]
def c(n,v):checks.append((n,bool(v)))
for k,s in t.items():
 if k not in ("xui","windows"):c(k+".braces",s.count("{")==s.count("}"));c(k+".parentheses",s.count("(")==s.count(")"))
r=t["replication"];u=t["ui"];a=t["admin"];l=t["lifecycle"];cmd=t["commands"];svc=t["service"]
c("binary.versioned","Write((ushort)1)" in r and "version!=1" in r)
c("snapshot.domains",all(x in r for x in ["Entries","Certifications","Specializations","Mentorships"]))
c("late.join.baseline","SendBaseline(ClientInfo client)" in r and "SendBaseline(data.ClientInfo)" in l)
c("revision.stale.reject","value.Revision<=old.Revision" in r and "staleRejected++" in r)
c("server.delta.publish","PublishDelta(q.NpcId)" in svc)
c("client.only.apply","c!=null&&c.IsServer" in r)
c("ui.replica.source","RebirthNpcProgressionClientCache.TryGet" in u)
c("ui.no.local.math","XpFloor" not in u and "ResolveLevel" not in u)
c("ui.all.domains",all(x in u for x in ["attributes","specialties","professions","certifications","specialization","mentorship"]))
c("xui.registration","rebirthNpcProgression" in t["xui"] and "RebirthNpcProgression" in t["windows"])
c("telemetry.bounded","Capacity=256" in r and "Recent.Dequeue" in r)
c("telemetry.events",all(x in r for x in ["accepted","rejected","duplicates","levelChanges","cacheQueries","cacheMisses"]))
c("admin.inspect","progressioninspect" in cmd and "Inspect(RebirthNpcStableId id)" in a)
c("admin.export","progressiondiagnosticexport" in cmd and "ProgressionTelemetry.Export" in a)
c("admin.repair.safe.reset","progressionreset" in cmd and "Export(id,out path,out detail)" in a and "RemoveRecordForRepair" in a)
c("admin.permission","progressionreset" in cmd and "RebirthNpcAdminPermission.Repair" in cmd)
c("lifecycle.package","typeof(NetPackageRebirthNpcProgression).FullName" in l)
c("lifecycle.services",all(x in l for x in ["RebirthNpcProgressionReplicationService","RebirthNpcProgressionProjectionService","RebirthNpcProgressionUiService","RebirthNpcProgressionAdministration"]))
c("world.reset","RebirthNpcProgressionReplicationService.Reset" in l)
for f in [files["xui"],files["windows"]]:
 try: ET.parse(f);c(f.name+".xml",True)
 except Exception:c(f.name+".xml",False)
for n,v in checks:print(("PASS" if v else "FAIL")+" "+n)
failed=[n for n,v in checks if not v]
print("RESULT: "+("PASS" if not failed else "FAIL")+" checks="+str(len(checks))+" failed="+str(len(failed)))
sys.exit(0 if not failed else 2)
