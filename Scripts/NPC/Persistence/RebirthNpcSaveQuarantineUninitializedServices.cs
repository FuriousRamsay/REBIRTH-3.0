using System;
using System.IO;
using System.Xml;

public static partial class RebirthNpcInventoryPersistenceStore
{
    private static RebirthNpcSaveQuarantineSavedAdoption.Store CapturePositiveEmptySavedAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||!(!dirty)||!(RebirthNpcInventoryTransactionService.CapturePersistentRecords().Length==0))return null;
            bool wasLoaded=loaded;var oldWorld=boundWorld;string oldPath=boundPath;var binding=originalBinding;
            string path=Path.Combine(scope.Directory,FileName);
            if(wasLoaded&&(File.Exists(path)||!scope.MatchesBinding(oldWorld as World,oldPath,true)))return null;
            if(!wasLoaded&&(oldWorld!=null||!string.IsNullOrEmpty(oldPath)||binding!=null))return null;
            var epoch=RebirthNpcInventoryTransactionService.RecoveryEpoch;
            string originalImage=QuarantineImage(RebirthNpcInventoryTransactionService.CapturePersistentRecords()).DocumentElement.OuterXml;
            bool Original(){lock(Sync)return loaded==wasLoaded&&!dirty&&ReferenceEquals(boundWorld,oldWorld)&&boundPath==oldPath&&ReferenceEquals(originalBinding,binding)&&
                ReferenceEquals(epoch,RebirthNpcInventoryTransactionService.RecoveryEpoch)&&RebirthNpcInventoryTransactionService.CapturePersistentRecords().Length==0&&QuarantineImage(RebirthNpcInventoryTransactionService.CapturePersistentRecords()).DocumentElement.OuterXml==originalImage;}
            bool Detached(){lock(Sync)return !loaded&&boundWorld==null&&boundPath==null&&!dirty&&!ReferenceEquals(epoch,RebirthNpcInventoryTransactionService.RecoveryEpoch)&&RebirthNpcInventoryTransactionService.CapturePersistentRecords().Length==0;}
            bool Incoming(World incoming){lock(Sync)return loaded&&!dirty&&ReferenceEquals(boundWorld,incoming)&&boundPath==path;}
            return RebirthNpcSaveQuarantineSavedAdoption.CaptureUninitialized("inventory",scope,path,Original,Detached,Incoming,
                ValidateDocument,()=>QuarantineImage(RebirthNpcInventoryTransactionService.CapturePersistentRecords()),EnsureLoaded);
        }
    }
}

public static partial class RebirthNpcEquipmentPersistenceStore
{
    private static RebirthNpcSaveQuarantineSavedAdoption.Store CapturePositiveEmptySavedAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||!(!dirty)||!(RebirthNpcEquipmentService.CapturePersistentRecords().Length==0))return null;
            bool wasLoaded=loaded;var oldWorld=boundWorld;string oldPath=boundPath;var binding=originalBinding;
            string path=Path.Combine(scope.Directory,FileName);
            if(wasLoaded&&(File.Exists(path)||!scope.MatchesBinding(oldWorld as World,oldPath,true)))return null;
            if(!wasLoaded&&(oldWorld!=null||!string.IsNullOrEmpty(oldPath)||binding!=null))return null;
            var epoch=RebirthNpcEquipmentService.RecoveryEpoch;
            string originalImage=QuarantineImage(RebirthNpcEquipmentService.CapturePersistentRecords()).DocumentElement.OuterXml;
            bool Original(){lock(Sync)return loaded==wasLoaded&&!dirty&&ReferenceEquals(boundWorld,oldWorld)&&boundPath==oldPath&&ReferenceEquals(originalBinding,binding)&&
                ReferenceEquals(epoch,RebirthNpcEquipmentService.RecoveryEpoch)&&RebirthNpcEquipmentService.CapturePersistentRecords().Length==0&&QuarantineImage(RebirthNpcEquipmentService.CapturePersistentRecords()).DocumentElement.OuterXml==originalImage;}
            bool Detached(){lock(Sync)return !loaded&&boundWorld==null&&boundPath==null&&!dirty&&!ReferenceEquals(epoch,RebirthNpcEquipmentService.RecoveryEpoch)&&RebirthNpcEquipmentService.CapturePersistentRecords().Length==0;}
            bool Incoming(World incoming){lock(Sync)return loaded&&!dirty&&ReferenceEquals(boundWorld,incoming)&&boundPath==path;}
            return RebirthNpcSaveQuarantineSavedAdoption.CaptureUninitialized("equipment",scope,path,Original,Detached,Incoming,
                Validate,()=>QuarantineImage(RebirthNpcEquipmentService.CapturePersistentRecords()),EnsureLoaded);
        }
    }
}

