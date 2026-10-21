using System;
using System.IO;
using System.Linq;
using System.Xml;

public static partial class RebirthNpcInventoryPersistenceStore
{
    internal static bool MatchesTradeSavedImage(RebirthNpcTradeOriginal original,RebirthNpcTradeNpcPlan plan,string image,bool completed)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||original==null||plan==null||image==null||image.Length>8*1024*1024)return false;
            using var text=new StringReader(image);
            using var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024});
            var document=new XmlDocument{XmlResolver=null};document.Load(reader);if(!ValidateDocument(document))return false;
            var npc=original.CopyQuote().NpcId;
            var rows=document.DocumentElement.SelectNodes("inventory").Cast<XmlElement>().Where(row=>row.GetAttribute("stableId")==npc.ToString()).ToArray();
            if(rows.Length!=1)return false;var saved=Read(rows[0]);
            return saved?.ReplayJournal!=null&&saved.ReplayJournal.Contains(original.Operation)==completed&&
                plan.Matches(new RebirthNpcInventorySnapshot{NpcId=saved.NpcId,Revision=saved.Revision,Quantities=saved.Quantities,
                    Reservations=saved.Reservations,NativeStacks=saved.NativeStacks,LogisticsEffects=saved.LogisticsEffects},completed);
        }
        catch{return false;}
    }
    // Read-only full saved NPC checkpoint. A returned image is not owner
    // payment or permission to apply this plan; the owning trade must separately
    // authenticate/reserve its original and qualify native owner Before/After.
    internal static bool TryReadTradePlanFromSavedBefore(RebirthNpcTradeOriginal original,string image,out RebirthNpcTradeNpcPlan plan)
    {
        plan=null;
        try
        {
            if(!ThreadManager.IsMainThread()||original==null||image==null||image.Length>8*1024*1024)return false;
            using var text=new StringReader(image);
            using var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024});
            var document=new XmlDocument{XmlResolver=null};document.Load(reader);if(!ValidateDocument(document))return false;
            var npc=original.CopyQuote().NpcId;
            var rows=document.DocumentElement.SelectNodes("inventory").Cast<XmlElement>().Where(row=>row.GetAttribute("stableId")==npc.ToString()).ToArray();
            if(rows.Length!=1)return false;var saved=Read(rows[0]);
            return saved?.ReplayJournal!=null&&!saved.ReplayJournal.Contains(original.Operation)&&saved.Reservations?.Count==0&&
                RebirthNpcTradeNpcPlan.TryCreate(original,new RebirthNpcInventorySnapshot{NpcId=saved.NpcId,Revision=saved.Revision,
                    Quantities=saved.Quantities,Reservations=saved.Reservations,NativeStacks=saved.NativeStacks,LogisticsEffects=saved.LogisticsEffects},out plan)&&
                MatchesTradeSavedImage(original,plan,image,false);
        }
        catch{plan=null;return false;}
    }
    internal static string CaptureTradeSaved(RebirthNpcTradeOriginal original,RebirthNpcTradeNpcPlan plan,bool completed)
    {
        if(!ThreadManager.IsMainThread()||original==null||plan==null)return null;
        lock(Sync)try
        {
            var world=GameManager.Instance?.World;var quote=original.CopyQuote();
            if(world?.IsRemote()!=false||world.worldState?.Guid!=original.World||!BoundCurrent||dirty||!CanRetireBacking||
                !RebirthNpcRecoveryGeneration.TryReadObservedGeneration(world,out var generation)||generation!=quote.RecoveryGeneration)return null;
            var expected=plan.CopySnapshot(completed);
            if(expected.NpcId.ToString()!=quote.NpcId.ToString()||expected.Revision!=quote.ExpectedNpcInventoryRevision+(completed?1u:0u)||
                !plan.Matches(RebirthNpcInventoryTransactionService.GetSnapshot(expected.NpcId),completed))return null;
            var path=boundPath;var file=new FileInfo(path);
            if(!file.Exists||file.Length<1||file.Length>8*1024*1024||(file.Attributes&FileAttributes.ReparsePoint)!=0)return null;
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024});
            var document=new XmlDocument{XmlResolver=null};document.Load(reader);
            if(!ValidateDocument(document))return null;
            var rows=document.DocumentElement.SelectNodes("inventory").Cast<XmlElement>().Where(row=>row.GetAttribute("stableId")==quote.NpcId.ToString()).ToArray();
            if(rows.Length!=1)return null;var saved=Read(rows[0]);
            if(saved?.ReplayJournal==null||saved.ReplayJournal.Contains(original.Operation)!=completed||
                !plan.Matches(new RebirthNpcInventorySnapshot{NpcId=saved.NpcId,Revision=saved.Revision,Quantities=saved.Quantities,
                    Reservations=saved.Reservations,NativeStacks=saved.NativeStacks,LogisticsEffects=saved.LogisticsEffects},completed))return null;
            string image=document.OuterXml;
            return image.Length<=8*1024*1024&&ReferenceEquals(world,GameManager.Instance?.World)&&boundPath==path&&BoundCurrent&&!dirty&&
                RebirthNpcRecoveryGeneration.TryReadObservedGeneration(world,out generation)&&generation==quote.RecoveryGeneration&&
                plan.Matches(RebirthNpcInventoryTransactionService.GetSnapshot(expected.NpcId),completed)?image:null;
        }
        catch{return null;}
    }
}
