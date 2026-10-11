from pathlib import Path
import shutil
paths=['Tools/PurgeActorOutcomeFixture/Program.cs','Tools/PurgeNativeWitnessFixture/Program.cs','Tools/PurgeNativeWitnessFixture/fixture.csproj','Tools/PurgeLifecycleFixture/Program.cs']
for name in paths:
 p=Path(name);d=Path('_Documentation/PurgeCorrectionPlan_20261009/before')/p;d.parent.mkdir(parents=True,exist_ok=True)
 if not d.exists():shutil.copy2(p,d)
p=Path(paths[0]);s=p.read_text();start=s.index('var hostile=');end=s.index('\nf=new Fixture(false);',start)
s=s[:start]+'''var observer=new HarmonyLib.Patch{owner="other-mod",PatchMethod=typeof(UnknownDeathPatch).GetMethod("Prefix")};deathInfo.Prefixes.Add(observer);Check(RebirthPoiInheritedActorOutcomeHooks.IsReady,"foreign observer presence does not disable native outcome tracking");deathInfo.Transpilers.Add(medicalPostfix);Check(RebirthPoiInheritedActorOutcomeHooks.IsReady,"transpiler presence alone does not replace actual completion checks");deathInfo.Transpilers.Remove(medicalPostfix);deathInfo.Prefixes.Remove(observer);
'''+s[end:];p.write_text(s)
p=Path(paths[1]);s=p.read_text().replace('Check(!RebirthPoiNativeResetWitnesses.IsReady,"late foreign transpiler closes readiness")','Check(RebirthPoiNativeResetWitnesses.IsReady,"foreign transpiler presence alone leaves outcome checks active")').replace('Check(!RebirthPoiNativeResetWitnesses.IsReady,"foreign result-mutating postfix closes readiness")','Check(RebirthPoiNativeResetWitnesses.IsReady,"foreign postfix presence alone leaves outcome checks active")').replace('Check(!RebirthPoiNativeResetWitnesses.IsReady,"passive observer exemption restricted to exact sleeper reset")','Check(RebirthPoiNativeResetWitnesses.IsReady,"passive observer may coexist")');s+='\ninternal static class RebirthPurgeReleasePolicy { internal static bool Enabled=true; }\n';p.write_text(s)
p=Path(paths[2]);s=p.read_text().replace('</ItemGroup>','<Compile Include="../../Scripts/Purge/Native/RebirthPoiNativeResetPostconditions.cs"/></ItemGroup>');p.write_text(s)
p=Path(paths[3]);s=p.read_text()+'\ninternal static class RebirthPurgeClearNotification { internal static void Reset(){} internal static void Pulse(){} }\ninternal static class RebirthPurgeHudProgress { internal static void Pulse(){} }\n';p.write_text(s)
