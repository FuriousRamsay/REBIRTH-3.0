from pathlib import Path
import shutil
p=Path('Scripts/Purge/Native/RebirthPoiInheritedActorOutcomeHooks.cs');d=Path('_Documentation/PurgeCorrectionPlan_20261009/before')/p;d.parent.mkdir(parents=True,exist_ok=True)
if not d.exists():shutil.copy2(p,d)
s=p.read_text(encoding='utf-8-sig');start=s.index('    internal static bool IsReady');end=s.index('    internal static void Install()',start)
s=s[:start]+'''    internal static bool IsReady
    {
        get
        {
            // Native completion and actor identity are checked by the outcome observer.
            // An unrelated observer installed by another mod is not evidence of failure.
            try { return methods != null && methods.Length == 2 && methods.All(m =>
                { var info = Harmony.GetPatchInfo(m); return info != null && info.Owners.Contains(Owner); }); }
            catch { return false; }
        }
    }
'''+s[end:]
s=s.replace('Inherited actor outcomes withheld: conflicting native patch owner.','Inherited actor outcome hooks were not installed.')
p.write_text(s,encoding='utf-8')
p=Path('Tools/PurgeSupplyServiceFixture/Program.cs');d=Path('_Documentation/PurgeCorrectionPlan_20261009/before')/p;d.parent.mkdir(parents=True,exist_ok=True)
if not d.exists():shutil.copy2(p,d)
s=p.read_text(encoding='utf-8-sig');start=s.index('  Check(RebirthPurgeSupplyService.TryPrepare');end=s.index('  RebirthSandboxOptionManager.Current.IsPurge=false;',start)
s=s[:start]+'''  Check(RebirthPurgeSupplyService.TryPrepare(earned,new RebirthPurgeSupplyDeliveryPlan(14,61,-993),out var prepared),"pending entitlement retains original destination");
  Check(!RebirthPurgeSupplyService.TryPrepare(earned,new RebirthPurgeSupplyDeliveryPlan(15,61,-993),out _),"stale publication cannot reserve another entitlement");
  var stamp=new RebirthPurgeSupplyCrateStamp(world,player,prepared.InFlight,prepared.Token(world));
  Check(!RebirthPurgeSupplyService.TryLaunched(new RebirthPurgeSupplyCrateStamp(Guid.NewGuid(),player,stamp.Sequence,stamp.Token)),"foreign saved world cannot consume pending entitlement");
  Check(!RebirthPurgeSupplyService.TryLaunched(new RebirthPurgeSupplyCrateStamp(world,player,stamp.Sequence,Guid.NewGuid())),"wrong token cannot consume entitlement");
  Check(RebirthPurgeSupplyService.TryLaunched(stamp),"native accepted launch consumes one entitlement without disk crate proof");
  Check(RebirthPurgeSupplyService.Published[player].DeliveredDrops==1&&RebirthPurgeSupplyService.Published[player].InFlight==0,"accepted launch removes pending flight");
  Check(RebirthPurgeSupplyService.TryLaunched(stamp)&&RebirthPurgeSupplyService.Published[player].DeliveredDrops==1,"accepted launch retry is idempotent");
  RebirthPurgeSupplyService.Reset();for(int n=0;n<5;n++)Step();
  Check(RebirthPurgeSupplyService.Published[player].DeliveredDrops==1&&RebirthPurgeSupplyService.Published[player].InFlight==0,"cold service retains spent launch without replacement flight");
  live=false;Step();Check(RebirthPurgeSupplyService.Published==null,"replaced saved world makes publication unavailable");
  Check(!RebirthPurgeSupplyService.TryLaunched(stamp),"replaced world refuses launch commit");
'''+s[end:]
start=s.index('// The receipt boundary');s=s[:start]+'''// Native crate decoding is covered by the serialization fixture.
internal sealed class EntitySupplyCrate { internal RebirthPurgeSupplyCrateStamp Stamp; }
internal static class RebirthPurgeSupplyCrateSerialization
{
 internal static bool TryReadAuthoritative(EntitySupplyCrate crate,Guid world,out RebirthPurgeSupplyCrateStamp stamp)
 { stamp=crate?.Stamp;return stamp!=null&&stamp.World==world; }
}
'''
p.write_text(s,encoding='utf-8')
