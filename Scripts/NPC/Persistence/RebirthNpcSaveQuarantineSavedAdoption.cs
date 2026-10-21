using System;
using System.IO;
using System.Xml;

// Positive persistent-backing adoption only. No native NPC/controller, action,
// lease or transient scheduler is restored or retired by these receipts.
internal sealed class RebirthNpcSaveQuarantineSavedAdoption:IDisposable
{
    internal sealed class Store:IDisposable
    {
        internal readonly string Name;
        private readonly World original;
        private readonly string directory,path,image;
        private readonly Func<bool> originalCurrent,empty;
        private readonly Func<World,bool> incomingCurrent;
        private readonly Func<XmlDocument,bool> validate;
        private readonly Func<XmlDocument,string> canonicalImage;
        private readonly Action load;
        private World adopted;
        private IDisposable retention;
        private bool disposed;
        private readonly bool allowEmptyPrimary;
        internal Store(string name,RebirthNpcSaveQuarantineScope scope,string path,string image,
            Func<bool> originalCurrent,Func<bool> empty,Func<World,bool> incomingCurrent,Func<XmlDocument,bool> validate,Action load,IDisposable retention,bool allowEmptyPrimary=false,Func<XmlDocument,string> canonicalImage=null)
        {Name=name;original=scope.World;directory=scope.Directory;this.path=path;this.image=image;
            this.originalCurrent=originalCurrent;this.empty=empty;this.incomingCurrent=incomingCurrent;this.validate=validate;this.load=load;this.retention=retention;this.allowEmptyPrimary=allowEmptyPrimary;this.canonicalImage=canonicalImage;}
        public void Dispose(){if(disposed)return;disposed=true;retention?.Dispose();retention=null;}
        internal bool IsOriginalCurrent=>!disposed&&ThreadManager.IsMainThread()&&originalCurrent()&&PrimaryCurrent();
        private bool PrimaryCurrent()
        {
            try
            {
                if(RebirthNpcPersistenceFile.IsRecoveryFenced(path)||File.Exists(path+".tmp"))return false;
                if(allowEmptyPrimary&&!File.Exists(path))return RebirthNpcPersistenceFile.CanInitializeEmpty(path);
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    if(stream.Length<1||stream.Length>33554432)return false;
                    var doc=new XmlDocument{XmlResolver=null};
                    using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=33554432}))doc.Load(reader);
                    return validate(doc)&&(canonicalImage==null?doc.DocumentElement.OuterXml:canonicalImage(doc))==image;
                }
            }
            catch{return false;}
        }
        private bool ScopeCurrent(RebirthNpcTradeLocalOwnerLoad.NativeReentry source)=>!disposed&&source!=null&&source.Current()&&
            ThreadManager.IsMainThread()&&source.World?.IsRemote()==false&&!ReferenceEquals(source.World,original)&&
            ReferenceEquals(source.World,GameManager.Instance?.World)&&SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer==true&&
            RebirthNpcSaveQuarantineScope.SameDirectory(directory,source.Directory)&&
            RebirthNpcSaveQuarantineScope.SameDirectory(directory,GameIO.GetSaveGameDir());
        internal bool CanAdopt(RebirthNpcTradeLocalOwnerLoad.NativeReentry source)
            =>CanAdopt(source?.World,()=>ScopeCurrent(source));
        private bool ScopeCurrent(RebirthNpcRuntimeNativeAdoption.NativeReentry source)=>!disposed&&source?.Current()==true&&
            !ReferenceEquals(source.World,original)&&RebirthNpcSaveQuarantineScope.SameDirectory(directory,source.Directory);
        internal bool CanAdopt(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
            =>CanAdopt(source?.World,()=>ScopeCurrent(source));
        private bool ScopeCurrent(RebirthDogLifecycleService.NativeOwnerReentry source)=>!disposed&&source?.Current()==true&&
            !ReferenceEquals(source.World,original)&&RebirthNpcSaveQuarantineScope.SameDirectory(directory,source.Directory)&&
            ReferenceEquals(source.World,GameManager.Instance?.World)&&source.World?.IsRemote()==false&&
            SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer==true&&RebirthNpcSaveQuarantineScope.SameDirectory(directory,GameIO.GetSaveGameDir());
        internal bool CanAdopt(RebirthDogLifecycleService.NativeOwnerReentry source)
            =>CanAdopt(source?.World,()=>ScopeCurrent(source));
        private bool CanAdopt(World incoming,Func<bool> sourceCurrent)
        {try{return sourceCurrent()&&PrimaryCurrent()&&(adopted==null?(empty()||incomingCurrent(incoming)):ReferenceEquals(adopted,incoming)&&incomingCurrent(incoming));}catch{return false;}}
        internal bool TryAdopt(RebirthNpcTradeLocalOwnerLoad.NativeReentry source)
            =>TryAdopt(source?.World,()=>ScopeCurrent(source));
        internal bool TryAdopt(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
            =>TryAdopt(source?.World,()=>ScopeCurrent(source));
        internal bool TryAdopt(RebirthDogLifecycleService.NativeOwnerReentry source)
            =>TryAdopt(source?.World,()=>ScopeCurrent(source));
        private bool TryAdopt(World incoming,Func<bool> sourceCurrent)
        {
            try
            {
                if(!CanAdopt(incoming,sourceCurrent))return false;
                if(adopted==null&&!incomingCurrent(incoming))
                {
                    // Pin the already-qualified primary while its normal typed
                    // loader runs. No old graph/DTO is installed into this World.
                    if(allowEmptyPrimary&&!File.Exists(path))
                    {if(!sourceCurrent()||!PrimaryCurrent()||!empty())return false;load();}
                    else using(var pin=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                    {if(!sourceCurrent()||!PrimaryCurrent()||!empty())return false;load();}
                }
                if(!sourceCurrent()||!PrimaryCurrent()||!incomingCurrent(incoming))return false;
                adopted=incoming;return true;
            }
            catch{return false;}
        }
    }
    private readonly Store[] stores;
    private RebirthNpcSaveQuarantineSavedAdoption(Store[] stores){this.stores=stores;}
    internal bool IsOriginalCurrent {get{foreach(var store in stores)if(!store.IsOriginalCurrent)return false;return true;}}
    internal static bool TryCapture(RebirthNpcSaveQuarantineScope scope,out RebirthNpcSaveQuarantineSavedAdoption receipt)
    {
        receipt=null;if(scope==null||!ThreadManager.IsMainThread())return false;
        var captured=new System.Collections.Generic.List<Store>();bool retained=false;
        try
        {
            captured.Add(RebirthNpcInventoryPersistenceStore.CaptureSavedAdoption(scope));
            captured.Add(RebirthNpcEquipmentPersistenceStore.CaptureSavedAdoption(scope));
            captured.Add(RebirthNpcSocialPersistenceStore.CaptureSavedAdoption(scope));
            captured.Add(RebirthNpcProgressionPersistenceStore.CaptureSavedAdoption(scope));
            captured.Add(RebirthNpcEconomyPersistenceStore.CaptureSavedAdoption(scope));
            captured.Add(RebirthNpcCombatPersistenceStore.CaptureSavedAdoption(scope));
            captured.Add(RebirthNpcSettlementPersistenceStore.CaptureSavedAdoption(scope));
            captured.Add(RebirthNpcLifecycleGameplayService.CaptureSavedAdoption(scope));
            captured.Add(RebirthNpcIdentityAuditLedger.CaptureSavedAdoption(scope));
            captured.Add(RebirthNpcAggregatePersistenceStore.CaptureSavedAdoption(scope));
            foreach(var store in captured)if(store==null||!store.IsOriginalCurrent)return false;
            receipt=new RebirthNpcSaveQuarantineSavedAdoption(captured.ToArray());retained=true;return true;
        }
        catch{return false;}
        finally{if(!retained)foreach(var store in captured)store?.Dispose();}
    }
    internal bool TryAdopt(RebirthNpcTradeLocalOwnerLoad.NativeReentry source)
    {
        // Full preflight precedes the first normal loader. A later refusal keeps
        // partial incoming staging held by the lifecycle caller; no rollback of
        // saved custody or admission is inferred from this subset.
        foreach(var store in stores)if(!store.CanAdopt(source))return false;
        foreach(var store in stores)if(!store.TryAdopt(source))return false;
        return true;
    }
    public void Dispose(){foreach(var store in stores)store.Dispose();}
    internal bool TryAdopt(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
    {
        foreach(var store in stores)if(!store.CanAdopt(source))return false;
        foreach(var store in stores)if(!store.TryAdopt(source))return false;
        return true;
    }
    internal bool TryAdopt(RebirthDogLifecycleService.NativeOwnerReentry source)
    {
        // These captured roles load persistent metadata only. Every final
        // primary/full private image and incoming binding check still applies;
        // no NPC runtime, action, controller or owner outcome is installed.
        foreach(var store in stores)if(!store.CanAdopt(source))return false;
        foreach(var store in stores)if(!store.TryAdopt(source))return false;
        return true;
    }
    internal static Store Capture(string name,RebirthNpcSaveQuarantineScope scope,World world,string path,bool ready,
        Func<bool> originalCurrent,Func<bool> empty,Func<World,bool> incomingCurrent,Func<XmlDocument,bool> validate,Func<XmlDocument> snapshot,Action load,Func<XmlDocument,string> canonicalImage=null)
    {
        if(!ready||world==null||string.IsNullOrEmpty(path)||!scope.MatchesBinding(world,path,true))return null;
        string image=snapshot().DocumentElement.OuterXml;
        if(image.Length>16777216||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*image.Length+1048576),out var retention))return null;
        // ready and originalCurrent are the typed owner's clean retirement
        // predicates. They must positively prove its private no-data state
        // when its backing is not initialized; absence alone is insufficient.
        bool emptyPrimary=!File.Exists(path)&&RebirthNpcPersistenceFile.CanInitializeEmpty(path);
        var receipt=new Store(name,scope,path,image,originalCurrent,empty,incomingCurrent,validate,load,retention,emptyPrimary,canonicalImage);
        if(receipt.IsOriginalCurrent)return receipt;
        receipt.Dispose();return null;
    }
    internal static Store CaptureUninitialized(string name,RebirthNpcSaveQuarantineScope scope,string path,
        Func<bool> originalEmpty,Func<bool> detachedEmpty,Func<World,bool> incomingCurrent,
        Func<XmlDocument,bool> validate,Func<XmlDocument> snapshot,Action load,Func<XmlDocument,string> canonicalImage=null)
    {
        if(scope==null||!ThreadManager.IsMainThread()||!scope.IsUnchangedOriginal()||!originalEmpty()||
            !scope.MatchesBinding(scope.World,path,true))return null;
        IDisposable retention=null;
        try
        {
            bool absent=!File.Exists(path);XmlDocument expected;
            if(absent)
            {
                if(!RebirthNpcPersistenceFile.CanInitializeEmpty(path))return null;
                expected=snapshot();if(!validate(expected))return null;
            }
            else
            {
                using(var pin=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    if(pin.Length<1||pin.Length>33554432)return null;
                    expected=new XmlDocument{XmlResolver=null};
                    using(var reader=XmlReader.Create(pin,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=33554432}))expected.Load(reader);
                }
                if(!validate(expected))return null;
            }
            string image=canonicalImage==null?expected.DocumentElement.OuterXml:canonicalImage(expected);
            if(image.Length>16777216||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*image.Length+1048576),out retention))return null;
            bool Incoming(World world)=>incomingCurrent(world)&&snapshot().DocumentElement.OuterXml==image;
            var receipt=new Store(name,scope,path,image,originalEmpty,detachedEmpty,Incoming,validate,load,retention,absent,canonicalImage);
            retention=null;
            if(receipt.IsOriginalCurrent)return receipt;receipt.Dispose();return null;
        }
        catch{return null;}
        finally{retention?.Dispose();}
    }
}