public static partial class RebirthNpcSocialPersistenceStore
{
    private static RebirthNpcSaveQuarantineSavedAdoption.Store CapturePositiveEmptySavedAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||!(!flushing)||!(QuarantineImage().SelectNodes("/*/*/*").Count==0))return null;
            bool wasLoaded=loaded;var oldWorld=boundWorld;string oldPath=boundPath;var binding=originalBinding;
            string path=Path.Combine(scope.Directory,FileName);
            if(wasLoaded&&(File.Exists(path)||!scope.MatchesBinding(oldWorld as World,oldPath,true)))return null;
            if(!wasLoaded&&(oldWorld!=null||!string.IsNullOrEmpty(oldPath)||binding!=null))return null;
            var epoch=RebirthNpcSocialService.RecoveryEpoch;var factionEpoch=RebirthNpcFactionGameplayService.RecoveryEpoch;var trades=tradeCompletions;
            string originalImage=QuarantineImage().DocumentElement.OuterXml;
            bool Original(){lock(Sync)return loaded==wasLoaded&&!flushing&&ReferenceEquals(boundWorld,oldWorld)&&boundPath==oldPath&&ReferenceEquals(originalBinding,binding)&&
                ReferenceEquals(epoch,RebirthNpcSocialService.RecoveryEpoch)&&ReferenceEquals(factionEpoch,RebirthNpcFactionGameplayService.RecoveryEpoch)&&ReferenceEquals(trades,tradeCompletions)&&QuarantineImage().SelectNodes("/*/*/*").Count==0&&QuarantineImage().DocumentElement.OuterXml==originalImage;}
            bool Detached(){lock(Sync)return !loaded&&boundWorld==null&&boundPath==null&&!flushing&&!ReferenceEquals(epoch,RebirthNpcSocialService.RecoveryEpoch)&&!ReferenceEquals(factionEpoch,RebirthNpcFactionGameplayService.RecoveryEpoch)&&!ReferenceEquals(trades,tradeCompletions)&&QuarantineImage().SelectNodes("/*/*/*").Count==0;}
            bool Incoming(World incoming){lock(Sync)return loaded&&!flushing&&ReferenceEquals(boundWorld,incoming)&&boundPath==path;}
            return RebirthNpcSaveQuarantineSavedAdoption.CaptureUninitialized("social-faction",scope,path,Original,Detached,Incoming,
                Validate,()=>QuarantineImage(),EnsureLoaded);
        }
    }
}

