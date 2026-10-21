using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthCookingMillingPublication
{
    internal sealed class StaticSourcePurpose:IDisposable
    {
        internal World World;internal string Save,Path,Birth,Hash;internal Guid Operation;internal Vector3i Position;
        internal byte[] Intent,Before,QueueAfter,Output;internal int BeforeOffset,Index;internal Recipe Recipe;internal XElement Image;
        internal IDisposable Memory;internal bool Closed,Adopted;
        internal readonly List<StaticNativePrefix> Prefixes=new List<StaticNativePrefix>();
        internal bool Current()=>!Closed&&ReferenceEquals(GameManager.Instance?.World,World)&&!World.IsRemote()&&
            System.IO.Path.GetFullPath(GameIO.GetSaveGameDir())==Save&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(Path,Intent);
        public void Dispose(){Closed=true;foreach(var prefix in Prefixes)prefix.Native?.Dispose();Prefixes.Clear();Memory?.Dispose();Memory=null;Intent=Before=QueueAfter=Output=null;Image=null;}
    }
    internal sealed class StaticNativePrefix
    {internal int Step,Elapsed;internal byte[] Body;internal RebirthRecipeOutputIntegration.MillingNativeSource Native;}
    private static readonly List<StaticSourcePurpose> staticMillingSources=new List<StaticSourcePurpose>();
    internal static void DiscoverStaticNativeSources(RegionFileManager manager)
    {
        if(!installed||!ThreadManager.IsMainThread()||GameManager.Instance?.World is not World world||world.IsRemote())return;
        foreach(var old in staticMillingSources.Where(value=>!ReferenceEquals(value.World,world)).ToArray()){old.Dispose();staticMillingSources.Remove(old);}
        string root=RebirthWorkstationCompletionOutbox.Root("cookingMilling");
        foreach(string path in Directory.EnumerateFiles(root,"*.xml"))
        {
            StaticSourcePurpose purpose=null;IDisposable memory=null;
            try
            {
                long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
                if(length<1||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(24L*length+65536),out memory))continue;
                var rootImage=RebirthWorkstationCompletionPublication.ReadPrimary(path);var image=rootImage?.Element("before")?.Element("cookingMillingOriginal");
                if(image==null||rootImage.Elements().Count()!=1||!Guid.TryParseExact((string)rootImage.Attribute("operation"),"N",out var operation)||
                    Path.GetFileName(path)!=operation.ToString("N")+".xml"||!MobileTerminalEnvelope(rootImage,operation,world,image)||
                    (string)image.Attribute("world")!=RebirthCookingOutputDelivery.WorldKey(world)||!string.IsNullOrEmpty((string)image.Attribute("mobile"))||
                    image.Element("owningUiBefore")!=null||image.Element("ownerTake")!=null||staticMillingSources.Any(value=>value.Operation==operation))continue;
                var intent=RebirthWorkstationCompletionPublication.CopyPublishedBytes(path);if(intent==null)continue;
                var completed=RebirthWorkstationCompletionPublication.ReadPrimary(path+".completed");var projection=completed?.Element("cookingMillingNativeProjection");
                var saved=RebirthWorkstationCompletionPublication.ReadPrimary(path+".saved");var savedTile=saved?.Element("cookingMillingSavedTile");
                if(MobileTerminalEnvelope(completed,operation,world,image)&&completed.Elements().Count()==2&&projection!=null&&
                    MobileTerminalEnvelope(saved,operation,world,image)&&saved.Elements().Count()==2&&savedTile!=null&&
                    (string)projection.Attribute("original")==path&&(string)savedTile.Attribute("original")==path&&
                    projection.Element("immutableOriginal")?.Value==Convert.ToBase64String(intent)&&
                    projection.Element("actualOutput")?.Value==image.Element("combinedOutput")?.Value&&
                    projection.Element("actualQueue")?.Value==image.Element("queueAfter")?.Value&&
                    savedTile.Element("combinedOutput")?.Value==image.Element("combinedOutput")?.Value&&savedTile.Element("queue")?.Value==image.Element("queueAfter")?.Value&&
                    (string)savedTile.Attribute("birth")==((string)image.Attribute("stationBirth"))&&savedTile.Element("nativeBody")!=null)continue;
                var unused=RebirthWorkstationCompletionPublication.ReadPrimary(path+".unused");var unusedAdmission=new XElement(rootImage);unusedAdmission.Add(new XElement("cookingMillingUnusedAdmission"));
                if(XNode.DeepEquals(unused,unusedAdmission))continue;
                var attempted=new XElement(rootImage);attempted.Add(new XElement("cookingMillingAttempted"));
                if(!XNode.DeepEquals(attempted,RebirthWorkstationCompletionPublication.ReadPrimary(path+".attempted")))continue;
                purpose=new StaticSourcePurpose{World=world,Save=Path.GetFullPath(GameIO.GetSaveGameDir()),Path=path,Operation=operation,Image=image,Intent=intent,
                    Hash=RebirthWorkstationCompletionOriginal.Hash(Convert.ToBase64String(intent)),Birth=(string)image.Attribute("stationBirth"),
                    Position=new Vector3i((int)image.Attribute("x"),(int)image.Attribute("y"),(int)image.Attribute("z")),BeforeOffset=(int)image.Attribute("beforeOffset"),
                    Before=Convert.FromBase64String(image.Element("nativeBefore").Value),QueueAfter=Convert.FromBase64String(image.Element("queueAfter").Value),
                    Output=Convert.FromBase64String(image.Element("combinedOutput").Value),Memory=memory};memory=null;
                // Retain the entered source before decoding. Unknown prefixes
                // remain a source hold, never a fresh completion permission.
                staticMillingSources.Add(purpose);
                ObserveMillingAuditSource(purpose);
                PrepareStaticNativePrefixes(purpose);purpose=null;
            }
            catch{/* Exact entered purposes remain retained without a setter. */}
            finally{memory?.Dispose();}
        }
    }
    private static void PrepareStaticNativePrefixes(StaticSourcePurpose source)
    {
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null||string.IsNullOrEmpty(source.Birth)||
                !RebirthProductiveEarnedNativeBody.TryMeasure(source.Before,source.BeforeOffset,out long graph,out long scratch)||
                !RebirthProductiveEarnedObservationBudget.TryResizeDetached(source.Memory,checked(24L*(source.Intent.Length+source.Before.Length)+graph+scratch+65536))||
                !RebirthProductiveEarnedNativeBody.TryRead(source.Before,source.BeforeOffset,out var model,out ulong clock))return;
            var rows=ReadMobileMillingQueue(source.QueueAfter);if(rows.Length!=model.Queue.Length)return;int selected=-1;
            for(int index=0;index<rows.Length;index++)
            {
                if(Queue(new[]{model.Queue[index]}).SequenceEqual(Queue(new[]{rows[index]})))continue;
                var old=model.Queue[index];if(selected>=0||old?.Recipe==null||old.Multiplier<1)return;
                var expected=new RecipeQueueItem{StartingEntityId=old.StartingEntityId,Quality=old.Quality,OneItemCraftTime=old.OneItemCraftTime};
                if(!Queue(new[]{expected}).SequenceEqual(Queue(new[]{rows[index]})))return;selected=index;
            }
            if(selected<0)return;source.Index=selected;source.Recipe=model.Queue[selected].Recipe;
            AddStaticNativePrefix(source,0,source.Before,source.BeforeOffset);
            model.Output=RebirthCamperInventorySnapshot.Restore(source.Output);AddStaticNativePrefix(source,1,RebirthProductiveEarnedNativeBody.Encode(model,clock,out int offset),offset);
            var row=model.Queue[selected];row.Multiplier=0;AddStaticNativePrefix(source,2,RebirthProductiveEarnedNativeBody.Encode(model,clock,out offset),offset);
            row.Recipe=null;AddStaticNativePrefix(source,3,RebirthProductiveEarnedNativeBody.Encode(model,clock,out offset),offset);
            row.IsCrafting=false;AddStaticNativePrefix(source,4,RebirthProductiveEarnedNativeBody.Encode(model,clock,out offset),offset);
            row.CraftingTimeLeft=0;AddStaticNativePrefix(source,5,RebirthProductiveEarnedNativeBody.Encode(model,clock,out offset),offset);
        }
    }
    private static void AddStaticNativePrefix(StaticSourcePurpose source,int step,byte[] body,int elapsed)
    {
        var duplicate=source.Prefixes.FirstOrDefault(value=>value.Elapsed==elapsed&&value.Body.SequenceEqual(body));
        if(duplicate!=null){duplicate.Step=step;return;}
        var native=RebirthRecipeOutputIntegration.MillingNativeSource.Register(source,body,elapsed);if(native==null)return;
        source.Prefixes.Add(new StaticNativePrefix{Step=step,Body=body,Elapsed=elapsed,Native=native});
    }
    private static bool HasStaticMillingSource(TileEntityWorkstation station)=>station!=null&&staticMillingSources.Any(value=>ReferenceEquals(value.World,GameManager.Instance?.World)&&
        !value.Closed&&value.Position==station.ToWorldPos()&&RebirthLogisticsEndpointIdentity.TryGetIncarnation(value.World,station,out var birth)&&birth==value.Birth);
    private static void PumpStaticNativeSources()
    {
        if(staticMillingSources.Count==0)return;
        foreach(var source in staticMillingSources.ToArray())
        {
            if(!ReferenceEquals(source.World,GameManager.Instance?.World)){source.Dispose();staticMillingSources.Remove(source);continue;}
            if(source.Adopted||!source.Current())continue;
            var matches=source.Prefixes.Where(value=>value.Native?.Current()==true).ToArray();if(matches.Length!=1)continue;
            var prefix=matches[0];var station=prefix.Native.Tile;var chunk=station?.GetChunk();if(station==null||chunk==null||pending.Count>=4)continue;
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*(source.Intent.Length+source.Before.Length+source.Output.Length+source.QueueAfter.Length)+65536),out var memory))continue;
            Original original=null;
            try
            {
                original=new Original{World=source.World,Station=station,Chunk=chunk,Recipe=source.Recipe,Operation=source.Operation,Path=source.Path,
                    StationBirth=source.Birth,OriginalImage=new XElement(source.Image),Intent=source.Intent,NativeBefore=source.Before,BeforeOffset=source.BeforeOffset,
                    QueueAfter=source.QueueAfter,Output=RebirthCamperInventorySnapshot.Restore(source.Output),Allocation=memory,
                    ColdRelease=()=>{source.Dispose();staticMillingSources.Remove(source);}};
                lock(chunk)
                {if(chunk.InProgressSaving||chunk.IsLocked||chunk.NeedsDecoration||chunk.InProgressDecorating||chunk.NeedsLightCalculation||chunk.InProgressLighting)continue;chunk.InProgressSaving=true;original.ChunkOwned=true;}
                var entry=Envelope(original,source.Image,new XElement("cookingMillingStaticNativePrefix",new XAttribute("step",prefix.Step),
                    new XElement("nativeLoadedBody",Convert.ToBase64String(prefix.Body))));
                string entryPath=source.Path+".native-prefix-"+RebirthWorkstationCompletionOriginal.Hash(Convert.ToBase64String(prefix.Body));
                if(!prefix.Native.Current()||!original.Current()||!RebirthWorkstationCompletionPublication.Write(entryPath,entry)||
                    !XNode.DeepEquals(entry,RebirthWorkstationCompletionPublication.ReadPrimary(entryPath)))continue;
                pending.Add(original);ObserveMillingAuditOriginal(original);source.Adopted=true;memory=null;
                station.SetDisableModifiedCheck(true);var row=station.Queue[source.Index];
                if(prefix.Step<1)station.Output=original.Output;
                if(prefix.Step<2)row.Multiplier=0;
                if(prefix.Step<3)row.Recipe=null;
                if(prefix.Step<4)row.IsCrafting=false;
                if(prefix.Step<5)row.CraftingTimeLeft=0;
                original.Mutated=true;station.SetDisableModifiedCheck(false);
                if(!original.Current()||!original.NativeTerminal())continue;
                var previous=publishingTile;publishingTile=original;try{station.setModified();}finally{publishingTile=previous;}
                original.NativeAccepted=true;original.ReleaseChunk();TryFinish(original);
            }
            catch{/* The same entered original stays held; no new generation. */}
            finally{if(memory!=null){if(original!=null)original.Dispose();else memory.Dispose();}}
        }
    }
}
