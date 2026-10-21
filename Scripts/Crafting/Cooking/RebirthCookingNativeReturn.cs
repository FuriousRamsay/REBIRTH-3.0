using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Fresh additive return to the original native endpoint. The grid journal is
// admission authority; an entered unknown child never calls its setter again.
internal static class RebirthCookingNativeReturn
{
    private sealed class Attempt:IDisposable
    {
        internal Guid Operation;internal string Body,Directory;internal EntityPlayerLocal Player;internal World World;internal int Deaths;
        internal RebirthRemoteResourceNativeBinding Source;internal ItemStack[] Before,After;internal bool[] Locks;
        internal RebirthCamperTransferJournal.Record Record;
        internal RebirthRemoteResourceNetworkScope Network;
        internal bool Invoked;internal RebirthRemoteResourceNativePublication.Watch Watch;internal IDisposable Memory;
        public void Dispose(){Watch?.Dispose();Watch=null;Memory?.Dispose();Memory=null;}
    }
    private sealed class Permit{internal Attempt Attempt;internal int Slot;internal ItemStack Value;internal ItemStack[] Output;internal bool Used;}
    private static readonly Dictionary<Guid,Attempt> Active=new Dictionary<Guid,Attempt>();
    private static Permit permit;private static World world;private static string directory;private static bool loaded,loading,unknown;
    private static string Root=>Path.Combine(GameIO.GetSaveGameDir(),"RebirthData","CookingNativeIngredientReturns");
    private static bool Same(ItemStack[] a,ItemStack[] b)=>a!=null&&b!=null&&a.Length==b.Length&&a.Select((value,index)=>RebirthStationGridIngredients.IsSameStackSnapshot(value,b[index])).All(value=>value);
    private static string Frame(ItemStack[] items)=>Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(items));
    private static ItemStack[] Items(XElement body,string field)=>RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(body.Element(field).Value));
    private static bool Pending(Attempt attempt)
    {
        if(!ReferenceEquals(GameManager.Instance?.World,attempt.World)||GameIO.GetSaveGameDir()!=attempt.Directory||
            !RebirthCookingGridCustody.NativeSourceReturnPending(attempt.Operation,out var record))return false;
        if(ReferenceEquals(record,attempt.Record))return true;
        if(!record.Before.SequenceEqual(Encoding.UTF8.GetBytes(attempt.Body)))return false;attempt.Record=record;return true;
    }
    private static bool Current(Attempt attempt)=>Pending(attempt)&&attempt.Network?.Current==true&&attempt.Player!=null&&
        RebirthCookingGridCustody.TryNativeSourceReturn(attempt.Operation,out var player,out _)&&ReferenceEquals(player,attempt.Player)&&attempt.Player.Died==attempt.Deaths&&!attempt.Player.IsDead()&&
        ReferenceEquals(attempt.World.GetPrimaryPlayer(),attempt.Player)&&ReferenceEquals(attempt.World.GetEntity(attempt.Player.entityId),attempt.Player)&&attempt.Source.Current();
    internal static void Refresh(){loaded=false;}
    private static void Bootstrap()
    {
        if(loading||!ThreadManager.IsMainThread())return;
        var actual=GameManager.Instance?.World;if(actual?.IsRemote()!=false)return;
        if(!ReferenceEquals(world,actual)||directory!=GameIO.GetSaveGameDir())
        {foreach(var value in Active.Values)value.Dispose();Active.Clear();world=actual;directory=GameIO.GetSaveGameDir();loaded=false;}
        if(loaded)return;loading=true;
        try
        {
            unknown=false;
            foreach(var record in RebirthCookingGridCustody.NativeSourceReturnOriginals())
            {
                if(Active.TryGetValue(record.Operation,out var existing))
                {if(ReferenceEquals(existing.Player,actual.GetPrimaryPlayer()))continue;existing.Dispose();Active.Remove(record.Operation);}
                var root=XElement.Parse(Encoding.UTF8.GetString(record.Before));var selected=root.Element("sourceReturn");var body=selected.Element("native");
                var player=actual.GetPrimaryPlayer();
                if((string)root.Attribute("action")!="source-return"||root.Element("beforeActor")?.Value!=root.Element("afterActor")?.Value||
                    !RebirthRemoteResourceNativeBinding.TryResolve(actual,(string)body.Attribute("source"),(RemoteResourceSourceKind)(int)body.Attribute("kind"),(string)body.Attribute("incarnation"),out var source))continue;
                if(Active.Count>=16){unknown=true;continue;}
                byte[] beforeBytes=Convert.FromBase64String(body.Element("before").Value),afterBytes=Convert.FromBase64String(body.Element("after").Value);
                long bytes=checked(64L*1024*1024+4L*(beforeBytes.Length+afterBytes.Length)+RemoteResourceTransactions.MeasureOriginalNativeFrame(beforeBytes)+RemoteResourceTransactions.MeasureOriginalNativeFrame(afterBytes));
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(bytes,out var memory)){unknown=true;continue;}
                ItemStack[] before,after;bool[] locks;
                try{before=RebirthCamperInventorySnapshot.Restore(beforeBytes);after=RebirthCamperInventorySnapshot.Restore(afterBytes);locks=body.Element("locks").Value.Select(value=>value=='1').ToArray();}
                catch{memory.Dispose();throw;}
                if(before.Length!=after.Length||locks.Length!=before.Length){memory.Dispose();unknown=true;continue;}
                Active.Add(record.Operation,new Attempt{Operation=record.Operation,Body=Encoding.UTF8.GetString(record.Before),Player=player,World=actual,Directory=directory,
                    Deaths=(int)root.Attribute("deaths"),Source=source,Before=before,After=after,Locks=locks,Memory=memory,Record=record,Network=player==null?null:RebirthRemoteResourceNetworkScope.Capture(player)});
            }
            loaded=true;
        }
        catch{unknown=true;}
        finally{loading=false;}
    }
    internal static bool Blocks(object target)
    {
        if(target==null||!ThreadManager.IsMainThread())return false;Bootstrap();
        object endpoint=target is ItemStackGrid grid?grid.holder:target is Bag bag?bag.ItemGrid?.holder:target;
        if(endpoint is TEFeatureStorage storage)endpoint=storage.Parent;
        foreach(var pair in Active.ToArray())
        {
            var attempt=pair.Value;
            if(!Pending(attempt)){attempt.Dispose();Active.Remove(pair.Key);if(unknown)loaded=false;continue;}
            if(ReferenceEquals(attempt.Source.Endpoint,endpoint)&&attempt.Source.Current())return true;
        }
        if(unknown&&(endpoint is TileEntityComposite||endpoint is TileEntityWorkstation||endpoint is EntityVehicle||endpoint is EntityDrone)){loaded=false;return true;}
        return false;
    }
    internal static bool Consume(ItemStackGrid grid,int slot,ItemStack value)
    {
        var one=permit;if(one==null||one.Used||!Current(one.Attempt)||!ReferenceEquals(one.Attempt.Source.Grid,grid)||one.Slot!=slot||!RebirthStationGridIngredients.IsSameStackSnapshot(one.Attempt.After[slot],value))return false;
        one.Used=true;return true;
    }
    internal static bool Consume(TileEntityWorkstation station,ItemStack[] value)
    {
        var one=permit;if(one==null||one.Used||!Current(one.Attempt)||!ReferenceEquals(one.Attempt.Source.Station,station)||!Same(one.Attempt.After,value))return false;
        one.Used=true;return true;
    }
    internal static int Plan(EntityPlayerLocal player,string sourceId,RemoteResourceSourceKind kind,string incarnation,int slot,ItemStack item,out XElement body)
    {
        body=null;
        if(!RebirthRemoteResourceNativeBinding.TryResolve(player.world,sourceId,kind,incarnation,out var source)||!source.IsAuthorized(player,out _)||
            RebirthRemoteResourceSourceCustody.Blocks(source.Endpoint))return 0;
        var before=source.CopySlots();var locks=source.CopyLocks();
        if(slot<0||slot>=before.Length||locks[slot])return 0;
        var old=before[slot];if(!old.IsEmpty()&&!RebirthStationGridIngredients.IsSameStackSnapshot(new ItemStack(old.itemValue,1),new ItemStack(item.itemValue,1)))return 0;
        int count=Math.Min(item.count,Math.Max(0,item.itemValue.ItemClass.Stacknumber.Value-(old.IsEmpty()?0:old.count)));if(count<1)return 0;
        var after=ItemStack.Clone(before);after[slot]=new ItemStack(item.itemValue.Clone(),checked((old.IsEmpty()?0:old.count)+count));
        body=new XElement("native",new XAttribute("source",sourceId),new XAttribute("kind",(int)kind),new XAttribute("incarnation",incarnation),new XAttribute("revision",source.Revision),
            new XElement("before",Frame(before)),new XElement("after",Frame(after)),new XElement("locks",string.Concat(locks.Select(value=>value?'1':'0'))));
        return count;
    }
    private static void PathCheck(string path)
    {if(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException();for(var info=new DirectoryInfo(Path.GetFullPath(path));info!=null;info=info.Parent)if(info.Exists&&(info.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException();}
    private static bool Write(Guid operation,string stage,string body)
    {
        byte[] bytes=new UTF8Encoding(false,true).GetBytes(body);if(bytes.Length>8*1024*1024)return false;PathCheck(Root);Directory.CreateDirectory(Root);
        string path=Path.Combine(Root,operation.ToString("N")+"."+stage+".xml");PathCheck(path);
        if(File.Exists(path))return new FileInfo(path).Length==bytes.Length&&File.ReadAllBytes(path).SequenceEqual(bytes);
        string temporary=path+".tmp";if(File.Exists(temporary))return false;
        using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        if(!File.ReadAllBytes(temporary).SequenceEqual(bytes))return false;File.Move(temporary,path);return File.ReadAllBytes(path).SequenceEqual(bytes);
    }
    internal static bool TryReadOutcome(Guid operation,XElement selected,out RebirthCookingNpcReturn.Result result)
    {
        result=new RebirthCookingNpcReturn.Result(0,true);
        try
        {
            if(!RebirthCookingGridCustody.TryNativeSourceReturn(operation,out _,out var original))
            {
                // Applied children remain recoverable. This is a read-only
                // lookup of the same checksummed local grid journal record.
                original=RebirthCamperTransferJournal.ReadExisting(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","CookingGridCustody"),operation);
            }
            if(original==null||!original.Before.SequenceEqual(original.After))return true;
            var root=XElement.Parse(Encoding.UTF8.GetString(original.Before));if(!XNode.DeepEquals(root.Element("sourceReturn"),selected))return true;
            string digest=RebirthRemoteResourceDurableJournal.Digest(selected.ToString(SaveOptions.DisableFormatting));
            if(File.Exists(Path.Combine(Root,operation.ToString("N")+".published.xml"))&&File.Exists(Path.Combine(Root,operation.ToString("N")+".refused.xml")))return true;
            foreach(string stage in new[]{"published","refused"})
            {
                string path=Path.Combine(Root,operation.ToString("N")+"."+stage+".xml");PathCheck(path);if(!File.Exists(path))continue;
                if(new FileInfo(path).Length>4096)return true;
                var body=RebirthCookingRemoteAdmission.ReadProtocolXml(File.ReadAllText(path,new UTF8Encoding(false,true)),4096);
                if(body.Name!="cookingNativeReturnOutcome"||body.Attributes().Count()!=4||body.HasElements||(int?)body.Attribute("version")!=1||
                    (string)body.Attribute("operation")!=operation.ToString("N")||(string)body.Attribute("stage")!=stage||(string)body.Attribute("original")!=digest)return true;
                int count=Items(selected,"items").Single().count;result=new RebirthCookingNpcReturn.Result(stage=="published"?count:0,false);return true;
            }
            return false;
        }
        catch{return true;}
    }
    private static bool Publish(Attempt attempt,string stage,XElement selected)=>Current(attempt)&&Write(attempt.Operation,stage,
        new XElement("cookingNativeReturnOutcome",new XAttribute("version",1),new XAttribute("operation",attempt.Operation.ToString("N")),new XAttribute("stage",stage),
            new XAttribute("original",RebirthRemoteResourceDurableJournal.Digest(selected.ToString(SaveOptions.DisableFormatting)))).ToString(SaveOptions.DisableFormatting))&&Current(attempt);
    internal static int Advance(Guid operation)
    {
        if(!RebirthCookingGridCustody.TryNativeSourceReturn(operation,out _,out var requested))return -1;
        var root=XElement.Parse(Encoding.UTF8.GetString(requested.Before));var selected=root.Element("sourceReturn");var body=selected.Element("native");
        if(TryReadOutcome(operation,selected,out var saved))return saved.Pending?-1:saved.Count>0?1:0;
        Bootstrap();if(!Active.TryGetValue(operation,out var attempt)||!Current(attempt))return -1;
        if(!RebirthCookingGridCustody.TryNativeSourceReturn(operation,out _,out var original)||original.State!=RebirthCamperTransferJournal.Phase.Uncertain)return -1;
        try
        {
            string path=Path.Combine(Root,operation.ToString("N")+".entered.xml");PathCheck(path);
            if(!attempt.Invoked)
            {
                if(File.Exists(path))return -1; // cold attempted effect is never reissued
                if(!attempt.Source.IsAuthorized(attempt.Player,out _)||attempt.Source.Revision!=(int)body.Attribute("revision")||
                    !Same(attempt.Source.CopySlots(),attempt.Before)||!attempt.Source.CopyLocks().SequenceEqual(attempt.Locks))
                    return Publish(attempt,"refused",selected)?0:-1;
                if(!Write(operation,"entered",selected.ToString(SaveOptions.DisableFormatting))||!Current(attempt))return -1;
                attempt.Invoked=true;
                for(int slot=0;slot<attempt.After.Length;slot++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(attempt.Before[slot],attempt.After[slot]))
                {
                    var desired=Items(body,"after")[slot];var one=new Permit{Attempt=attempt,Slot=slot,Value=desired};
                    if(attempt.Source.Station!=null){one.Output=Items(body,"after");}
                    permit=one;
                    try{if(attempt.Source.Station!=null)attempt.Source.Station.Output=one.Output;else attempt.Source.SetSlot(slot,desired);}finally{permit=null;}
                    if(!one.Used||!Current(attempt))return -1;
                }
                if(!Same(attempt.Source.CopySlots(),attempt.After)||!attempt.Source.CopyLocks().SequenceEqual(attempt.Locks))return -1;
                attempt.Source.MarkModified();
            }
            if(!Current(attempt)||!Same(attempt.Source.CopySlots(),attempt.After)||!attempt.Source.CopyLocks().SequenceEqual(attempt.Locks))return -1;
            if(attempt.Watch==null||!attempt.Watch.Current())
            {attempt.Watch?.Dispose();attempt.Watch=RebirthRemoteResourceNativePublication.Begin(attempt.Source,()=>Current(attempt));}
            if(attempt.Watch?.Saved()!=true)return -1;
            return Publish(attempt,"published",selected)?1:-1;
        }
        catch{return -1;}
    }
}
