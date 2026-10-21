using System;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourceDurableOutcome
{
    internal static bool IsRestoredCookingRefusal(RebirthRemoteResourceDurableJournal.Record original)
        =>IsRestoredHistoricalRefusal(original,RemoteResourceClientOperation.CookingPull);
    internal static bool IsRestoredPersonalCraftRefusal(RebirthRemoteResourceDurableJournal.Record original)
        =>IsRestoredHistoricalRefusal(original,RemoteResourceClientOperation.Craft);
    internal static bool IsRestoredBlockRefusal(RebirthRemoteResourceDurableJournal.Record original)
        =>original!=null&&((RemoteResourceClientOperation)(int)original.Copy().Attribute("kind") is RemoteResourceClientOperation.BlockRepair or RemoteResourceClientOperation.BlockUpgrade)&&
            IsRestoredHistoricalRefusal(original,(RemoteResourceClientOperation)(int)original.Copy().Attribute("kind"));
    internal static bool HasSavedHistoricalFetchAdmission(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent intent,RebirthRemoteResourceDurableJournal.Record root)
    {
        if(intent==null||root==null)return false;
        if(intent.Kind==RemoteResourceClientOperation.Craft)return RebirthRemoteResourceCraftProtocol.HasSavedPersonalAdmission(actor,intent,root);
        return RebirthRemoteResourceBlockAdoption.HasSavedAdmission(actor,intent,root);
    }
    private static bool IsRestoredHistoricalRefusal(RebirthRemoteResourceDurableJournal.Record original,RemoteResourceClientOperation kind)
    {
        try
        {
            if(original==null||original.Phase!=3||original.HasOwnerAction)return false;
            var root=original.Copy();
            return (int)root.Attribute("kind")== (int)kind&&(int)root.Attribute("outcome")==2&&
                (((int?)root.Attribute("aborting")??0)==1||(kind==RemoteResourceClientOperation.Craft||kind==RemoteResourceClientOperation.BlockRepair||kind==RemoteResourceClientOperation.BlockUpgrade)&&root.Element("sources").Elements("source").All(source=>(int)source.Attribute("attempted")==0&&(int)source.Attribute("published")==0))&&
                RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element("removed").Value)).Length==0&&
                root.Element("sources").Elements("source").All(source=>(int)source.Attribute("attempted")==0||
                    (int)source.Attribute("kind")!=5&&((int?)source.Attribute("restored")??0)==1&&!string.IsNullOrEmpty((string)source.Attribute("restoreProof")));
        }
        catch{return false;}
    }
    internal static bool AcknowledgeCookingNoPayment(EntityPlayer actor,Guid operation,string binding)
        =>AcknowledgeHistoricalNoPayment(actor,operation,binding,null);
    internal static bool AcknowledgePersonalCraftNoPayment(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent intent,Guid operation,string binding)
        =>intent?.Kind==RemoteResourceClientOperation.Craft&&AcknowledgeHistoricalNoPayment(actor,operation,binding,intent);
    internal static bool AcknowledgeBlockNoPayment(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent intent,Guid operation,string binding)
        =>intent!=null&&(intent.Kind==RemoteResourceClientOperation.BlockRepair||intent.Kind==RemoteResourceClientOperation.BlockUpgrade)&&AcknowledgeHistoricalNoPayment(actor,operation,binding,intent);
    private static bool AcknowledgeHistoricalNoPayment(EntityPlayer actor,Guid operation,string binding,RebirthRemoteResourceClientOriginal.Intent craft)
    {
        if(craft==null&&AcknowledgeNoPayment(actor,operation,binding))return true;
        try
        {
            if(!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
                !RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,operation,out var original,out bool unknown)||unknown||original==null||original.Binding!=binding)return false;
            if(craft!=null&&(!HasSavedHistoricalFetchAdmission(actor,craft,original)||
                RebirthRemoteResourcePlacedStationOriginal.IsMappedRoot(original)||RebirthRemoteResourceFetchedStationOriginal.IsMappedRoot(original)))return false;
            if(craft!=null&&AcknowledgeNoPayment(actor,operation,binding))return true;
            var network=RebirthRemoteResourceNetworkScope.Capture(actor);int deaths=actor?.Died??-1;
            bool Current()=>network?.Current==true&&!actor.IsDead()&&actor.Died==deaths&&RebirthOriginalActionScope.Matches(actor,original.Owner,original.Creation)&&
                RemoteResourceWorldIdentity.TryGet(actor.world,out string world)&&world==original.World;
            var kind=craft==null?RemoteResourceClientOperation.CookingPull:craft.Kind;
            if(!Current()||(int)original.Copy().Attribute("kind")!=(int)kind||(int)original.Copy().Attribute("deaths")>=deaths)return false;
            if(original.Phase==4)return (string)original.Copy().Attribute("disposition")=="unused-no-payment";
            if(!IsRestoredHistoricalRefusal(original,kind))return false;
            string proof=craft!=null?RebirthRemoteResourceDurableJournal.Digest("authenticated-no-payment|"+original.Owner+"|"+original.Binding):
                RebirthRemoteResourceDurableJournal.Digest("authenticated-cooking-source-restoration|"+original.Owner+"|"+original.Binding+"|"+original.Copy().ToString(SaveOptions.DisableFormatting));
            return RebirthRemoteResourceDurableJournal.TryAdvance(original,value=>{
                value.SetAttributeValue("phase",4);value.SetAttributeValue("disposition","unused-no-payment");value.SetAttributeValue("ownerProof",proof);
            },Current,out _);
        }
        catch{return false;}
    }
    // Historical cooking reconciliation owns no actor setter or new debit.
    // Native restitution enters only the existing exact original restore API
    // after its durable abort fence and positive saved effect qualification.
    internal static bool TryReconcileCookingSources(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record original,
        out RebirthRemoteResourceDurableJournal.Record next)
        =>TryReconcileHistoricalSources(actor,original,null,out next);
    internal static bool TryReconcilePersonalCraftSources(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent intent,RebirthRemoteResourceDurableJournal.Record original,
        out RebirthRemoteResourceDurableJournal.Record next)
    {next=null;return intent?.Kind==RemoteResourceClientOperation.Craft&&TryReconcileHistoricalSources(actor,original,intent,out next);}
    internal static bool TryReconcileBlockSources(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent intent,RebirthRemoteResourceDurableJournal.Record original,
        out RebirthRemoteResourceDurableJournal.Record next)
    {next=null;return intent!=null&&(intent.Kind==RemoteResourceClientOperation.BlockRepair||intent.Kind==RemoteResourceClientOperation.BlockUpgrade)&&TryReconcileHistoricalSources(actor,original,intent,out next);}
    private static bool TryReconcileHistoricalSources(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record original,RebirthRemoteResourceClientOriginal.Intent craft,
        out RebirthRemoteResourceDurableJournal.Record next)
    {
        next=null;
        try
        {
            if(original==null||original.Phase>1||original.HasOwnerAction)return false;
            bool personal=craft!=null;
            if(personal&&!HasSavedHistoricalFetchAdmission(actor,craft,original))return false;
            var network=RebirthRemoteResourceNetworkScope.Capture(actor);int currentDeaths=actor?.Died??-1;
            bool Current()=>network?.Current==true&&!actor.IsDead()&&actor.Died==currentDeaths&&
                RebirthOriginalActionScope.Matches(actor,original.Owner,original.Creation)&&
                RemoteResourceWorldIdentity.TryGet(actor.world,out string world)&&world==original.World;
            bool OriginalCurrent()=>Current()&&
                RebirthRemoteResourceDurableJournal.TryRead(original.Owner,original.Operation,out var saved,out bool unknown)&&!unknown&&saved!=null&&
                saved.Binding==original.Binding&&saved.Revision==original.Revision&&saved.Phase==original.Phase;
            var root=original.Copy();var sources=root.Element("sources").Elements("source").ToArray();
            if(!OriginalCurrent()||(int)root.Attribute("kind")!=(int)(personal?craft.Kind:RemoteResourceClientOperation.CookingPull)||
                (int)root.Attribute("deaths")>=currentDeaths)return false;
            if(original.Phase==0)
            {
                if(sources.Any(source=>(int)source.Attribute("attempted")!=0))return false;
                return RebirthRemoteResourceDurableJournal.TryAdvance(original,value=>{
                    value.SetAttributeValue("phase",3);value.SetAttributeValue("outcome",2);
                    value.Element("reason").Value="Original owner action ended before any source attempt.";
                },Current,out next);
            }
            if(sources.All(source=>(int)source.Attribute("kind")!=5||(int)source.Attribute("attempted")==0))
            {
                if(((int?)root.Attribute("aborting")??0)==0)
                    return RebirthRemoteResourceDurableJournal.TryAdvance(original,value=>value.SetAttributeValue("aborting",1),Current,out next);
                foreach(var source in sources.Where(value=>(int)value.Attribute("attempted")==1&&((int?)value.Attribute("restored")??0)==0))
                {
                    string id=(string)source.Attribute("id");
                    if(!RebirthRemoteResourceSourceCustody.TryRestoreHistoricalCookingDebit(actor,original,id,OriginalCurrent,out string proof,craft)||string.IsNullOrEmpty(proof))return false;
                    return RebirthRemoteResourceDurableJournal.TryAdvance(original,value=>{
                        var item=value.Element("sources").Elements("source").Single(entry=>(string)entry.Attribute("id")==id);
                        item.SetAttributeValue("restored",1);item.SetAttributeValue("restoreProof",proof);
                    },Current,out next);
                }
                if(sources.Any(source=>(int)source.Attribute("attempted")!=0&&(((int?)source.Attribute("restored")??0)!=1||string.IsNullOrEmpty((string)source.Attribute("restoreProof")))))return false;
                return RebirthRemoteResourceDurableJournal.TryAdvance(original,value=>{
                    value.SetAttributeValue("phase",3);value.SetAttributeValue("outcome",2);
                    value.Element("removed").Value=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(new ItemStack[0]));
                    value.Element("reason").Value="Original cooking sources were restored after owner life ended.";
                },Current,out next);
            }
            if(((int?)root.Attribute("aborting")??0)!=0||sources.Any(source=>((int?)source.Attribute("restored")??0)!=0))return false;
            // A zero attempt cannot be upgraded to a debit after owner death.
            // Every remaining item must have its existing native effect proof.
            foreach(var source in sources.Where(value=>(int)value.Attribute("attempted")==1&&(int)value.Attribute("published")==0))
            {
                string id=(string)source.Attribute("id"),proof;
                if((int)source.Attribute("kind")==5)
                {
                    var plan=ReadNpc(original,source,OriginalCurrent);
                    if(plan==null||!TryObserveSavedNpc(plan,true,out proof))return false;
                }
                else if(!RebirthRemoteResourceSourceCustody.TryObserveEnteredCookingDebit(original,id,out proof,actor,craft))return false;
                if(string.IsNullOrEmpty(proof)||!Current())return false;
                return RebirthRemoteResourceDurableJournal.TryAdvance(original,value=>{
                    var item=value.Element("sources").Elements("source").Single(entry=>(string)entry.Attribute("id")==id);
                    item.SetAttributeValue("published",1);item.SetAttributeValue("proof",proof);
                },Current,out next);
            }
            if(sources.Any(source=>(int)source.Attribute("attempted")==1&&((int)source.Attribute("published")!=1||string.IsNullOrEmpty((string)source.Attribute("proof")))))return false;
            bool complete=sources.All(source=>(int)source.Attribute("published")==1);
            var paid=sources.Where(source=>(int)source.Attribute("published")==1).SelectMany(source=>RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(source.Element("paid").Value))).ToArray();
            return RebirthRemoteResourceDurableJournal.TryAdvance(original,value=>{
                value.SetAttributeValue("phase",complete?2:3);value.SetAttributeValue("outcome",complete?1:2);
                value.Element("removed").Value=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(paid));
                value.Element("reason").Value=complete?"":"Original owner action ended after positively saved partial source payment.";
            },Current,out next);
        }
        catch{next=null;return false;}
    }
}