public static partial class RebirthNpcProgressionPersistenceStore
{
    private static RebirthNpcSaveQuarantineSavedAdoption.Store CapturePositiveEmptySavedAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||!(!dirty&&!flushing&&!loadFailed)||!(RebirthNpcProgressionService.ExportRecords().Length==0&&RebirthNpcProgressionService.ExportReplayIds().Length==0))return null;
            bool wasLoaded=loaded;var oldWorld=boundWorld;string oldPath=boundPath;var binding=originalBinding;
            string path=Path.Combine(scope.Directory,"RebirthNpcProgression.xml");
            if(wasLoaded&&(File.Exists(path)||!scope.MatchesBinding(oldWorld as World,oldPath,true)))return null;
            if(!wasLoaded&&(oldWorld!=null||!string.IsNullOrEmpty(oldPath)||binding!=null))return null;
            var epoch=RebirthNpcProgressionService.RecoveryEpoch;
            string originalImage=QuarantineImage().DocumentElement.OuterXml;
            bool Original(){lock(Sync)return loaded==wasLoaded&&!dirty&&!flushing&&!loadFailed&&ReferenceEquals(boundWorld,oldWorld)&&boundPath==oldPath&&ReferenceEquals(originalBinding,binding)&&
                ReferenceEquals(epoch,RebirthNpcProgressionService.RecoveryEpoch)&&RebirthNpcProgressionService.ExportRecords().Length==0&&RebirthNpcProgressionService.ExportReplayIds().Length==0&&QuarantineImage().DocumentElement.OuterXml==originalImage;}
            bool Detached(){lock(Sync)return !loaded&&boundWorld==null&&boundPath==null&&!dirty&&!flushing&&!loadFailed&&!ReferenceEquals(epoch,RebirthNpcProgressionService.RecoveryEpoch)&&RebirthNpcProgressionService.ExportRecords().Length==0&&RebirthNpcProgressionService.ExportReplayIds().Length==0;}
            bool Incoming(World incoming){lock(Sync)return loaded&&!dirty&&!flushing&&!loadFailed&&ReferenceEquals(boundWorld,incoming)&&boundPath==path;}
            return RebirthNpcSaveQuarantineSavedAdoption.CaptureUninitialized("progression",scope,path,Original,Detached,Incoming,
                doc=>{ReadDocument(doc);return true;},()=>QuarantineImage(),EnsureLoaded);
        }
    }
}

public static partial class RebirthNpcEconomyPersistenceStore
{
    private static RebirthNpcSaveQuarantineSavedAdoption.Store CapturePositiveEmptySavedAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||!(!dirty&&!flushing&&!loadFailed)||!(RebirthNpcEconomyLogisticsService.SavedAdoptionEmpty()))return null;
            bool wasLoaded=loaded;var oldWorld=boundWorld;string oldPath=boundPath;var binding=originalBinding;
            string path=Path.Combine(scope.Directory,"RebirthNpcEconomyOffline.xml");
            if(wasLoaded&&(File.Exists(path)||!scope.MatchesBinding(oldWorld as World,oldPath,true)))return null;
            if(!wasLoaded&&(oldWorld!=null||!string.IsNullOrEmpty(oldPath)||binding!=null))return null;
            var epoch=RebirthNpcEconomyLogisticsService.RecoveryEpoch;
            string originalImage=QuarantineImage().DocumentElement.OuterXml;
            bool Original(){lock(Sync)return loaded==wasLoaded&&!dirty&&!flushing&&!loadFailed&&ReferenceEquals(boundWorld,oldWorld)&&boundPath==oldPath&&ReferenceEquals(originalBinding,binding)&&
                ReferenceEquals(epoch,RebirthNpcEconomyLogisticsService.RecoveryEpoch)&&RebirthNpcEconomyLogisticsService.SavedAdoptionEmpty()&&QuarantineImage().DocumentElement.OuterXml==originalImage;}
            bool Detached(){lock(Sync)return !loaded&&boundWorld==null&&boundPath==null&&!dirty&&!flushing&&!loadFailed&&!ReferenceEquals(epoch,RebirthNpcEconomyLogisticsService.RecoveryEpoch)&&RebirthNpcEconomyLogisticsService.SavedAdoptionEmpty();}
            bool Incoming(World incoming){lock(Sync)return loaded&&!dirty&&!flushing&&!loadFailed&&ReferenceEquals(boundWorld,incoming)&&boundPath==path;}
            return RebirthNpcSaveQuarantineSavedAdoption.CaptureUninitialized("economy",scope,path,Original,Detached,Incoming,
                Validate,()=>QuarantineImage(),Load);
        }
    }
}

