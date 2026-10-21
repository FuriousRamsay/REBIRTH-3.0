using System;
using System.IO;
using System.Xml;

// Saved metadata adoption qualified by the actual incoming NPC native read and
// activation. This cannot install a runtime, actor, spawn, marker or native action.
internal sealed class RebirthNpcSaveQuarantineNativeSavedStore:IDisposable
{
    internal readonly string Name;
    private readonly World originalWorld;
    private readonly string directory,path,image;
    private readonly Func<bool> original,empty;
    private readonly Func<World,bool> incoming;
    private readonly Func<RebirthNpcRuntimeNativeAdoption.NativeReentry,bool> native;
    private readonly Func<XmlDocument,bool> validate;
    private readonly Action load;
    private Func<RebirthDogLifecycleService.NativeOwnerReentry,bool> ownerScope;
    private IDisposable retention;private bool disposed;private World adopted;
    private readonly RebirthNpcSaveQuarantineSavedAdoption.Store uninitialized;
    private RebirthNpcSaveQuarantineNativeSavedStore(RebirthNpcSaveQuarantineSavedAdoption.Store store)
    {Name=store.Name;uninitialized=store;}
    private RebirthNpcSaveQuarantineNativeSavedStore(string name,RebirthNpcSaveQuarantineScope scope,string path,string image,
        Func<bool> original,Func<bool> empty,Func<World,bool> incoming,Func<RebirthNpcRuntimeNativeAdoption.NativeReentry,bool> native,
        Func<XmlDocument,bool> validate,Action load,IDisposable retention)
    {Name=name;originalWorld=scope.World;directory=scope.Directory;this.path=path;this.image=image;this.original=original;
        this.empty=empty;this.incoming=incoming;this.native=native;this.validate=validate;this.load=load;this.retention=retention;}
    private bool PrimaryCurrent()
    {
        try
        {
            if(RebirthNpcPersistenceFile.IsRecoveryFenced(path)||File.Exists(path+".tmp"))return false;
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(stream.Length<1||stream.Length>33554432)return false;
                var document=new XmlDocument{XmlResolver=null};
                using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=33554432}))document.Load(reader);
                return validate(document)&&document.DocumentElement.OuterXml==image;
            }
        }
        catch{return false;}
    }
    internal bool IsOriginalCurrent {get{try{return !disposed&&(uninitialized!=null?uninitialized.IsOriginalCurrent:ThreadManager.IsMainThread()&&original()&&PrimaryCurrent());}catch{return false;}}}
    private bool ScopeCurrent(RebirthNpcRuntimeNativeAdoption.NativeReentry source)=>!disposed&&ThreadManager.IsMainThread()&&source?.Current()==true&&
        !ReferenceEquals(source.World,originalWorld)&&ReferenceEquals(source.World,GameManager.Instance?.World)&&
        RebirthNpcSaveQuarantineScope.SameDirectory(directory,source.Directory)&&RebirthNpcSaveQuarantineScope.SameDirectory(directory,GameIO.GetSaveGameDir())&&native(source);
    internal bool CanAdopt(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
    {try{return ScopeCurrent(source)&&PrimaryCurrent()&&(adopted==null?(empty()||incoming(source.World)):ReferenceEquals(adopted,source.World)&&incoming(source.World));}catch{return false;}}
    internal bool TryAdopt(RebirthNpcRuntimeNativeAdoption.NativeReentry source)
    {
        try
        {
            if(!CanAdopt(source))return false;
            if(adopted==null&&!incoming(source.World))
            {
                using(var pin=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                {if(!ScopeCurrent(source)||!PrimaryCurrent()||!empty())return false;load();}
            }
            if(!ScopeCurrent(source)||!PrimaryCurrent()||!incoming(source.World))return false;
            adopted=source.World;return true;
        }
        catch{return false;}
    }
    public void Dispose(){if(disposed)return;disposed=true;uninitialized?.Dispose();retention?.Dispose();retention=null;}
    internal bool CanAdoptOwnerScope(RebirthDogLifecycleService.NativeOwnerReentry source)
    {
        if(uninitialized!=null)return !disposed&&uninitialized.CanAdopt(source);
        try{return !disposed&&ThreadManager.IsMainThread()&&source?.Current()==true&&ownerScope?.Invoke(source)==true&&
            !ReferenceEquals(source.World,originalWorld)&&RebirthNpcSaveQuarantineScope.SameDirectory(directory,source.Directory)&&
            PrimaryCurrent()&&(adopted==null?(empty()||incoming(source.World)):ReferenceEquals(adopted,source.World)&&incoming(source.World));}
        catch{return false;}
    }
    internal bool TryAdoptOwnerScope(RebirthDogLifecycleService.NativeOwnerReentry source)
    {
        if(uninitialized!=null)return !disposed&&uninitialized.TryAdopt(source);
        try
        {
            if(!CanAdoptOwnerScope(source))return false;
            if(adopted==null&&!incoming(source.World))
                using(var pin=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                {if(!CanAdoptOwnerScope(source)||!empty())return false;load();}
            if(!source.Current()||ownerScope?.Invoke(source)!=true||!PrimaryCurrent()||!incoming(source.World))return false;
            adopted=source.World;return true;
        }
        catch{return false;}
    }
    internal static RebirthNpcSaveQuarantineNativeSavedStore CaptureUninitializedOwner(string name,RebirthNpcSaveQuarantineScope scope,string path,
        Func<bool> originalEmpty,Func<bool> detachedEmpty,Func<World,bool> incoming,Func<XmlDocument,bool> validate,Func<XmlDocument> snapshot,Action load)
    {
        var store=RebirthNpcSaveQuarantineSavedAdoption.CaptureUninitialized(name,scope,path,originalEmpty,detachedEmpty,incoming,validate,snapshot,load);
        return store==null?null:new RebirthNpcSaveQuarantineNativeSavedStore(store);
    }
    internal static RebirthNpcSaveQuarantineNativeSavedStore Capture(string name,RebirthNpcSaveQuarantineScope scope,World world,string path,bool ready,
        Func<bool> original,Func<bool> empty,Func<World,bool> incoming,Func<RebirthNpcRuntimeNativeAdoption.NativeReentry,bool> native,
        Func<XmlDocument,bool> validate,XmlDocument snapshot,Action load,
        Func<RebirthDogLifecycleService.NativeOwnerReentry,bool> ownerScope=null)
    {
        if(!ready||scope==null||world==null||!ThreadManager.IsMainThread()||!scope.MatchesBinding(world,path,true)||snapshot?.DocumentElement==null)return null;
        string image=snapshot.DocumentElement.OuterXml;
        if(image.Length>16777216||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*image.Length+1048576),out var retention))return null;
        var receipt=new RebirthNpcSaveQuarantineNativeSavedStore(name,scope,path,image,original,empty,incoming,native,validate,load,retention);
        receipt.ownerScope=ownerScope;
        if(receipt.IsOriginalCurrent)return receipt;receipt.Dispose();return null;
    }
}
