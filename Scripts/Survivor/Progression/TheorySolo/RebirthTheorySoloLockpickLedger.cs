using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

// One native player lock can be active at a time; retired generations need no permanent GUID archive.
public sealed class RebirthTheorySoloLockpickLedger
{
    public sealed class Original
    {
        public long Ordinal;public string Lease,Generation,Creation;public int Actor,X,Y,Z,BeforeDamage,AfterDamage;
        public uint Before,After;public float NativeSeconds,Difficulty;public bool EvidenceAttempted;
        public Original Clone()=>(Original)MemberwiseClone();
    }
    public long Issued {get;private set;}
    public Original Active {get;private set;}
    public RebirthTheorySoloLockpickLedger Clone()=>new RebirthTheorySoloLockpickLedger{Issued=Issued,Active=Active?.Clone()};
    public Original Reserve(string lease,string generation,string creation,int actor,int x,int y,int z,uint before,uint after,int beforeDamage,int afterDamage,float seconds,float difficulty)
    {
        var candidate=new Original{Lease=lease,Generation=generation,Creation=creation,Actor=actor,X=x,Y=y,Z=z,Before=before,After=after,BeforeDamage=beforeDamage,AfterDamage=afterDamage,NativeSeconds=seconds,Difficulty=difficulty};
        if(!Valid(candidate))return null;
        if(Active!=null)return Same(Active,candidate)?Active:null;
        if(Issued==long.MaxValue)return null;
        candidate.Ordinal=++Issued;Active=candidate;return candidate;
    }
    public bool TryGet(long ordinal,string lease,string generation,out Original original){original=null;if(Active==null||Active.Ordinal!=ordinal||Active.Lease!=lease||Active.Generation!=generation)return false;original=Active;return true;}
    public bool MarkEvidence(long ordinal,string lease,string generation,out bool already){already=false;if(!TryGet(ordinal,lease,generation,out var original))return false;already=original.EvidenceAttempted;original.EvidenceAttempted=true;return true;}
    public bool Retire(long ordinal,string lease,string generation){if(!TryGet(ordinal,lease,generation,out _))return false;Active=null;return true;}
    public XElement Write(string creation)
    {
        var root=new XElement("lockpickOriginal",new XAttribute("issued",Issued));var a=Active;
        if(a!=null)root.Add(new XElement("task",new XAttribute("ordinal",a.Ordinal),new XAttribute("lease",a.Lease),new XAttribute("generation",a.Generation),new XAttribute("creation",a.Creation),new XAttribute("actor",a.Actor),new XAttribute("x",a.X),new XAttribute("y",a.Y),new XAttribute("z",a.Z),new XAttribute("before",a.Before),new XAttribute("after",a.After),new XAttribute("beforeDamage",a.BeforeDamage),new XAttribute("afterDamage",a.AfterDamage),new XAttribute("nativeSeconds",a.NativeSeconds.ToString("R",CultureInfo.InvariantCulture)),new XAttribute("difficulty",a.Difficulty.ToString("R",CultureInfo.InvariantCulture)),new XAttribute("evidence",a.EvidenceAttempted?"1":"0")));
        return root;
    }
    public static bool TryRead(XElement node,string creation,out RebirthTheorySoloLockpickLedger ledger)
    {
        ledger=null;try
        {
            if(node==null||node.Name!="lockpickOriginal"||node.Attributes().Count()!=1)return false;
            var next=new RebirthTheorySoloLockpickLedger{Issued=(long)node.Attribute("issued")};if(next.Issued<0||node.Elements().Count()>1)return false;
            var row=node.Element("task");if(row!=null)
            {
                var a=new Original{Ordinal=(long)row.Attribute("ordinal"),Lease=(string)row.Attribute("lease"),Generation=(string)row.Attribute("generation"),Creation=(string)row.Attribute("creation"),Actor=(int)row.Attribute("actor"),X=(int)row.Attribute("x"),Y=(int)row.Attribute("y"),Z=(int)row.Attribute("z"),Before=(uint)row.Attribute("before"),After=(uint)row.Attribute("after"),BeforeDamage=(int)row.Attribute("beforeDamage"),AfterDamage=(int)row.Attribute("afterDamage"),NativeSeconds=(float)row.Attribute("nativeSeconds"),Difficulty=(float)row.Attribute("difficulty"),EvidenceAttempted=(string)row.Attribute("evidence")=="1"};
                if(!Valid(a)||a.Ordinal<1||a.Ordinal!=next.Issued||a.Creation!=creation||((string)row.Attribute("evidence")!="0"&&(string)row.Attribute("evidence")!="1"))return false;next.Active=a;
            }
            if(!XNode.DeepEquals(next.Write(creation),node))return false;ledger=next;return true;
        }
        catch{return false;}
    }
    private static bool Id(string s)=>Guid.TryParseExact(s,"N",out var g)&&g!=Guid.Empty;
    private static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
    private static bool Valid(Original a)=>a!=null&&Id(a.Lease)&&Id(a.Generation)&&RebirthTheorySoloState.Text(a.Creation,71)&&a.Actor>=0&&(a.Before&65535)>0&&(a.After&65535)>0&&a.Before!=a.After&&a.BeforeDamage>=0&&a.AfterDamage>=0&&Finite(a.NativeSeconds)&&a.NativeSeconds>0&&Finite(a.Difficulty)&&a.Difficulty>=0&&a.Difficulty<=100;
    private static bool Same(Original a,Original b)=>a.Lease==b.Lease&&a.Generation==b.Generation&&a.Creation==b.Creation&&a.Actor==b.Actor&&a.X==b.X&&a.Y==b.Y&&a.Z==b.Z&&a.Before==b.Before&&a.After==b.After&&a.BeforeDamage==b.BeforeDamage&&a.AfterDamage==b.AfterDamage&&a.NativeSeconds==b.NativeSeconds&&a.Difficulty==b.Difficulty;
}