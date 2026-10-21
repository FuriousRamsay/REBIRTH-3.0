using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Immutable description only. Native owner authentication, applying/applied
// receipts and joint metabolism publication remain separate required producers.
internal sealed class RebirthHydrationTransferOriginal
{
    internal readonly Guid Operation,World;
    internal readonly string Owner,Creation,Digest,BagLocks;
    internal readonly int Deaths,Revision,OwnedBelt;
    internal readonly bool Equip;
    private readonly XElement image;
    private RebirthHydrationTransferOriginal(XElement value,Guid operation,Guid world,string creation,
        int deaths,int revision,int owned,bool equip)
    {
        image=new XElement(value);Operation=operation;World=world;Owner=(string)value.Attribute("owner");
        Creation=creation;Deaths=deaths;Revision=revision;OwnedBelt=owned;Equip=equip;
        BagLocks=(string)value.Attribute("locks");
        using(var hash=System.Security.Cryptography.SHA256.Create())
            Digest=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(image.ToString(SaveOptions.DisableFormatting)))).Replace("-","").ToLowerInvariant();
    }
    internal XElement Write()=>new XElement(image);
    internal bool TryGetPlan(out RebirthGearInventoryPlan plan)=>RebirthGearInventoryPlanCodec.TryRead(image.Element("gearPlan"),out plan);
    internal bool Matches(Guid world,string owner,string creation,int deaths)=>world==World&&owner==Owner&&creation==Creation&&deaths==Deaths;

    internal static bool TryCreate(Guid operation,Guid world,string owner,string creation,int deaths,int revision,
        int ownedBelt,bool[] lockedBag,RebirthGearInventoryPlan plan,out RebirthHydrationTransferOriginal original)
    {
        original=null;
        if(plan==null||!plan.IsConserved()||lockedBag!=null&&lockedBag.Length!=plan.BagSlotsBefore)return false;
        string locks=lockedBag==null?new string('0',plan.BagSlotsBefore):new string(lockedBag.Select(value=>value?'1':'0').ToArray());
        var node=new XElement("hydrationTransfer",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),
            new XAttribute("world",world.ToString("N")),new XAttribute("owner",owner??""),new XAttribute("creation",creation??""),
            new XAttribute("deaths",deaths),new XAttribute("revision",revision),new XAttribute("ownedBelt",ownedBelt),
            new XAttribute("locks",locks),new XAttribute("action",plan.GearAfter.Count==1?"equip":"unequip"),
            RebirthGearInventoryPlanCodec.Write(plan));
        return TryRead(node,out original);
    }
    internal static bool TryRead(XElement node,out RebirthHydrationTransferOriginal original)
    {
        original=null;
        try
        {
            string[] attributes={"version","operation","world","owner","creation","deaths","revision","ownedBelt","locks","action"};
            if(node==null||node.Name!="hydrationTransfer"||node.Attributes().Count()!=attributes.Length||
                attributes.Any(name=>node.Attribute(name)==null)||node.Elements().Count()!=1||node.Element("gearPlan")==null||
                node.Nodes().Any(value=>!(value is XElement))||(string)node.Attribute("version")!="1"||
                !Guid.TryParseExact((string)node.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
                !Guid.TryParseExact((string)node.Attribute("world"),"N",out var world)||world==Guid.Empty||
                !ValidOwner((string)node.Attribute("owner"))||
                !RebirthSurvivorRequestScope.TryNormalize((string)node.Attribute("creation"),out var creation)||
                creation!=(string)node.Attribute("creation")||
                !Number(node,"deaths",out var deaths)||!Number(node,"revision",out var revision)||revision==int.MaxValue||
                !Number(node,"ownedBelt",out var owned)||owned<4||owned>18||
                !RebirthGearInventoryPlanCodec.TryRead(node.Element("gearPlan"),out var plan))return false;
            string locks=(string)node.Attribute("locks"),action=(string)node.Attribute("action");
            if(plan.BagSlotsBefore!=plan.BagSlotsAfter||plan.BeltSlotsBefore!=plan.BeltSlotsAfter||
                owned>plan.BeltSlotsBefore||plan.Recovery.Count!=0||plan.Changes.Count!=1||
                locks.Length!=plan.BagSlotsBefore||locks.Any(value=>value!='0'&&value!='1'))return false;
            var change=plan.Changes[0];
            if(change.IsBag?locks[change.Index]!='0':change.Index>=owned)return false;
            bool equip=action=="equip";
            if(equip)
            {
                if(plan.GearBefore.Count!=0||plan.GearAfter.Count!=1||change.Before.Count<1||
                    change.After.Count!=change.Before.Count-1||change.Before.ItemData!=plan.GearAfter.ItemData||
                    change.After.Count>0&&change.After.ItemData!=change.Before.ItemData)return false;
            }
            else if(action!="unequip"||!change.IsBag||plan.GearBefore.Count!=1||plan.GearAfter.Count!=0||
                change.Before.Count!=0||change.After.Count!=1||change.After.ItemData!=plan.GearBefore.ItemData)return false;
            // Canonical spelling/order emitted by our writer defines the digest.
            var canonical=new XElement("hydrationTransfer",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),
                new XAttribute("world",world.ToString("N")),new XAttribute("owner",(string)node.Attribute("owner")),
                new XAttribute("creation",creation),new XAttribute("deaths",deaths),new XAttribute("revision",revision),
                new XAttribute("ownedBelt",owned),new XAttribute("locks",locks),new XAttribute("action",action),RebirthGearInventoryPlanCodec.Write(plan));
            if(!XNode.DeepEquals(node,canonical))return false;
            original=new RebirthHydrationTransferOriginal(canonical,operation,world,creation,deaths,revision,owned,equip);return true;
        }
        catch{return false;}
    }
    private static bool Number(XElement node,string name,out int value)=>
        int.TryParse((string)node.Attribute(name),NumberStyles.None,CultureInfo.InvariantCulture,out value)&&value>=0;
    private static bool ValidOwner(string value)=>!string.IsNullOrEmpty(value)&&value.Length<=256&&value==value.Trim()&&!value.Any(char.IsControl);
}
