using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

// PREPARED, NOT RUN. Actual detached codec/candidate loader, temporary files only.
internal static class RebirthMetabolismRecoveryFixture
{
    internal static void Verify()
    {
        var row=(XElement)typeof(RebirthMetabolismStateRepository).GetMethod("SerializePlayer",BindingFlags.Static|BindingFlags.NonPublic)
            .Invoke(null,new object[]{"fixture-owner",new RebirthMetabolismState()});
        var valid=new XDocument(new XElement("rebirthMetabolismPlayers",new XAttribute("version",RebirthMetabolismState.CurrentVersion),row));
        Require(RebirthMetabolismStateRepository.DecodeStore(valid).Count==1);
        Reject(new XDocument(new XElement("unrelated",new XAttribute("version",9))));
        var future=new XDocument(valid);future.Root.SetAttributeValue("version",999);Reject(future);
        var duplicate=new XDocument(valid);duplicate.Root.Add(new XElement(row));Reject(duplicate);
        var unknown=new XDocument(valid);unknown.Root.Add(new XElement("other"));Reject(unknown);
        var invalid=new XDocument(valid);invalid.Root.Element("player").SetAttributeValue("autoSip","invalid");Reject(invalid);
        var incomplete=new XDocument(valid);incomplete.Root.Element("player").Attribute("energy").Remove();Reject(incomplete);
        var legacy=new XDocument(new XElement("rebirthMetabolismPlayers",new XAttribute("version",1),new XElement("player",new XAttribute("id","legacy"))));
        var migrated=RebirthMetabolismStateRepository.DecodeStore(legacy)["legacy"];
        Require(migrated.Version==RebirthMetabolismState.CurrentVersion&&migrated.Dirty);
        string root=Path.Combine(Path.GetTempPath(),"rebirth-metabolism-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        string path=Path.Combine(root,"players.xml");
        try
        {
            bool backup;Require(RebirthMetabolismStateRepository.ReadStoreCandidates(path,out backup).Count==0&&!backup);
            File.WriteAllText(path+".tmp","interrupted");Refuses(()=>RebirthMetabolismStateRepository.ReadStoreCandidates(path,out _));File.Delete(path+".tmp");
            valid.Save(path+".bak");Require(RebirthMetabolismStateRepository.ReadStoreCandidates(path,out backup).Count==1&&backup);
            File.WriteAllText(path,"corrupt");File.WriteAllText(path+".tmp","interrupted");
            var recovered=RebirthMetabolismStateRepository.ReadStoreCandidates(path,out backup);Require(backup&&recovered.Values.All(state=>state.Dirty));
            typeof(RebirthMetabolismStateRepository).GetMethod("PreserveLoadEvidence",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{path,true});
            Require(!File.Exists(path)&&File.Exists(path+".bak")&&!File.Exists(path+".tmp"));
            Require(Directory.GetFiles(root,"players.xml.corrupt.*").Length==1&&Directory.GetFiles(root,"players.xml.candidate.*").Length==1);
            // Interrupted portable publication still leaves the original backup
            // available; writing a new primary must not destroy it.
            Require(RebirthAtomicXmlFile.TryWrite(path,valid,out _));Require(RebirthMetabolismStateRepository.ReadStoreCandidates(path,out backup).Count==1&&!backup);
            File.WriteAllText(path,"corrupt");File.WriteAllText(path+".bak","corrupt");Refuses(()=>RebirthMetabolismStateRepository.ReadStoreCandidates(path,out _));
            File.Delete(path);File.Delete(path+".bak");Refuses(()=>RebirthMetabolismStateRepository.ReadStoreCandidates(path,out _));
        }
        finally{Directory.Delete(root,true);}
    }
    private static void Reject(XDocument document)=>Refuses(()=>RebirthMetabolismStateRepository.DecodeStore(document));
    private static void Refuses(Action action){bool refused=false;try{action();}catch(Exception error)when(error is IOException||error is InvalidDataException||error is System.Xml.XmlException){refused=true;}Require(refused);}
    private static void Require(bool value){if(!value)throw new Exception("Metabolism recovery invariant failed.");}
}
