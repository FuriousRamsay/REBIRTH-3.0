using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourceDurableOutcome
{
    // Invoked only after VerifyWitness qualifies the CURRENT native saved owner
    // file and exact Before/After phase. Source deaths identify the old debt;
    // body deaths identify this new native recipient, never an old actor replay.
    internal static bool WitnessHistoricalCookingRefund(EntityPlayer actor,Guid child,int phase,string expectedActor,string body)
    {
        try
        {
            if(child==Guid.Empty||(phase!=1&&phase!=2)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
                !RebirthWorldCharacterService.TryGetIdentity(actor,out var identity)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string world))return false;
            var image=RebirthCookingRemoteAdmission.ReadProtocolXml(body,8*1024*1024);
            if(image.Name!="cookingGridOriginal"||(int?)image.Attribute("version")!=1||(string)image.Attribute("action")!="fetched-return"||
                (long?)image.Attribute("fetchId") is not long request||request<=0||(int?)image.Attribute("sourceDeaths") is not int sourceDeaths||sourceDeaths<0||sourceDeaths>=actor.Died||
                (int?)image.Attribute("deaths")!=actor.Died||(string)image.Attribute("world")!=actor.world.worldState.Guid||
                (string)image.Attribute("owner")!=identity.CanonicalId||(string)image.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(actor)||
                image.Element(phase==1?"beforeActor":"afterActor")?.Value!=expectedActor||image.Attribute("parentAdmission")!=null)return false;
            Guid operation=RebirthRemoteResourceDurableJournal.Operation(world,owner.StorageKey,(string)image.Attribute("creation"),1,(ulong)request,RemoteResourceClientOperation.CookingPull);
            if(!RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,operation,out var original,out bool unknown)||unknown||original==null||original.Phase<2||
                original.Owner!=owner.StorageKey||original.World!=world||original.Creation!=(string)image.Attribute("creation"))return false;
            var root=original.Copy();
            if((int?)root.Attribute("deaths")!=sourceDeaths||(int?)root.Attribute("kind")!=(int)RemoteResourceClientOperation.CookingPull||root.Element("ownerRefund")!=null)return false;
            int currentDeaths=actor.Died;var network=RebirthRemoteResourceNetworkScope.Capture(actor);
            bool Current()=>network?.Current==true&&!actor.IsDead()&&actor.Died==currentDeaths&&RebirthOriginalActionScope.Matches(actor,original.Owner,original.Creation)&&
                RemoteResourceWorldIdentity.TryGet(actor.world,out string currentWorld)&&currentWorld==world;
            if(!Current())return false;
            string action=new XElement("ownerAction",new XAttribute("domain","cooking-grid"),new XAttribute("child",child.ToString("N")),new XElement("original",body)).ToString(SaveOptions.DisableFormatting);
            if(root.Element("ownerAction")!=null&&root.Element("ownerAction").ToString(SaveOptions.DisableFormatting)!=action)return false;
            if(original.Phase==4)return root.Element("ownerAction")?.ToString(SaveOptions.DisableFormatting)==action&&(string)root.Attribute("disposition")=="refunded";
            var paid=ReadSlots(root.Element("removed"));if(paid.Length==0||!SameTotals(Totals(paid),Totals(ReadSlots(image.Element("fetched")))))return false;
            var beforeGrid=ReadSlots(image.Element("beforeGrid"));var afterGrid=ReadSlots(image.Element("afterGrid"));
            if(beforeGrid.Length!=12||afterGrid.Length!=12||beforeGrid.Concat(afterGrid).Any(item=>!item.IsEmpty()))return false;
            RebirthCamperEnergyActorImage Actor(string text)
            {using(var stream=new MemoryStream(Convert.FromBase64String(text),false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true)){var value=RebirthCamperEnergyActorImage.Read(reader);if(stream.Position!=stream.Length)throw new InvalidDataException();return value;}}
            var before=Actor(image.Element("beforeActor").Value);var after=Actor(image.Element("afterActor").Value);
            if(before.Bag.Length!=after.Bag.Length||before.Belt.Length!=after.Belt.Length||before.BagLocks==null||after.BagLocks==null||!before.BagLocks.SequenceEqual(after.BagLocks)||
                !SameTotals(Totals(before.Bag.Concat(before.Belt).Concat(new[]{before.Cursor}).Concat(paid)),Totals(after.Bag.Concat(after.Belt).Concat(new[]{after.Cursor}))))return false;
            if(root.Element("ownerAction")==null)
            {
                if(phase!=1||!RebirthRemoteResourceDurableJournal.TryAdvance(original,value=>value.Add(XElement.Parse(action)),Current,out var admitted))return false;
                original=admitted;
            }
            if(phase==1)return Current();
            return RebirthRemoteResourceDurableJournal.TryAdvance(original,value=>{
                value.SetAttributeValue("phase",4);value.SetAttributeValue("disposition","refunded");
                value.SetAttributeValue("ownerProof",RebirthRemoteResourceDurableJournal.Digest(child.ToString("N")+"|2|"+expectedActor+"|"+action));
            },Current,out _);
        }
        catch{return false;}
    }
}
