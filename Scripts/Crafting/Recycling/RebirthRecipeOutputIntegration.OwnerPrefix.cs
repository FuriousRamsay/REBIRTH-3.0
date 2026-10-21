using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

public static partial class RebirthRecipeOutputIntegration
{
    private sealed class UiOwnerPrefix:IDisposable
    {
        internal World World;internal EntityPlayerLocal Actor;internal Guid Operation;internal int Index,Deaths,Entity;
        internal XUiC_RecipeStack Card;internal Recipe Recipe;internal int BeforeCount;internal long NativeVersion;
        internal string Owner,Creation,PlayerRoot,PlayerName,SourcePath,OriginalHash,AcceptedHash;
        internal byte[] Bag,Belt,Equipment,BeforeBag,BeforeBelt,BeforeEquipment;internal IDisposable Memory;internal bool Saved,Remote,Personal;
        public void Dispose(){Memory?.Dispose();Memory=null;Bag=Belt=Equipment=BeforeBag=BeforeBelt=BeforeEquipment=null;}
    }
    private sealed class UiOwnerPrefixes
    {
        internal readonly List<UiOwnerPrefix> Values=new List<UiOwnerPrefix>();
        ~UiOwnerPrefixes(){lock(uiOwnerGate){foreach(var value in Values)value.Dispose();Values.Clear();}}
    }
    private sealed class UiOwnerCapture
    {
        internal UiOwnerPrefix[] Prefixes;internal string NativeHash;internal int NativeLength;
    }
    private sealed class UiOwnerSave
    {
        internal UiOwnerCapture Capture;internal string Path;internal byte[] Native;internal IDisposable Memory;internal object PersonalQueue,StationBefore;
    }
    private static readonly object uiOwnerGate=new object();
    private static readonly ConditionalWeakTable<EntityPlayerLocal,UiOwnerPrefixes> uiOwnerPrefixes=new ConditionalWeakTable<EntityPlayerLocal,UiOwnerPrefixes>();
    private static readonly ConditionalWeakTable<PlayerDataFile,UiOwnerCapture> uiOwnerCaptures=new ConditionalWeakTable<PlayerDataFile,UiOwnerCapture>();
    private static void RegisterUiOutputOwnerPrefix(UiOutputOriginal original,int index,byte[] bag,byte[] belt,byte[] equipment,string acceptedHash)
    {
        // Remote entries await the existing authenticated native viewer Saved
        // capability. Server inventory replicas never qualify a prefix.
        IDisposable allocation=null;
        try
        {
            if(!original.Current()||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity)||identity.StorageKey!=original.Owner)return;
            lock(uiOwnerGate)
            {
                var values=uiOwnerPrefixes.GetValue(original.Actor,_=>new UiOwnerPrefixes()).Values;
                foreach(var stale in values.Where(value=>value.Saved||!ReferenceEquals(value.World,original.World)||value.Deaths!=original.Deaths||value.Creation!=original.Creation).ToArray())
                {values.Remove(stale);stale.Dispose();}
                if(values.Count>=4*65||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*(bag.Length+belt.Length+equipment.Length+
                    original.OwnerBeforeBag.Length+original.OwnerBeforeBelt.Length+original.OwnerBeforeEquipment.Length)+65536),out allocation))return;
                values.Add(new UiOwnerPrefix{World=original.World,Actor=original.Actor,Operation=original.Operation,Index=index,Deaths=original.Deaths,Entity=original.Actor.entityId,
                    Owner=original.Owner,Creation=original.Creation,PlayerRoot=Path.GetFullPath(GameIO.GetPlayerDataDir()),PlayerName=identity.CanonicalId,
                    SourcePath=original.Path,OriginalHash=UiOriginalHash(original.Original),AcceptedHash=acceptedHash,
                    Card=original.Scope.Card,Recipe=original.Recipe,BeforeCount=original.Count,NativeVersion=original.NativeVersionBefore,
                    BeforeBag=original.OwnerBeforeBag,BeforeBelt=original.OwnerBeforeBelt,BeforeEquipment=original.OwnerBeforeEquipment,
                    Bag=bag,Belt=belt,Equipment=equipment,Remote=original.World.IsRemote(),Personal=IsUiOutputPersonalCard(original),Memory=allocation});allocation=null;
            }
        }
        catch{/* Accepted native output remains recorded; no saved-owner proof. */}
        finally{allocation?.Dispose();}
    }
    internal static void ObserveUiOutputRemoteOwnerSaved(RebirthWorkstationCompletionPipeline.AdditionalOutputOwnerSaved saved)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||saved?.Current()!=true)return;
            ObserveRemoteOutputCallbackOwnerSaved(saved);
            var observation=saved.Observation;var request=saved.Request;var ack=saved.Acknowledgment;
            var frame=request.Element("nativeOwnerFrame");var delivery=request.Element("physicalDelivery")?.Elements().SingleOrDefault();
            if(observation?.Viewer==null||!observation.World.IsRemote()||frame==null||delivery==null||
                !uiOwnerPrefixes.TryGetValue(observation.Viewer,out var retained))return;
            lock(uiOwnerGate)foreach(var prefix in retained.Values)
            {
                if(prefix.Saved||!prefix.Remote||!ReferenceEquals(prefix.World,observation.World)||!ReferenceEquals(prefix.Actor,observation.Viewer)||
                    !ReferenceEquals(prefix.Card,observation.Card)||!ReferenceEquals(prefix.Recipe,observation.Recipe)||
                    prefix.BeforeCount!=observation.BeforeCount||prefix.NativeVersion!=observation.NativeVersionBefore||
                    prefix.Entity!=observation.Viewer.entityId||prefix.Deaths!=observation.ViewerDeaths||prefix.Creation!=observation.ViewerCreation||prefix.Owner!=observation.ViewerOwnerKey||
                    !prefix.BeforeBag.SequenceEqual(observation.BagBefore)||!prefix.BeforeBelt.SequenceEqual(observation.BeltBefore)||!prefix.BeforeEquipment.SequenceEqual(observation.EquipmentBefore)||
                    !prefix.Bag.SequenceEqual(Convert.FromBase64String((string)frame.Element("bag")))||
                    !prefix.Belt.SequenceEqual(Convert.FromBase64String((string)frame.Element("belt")))||
                    !prefix.Equipment.SequenceEqual(Convert.FromBase64String((string)frame.Element("equipment")))||
                    (string)delivery.Attribute("sink")!="Inventory"||!saved.Current())continue;
                var proof=new XElement("recipeOutputOwnerPrefixSaved",new XAttribute("operation",prefix.Operation.ToString("N")),new XAttribute("index",prefix.Index),
                    new XAttribute("original",prefix.OriginalHash),new XAttribute("accepted",prefix.AcceptedHash),new XAttribute("owner",prefix.Owner),
                    new XAttribute("creation",prefix.Creation),new XAttribute("deaths",prefix.Deaths),new XAttribute("entity",prefix.Entity),new XAttribute("remote",true),
                    new XAttribute("nativeOperation",observation.Operation.ToString("N")),new XAttribute("nativeGeneration",(string)request.Attribute("generation")),
                    new XAttribute("nativeNonce",(string)ack.Attribute("nativeNonce")),new XAttribute("nativeSource",(string)ack.Attribute("nativeSource")),
                    new XAttribute("nativeDigest",(string)request.Attribute("pdsDigest")),new XAttribute("nativeLength",(int)request.Attribute("pdsLength")),new XAttribute("requestDigest",(string)ack.Attribute("digest")));
                if(!saved.Current())return;
                prefix.Saved=PublishUiOriginal(prefix.SourcePath+".output-"+prefix.Index+".owner-saved",UiOriginalBytes(proof));
                if(prefix.Saved)prefix.Dispose();
            }
        }
        catch{/* No complete native Saved postimage means no prefix proof. */}
    }
    internal static void ObserveRecipeOutputOwnerFromPlayer(PlayerDataFile data,EntityPlayer actor,bool ran)
    {
        try
        {
            if(!ran||!ThreadManager.IsMainThread()||actor is not EntityPlayerLocal local||actor.world?.IsRemote()!=false||
                !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetPrimaryPlayer(),actor)||
                !ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||actor.IsDead()||data.id!=actor.entityId||data.deaths!=actor.Died||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity))return;
            ObserveUiOutputPersonalQueueFromPlayer(data,local);
            ObserveStationOutputBeforeOwnerFromPlayer(data,local);
            lock(uiOwnerGate)
            {
                uiOwnerCaptures.Remove(data);
                if(!uiOwnerPrefixes.TryGetValue(local,out var retained))return;
                var bag=OwningGrid(RebirthGearPlayerDataInventory.ReadSlots(data,true));var belt=OwningGrid(RebirthGearPlayerDataInventory.ReadSlots(data,false));
                var equipment=data.equipmentData?.ToArray();string creation=RebirthCamperCharacterAccess.CurrentCreation(actor);
                var matches=retained.Values.Where(value=>!value.Saved&&!value.Remote&&ReferenceEquals(value.World,actor.world)&&value.Entity==actor.entityId&&value.Deaths==actor.Died&&
                    value.Creation==creation&&value.Owner==identity.StorageKey&&value.PlayerName==identity.CanonicalId&&
                    (!value.Personal||HasUiOutputPersonalPrefix(data,value.Operation,value.Index+1))&&
                    value.Bag.SequenceEqual(bag)&&value.Belt.SequenceEqual(belt)&&equipment!=null&&value.Equipment.SequenceEqual(equipment)).ToArray();
                if(matches.Length==0)return;
                using(var capture=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                    if(capture==null)return;var native=RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data);
                    uiOwnerCaptures.Add(data,new UiOwnerCapture{Prefixes=matches,NativeHash=UiOriginalHash(native),NativeLength=native.Length});
                }
            }
        }
        catch{/* Only the actual completed native capture can qualify a prefix. */}
    }
    internal static void EnterRecipeOutputOwnerSave(PlayerDataFile data,string directory,string playerName,bool ran,out object state)
    {
        state=null;IDisposable memory=null;
        EnterUiOutputPersonalQueueSave(data,directory,playerName,ran,out var personalQueue);
        EnterStationOutputBeforeOwnerSave(data,directory,playerName,ran,out var stationBefore);
        if(personalQueue!=null||stationBefore!=null)state=new UiOwnerSave{PersonalQueue=personalQueue,StationBefore=stationBefore};
        try
        {
            lock(uiOwnerGate)
            {
                if(!ran||!uiOwnerCaptures.TryGetValue(data,out var capture)||capture.Prefixes.Length<1)return;
                var pending=capture.Prefixes.Where(value=>!value.Saved).ToArray();if(pending.Length==0)return;
                capture=new UiOwnerCapture{Prefixes=pending,NativeHash=capture.NativeHash,NativeLength=capture.NativeLength};
                string root=Path.GetFullPath(directory),path=Path.GetFullPath(Path.Combine(root,playerName+"."+PlayerDataFile.EXT));
                if(Path.GetDirectoryName(path)!=root||capture.Prefixes.Any(value=>value.PlayerRoot!=root||value.PlayerName!=playerName)||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*capture.NativeLength+65536),out memory))return;
                RebirthWorkstationCompletionPublication.RequireNativeSaveFile(path);
                var native=RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data);
                if(native.Length!=capture.NativeLength||UiOriginalHash(native)!=capture.NativeHash)return;
                state=new UiOwnerSave{Capture=capture,Path=path,Native=native,Memory=memory,PersonalQueue=personalQueue,StationBefore=stationBefore};memory=null;
            }
        }
        catch{/* No native Save admission is changed by this observer. */}
        finally{memory?.Dispose();}
    }
    internal static void ObserveRecipeOutputOwnerSave(bool ran,object state)
    {
        if(state is not UiOwnerSave save)return;
        try
        {
            if(save.Capture==null||!ran||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(save.Path,save.Native))return;
            lock(uiOwnerGate)foreach(var prefix in save.Capture.Prefixes)
            {
                if(prefix.Saved)continue;
                var proof=new XElement("recipeOutputOwnerPrefixSaved",new XAttribute("operation",prefix.Operation.ToString("N")),new XAttribute("index",prefix.Index),
                    new XAttribute("original",prefix.OriginalHash),new XAttribute("accepted",prefix.AcceptedHash),new XAttribute("owner",prefix.Owner),
                    new XAttribute("creation",prefix.Creation),new XAttribute("deaths",prefix.Deaths),new XAttribute("entity",prefix.Entity),
                    new XAttribute("playerFile",save.Path),new XAttribute("nativeDigest",save.Capture.NativeHash),new XAttribute("nativeLength",save.Native.Length));
                if(prefix.Personal)proof.Add(new XAttribute("personalPrefix",prefix.Index+1));
                prefix.Saved=PublishUiOriginal(prefix.SourcePath+".output-"+prefix.Index+".owner-saved",UiOriginalBytes(proof));
                if(prefix.Saved)prefix.Dispose();
            }
        }
        catch{/* A failed final-file observation is not a saved prefix. */}
        finally
        {
            save.Memory?.Dispose();save.Memory=null;save.Native=null;
            ObserveUiOutputPersonalQueueSave(ran,save.PersonalQueue);save.PersonalQueue=null;
            ObserveStationOutputBeforeOwnerSave(ran,save.StationBefore);save.StationBefore=null;
        }
    }
}
