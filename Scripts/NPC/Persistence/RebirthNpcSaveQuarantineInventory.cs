using System;
using System.Collections.Generic;
using System.Xml;

public static partial class RebirthNpcInventoryTransactionService
{
    internal static RebirthNpcSaveQuarantineDomain PrepareInventoryQuarantine()
    {
        lock(Sync)
        {
            var original=States;var epoch=generationEpoch;
            var empty=new Dictionary<RebirthNpcStableId,InventoryState>();var nextEpoch=new object();
            var document=RebirthNpcInventoryPersistenceStore.QuarantineImage(CapturePersistentRecords());
            return new RebirthNpcSaveQuarantineDomain("inventory-service",document,
                ()=>{lock(Sync)return ReferenceEquals(States,original)&&ReferenceEquals(generationEpoch,epoch)&&RebirthNpcInventoryPersistenceStore.QuarantineImage(CapturePersistentRecords()).OuterXml==document.OuterXml;},
                ()=>{lock(Sync){States=empty;generationEpoch=nextEpoch;}},
                ()=>{lock(Sync){States=original;generationEpoch=epoch;}});
        }
    }
}
public static partial class RebirthNpcInventoryPersistenceStore
{
    internal static XmlDocument QuarantineImage(RebirthNpcInventoryPersistentRecord[] records)
    {var document=new XmlDocument{XmlResolver=null};document.LoadXml(RecoveryBackingImage(records));if(!ValidateDocument(document))throw new InvalidOperationException("Retained inventory cannot be captured losslessly");return document;}
    internal static bool TryPrepareQuarantine(RebirthNpcSaveQuarantineScope scope,List<RebirthNpcSaveQuarantineDomain> domains)
    {
        lock(Sync)
        {
            if(!scope.MatchesBinding(boundWorld,boundPath,loaded||dirty||RebirthNpcInventoryTransactionService.CapturePersistentRecords().Length!=0))return false;
            bool priorLoaded=loaded,priorDirty=dirty;var world=boundWorld;string path=boundPath;var binding=originalBinding;long next=nextSaveUtcTicks;
            var doc=new XmlDocument();var root=doc.CreateElement("inventoryBinding");doc.AppendChild(root);root.SetAttribute("loaded",priorLoaded?"1":"0");root.SetAttribute("dirty",priorDirty?"1":"0");root.SetAttribute("path",path??string.Empty);
            var service=RebirthNpcInventoryTransactionService.PrepareInventoryQuarantine();
            var store=new RebirthNpcSaveQuarantineDomain("inventory-binding",doc,
                ()=>{lock(Sync)return loaded==priorLoaded&&dirty==priorDirty&&ReferenceEquals(boundWorld,world)&&boundPath==path&&ReferenceEquals(originalBinding,binding)&&nextSaveUtcTicks==next;},
                ()=>{lock(Sync){loaded=false;dirty=false;boundWorld=null;boundPath=null;originalBinding=null;nextSaveUtcTicks=0;}},
                ()=>{lock(Sync){loaded=priorLoaded;dirty=priorDirty;boundWorld=world;boundPath=path;originalBinding=binding;nextSaveUtcTicks=next;}});
            domains.Add(service);domains.Add(store);return true;
        }
    }
}

public static partial class RebirthNpcEquipmentService
{
    internal static RebirthNpcSaveQuarantineDomain PrepareEquipmentQuarantine()
    {
        lock(Sync)
        {
            var original=States;var epoch=generationEpoch;var empty=new Dictionary<RebirthNpcStableId,State>();var nextEpoch=new object();
            var document=RebirthNpcEquipmentPersistenceStore.QuarantineImage(CapturePersistentRecords());
            return new RebirthNpcSaveQuarantineDomain("equipment-service",document,
                ()=>{lock(Sync)return ReferenceEquals(States,original)&&ReferenceEquals(generationEpoch,epoch)&&RebirthNpcEquipmentPersistenceStore.QuarantineImage(CapturePersistentRecords()).OuterXml==document.OuterXml;},
                ()=>{lock(Sync){States=empty;generationEpoch=nextEpoch;}},
                ()=>{lock(Sync){States=original;generationEpoch=epoch;}});
        }
    }
}
public static partial class RebirthNpcEquipmentPersistenceStore
{
    internal static XmlDocument QuarantineImage(RebirthNpcEquipmentPersistentRecord[] records)
    {var document=new XmlDocument{XmlResolver=null};document.LoadXml(RecoveryBackingImage(records));if(!Validate(document))throw new InvalidOperationException("Retained equipment cannot be captured losslessly");return document;}
    internal static bool TryPrepareQuarantine(RebirthNpcSaveQuarantineScope scope,List<RebirthNpcSaveQuarantineDomain> domains)
    {
        lock(Sync)
        {
            if(!scope.MatchesBinding(boundWorld,boundPath,loaded||dirty||RebirthNpcEquipmentService.CapturePersistentRecords().Length!=0))return false;
            bool priorLoaded=loaded,priorDirty=dirty;var world=boundWorld;string path=boundPath;var binding=originalBinding;long next=nextSaveUtcTicks;
            var doc=new XmlDocument();var root=doc.CreateElement("equipmentBinding");doc.AppendChild(root);root.SetAttribute("loaded",priorLoaded?"1":"0");root.SetAttribute("dirty",priorDirty?"1":"0");root.SetAttribute("path",path??string.Empty);
            var service=RebirthNpcEquipmentService.PrepareEquipmentQuarantine();
            var store=new RebirthNpcSaveQuarantineDomain("equipment-binding",doc,
                ()=>{lock(Sync)return loaded==priorLoaded&&dirty==priorDirty&&ReferenceEquals(boundWorld,world)&&boundPath==path&&ReferenceEquals(originalBinding,binding)&&nextSaveUtcTicks==next;},
                ()=>{lock(Sync){loaded=false;dirty=false;boundWorld=null;boundPath=null;originalBinding=null;nextSaveUtcTicks=0;}},
                ()=>{lock(Sync){loaded=priorLoaded;dirty=priorDirty;boundWorld=world;boundPath=path;originalBinding=binding;nextSaveUtcTicks=next;}});
            domains.Add(service);domains.Add(store);return true;
        }
    }
}
