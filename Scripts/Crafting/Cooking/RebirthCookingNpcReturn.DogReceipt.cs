using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthCookingNpcReturn
{
    private const string DogReceiptName="rebirthCookingIngredientReturn";
    [ThreadStatic]private static Attempt dogMutation;
    private static string DogReceipt(Attempt attempt)=>new XElement(DogReceiptName,new XAttribute("version",1),
        new XAttribute("operation",attempt.Operation.ToString("N")),new XAttribute("intent",RebirthRemoteResourceDurableJournal.Digest(attempt.Intent)),
        new XAttribute("world",attempt.NativeWorld),new XAttribute("save",attempt.Save),new XAttribute("owner",attempt.Owner),new XAttribute("creation",attempt.Creation),
        new XAttribute("deaths",attempt.Deaths),new XAttribute("npc",attempt.Source.NativeIdentity),new XAttribute("before",attempt.Revision),
        new XAttribute("after",checked(attempt.Revision+1)),new XAttribute("recovery",attempt.Recovery)).ToString(SaveOptions.DisableFormatting);

    private static XElement ReadDogReceipt(string text)
    {
        if(text==null||text.Length<1||text.Length>4096)throw new InvalidDataException("Cooking dog receipt exceeds bound");
        var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096};
        using(var reader=XmlReader.Create(new StringReader(text),settings))
        {int nodes=0;while(reader.Read())if(++nodes>32||reader.Depth>2||reader.AttributeCount>12)throw new InvalidDataException("Cooking dog receipt graph exceeds bound");}
        XElement node;using(var reader=XmlReader.Create(new StringReader(text),settings))node=XElement.Load(reader);
        string[] attributes={"version","operation","intent","world","save","owner","creation","deaths","npc","before","after","recovery"};
        if(node.Name!=DogReceiptName||node.HasElements||node.Nodes().Any()||node.Attributes().Count()!=attributes.Length||
            node.Attributes().Any(attribute=>!attributes.Contains(attribute.Name.ToString()))||(int?)node.Attribute("version")!=1||
            !Guid.TryParseExact((string)node.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
            ((string)node.Attribute("intent"))?.Length!=64||(uint)node.Attribute("before")==uint.MaxValue||
            (uint)node.Attribute("after")!=(uint)node.Attribute("before")+1||node.ToString(SaveOptions.DisableFormatting)!=text)
            throw new InvalidDataException("Cooking dog receipt is invalid");
        return node;
    }
    private static bool DogReceipts(RebirthNpcPersistentRecord row,out string own,out List<string> unrelated)
    {
        own=null;unrelated=new List<string>();var fragments=row?.UnknownOptionalElements??Array.Empty<string>();
        if(fragments.Length>128)return false;
        foreach(string fragment in fragments)
        {
            if(fragment==null)return false;
            if(!fragment.TrimStart().StartsWith("<"+DogReceiptName,StringComparison.Ordinal)){unrelated.Add(fragment);continue;}
            if(own!=null)return false;
            try{ReadDogReceipt(fragment);}catch{return false;}own=fragment;
        }
        return true;
    }

    internal static bool ValidateDogCandidateBefore(RebirthNpcPersistentRecord row)
    {
        var attempt=dogMutation;if(attempt==null)return true;
        if(!Active.Contains(attempt)||!Current(attempt)||!attempt.Invoked||attempt.State!=OutcomeState.Unknown||
            row?.Identity?.StableNpcId!=attempt.Source.NativeIdentity||attempt.DogSlot<0||attempt.DogSlot>=attempt.DogBefore.Length||
            !RebirthRemoteResourceDogTransfer.Matches(row,attempt.DogBefore,attempt.Locks,attempt.Revision))return false;
        if(row.Inventory?.SlotRecords!=null&&(row.Inventory.SlotRecords[attempt.DogSlot].Reserved||row.Inventory.SlotRecords[attempt.DogSlot].Locked))return false;
        for(int slot=0;slot<attempt.DogBefore.Length;slot++)
            if(slot!=attempt.DogSlot&&!RebirthStationGridIngredients.IsSameStackSnapshot(attempt.DogBefore[slot],attempt.DogAfter[slot]))return false;
        return true;
    }
    internal static bool PreserveUnchangedDogSlot(int slot)=>dogMutation!=null&&slot!=dogMutation.DogSlot;

    // Called inside the native aggregate candidate after its exact slot/lock
    // projection. The private scope is issued only by this original's one call.
    internal static bool StampDogCandidate(RebirthNpcPersistentRecord row)
    {
        var attempt=dogMutation;if(attempt==null)return true;
        if(!Active.Contains(attempt)||!Current(attempt)||!attempt.Invoked||attempt.State!=OutcomeState.Unknown||!attempt.Source.IsDogStorage||
            row?.Identity?.StableNpcId!=attempt.Source.NativeIdentity||
            !RebirthRemoteResourceDogTransfer.Matches(row,attempt.DogAfter,attempt.Locks,attempt.Revision+1)||
            !DogReceipts(row,out var previous,out var unrelated))return false;
        if(previous!=null&&previous!=DogReceipt(attempt)&&!TerminalDogReceipt(attempt,previous))return false;
        string receipt=DogReceipt(attempt);if(receipt.Length>4096||unrelated.Count>=128)return false;
        unrelated.Add(receipt);row.UnknownOptionalElements=unrelated.ToArray();return true;
    }
    private static bool HasSavedDogReceipt(Attempt attempt,RebirthNpcPersistentRecord row)
        =>row?.Identity?.StableNpcId==attempt.Source.NativeIdentity&&row.Inventory!=null&&row.Inventory.InventoryRevision>=attempt.Revision+1&&DogReceipts(row,out var receipt,out _)&&receipt==DogReceipt(attempt)&&
            (row.Inventory.InventoryRevision>attempt.Revision+1||RebirthRemoteResourceDogTransfer.Matches(row,attempt.DogAfter,attempt.Locks,attempt.Revision+1));

    // Replace only this purpose's earlier positively saved terminal marker.
    // An unknown original never becomes permission for a second deposit.
    private static bool TerminalDogReceipt(Attempt attempt,string previous)
    {
        try
        {
            var prior=ReadDogReceipt(previous);string id=(string)prior.Attribute("operation"),digest=(string)prior.Attribute("intent");
            if(id==attempt.Operation.ToString("N")||(string)prior.Attribute("npc")!=attempt.Source.NativeIdentity.ToString()||
                (string)prior.Attribute("world")!=attempt.NativeWorld||(string)prior.Attribute("save")!=attempt.Save)return false;
            string intent=Path.Combine(attempt.Directory,id+".intent.xml"),terminal=Path.Combine(attempt.Directory,id+".published.xml");
            PathCheck(intent);PathCheck(terminal);
            if(File.Exists(intent+".tmp")||File.Exists(terminal+".tmp")||
                (File.GetAttributes(intent)&FileAttributes.ReparsePoint)!=0||(File.GetAttributes(terminal)&FileAttributes.ReparsePoint)!=0)return false;
            using(var input=new FileStream(intent,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var hash=SHA256.Create())
            {
                if(input.Length<1||input.Length>MaximumJournal)return false;
                string actual=BitConverter.ToString(hash.ComputeHash(input)).Replace("-","").ToLowerInvariant();if(actual!=digest)return false;
            }
            string text;
            using(var input=new FileStream(terminal,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(input.Length<1||input.Length>4096)return false;
                var bytes=new byte[(int)input.Length];int offset=0;while(offset<bytes.Length){int read=input.Read(bytes,offset,bytes.Length-offset);if(read<1)return false;offset+=read;}
                text=new UTF8Encoding(false,true).GetString(bytes);
            }
            XElement node;using(var reader=XmlReader.Create(new StringReader(text),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096}))node=XElement.Load(reader);
            return node.Name=="cookingIngredientReturnOutcome"&&!node.Nodes().Any()&&node.Attributes().Count()==5&&node.ToString(SaveOptions.DisableFormatting)==text&&
                (int?)node.Attribute("version")==1&&(string)node.Attribute("operation")==id&&
                (string)node.Attribute("stage")=="published"&&(bool?)node.Attribute("invoked")==true&&(string)node.Attribute("intent")==digest&&Current(attempt);
        }
        catch{return false;}
    }
}
