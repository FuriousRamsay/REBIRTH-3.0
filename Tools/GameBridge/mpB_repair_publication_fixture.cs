// Deferred actual publication helper extraction; AUTHORED_NOT_RUN.
// Native atomic commit and canonical receipt codec are explicit adapters.
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Xml.Linq;
static class GameIO {internal static string Root;internal static string GetSaveGameDir()=>Root;}
static class RebirthRemoteResourceDurableJournal {internal static string Digest(string text){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}}
static class RebirthDurableFileCommit
{
    internal static bool Refuse,UncertainAfterMove;
    internal static bool TryPublish(string candidate,string primary,out string reason)
    {reason=null;if(Refuse)return false;if(File.Exists(primary))File.Replace(candidate,primary,primary+".bak");else File.Move(candidate,primary);return !UncertainAfterMove;}
}
internal static partial class RebirthRepairOriginalCorrection
{
    private const int MaximumImage=262144;
    private sealed class Receipt {internal string Text,File;}
    private static string Encode(Receipt value)=>value.Text;
    private static Receipt Decode(string value){if(XElement.Parse(value).ToString(SaveOptions.DisableFormatting)!=value)throw new InvalidDataException();return new Receipt{Text=value};}
    // Exact receipt identity is exercised separately; these explicit adapters
    // retain only the native publication method's current/proposed text pair.
    private static string PathFor(Receipt receipt,bool server,string directory=null)=>receipt.File;
    private static bool Read(Receipt key,bool server,out Receipt result,string directory=null){result=null;try{result=new Receipt{Text=ReadRepairText(key.File),File=key.File};return true;}catch{return false;}}
    internal static bool Write(string path,string image,string expected=null)=>PublishRepairCandidate(path,image,expected);
    internal static string Read(string path)=>ReadRepairText(path);
    internal static void LoseProcessOwnership()=>ownedCandidates.Clear();
    internal static bool SaveForFixture(string path,string text,string expected=null)=>Save(new Receipt{File=path,Text=text},false,expected);
}
static class RepairPublicationFixture
{
    private static void Need(bool value,string message){if(!value)throw new Exception(message);}
    private static bool Refuses(Action action){try{action();return false;}catch{return true;}}
    public static void Main(string[] args)
    {
        GameIO.Root=args[0];string directory=Path.Combine(GameIO.Root,"rebirth","repair-original","owner");Directory.CreateDirectory(directory);
        string path=Path.Combine(directory,Guid.NewGuid().ToString("N")+".xml"),before="<repair value=\"before\" />",after="<repair value=\"after\" />";
        Need(RebirthRepairOriginalCorrection.Write(path,before),"first original publication refused");
        Need(RebirthRepairOriginalCorrection.Read(path)==before,"exact primary readback differs");
        RebirthDurableFileCommit.Refuse=true;
        Need(!RebirthRepairOriginalCorrection.Write(path,after,before),"refused atomic commit reported published");
        var candidates=Directory.GetFiles(directory,Path.GetFileName(path)+"*.tmp");Need(candidates.Length==1,"exact immutable candidate not retained");
        Need(RebirthRepairOriginalCorrection.Read(path)==before,"failed publication changed original");
        Need(!RebirthRepairOriginalCorrection.Write(path,"<repair value=\"other\" />",before),"changed outcome overwrote owned candidate");
        RebirthDurableFileCommit.Refuse=false;
        Need(RebirthRepairOriginalCorrection.Write(path,after,before),"exact same-process candidate could not resume");
        Need(RebirthRepairOriginalCorrection.Read(path)==after,"resumed primary differs");
        Need(!RebirthRepairOriginalCorrection.Write(path,before,"<wrong />"),"wrong expected primary published");
        string cold=Path.Combine(directory,Guid.NewGuid().ToString("N")+".xml");
        RebirthDurableFileCommit.Refuse=true;Need(!RebirthRepairOriginalCorrection.Write(cold,before),"cold setup should retain candidate");
        string retained=Directory.GetFiles(directory,Path.GetFileName(cold)+"*.tmp").Single();string retainedImage=File.ReadAllText(retained);
        RebirthRepairOriginalCorrection.LoseProcessOwnership();RebirthDurableFileCommit.Refuse=false;
        Need(!RebirthRepairOriginalCorrection.Write(cold,before)&&!File.Exists(cold)&&File.ReadAllText(retained)==retainedImage,"cold unknown candidate was adopted/replaced");
        string invalid=Path.Combine(directory,"invalid.xml");File.WriteAllBytes(invalid,new byte[]{0xff,0xfe,0xff});
        Need(Refuses(()=>RebirthRepairOriginalCorrection.Read(invalid)),"invalid UTF8 accepted");
        File.WriteAllBytes(invalid,new byte[262144*12+1]);Need(Refuses(()=>RebirthRepairOriginalCorrection.Read(invalid)),"overbound primary accepted");
        Need(!RebirthRepairOriginalCorrection.Write(Path.Combine(GameIO.Root,"outside.xml"),before),"outside repair root accepted");
        string uncertain=Path.Combine(directory,Guid.NewGuid().ToString("N")+".xml");
        Need(RebirthRepairOriginalCorrection.SaveForFixture(uncertain,before),"initial actual Save refused");
        RebirthDurableFileCommit.UncertainAfterMove=true;
        Need(!RebirthRepairOriginalCorrection.SaveForFixture(uncertain,after,before)&&RebirthRepairOriginalCorrection.Read(uncertain)==after,"uncertain commit setup failed");
        RebirthDurableFileCommit.UncertainAfterMove=false;
        Need(RebirthRepairOriginalCorrection.SaveForFixture(uncertain,after,before),"positive exact outcome could not finish stale expected-head retry");
        Console.WriteLine("PASS actual repair publication helpers; native commit/receipt codec adapted; no gameplay qualification");
    }
}
