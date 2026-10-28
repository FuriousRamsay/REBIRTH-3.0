using System;
using System.IO;
using System.Reflection;
using System.Xml.Linq;

// PREPARED, NOT RUN. Canonical detached observation codec only. This fixture
// cannot create an accepted spawn, native GUID marker or Saved region witness.
internal static class mpA_logistics_accepted_body_codec_fixture
{
    internal static void Verify(Assembly production,byte[] actualNativeBag)
    {
        var owner=production.GetType("RebirthLogisticsAcceptedBodyJournal",true);
        var recordType=owner.GetNestedType("Record",BindingFlags.NonPublic);
        object record=Activator.CreateInstance(recordType,true);
        void Set(string field,object value)=>recordType.GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(record,value);
        Set("Operation",Guid.NewGuid());Set("Incarnation",Guid.NewGuid());Set("Owner",new string('a',64));Set("World",Guid.NewGuid().ToString("N"));Set("Creation",Guid.NewGuid().ToString("N"));Set("Binding",new string('b',64));
        Set("Deaths",2);Set("OwnerEntity",9);Set("Entity",12);Set("Class",7);Set("X",0);Set("Z",0);Set("PacketBag",new string('c',64));Set("PacketEntity",new string('d',64));Set("PacketPosition",new UnityEngine.Vector3(2,3,4));Set("Bag",actualNativeBag);
        var write=owner.GetMethod("Write",BindingFlags.Static|BindingFlags.NonPublic);var read=owner.GetMethod("ReadFile",BindingFlags.Static|BindingFlags.NonPublic);
        XElement Encode()=>(XElement)write.Invoke(null,new[]{record});
        bool Refused(Action action)
        {try{action();return false;}catch(TargetInvocationException error){return error.InnerException is InvalidDataException;}}
        XElement original=Encode();if((int)original.Attribute("state")!=0||(string)original.Attribute("proof")!=string.Empty)throw new Exception("Prepared observation manufactured publication proof.");
        Set("Proof",new string('e',64));if(!Refused(()=>Encode()))throw new Exception("State0 accepted a publication proof.");Set("Proof",null);
        Set("State",1);Set("Proof",new string('e',64));if(!Refused(()=>Encode()))throw new Exception("Unbound State1 proof accepted.");Set("State",0);Set("Proof",null);
        string path=Path.Combine(Path.GetTempPath(),"mpA_accepted_"+Guid.NewGuid().ToString("N")+".xml");
        try
        {
            original.Save(path,SaveOptions.DisableFormatting);var decoded=read.Invoke(null,new object[]{path});if(decoded==null)throw new Exception("Actual native bag observation codec refused.");
            var extra=new XElement(original);extra.Add(new XAttribute("unknown",1));extra.Save(path,SaveOptions.DisableFormatting);
            if(!Refused(()=>read.Invoke(null,new object[]{path})))throw new Exception("Unknown receipt schema accepted.");
            var invalidated=new XElement(original);invalidated.Add(new XAttribute("invalidated",0));invalidated.Save(path,SaveOptions.DisableFormatting);
            if(!Refused(()=>read.Invoke(null,new object[]{path})))throw new Exception("Noncanonical invalidation marker accepted.");
        }
        finally{if(File.Exists(path))File.Delete(path);}
        // State1 authority requires the production actual native same-snapshot
        // callback and source journal; no synthetic positive fixture is a proof.
    }
}
