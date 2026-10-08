using System;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

// Original pre-offer request. Its digest binds every physical inventory slot,
// including retired tails, and the selected source position. This is evidence
// binding only: it never holds, saves, transfers or authenticates a connection.
public sealed class RebirthGearPreparationIntent
{
    public string CreationId { get; private set; }
    public Guid TransactionId { get; private set; }
    public long ExpectedRevision { get; private set; }
    public int ItemType { get; private set; }
    public ushort ItemSeed { get; private set; }
    public bool SourceIsBag { get; private set; }
    public int SourceIndex { get; private set; }
    public string InventoryDigest { get; private set; }
    public string UnequipSlot { get; private set; }
    public bool IsUnequip => UnequipSlot!=null;
    private RebirthGearPreparationIntent() { }
    public static bool TryCreate(string creation, Guid transaction, long revision, int type, ushort seed,
        bool bag, int index, RebirthGearInventorySnapshot snapshot, out RebirthGearPreparationIntent intent)
    {
        intent = null;
        if (!ValidScope(creation, transaction, revision, type, bag, index, out var normalized) ||
            snapshot == null || !snapshot.IsUsableSource(bag,index) ||
            !TryDigest(snapshot,out var digest)) return false;
        var source=(bag?snapshot.Bag:snapshot.Belt)[index];
        if(source.Count<=0)return false;
        intent = new RebirthGearPreparationIntent { CreationId=normalized,TransactionId=transaction,
            ExpectedRevision=revision,ItemType=type,ItemSeed=seed,SourceIsBag=bag,SourceIndex=index,InventoryDigest=digest };
        return true;
    }
    // Version 2 is explicitly unequip; version 1 equip bytes remain unchanged.
    // Server gear revision binds the selected slot's exact persisted native item.
    public static bool TryCreateUnequip(string creation,Guid transaction,long revision,string slot,
        RebirthGearInventorySnapshot snapshot,out RebirthGearPreparationIntent intent)
    {
        intent=null;
        if(!RebirthSurvivorRequestScope.TryNormalize(creation,out var normalized)||transaction==Guid.Empty||
            revision<0||revision==long.MaxValue||!ValidUnequipSlot(slot)||!TryDigest(snapshot,out var digest))return false;
        intent=new RebirthGearPreparationIntent{CreationId=normalized,TransactionId=transaction,
            ExpectedRevision=revision,SourceIndex=-1,UnequipSlot=slot,InventoryDigest=digest};return true;
    }
    public bool MatchesInventory(RebirthGearInventorySnapshot snapshot)
        => snapshot!=null && (IsUnequip||snapshot.IsUsableSource(SourceIsBag,SourceIndex)) &&
            TryDigest(snapshot,out var digest) && digest==InventoryDigest &&
            (IsUnequip||(SourceIsBag?snapshot.Bag:snapshot.Belt)[SourceIndex].Count>0);
    public XElement Write() => IsUnequip?new XElement("gearPreparationIntent",new XAttribute("version",2),
        new XAttribute("creation",CreationId),new XAttribute("transaction",TransactionId.ToString("N")),
        new XAttribute("revision",ExpectedRevision),new XAttribute("slot",UnequipSlot),new XAttribute("digest",InventoryDigest))
        :new XElement("gearPreparationIntent",new XAttribute("version",1),
        new XAttribute("creation",CreationId),new XAttribute("transaction",TransactionId.ToString("N")),
        new XAttribute("revision",ExpectedRevision),new XAttribute("type",ItemType),new XAttribute("seed",ItemSeed),
        new XAttribute("bag",SourceIsBag),new XAttribute("index",SourceIndex),new XAttribute("digest",InventoryDigest));
    public static bool TryRead(XElement node,out RebirthGearPreparationIntent intent)
    {
        intent=null;
        if(node!=null&&(string)node.Attribute("version")=="2")return TryReadUnequip(node,out intent);
        if(node==null || node.Name!="gearPreparationIntent" || node.Attributes().Count()!=9 ||
            (string)node.Attribute("version")!="1" || node.Elements().Any() ||
            node.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value)) ||
            !Guid.TryParseExact((string)node.Attribute("transaction"),"N",out var transaction) ||
            !long.TryParse((string)node.Attribute("revision"),NumberStyles.None,CultureInfo.InvariantCulture,out var revision) ||
            !int.TryParse((string)node.Attribute("type"),NumberStyles.None,CultureInfo.InvariantCulture,out var type) ||
            !ushort.TryParse((string)node.Attribute("seed"),NumberStyles.None,CultureInfo.InvariantCulture,out var seed) ||
            !bool.TryParse((string)node.Attribute("bag"),out var bag) ||
            !int.TryParse((string)node.Attribute("index"),NumberStyles.None,CultureInfo.InvariantCulture,out var index) ||
            !ValidScope((string)node.Attribute("creation"),transaction,revision,type,bag,index,out var creation))return false;
        string digest=(string)node.Attribute("digest");
        if(digest==null||digest.Length!=64||digest.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f')))return false;
        intent=new RebirthGearPreparationIntent {CreationId=creation,TransactionId=transaction,ExpectedRevision=revision,
            ItemType=type,ItemSeed=seed,SourceIsBag=bag,SourceIndex=index,InventoryDigest=digest};return true;
    }
    private static bool TryReadUnequip(XElement node,out RebirthGearPreparationIntent intent)
    {
        intent=null;
        if(node.Name!="gearPreparationIntent"||node.Attributes().Count()!=6||node.Elements().Any()||
            node.Nodes().Any(n=>!(n is XText)||!string.IsNullOrWhiteSpace(((XText)n).Value))||
            !Guid.TryParseExact((string)node.Attribute("transaction"),"N",out var transaction)||transaction==Guid.Empty||
            !long.TryParse((string)node.Attribute("revision"),NumberStyles.None,CultureInfo.InvariantCulture,out var revision)||
            revision<0||revision==long.MaxValue||
            !RebirthSurvivorRequestScope.TryNormalize((string)node.Attribute("creation"),out var creation)||
            !ValidUnequipSlot((string)node.Attribute("slot")))return false;
        string digest=(string)node.Attribute("digest");
        if(digest==null||digest.Length!=64||digest.Any(c=>!(c>='0'&&c<='9'||c>='a'&&c<='f')))return false;
        intent=new RebirthGearPreparationIntent{CreationId=creation,TransactionId=transaction,ExpectedRevision=revision,
            SourceIndex=-1,UnequipSlot=(string)node.Attribute("slot"),InventoryDigest=digest};return true;
    }
    private static bool ValidUnequipSlot(string slot)=>slot=="backpack"||slot=="belt"||slot=="support"||slot=="walkman";
    private static bool ValidScope(string creation,Guid transaction,long revision,int type,bool bag,int index,out string normalized)
        => RebirthSurvivorRequestScope.TryNormalize(creation,out normalized) && transaction!=Guid.Empty &&
            revision>=0 && revision<long.MaxValue && type>0 && index>=0 && index<(bag?169:18);
    private static bool TryDigest(RebirthGearInventorySnapshot snapshot,out string digest)
    {
        digest=null;
        if(snapshot?.Bag==null||snapshot.Belt==null||snapshot.Bag.Length<52||snapshot.Bag.Length>169||
            snapshot.Belt.Length<4||snapshot.Belt.Length>20||snapshot.OwnedBeltSlots<4||snapshot.OwnedBeltSlots>18||
            snapshot.OwnedBeltSlots>snapshot.Belt.Length)return false;
        try
        {
            using(var hash=SHA256.Create())
            using(var stream=new CryptoStream(Stream.Null,hash,CryptoStreamMode.Write))
            using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {
                writer.Write(1);writer.Write(snapshot.OwnedBeltSlots);int budget=4*1024*1024;
                foreach(var slots in new[]{snapshot.Bag,snapshot.Belt})
                {
                    writer.Write(slots.Length);
                    foreach(var stack in slots)
                    {
                        if(stack==null||stack.Count<0||stack.ItemData==null)return false;
                        if(stack.Count==0) {if(stack.ItemData.Length!=0)return false;}
                        else
                        {
                            if(stack.ItemData.Length==0||stack.ItemData.Length>262144||stack.ItemData.Length>budget)return false;
                            var bytes=Convert.FromBase64String(stack.ItemData);
                            if(bytes.Length==0||bytes.Length>196608||Convert.ToBase64String(bytes)!=stack.ItemData)return false;
                            budget-=stack.ItemData.Length;
                        }
                        writer.Write(stack.Count);writer.Write(stack.ItemData);
                    }
                }
                writer.Flush();stream.FlushFinalBlock();
                digest=BitConverter.ToString(hash.Hash).Replace("-",string.Empty).ToLowerInvariant();return true;
            }
        }
        catch {return false;}
    }
}