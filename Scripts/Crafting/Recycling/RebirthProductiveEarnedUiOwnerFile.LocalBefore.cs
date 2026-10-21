using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiOwnerFile
{
    // Issued only by the existing actual FromPlayer postfix. A live projection
    // alone does not certify a file or authorize Check.
    private static bool ObserveLocalBeforePlayer(PlayerDataFile data,EntityPlayer player)
    {
        try{lock(gate)
        {
            var scope=localOwners.Values.SingleOrDefault(value=>!value.Closed&&!value.Attempted&&ReferenceEquals(value.Actor,player));
            if(scope==null)return false;
            files.Remove(data);
            if(!scope.CurrentWindow()||scope.Unknown||data.id!=player.entityId||data.deaths!=scope.Deaths||
                !MatchesDeferredOwnerFrame(data,scope.BeforeFrame)||!scope.BodyCurrent(false))return true;
            long measured=MeasureNative(writer=>{writer.Write('t');writer.Write('t');writer.Write('p');writer.Write((byte)0);
                writer.Write((byte)PlayerDataFile.cFileVersion);data.Write(writer,StreamModeWrite.Persistency);});
            if(!scope.ReserveNative(measured))return true;
            var image=NativeImage(data,checked((int)measured));if(image.Length!=measured)return true;
            files.Add(data,new NativeFile{LocalBefore=scope,NativeLength=image.Length,NativeHash=Hash(image)});
            return true;
        }}catch{return true;}
    }
    private static bool TryLocalBeforeSave(PlayerDataFile data,string directory,string name,bool run,out NativeFile result)
    {
        result=null;
        try{lock(gate)
        {
            if(!files.TryGetValue(data,out var source)||source.LocalBefore==null)return false;
            var scope=source.LocalBefore;
            if(!run||scope.Closed||scope.Unknown||scope.Attempted||scope.BeforeSaved||name!=scope.Name||
                Path.GetFullPath(directory)!=scope.Root)return true;
            string path=Path.GetFullPath(Path.Combine(directory,name+"."+PlayerDataFile.EXT));
            if(Path.GetDirectoryName(path)!=scope.Root||!Safe(path))return true;
            // No World/UI queries on the native Save worker. The actual
            // captured object/image and original immutable scope are retained.
            var lease=scope.RetainWorker();if(lease==null)return true;
            try
            {
                if(scope.Consumed)return true;
                var image=NativeImage(data,source.NativeLength);
                if(image.Length!=source.NativeLength||Hash(image)!=source.NativeHash)return true;
                result=new NativeFile{LocalBefore=scope,NativeLength=image.Length,Image=image,Path=path,
                    NativeHash=source.NativeHash,DeferredWorkerRetention=lease};lease=null;return true;
            }
            finally{lease?.Dispose();}
        }}catch{return true;}
    }
    private static void ObserveLocalBeforeSave(NativeFile native)
    {
        lock(gate)
        {
            var scope=native.LocalBefore;
            if(scope==null||scope.Closed||scope.Unknown||scope.Attempted||scope.BeforeSaved||
                !ReadFinal(native.Path,native.Image))return;
            scope.BeforeFile=native.Image;scope.BeforeFilePath=native.Path;scope.BeforeSaved=true;
        }
    }
    internal static void PumpLocalOwnerBefore()
    {
        if(!ThreadManager.IsMainThread())return;
        foreach(var scope in localOwners.Values.ToArray())
        {
            try
            {
                if(scope.Closed||scope.Unknown||scope.Attempted||!scope.CurrentWindow()||!scope.BodyCurrent(false))continue;
                if(scope.BeforePublished){InvokePreparedLocalOwner(scope);continue;}
                // A host-local receipt imported after physical upload must not
                // destroy its only initial publication by consuming first.
                if(scope.Members.Any(value=>value.HostLocalOriginal!=null&&!value.HostLocalOriginal.Source.Original.TrySealStation()))continue;
                foreach(var member in scope.Members.Where(value=>value.HostLocalOriginal!=null))member.Delivered=true;
                if(!scope.BeforeSaved){GameManager.Instance.SaveLocalPlayerData();continue;}
                if(scope.BeforeFile==null||!ReadFinal(scope.BeforeFilePath,scope.BeforeFile))continue;
                long bound=RebirthProductiveEarnedUiDeferredTransport.MeasurePublicationElement(scope.BeforeFrame)+
                    65536L+4*((scope.NativeBefore.Length+2L)/3);
                using(var workspace=scope.EnterPublication(bound))
                {
                if(workspace==null)continue;
                var evidence=new XElement("localNativeOwnerBeforeSaved",new XAttribute("nativePath",scope.BeforeFilePath),
                    new XAttribute("nativeDigest",Hash(scope.BeforeFile)),new XAttribute("window",scope.WindowSession),
                    new XElement("ownerBefore",new XElement(scope.BeforeFrame)),
                    new XElement("nativeBefore",new XAttribute("offset",scope.BeforeOffset),Convert.ToBase64String(scope.NativeBefore)),
                    new XElement("members",scope.Members.Select(value=>new XElement("member",new XAttribute("operation",value.Operation.ToString("N"))))));
                if(SaveLocalStage(scope,"beforeSaved",evidence))scope.BeforePublished=true;
                }
            }
            catch{/* Retain the exact original; no file absence or failed save grants Check. */}
        }
    }
}
