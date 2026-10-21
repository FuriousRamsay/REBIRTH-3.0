using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    internal sealed partial class Original
    {
        internal bool RebindActualRemoteHydration(RebirthProductiveEarnedUiDeferredTransport.ClientHydration hydration)
        {
            if(!RebirthProductiveEarnedUiDeferredTransport.IsActualHydrationRead(hydration)||!OwnerLineageCurrent()||
                !ReferenceEquals(hydration.World,World)||!ReferenceEquals(hydration.Actor,Actor)||ReferenceEquals(hydration.Station,Station)||
                hydration.Station.ToWorldPos()!=Station.ToWorldPos()||!providerCurrent())return false;
            var old=Station;Station=hydration.Station;if(Current())return true;Station=old;return false;
        }
    }
    private static RecyclerClient TryRebindActualRecyclerClient(RebirthProductiveEarnedUiDeferredTransport.ClientHydration hydration,XElement header)
    {
        if(!RebirthProductiveEarnedUiDeferredTransport.IsActualHydrationRead(hydration)||!Guid.TryParseExact((string)header.Attribute("operation"),"N",out var operation)||
            !Guid.TryParseExact((string)header.Attribute("nonce"),"N",out var nonce)||nonce==Guid.Empty)return null;
        var client=recyclerClients.SingleOrDefault(value=>value.Source?.Operation==operation&&ReferenceEquals(value.Connection,hydration.Connection)&&
            ReferenceEquals(value.Source.World,hydration.World)&&ReferenceEquals(value.Source.Actor,hydration.Actor));
        var source=client?.Source;
        if(source?.OwnerLineageCurrent()!=true||client.Unknown||ReferenceEquals(source.Station,hydration.Station)||
            client.Nonce!=Guid.Empty&&client.Nonce!=nonce||(string)header.Attribute("owner")!=source.Owner||(string)header.Attribute("creation")!=source.Creation||
            (int?)header.Attribute("deaths")!=source.Deaths||(string)header.Attribute("world")!=source.WorldKey||
            (string)header.Attribute("original")!=RebirthStationPublicationRecord.Digest(source.Image))return null;
        string stage=(string)header.Attribute("stage");bool after=stage=="after";int hostPhase=(int?)header.Attribute("ownerPhase")??-1,hostCount=(int?)header.Attribute("ownerCompleted")??-1;
        if(stage!="before"&&!after||(after?(!client.Applied||!client.NativeSent):client.Applied||client.PrincipalEntered))return null;
        var checkpoint=client.Owner?.ObserveOwnedRemoteRebind();
        if(checkpoint==null)
        {
            if(after||client.Owner!=null||hostPhase!=0||hostCount!=0||!source.MatchesOwnerCheckpoint(source.Actor.bag.ItemGrid.items,source.Actor.inventory.ItemGrid.items,0))return null;
        }
        else if(!after?(checkpoint.Phase!=1||checkpoint.Completed!=0||hostCount!=0||hostPhase<0||hostPhase>1):
            hostPhase<1||hostPhase>3||hostCount<0||hostCount>checkpoint.Completed||checkpoint.Completed-hostCount>1||
            hostPhase==3&&(checkpoint.Phase!=3||checkpoint.Completed!=source.Slots.Length))return null;
        if(!RebirthProductiveEarnedNativeBody.MatchesCurrent(hydration.Station,after?source.AfterNative:source.BeforeNative,after?source.AfterOffset:source.BeforeOffset)||
            !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*source.Image.Length+65536),out var memory))return null;
        using(memory)
        {
            var record=new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world",source.WorldKey),
                new XElement("before",XElement.Parse(Encoding.UTF8.GetString(source.Image))),new XElement("recyclerRetainedNativeReentry",
                    new XAttribute("actualReadGroup",hydration.Group.ToString("N")),new XAttribute("sameActualOwnerBody",true),new XElement(header),checkpoint?.CopyLocalSavedDescriptor()));
            string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),operation.ToString("N")+".hot."+hydration.Group.ToString("N")+".xml");
            if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path))||
                !source.RebindActualRemoteHydration(hydration))return null;
            client.Nonce=nonce;if(!client.PrincipalEntered)client.Generation=hydration.Group;return client;
        }
    }
}
