using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthCookingNpcReturn
{
    // The immutable grid child supplies identity before any source invocation.
    // A cold intent without its positive outcome is uncertainty, never retry.
    internal static Result RestoreBound(EntityPlayer actor,RemoteNpcResourceSource source,ItemStack item,int slot,int count,uint revision,Guid operation,ref Attempt retained)
    {
        if(operation==Guid.Empty)return new Result(0,true);
        if(retained!=null)return Restore(actor,source,item,slot,count,ref retained,operation,revision);
        if(TryReadBoundOutcome(actor,operation,source.NativeIdentity.ToString(),item,slot,count,revision,actor.Died,out var known))return known;
        return Restore(actor,source,item,slot,count,ref retained,operation,revision);
    }
    internal static bool TryReadBoundOutcome(EntityPlayer actor,Guid operation,string npc,ItemStack item,int slot,int count,uint revision,int originalDeaths,out Result result)
    {
        result=new Result(0,true);
        try
        {
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,actor.world)||
                !ReferenceEquals(actor.world.GetPrimaryPlayer(),actor)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||actor.IsDead()||actor.Died<originalDeaths||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner))return true;
            string directory=Path.Combine(Path.GetFullPath(GameIO.GetSaveGameDir()),"RebirthData","CookingIngredientReturns");
            string path=Path.Combine(directory,operation.ToString("N")+".intent.xml");PathCheck(path);
            if(!File.Exists(path))return false;
            var info=new FileInfo(path);if(info.Length<1||info.Length>MaximumJournal)return true;
            string text=File.ReadAllText(path,new UTF8Encoding(false,true));
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(text,MaximumJournal);
            if(body.Name!="cookingIngredientReturn"||(int?)body.Attribute("version")!=1||(string)body.Attribute("operation")!=operation.ToString("N")||
                (string)body.Attribute("world")!=actor.world.worldState.Guid||(string)body.Attribute("save")!=Path.GetFullPath(GameIO.GetSaveGameDir())||
                (string)body.Attribute("owner")!=owner.CanonicalId||(string)body.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(actor)||
                (int?)body.Attribute("deaths")!=originalDeaths||(string)body.Attribute("npc")!=npc||(int?)body.Attribute("slot")!=slot||
                (int?)body.Attribute("count")!=count||(uint?)body.Attribute("revision")!=revision||body.Element("item")?.Value!=RebirthNativeItemCodec.Encode(item.itemValue))return true;
            string digest=RebirthRemoteResourceDurableJournal.Digest(text);
            if(File.Exists(Path.Combine(directory,operation.ToString("N")+".published.xml"))&&File.Exists(Path.Combine(directory,operation.ToString("N")+".refused.xml")))return true;
            foreach(string stage in new[]{"published","refused"})
            {
                string outcome=Path.Combine(directory,operation.ToString("N")+"."+stage+".xml");PathCheck(outcome);
                if(!File.Exists(outcome))continue;
                if(new FileInfo(outcome).Length>4096)return true;
                var saved=RebirthCookingRemoteAdmission.ReadProtocolXml(File.ReadAllText(outcome,new UTF8Encoding(false,true)),4096);
                if(saved.Name!="cookingIngredientReturnOutcome"||(int?)saved.Attribute("version")!=1||saved.Attributes().Count()!=5||saved.HasElements||
                    (string)saved.Attribute("operation")!=operation.ToString("N")||(string)saved.Attribute("stage")!=stage||(string)saved.Attribute("intent")!=digest)return true;
                if(stage=="published"&&(bool?)saved.Attribute("invoked")!=true)return true;
                result=new Result(stage=="published"?count:0,false);return true;
            }
            return true;
        }
        catch{return true;}
    }
    internal static int PlanBound(RemoteNpcResourceSource source,EntityPlayer actor,ItemStack item,int slot,int requested,out uint revision)
    {
        revision=0;
        if(source==null||!source.IsLoaded||source.IsBusy||!source.IsAuthorized(actor,false,out _)||RebirthRemoteResourceSourceCustody.BlocksSource(source)||
            RebirthNpcMetabolismCustody.Blocks(source.NativeIdentity)||RemoteResourceTransactions.ActionBlocksNpc(source.NativeIdentity))return 0;
        if(source.IsDogStorage)
        {
            var fresh=new RemoteNpcResourceSource(source.NativeActor);revision=fresh.OriginalRevision;
            if(revision==uint.MaxValue||slot<0||slot>=fresh.Slots.Length||fresh.SlotLocks[slot])return 0;
            var live=fresh.Slots[slot];if(!live.IsEmpty()&&!RebirthStationGridIngredients.IsSameStackSnapshot(new ItemStack(live.itemValue.Clone(),1),new ItemStack(item.itemValue.Clone(),1)))return 0;
            return Math.Min(requested,Math.Max(0,item.itemValue.ItemClass.Stacknumber.Value-(live.IsEmpty()?0:live.count)));
        }
        string key=item.itemValue.ItemClass.GetItemName();var baseline=ItemClass.GetItem(key,false);
        if(baseline==null||!RebirthStationGridIngredients.IsSameStackSnapshot(new ItemStack(baseline,1),new ItemStack(item.itemValue.Clone(),1))||RebirthCompanionInventoryLockService.IsNpcItemLocked(source.NativeIdentity,key))return 0;
        var before=RebirthNpcInventoryTransactionService.GetSnapshot(source.NativeIdentity);if(before==null||before.Revision==uint.MaxValue)return 0;
        revision=before.Revision;before.Quantities.TryGetValue(key,out int existing);
        return existing<0?0:(int)Math.Min(requested,(long)int.MaxValue-existing);
    }
}
