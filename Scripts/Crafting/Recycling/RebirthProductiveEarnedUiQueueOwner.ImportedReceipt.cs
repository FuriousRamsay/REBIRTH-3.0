using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    private static XElement ReadImportedCreator(string text)
    {
        if(text==null||text.Length<1||text.Length>4096)throw new InvalidDataException();
        var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4096,MaxCharactersFromEntities=1};
        using(var source=new StringReader(text))using(var reader=XmlReader.Create(source,settings))
        {var image=XElement.Load(reader);if(image.HasElements||image.Nodes().Any(node=>!(node is XText value&&string.IsNullOrWhiteSpace(value.Value))))throw new InvalidDataException();return image;}
    }
    // This issuer observes a CURRENT host-attested receipt hydration. It never
    // creates an earlier enqueue witness or makes the imported source queueable.
    internal static bool ImportActualHydratedAggregate(RebirthProductiveEarnedUiDeferredTransport.ClientHydration original,
        XElement evidence,CraftCompleteData receipt,RebirthProductiveEarnedUiDeferredTransport.ImportedContribution[] members)
    {
        if(!RebirthProductiveEarnedUiDeferredTransport.IsActualHydrationRead(original)||members==null||members.Length<1||members.Length>4||
            members.Any(value=>!ReferenceEquals(value.Original,original)||!ReferenceEquals(value.Receipt,receipt))||
            original.Station.CraftCompleteList?.Count(value=>ReferenceEquals(value,receipt))!=1||aggregates.Any(value=>ReferenceEquals(value.Receipt,receipt)))return false;
        var group=new Aggregate{Receipt=receipt,Image=RowImage(receipt)};
        foreach(var member in members)
        {
            var creator=ReadImportedCreator(member.CreatorPurpose);
            if(creator.Name!="queueOwnerPurpose"||(int?)creator.Attribute("version")!=1||
                (string)creator.Attribute("birth")!=member.Birth.ToString("N")||(string)creator.Attribute("world")!=original.WorldKey||
                !RebirthOriginalActionScope.Validate((string)creator.Attribute("owner"),(string)creator.Attribute("creation")))throw new InvalidDataException();
            var source=new Witness{World=original.World,Station=original.Station,ClientReceipt=member,Published=true,RowRetired=true,
                WorldKey=original.WorldKey,Save=Path.GetFullPath(GameIO.GetSaveGameDir()),OriginalOwnerKey=(string)creator.Attribute("owner"),Creation=(string)creator.Attribute("creation"),
                StartingEntityId=(int)creator.Attribute("entity"),Deaths=(int)creator.Attribute("deaths"),Birth=member.Birth,
                Operation=Guid.ParseExact((string)creator.Attribute("operation"),"N")};
            if(member.CreatorPurpose==(string)evidence.Element("recipient"))
            {
                if(source.StartingEntityId!=original.Actor.entityId||source.OriginalOwnerKey!=original.Owner||
                    source.Creation!=original.Creation||source.Deaths!=original.Deaths)throw new InvalidDataException();if(group.Recipient==null)group.Recipient=source;
            }
            var allocation=member.RetainForCompletion();var hold=member.RetainForCompletion();
            if(allocation==null||hold==null){allocation?.Dispose();hold?.Dispose();throw new InvalidDataException();}
            var token=new DeferredReceipt(deferredReceiptIssuer,source,null,null,receipt,member.BeforeComplete,member.AfterComplete,allocation,hold,group,member.Contribution,member);
            member.Token=token;group.Members.Add(token);
        }
        if(group.Recipient==null)throw new InvalidDataException();aggregates.Add(group);
        foreach(var member in members)if(!RebirthProductiveEarnedUiOwnerFile.AttachReceipt(member.Token))throw new InvalidDataException();
        return true;
    }
}
