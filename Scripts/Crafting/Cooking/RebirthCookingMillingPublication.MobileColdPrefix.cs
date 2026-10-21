using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthCookingMillingPublication
{
    private static RecipeQueueItem[] ReadMobileMillingQueue(byte[] bytes)
    {
        if(bytes==null||bytes.Length<4||bytes.Length>2*1024*1024)throw new InvalidDataException();
        using(var stream=new MemoryStream(bytes,false))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);int count=reader.ReadInt32();if(count<1||count>255)throw new InvalidDataException();
            var rows=new RecipeQueueItem[count];for(int index=0;index<count;index++){rows[index]=new RecipeQueueItem();rows[index].Read(reader);}
            if(stream.Position!=stream.Length||!Queue(rows).SequenceEqual(bytes))throw new InvalidDataException();return rows;
        }
    }
    // Positive actual native-loaded source prefixes only. The UI producer owns
    // separate owner/callback effects and cannot enter this tile-only route.
    private static bool TryContinueMobileTilePrefix(MobileLoadedSource source,RebirthCamperModuleRuntime runtime,
        RebirthCamperStationState station,ItemStack installedItem,string path,Guid operation,XElement image,byte[] intent)
    {
        if(image.Element("owningUiBefore")!=null||image.Element("ownerTake")!=null||pending.Count>=4||
            pending.Any(value=>value.Operation==operation)||runtime.TransferPending||!MobileNativeModulesCurrent(source)||
            !ReferenceEquals(station.BoundVehicle,source.Vehicle)||!RebirthWorkstationCompletionPublication.MatchesPublishedBytes(path,intent))return false;
        var before=Convert.FromBase64String(image.Element("nativeBefore")?.Value??"");
        var afterQueue=Convert.FromBase64String(image.Element("queueAfter")?.Value??"");
        var outputBytes=Convert.FromBase64String(image.Element("combinedOutput")?.Value??"");
        var output=RebirthCamperInventorySnapshot.Restore(outputBytes);var rows=ReadMobileMillingQueue(afterQueue);
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null)return false;
            var model=RebirthCamperStationSnapshot.Restore(station.InstallationId,installedItem,before);
            if(!RebirthCamperStationSnapshot.Capture(model.InstallationId,model).SequenceEqual(before)||model.Queue.Length!=rows.Length)return false;
            int selected=-1;
            for(int index=0;index<rows.Length;index++)
            {
                if(Queue(new[]{model.Queue[index]}).SequenceEqual(Queue(new[]{rows[index]})))continue;
                if(selected>=0||model.Queue[index]?.Recipe==null||model.Queue[index].Multiplier<1)return false;
                var old=model.Queue[index];var expected=new RecipeQueueItem{StartingEntityId=old.StartingEntityId,Quality=old.Quality,OneItemCraftTime=old.OneItemCraftTime};
                if(!Queue(new[]{expected}).SequenceEqual(Queue(new[]{rows[index]})))return false;selected=index;
            }
            if(selected<0||output==null||!RebirthCamperInventorySnapshot.Capture(output).SequenceEqual(outputBytes))return false;
            // Detached replay predicts the precise existing setter sequence; it
            // cannot become an installed native source or an owner grant.
            var actual=RebirthCamperStationSnapshot.Capture(station.InstallationId,station);int prefix=actual.SequenceEqual(before)?0:-1;
            if(source.Bodies==null||!source.Bodies.TryGetValue(station.InstallationId,out var nativeLoaded)||!nativeLoaded.SequenceEqual(actual))return false;
            var row=model.Queue[selected];var recipe=row.Recipe;
            model.SetDisableModifiedCheck(true);
            model.Output=ItemStack.Clone(output);if(RebirthCamperStationSnapshot.Capture(model.InstallationId,model).SequenceEqual(actual))prefix=1;
            row.Multiplier=0;if(RebirthCamperStationSnapshot.Capture(model.InstallationId,model).SequenceEqual(actual))prefix=2;
            row.Recipe=null;if(RebirthCamperStationSnapshot.Capture(model.InstallationId,model).SequenceEqual(actual))prefix=3;
            row.IsCrafting=false;if(RebirthCamperStationSnapshot.Capture(model.InstallationId,model).SequenceEqual(actual))prefix=4;
            row.CraftingTimeLeft=0f;if(RebirthCamperStationSnapshot.Capture(model.InstallationId,model).SequenceEqual(actual))prefix=5;
            model.SetDisableModifiedCheck(false);model.setModified();
            var complete=RebirthCamperStationSnapshot.Capture(model.InstallationId,model);if(complete.SequenceEqual(actual))prefix=6;
            if(prefix<0||!Queue(model.Queue).SequenceEqual(afterQueue)||!MobileNativeModulesCurrent(source))return false;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*(intent.Length+before.Length+complete.Length+outputBytes.Length+afterQueue.Length)+65536),out var allocation))return false;
            Original original=null;
            try
            {
                original=new Original{World=source.World,Vehicle=source.Vehicle,Station=station,Recipe=recipe,Operation=operation,Path=path,
                    OriginalImage=new XElement(image),Intent=intent,NativeBefore=before,QueueAfter=afterQueue,Output=ItemStack.Clone(output),Allocation=allocation,
                    ColdRelease=()=>source.Scan.Release(station.InstallationId,operation)};
                var entry=Envelope(original,image,new XElement("cookingMillingColdNativePrefix",new XAttribute("prefix",prefix),
                    new XAttribute("nativeFile",source.Record.Read.Path),new XAttribute("nativeFileDigest",source.Record.Read.Digest),
                    new XElement("nativeLoadedBody",Convert.ToBase64String(actual)),new XElement("expectedCompleteBody",Convert.ToBase64String(complete))));
                string entryPath=path+".cold-prefix-"+prefix+"-"+RebirthWorkstationCompletionOriginal.Hash(source.Record.Read.Digest);
                if(!original.Current()||!MobileNativeModulesCurrent(source)||
                    !RebirthWorkstationCompletionPublication.Write(entryPath,entry)||
                    !XNode.DeepEquals(entry,RebirthWorkstationCompletionPublication.ReadPrimary(entryPath)))return false;
                // Retain before the first remaining native setter. Any exception
                // stays under this same original; it cannot retry a fresh plan.
                pending.Add(original);ObserveMillingAuditOriginal(original);allocation=null;
                station.SetDisableModifiedCheck(true);var live=station.Queue[selected];
                if(prefix<1)station.Output=ItemStack.Clone(output);
                if(prefix<2)live.Multiplier=0;
                if(prefix<3)live.Recipe=null;
                if(prefix<4)live.IsCrafting=false;
                if(prefix<5)live.CraftingTimeLeft=0f;
                original.Mutated=true;station.SetDisableModifiedCheck(false);
                if(prefix<6)station.setModified();
                if(!original.Current()||!original.NativeTerminal()||!RebirthCamperStationSnapshot.Capture(station.InstallationId,station).SequenceEqual(complete))return false;
                original.NativeAfter=complete;original.NativeAfterMode="RCS";original.NativeAccepted=true;
                source.Bodies[station.InstallationId]=complete;
                runtime.Persist();
                // Advance the retained metadata baseline only through this
                // exact own source effect, corroborating every other installed
                // native source. A sync callback cannot smuggle another module
                // change into a later original's native-load capability.
                int seen=0;
                for(int slot=0;slot<RebirthCamperCraftingStation.ModuleSlots;slot++)
                {
                    var installed=runtime.Stations.GetSlot(slot);if(installed==null)continue;seen++;
                    if(!source.Bodies.TryGetValue(installed.Id,out var expected)||!RebirthCamperStationSnapshot.Capture(installed.Id,installed.State).SequenceEqual(expected))return false;
                }
                if(seen!=source.Bodies.Count||!source.Vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperModules",out string modules))return false;
                source.Modules=modules;TryFinish(original);return original.Finished;
            }
            finally{if(allocation!=null){if(original!=null)original.Dispose();else allocation.Dispose();}}
        }
    }
}
