using System;
using System.IO;
using System.Security.Cryptography;
using System.Xml;

// Provenance for retiring an already loaded typed store, never admission to a
// different world. Unknown recovery heads and unobserved mutations stay held.
internal sealed class RebirthNpcOriginalSaveBinding
{
    private readonly World world;
    private readonly string path,directory,fingerprint,generationImage;
    private readonly ulong generation;
    private object ownedEpoch;
    private string head;
    private volatile bool uncertain;
    internal void InvalidateMutation()=>uncertain=true;
    private RebirthNpcOriginalSaveBinding(World world,string path,object epoch)
    {
        this.world=world;this.path=Path.GetFullPath(path);directory=Path.GetDirectoryName(this.path);
        fingerprint=RebirthNpcSaveScope.ComputeFingerprint(directory);
        generation=RebirthNpcRecoveryGeneration.RequireCurrent();
        generationImage=GenerationImage(directory);
        head=Image(this.path);ownedEpoch=epoch;
    }
    internal static RebirthNpcOriginalSaveBinding Capture(World world,string path,object epoch)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(world,GameManager.Instance?.World)||epoch==null||
                !string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)),Path.GetFullPath(GameIO.GetSaveGameDir()).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)||
                RebirthNpcPersistenceFile.IsRecoveryFenced(path)||File.Exists(path+".recovery-publications.xml"))return null;
            return new RebirthNpcOriginalSaveBinding(world,path,epoch);
        }catch{return null;}
    }
    internal void ObserveMutation(object epoch)
    {
        if(uncertain)return;
        if(!ThreadManager.IsMainThread()||!ReferenceEquals(world,GameManager.Instance?.World)||
            !string.Equals(directory,GameIO.GetSaveGameDir()?.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)||epoch==null)
        {uncertain=true;return;}
        ownedEpoch=epoch;
    }
    private bool OwnsScope(object epoch)
    {
        return !uncertain&&ReferenceEquals(epoch,ownedEpoch)&&ThreadManager.IsMainThread()&&GameManager.Instance?.World?.IsRemote()==false&&SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer==true&&
            fingerprint==RebirthNpcSaveScope.ComputeFingerprint(directory)&&
            generationImage==GenerationImage(directory)&&
            !RebirthNpcPersistenceFile.IsRecoveryFenced(path)&&!File.Exists(path+".recovery-publications.xml");
    }
    private bool Owns(object epoch)=>OwnsScope(epoch)&&head==Image(path);
    internal bool MatchesRetirement(object epoch)
    {try{return ThreadManager.IsMainThread()&&Owns(epoch);}catch{return false;}}
    private static string GenerationImage(string directory)
    {
        string file=Path.Combine(directory,"RebirthNpcRecoveryGeneration.xml");
        if(!File.Exists(file)&&Directory.Exists(directory))
            foreach(var ignored in Directory.EnumerateFiles(directory,"RebirthNpcRecoveryGeneration.xml.*"))
                throw new InvalidDataException("Original recovery generation has unresolved publication evidence.");
        return Image(file);
    }
    internal bool TryPublish(Func<XmlDocument> capture,Func<XmlDocument,bool> validate,Func<object> epoch,out string error)
    {
        error=string.Empty;
        try
        {
            if(capture==null||validate==null||epoch==null||!OwnsScope(epoch()))return false;
            // A prior atomic commit may have succeeded before its readback
            // failed. Positive full primary equality permits observation only;
            // an unknown/different head never permits another write.
            if(!Owns(epoch()))
            {
                var detached=capture();if(detached==null||!validate(detached))return false;
                var published=new XmlDocument{XmlResolver=null};
                using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=67108864}))published.Load(reader);
                string observed=Image(path);
                if(!validate(published)||published.DocumentElement.OuterXml!=detached.DocumentElement.OuterXml||!OwnsScope(epoch())||observed!=Image(path))return false;
                head=observed;return true;
            }
            using(RebirthNpcPersistenceFile.BeginOriginalWrite(path,()=>Owns(epoch())))
            {
                // Writer lease precedes the typed snapshot. No competing store
                // publication can change its head while the image is captured.
                var detached=capture();
                if(detached==null||!validate(detached)||!Owns(epoch()))return false;
                RebirthNpcPersistenceFile.SaveAtomic(path,detached);
                var read=new XmlDocument{XmlResolver=null};
                using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=67108864}))read.Load(reader);
                if(!validate(read)||read.DocumentElement.OuterXml!=detached.DocumentElement.OuterXml||!OwnsScope(epoch()))
                    throw new InvalidDataException("Original-save retirement readback changed.");
                head=Image(path);
                return true;
            }
        }catch(Exception exception){error=exception.GetType().Name+": "+exception.Message;return false;}
    }
    private static string Image(string file)
    {
        if(!File.Exists(file))
        {
            if(File.Exists(file+".bak"))throw new InvalidDataException("Original primary missing with backup.");
            string dir=Path.GetDirectoryName(file);
            if(Directory.Exists(dir))foreach(var ignored in Directory.EnumerateFiles(dir,Path.GetFileName(file)+".corrupt.*"))throw new InvalidDataException("Original primary quarantined.");
            return "missing";
        }
        using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(stream.Length<1||stream.Length>67108864)throw new InvalidDataException("Original primary size bound.");
            using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-",string.Empty);
        }
    }
}
