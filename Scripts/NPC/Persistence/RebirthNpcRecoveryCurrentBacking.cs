using System;
using System.IO;
using System.Xml;

// Strict current-primary priming of unaffected read dependencies under the
// recovery's already held writer fences. No backup fallback or native effects.
internal static class RebirthNpcRecoveryCurrentBacking
{
    internal static void Prime(string directory,Guid operation,string target,RebirthNpcLiveRecoveryGroup group=null)
    {
        if(target!=RebirthNpcAggregatePersistenceStore.FileName&&!RebirthNpcAggregatePersistenceStore.IsRecoveryBackingLoaded)
        {
            if(!RebirthNpcAggregatePersistenceStore.CanPrimeRecoveryBacking)throw new InvalidDataException("Unloaded aggregate retains original semantic custody");
            var doc=Current(directory,RebirthNpcAggregatePersistenceStore.FileName,operation,"rebirthNpcPersistentRecords",group);
            if(!RebirthNpcAggregatePersistenceStore.TryPrepareRecovery(doc,out var prepared,out _,out var error)||!prepared.Commit())throw new InvalidDataException("Current semantic identity dependency: "+error);
        }
        if(target!="RebirthNpcExecutionState.xml"&&!RebirthNpcExecutionPersistenceStore.IsRecoveryBackingLoaded)
        {
            if(!RebirthNpcExecutionPersistenceStore.CanPrimeRecoveryBacking)throw new InvalidDataException("Unloaded execution retains original custody");
            var doc=Current(directory,"RebirthNpcExecutionState.xml",operation,"rebirthNpcExecutionState",group);
            var rows=RebirthNpcExecutionPersistenceStore.ReadRecoveryRecords(doc);
            if(rows.Length!=0&&!(group?.Phase>=2))throw new InvalidDataException("Unloaded current execution retains originals; ordinary restoration required before recovery");
            if(!RebirthNpcExecutionPersistenceStore.TryPrepareRecovery(doc,out var prepared,out _,out var error)||!prepared.Commit())throw new InvalidDataException("Current execution dependency: "+error);
            if(group?.Phase>=2)RebirthNpcExecutionLeaseRegistry.ReserveCurrentRecoveryExecution(rows);
        }
        if(target!="rebirth_npc_inventory.xml"&&!RebirthNpcInventoryPersistenceStore.IsTransferLoaded)
        {
            if(RebirthNpcInventoryTransactionService.CapturePersistentRecords().Length!=0)throw new InvalidDataException("Unloaded inventory retains runtime custody");
            var doc=Current(directory,"rebirth_npc_inventory.xml",operation,"rebirthNpcInventoryState",group);
            if(!RebirthNpcInventoryPersistenceStore.TryPrepareRecovery(doc,out var prepared,out _,out var error)||!prepared.Commit())throw new InvalidDataException("Current inventory dependency: "+error);
        }
        if(target!="rebirth_npc_equipment.xml"&&!RebirthNpcEquipmentPersistenceStore.IsRecoveryProjectionLoaded)
        {
            if(RebirthNpcEquipmentService.CapturePersistentRecords().Length!=0)throw new InvalidDataException("Unloaded equipment retains runtime custody");
            var doc=Current(directory,"rebirth_npc_equipment.xml",operation,"rebirthNpcEquipmentState",group);
            if(!RebirthNpcEquipmentPersistenceStore.TryPrepareRecovery(doc,out var prepared,out _,out var error)||!prepared.Commit())throw new InvalidDataException("Current equipment dependency: "+error);
        }
        if(!(group?.Phase>=2&&target==RebirthNpcAggregatePersistenceStore.FileName)&&!RebirthNpcWorldIntegrationService.IsRecoveryBackingLoaded)
        {
            var doc=Current(directory,"RebirthNpcWorldIntegration.xml",operation,null,group);
            if(!RebirthNpcWorldIntegrationService.TryPrepareInstalledRecoveryDocument(doc,out var prepared,out var error)||!prepared.Commit())throw new InvalidDataException("Current native identity dependency: "+error);
        }
    }
    private static XmlDocument Current(string directory,string file,Guid operation,string emptyRoot,RebirthNpcLiveRecoveryGroup group)
    {
        string path=Path.Combine(directory,file);
        if(!RebirthNpcPersistenceFile.HasRecoveryLease(path,operation))throw new InvalidOperationException("Current backing observation requires original writer fence");
        RebirthNpcLiveRecoveryGroup.Member member=null;if(group!=null)foreach(var row in group.Members)if(row.File==file)member=row;
        if(File.Exists(path))
        {
            if(member!=null)
            {string hash=RebirthNpcLiveRecoveryIntent.Hash(path);if(hash!=member.Hash&&hash!=member.BeforeHash)throw new InvalidDataException("Current member is neither recorded original nor publication");}
            return Read(path);
        }
        if(member!=null)
        {
            if(member.BeforeHash!="absent")throw new InvalidDataException("Recorded current primary missing");
        }
        else if(Directory.Exists(directory))
            foreach(var ignored in Directory.EnumerateFiles(directory,file+".*"))throw new InvalidDataException("Missing current primary has retained evidence");
        if(emptyRoot==null)return RebirthNpcWorldIntegrationService.CaptureEmptyRecoveryDocument();
        var doc=new XmlDocument();var root=doc.CreateElement(emptyRoot);root.SetAttribute("format","1");doc.AppendChild(root);return doc;
    }
    internal static XmlDocument Read(string path)
    {
        using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(input.Length<1||input.Length>67108864)throw new InvalidDataException("Current recovery backing size unsupported");
            var doc=new XmlDocument{XmlResolver=null};using(var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=67108864}))doc.Load(reader);return doc;
        }
    }
}
