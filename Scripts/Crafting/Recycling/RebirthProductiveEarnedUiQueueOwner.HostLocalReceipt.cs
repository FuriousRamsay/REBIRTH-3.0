using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedUiQueueOwner
{
    internal static bool ImportActualHostLocalReceipt(RebirthProductiveEarnedUiDeferredTransport.HostLocalReceipt source)
    {
        if(source?.Current()!=true||!source.AcceptingSource()||
            source.Members.Count<1||source.Members.Count>4||source.Original.Station.CraftCompleteList?.Count(value=>ReferenceEquals(value,source.Receipt))!=1||
            aggregates.Any(value=>ReferenceEquals(value.Receipt,source.Receipt)))return false;
        var group=new Aggregate{Receipt=source.Receipt,Recipient=source.Original.Recipient,Image=RowImage(source.Receipt)};
        if(!group.Image.SequenceEqual(source.Original.Aggregate))return false;
        foreach(var member in source.Members)
        {
            var allocation=member.Retain();var hold=member.Creator.RetainPublishedSource();
            if(allocation==null||hold==null){allocation?.Dispose();hold?.Dispose();throw new InvalidDataException();}
            var token=new DeferredReceipt(deferredReceiptIssuer,member.Creator,null,null,source.Receipt,member.BeforeComplete,member.AfterComplete,
                allocation,hold,group,(int)member.Member.Attribute("contribution"),hostLocal:member);
            member.Token=token;group.Members.Add(token);
        }
        aggregates.Add(group);
        foreach(var member in source.Members)if(!RebirthProductiveEarnedUiOwnerFile.AttachReceipt(member.Token))throw new InvalidDataException();
        return true;
    }
}