public static partial class RebirthNpcCombatPersistenceStore
{
    private static RebirthNpcSaveQuarantineSavedAdoption.Store CapturePositiveEmptySavedAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||!(!dirty)||!(RebirthNpcCombatEmergencyService.SavedAdoptionEmpty()))return null;
            bool wasLoaded=loaded;var oldWorld=boundWorld;string oldPath=boundPath;var binding=originalBinding;
            string path=Path.Combine(scope.Directory,FileName);
            if(wasLoaded&&(File.Exists(path)||!scope.MatchesBinding(oldWorld as World,oldPath,true)))return null;
            if(!wasLoaded&&(oldWorld!=null||!string.IsNullOrEmpty(oldPath)||binding!=null))return null;
            var epoch=RebirthNpcCombatEmergencyService.RecoveryEpoch;
            string originalImage=QuarantineImage().DocumentElement.OuterXml;
            bool Original(){lock(Sync)return loaded==wasLoaded&&!dirty&&ReferenceEquals(boundWorld,oldWorld)&&boundPath==oldPath&&ReferenceEquals(originalBinding,binding)&&
                ReferenceEquals(epoch,RebirthNpcCombatEmergencyService.RecoveryEpoch)&&RebirthNpcCombatEmergencyService.SavedAdoptionEmpty()&&QuarantineImage().DocumentElement.OuterXml==originalImage;}
            bool Detached(){lock(Sync)return !loaded&&boundWorld==null&&boundPath==null&&!dirty&&!ReferenceEquals(epoch,RebirthNpcCombatEmergencyService.RecoveryEpoch)&&RebirthNpcCombatEmergencyService.SavedAdoptionEmpty();}
            bool Incoming(World incoming){lock(Sync)return loaded&&!dirty&&ReferenceEquals(boundWorld,incoming)&&boundPath==path;}
            return RebirthNpcSaveQuarantineSavedAdoption.CaptureUninitialized("combat",scope,path,Original,Detached,Incoming,
                ValidateDocument,()=>QuarantineImage(),EnsureLoaded);
        }
    }
}

public static partial class RebirthNpcSettlementPersistenceStore
{
    private static RebirthNpcSaveQuarantineSavedAdoption.Store CapturePositiveEmptySavedAdoption(RebirthNpcSaveQuarantineScope scope)
    {
        lock(Sync)
        {
            if(scope==null||!(!dirty)||!(RebirthNpcSettlementSimulation.SavedAdoptionEmpty()))return null;
            bool wasLoaded=loaded;var oldWorld=boundWorld;string oldPath=boundPath;var binding=originalBinding;
            string path=Path.Combine(scope.Directory,FileName);
            if(wasLoaded&&(File.Exists(path)||!scope.MatchesBinding(oldWorld as World,oldPath,true)))return null;
            if(!wasLoaded&&(oldWorld!=null||!string.IsNullOrEmpty(oldPath)||binding!=null))return null;
            var epoch=RebirthNpcSettlementSimulation.RecoveryEpoch;
            string originalImage=QuarantineImage().DocumentElement.OuterXml;
            bool Original(){lock(Sync)return loaded==wasLoaded&&!dirty&&ReferenceEquals(boundWorld,oldWorld)&&boundPath==oldPath&&ReferenceEquals(originalBinding,binding)&&
                ReferenceEquals(epoch,RebirthNpcSettlementSimulation.RecoveryEpoch)&&RebirthNpcSettlementSimulation.SavedAdoptionEmpty()&&QuarantineImage().DocumentElement.OuterXml==originalImage;}
            bool Detached(){lock(Sync)return !loaded&&boundWorld==null&&boundPath==null&&!dirty&&!ReferenceEquals(epoch,RebirthNpcSettlementSimulation.RecoveryEpoch)&&RebirthNpcSettlementSimulation.SavedAdoptionEmpty();}
            bool Incoming(World incoming){lock(Sync)return loaded&&!dirty&&ReferenceEquals(boundWorld,incoming)&&boundPath==path;}
            return RebirthNpcSaveQuarantineSavedAdoption.CaptureUninitialized("settlement",scope,path,Original,Detached,Incoming,
                ValidateDocument,()=>QuarantineImage(),EnsureLoaded);
        }
    }
}


