using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

// Permanent retirement with only bounded, genuinely pending original lessons retained.
public sealed class RebirthTheorySoloTeachingRetirement
{
    public const int MaximumOpen=256;
    public long Through {get;private set;}
    private readonly SortedSet<long> open=new SortedSet<long>();
    public RebirthTheorySoloTeachingRetirement Clone(){var copy=new RebirthTheorySoloTeachingRetirement{Through=Through};foreach(long n in open)copy.open.Add(n);return copy;}
    public bool TryAcknowledge(long originalOrdinal,IEnumerable<long> originalPending,out bool already)
    {
        already=false;if(originalOrdinal<1||originalPending==null)return false;
        if(originalOrdinal<=Through){if(!open.Remove(originalOrdinal))already=true;return true;}
        var retain=new SortedSet<long>(open);
        foreach(long n in originalPending){if(n<1)return false;if(n>Through&&n<originalOrdinal)retain.Add(n);if(retain.Count>MaximumOpen)return false;}
        open.Clear();foreach(long n in retain)open.Add(n);Through=originalOrdinal;return true;
    }
    public bool IsAcknowledged(long originalOrdinal)=>originalOrdinal>0&&originalOrdinal<=Through&&!open.Contains(originalOrdinal);
    public XElement Write()=>new XElement("teachingOriginal",new XAttribute("through",Through),open.Select(n=>new XElement("open",new XAttribute("ordinal",n))));
    public static bool TryRead(XElement node,out RebirthTheorySoloTeachingRetirement retirement)
    {
        retirement=null;try
        {
            if(node==null||node.Name!="teachingOriginal"||node.Attributes().Count()!=1)return false;
            var next=new RebirthTheorySoloTeachingRetirement{Through=(long)node.Attribute("through")};if(next.Through<0)return false;
            foreach(var row in node.Elements()){long n=(long)row.Attribute("ordinal");if(row.Name!="open"||row.HasElements||row.Attributes().Count()!=1||n<1||n>=next.Through||!next.open.Add(n)||next.open.Count>MaximumOpen)return false;}
            if(!XNode.DeepEquals(next.Write(),node))return false;retirement=next;return true;
        }
        catch{return false;}
    }
}