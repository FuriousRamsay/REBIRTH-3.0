using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthPlacedOriginalDeathPartition
{
    private static string OwedRejectionPath(OwedChild c)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","owed-rejections-server",c.Parent.ToString("N"),c.Sequence.ToString("D10",System.Globalization.CultureInfo.InvariantCulture)+".xml");
    private static string OwedRejectionBody(OwedChild c)
    {var x=new XElement("placedOwedNeverPermitted",new XAttribute("version",1),new XAttribute("sourceHead",c.Head),new XElement("proposal",ChildBody(c)));x.SetAttributeValue("checksum",Hash(x.ToString(SaveOptions.DisableFormatting)));return x.ToString(SaveOptions.DisableFormatting);}
    private static OwedChild DecodeOwedRejection(string image)
    {var x=OwedXml(image);if(x.Name!="placedOwedNeverPermitted"||(int?)x.Attribute("version")!=1)throw new InvalidDataException();var c=DecodeChild((string)x.Element("proposal"));if(c.Phase!=0||c.Applied||c.Proof.Length!=0||OwedRejectionBody(c)!=image)throw new InvalidDataException();return c;}
    private static bool OwedRejectionExists(OwedChild c)
    {string path=OwedRejectionPath(c),folder=Path.GetDirectoryName(path);return File.Exists(path)||File.Exists(path+".bak")||Directory.Exists(folder)&&Directory.EnumerateFiles(folder,Path.GetFileName(path)+".*.tmp").Any();}
    private static bool ReadOwedRejection(OwedChild proposed,out OwedChild rejected)
    {
        rejected=null;try
        {
            string path=OwedRejectionPath(proposed),folder=Path.GetDirectoryName(path);if(File.Exists(path)||File.Exists(path+".bak")){if(!File.Exists(path)||new FileInfo(path).Length>Maximum)return false;rejected=DecodeOwedRejection(File.ReadAllText(path,new UTF8Encoding(false,true)));return SameChild(rejected,proposed);}
            if(!Directory.Exists(folder))return false;var files=Directory.EnumerateFiles(folder,Path.GetFileName(path)+".*.tmp").ToArray();if(files.Length!=1||new FileInfo(files[0]).Length>Maximum)return false;var c=DecodeOwedRejection(File.ReadAllText(files[0],new UTF8Encoding(false,true)));if(!SameChild(c,proposed)||!RebirthDurableFileCommit.TryPublish(files[0],path,out _))return false;rejected=c;return File.ReadAllText(path,new UTF8Encoding(false,true))==OwedRejectionBody(c);
        }catch{return false;}
    }
    private static bool RecoverOwedSlotRejection(OwedChild key,out OwedChild terminal)
    {
        terminal=null;try{string path=OwedRejectionPath(key),folder=Path.GetDirectoryName(path);string candidate=path;if(!File.Exists(candidate)){if(!Directory.Exists(folder))return false;var files=Directory.EnumerateFiles(folder,Path.GetFileName(path)+".*.tmp").ToArray();if(files.Length!=1)return false;candidate=files[0];}if(new FileInfo(candidate).Length>Maximum)return false;var c=DecodeOwedRejection(File.ReadAllText(candidate,new UTF8Encoding(false,true)));return c.Parent==key.Parent&&c.Sequence==key.Sequence&&PublishOwedRejection(c,out terminal);}catch{return false;}
    }
    private static bool SaveOwedRejection(OwedChild c)
    {
        try
        {
            if(OwedRejectionExists(c))return ReadOwedRejection(c,out _);string path=OwedRejectionPath(c);Directory.CreateDirectory(Path.GetDirectoryName(path));string image=OwedRejectionBody(c);DecodeOwedRejection(image);string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}return RebirthDurableFileCommit.TryPublish(temp,path,out _)&&ReadOwedRejection(c,out _);
        }catch{return false;}
    }
    private static bool PublishOwedRejection(OwedChild original,out OwedChild terminal)
    {
        terminal=null;try
        {
            if(!ReadOwedRejection(original,out var rejected))return false;var next=DecodeChild(ChildBody(rejected));next.Phase=4;next.Proof=Hash("positive-never-permitted|"+OwedRejectionBody(rejected));string path=ChildPath(next,true);
            // A positive existing permission/attempt is never converted into a
            // rejection. Unknown prepared child files are retained as well.
            if(File.Exists(path)||File.Exists(path+".bak")){if(!ReadChild(path,out var old)||!SameChild(old,next)||old.Phase!=4||ChildBody(old)!=ChildBody(next))return false;}
            if(!SaveChild(next,true)||!AdvanceOwedHead(next)&&!OwedTerminalCovered(next))return false;terminal=next;return true;
        }catch{return false;}
    }
    private static void RejectUnpermittedOwed(EntityPlayer actor,string image)
    {
        try
        {
            var x=OwedXml(image);if(x.Name!="placedOwedReject")return;var c=DecodeChild((string)x.Element("proposal"));if(actor?.world?.IsRemote()!=false||c.Phase!=0||!OwedIdentity(actor,c)||actor.Died<=c.Deaths)return;
            if(OwedRejectionExists(c)){if(PublishOwedRejection(c,out var retained))ReplyOwed((World)actor.world,actor,ChildBody(retained));return;}
            if(!OwedSource(c,out _,out var h,out var rows)||c.Sequence!=h.Sequence+1||c.Head!=Hash(HeadBody(h))||c.DeliveredBefore!=h.Delivered[c.Row])return;int first=-1;for(int i=0;i<rows.Length;i++)if(h.Delivered[i]<rows[i].count){first=i;break;}if(first!=c.Row)return;
            var owed=rows[first].Clone();owed.count-=h.Delivered[first];if(!TryPlanOwedFragment(RebirthPlacedOriginal.ReadActor(c.Before),owed,out var after,out bool belt,out int slot,out int quantity)||belt!=c.Belt||slot!=c.Slot||quantity!=c.Quantity||!after.Matches(RebirthPlacedOriginal.ReadActor(c.After)))return;
            string path=ChildPath(c,true),folder=Path.GetDirectoryName(path);if(File.Exists(path)||File.Exists(path+".bak")||Directory.Exists(folder)&&Directory.EnumerateFiles(folder,Path.GetFileName(path)+".*.tmp").Any())return;
            // Linearized host declaration BEFORE any permission for this exact
            // proposal/source head. Admission consults this slot tombstone even
            // if terminal publication or ACK subsequently fails.
            if(SaveOwedRejection(c)&&PublishOwedRejection(c,out var terminal))ReplyOwed((World)actor.world,actor,ChildBody(terminal));
        }catch{/* No absent-file refund or unproved unused result. */}
    }
    private static void RequestOwedRejection(EntityPlayerLocal player,OwedChild c)
    {if(c.Phase==0&&!c.Applied&&OwedIdentity(player,c)&&player.Died>c.Deaths)SendOwed(player,new XElement("placedOwedReject",new XElement("proposal",ChildBody(c))).ToString(SaveOptions.DisableFormatting));}
}