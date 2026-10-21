using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

internal static partial class RebirthRemoteResourceDurableDispatch
{
    // Terminal reads never mutate. Replay may refuse an ended-life original
    // with positive write-ahead evidence that no source was ever attempted.
    // Neither path rearms admission, withdraws a source or delivers owner items.
    internal static bool TryReadCookingTerminal(EntityPlayer actor,long request,IList<ItemStack> requirements,out RebirthRemoteResourceDurableJournal.Record terminal)
        =>TryReadCookingReplay(actor,request,requirements,false,false,out terminal);
    internal static bool TryReadCookingReplay(EntityPlayer actor,long request,IList<ItemStack> requirements,out RebirthRemoteResourceDurableJournal.Record terminal)
        =>TryReadCookingReplay(actor,request,requirements,true,false,out terminal);
    internal static bool TryReadCookingMetadata(EntityPlayer actor,long request,IList<ItemStack> requirements,out RebirthRemoteResourceDurableJournal.Record terminal)
        =>TryReadCookingReplay(actor,request,requirements,true,true,out terminal);
    private static bool TryReadCookingReplay(EntityPlayer actor,long request,IList<ItemStack> requirements,bool allowRefusal,bool metadataOnly,out RebirthRemoteResourceDurableJournal.Record terminal)
    {
        terminal=null;
        try
        {
            if(request<=0||requirements==null||requirements.Count<1||requirements.Count>12||requirements.Any(item=>item==null||item.IsEmpty()||item.count<1)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||!RemoteResourceWorldIdentity.TryGet(actor?.world,out string world))return false;
            string creation=RebirthCamperCharacterAccess.CurrentCreation(actor);
            if(!Scope(actor,identity.StorageKey,creation))return false;
            Guid operation=RebirthRemoteResourceDurableJournal.Operation(world,identity.StorageKey,creation,1,(ulong)request,RemoteResourceClientOperation.CookingPull);
            if(!RebirthRemoteResourceDurableJournal.TryRead(identity.StorageKey,operation,out var original,out bool unknown)||unknown||original==null||
                original.Operation!=operation||original.World!=world||original.Owner!=identity.StorageKey||original.Creation!=creation)return false;
            var root=original.Copy();int deaths=(int)root.Attribute("deaths");
            if(deaths<0||deaths>actor.Died||(int)root.Attribute("kind")!=(int)RemoteResourceClientOperation.CookingPull)return false;
            using(var hash=SHA256.Create())
            {
                string actual=BitConverter.ToString(hash.ComputeHash(RebirthCamperInventorySnapshot.Capture(requirements.ToArray()))).Replace("-","").ToLowerInvariant();
                if(actual!=(string)root.Attribute("requirementsHash"))return false;
            }
            if(allowRefusal&&original.Phase<2&&RebirthRemoteResourceDurableOutcome.TryReconcileCookingSources(actor,original,out var reconciled))
            {original=reconciled;root=original.Copy();}
            if(allowRefusal&&original.Phase<4&&RebirthRemoteResourceDurableOutcome.TryCompleteSavedCookingHandoff(actor,original,out var completed))
            {original=completed;root=original.Copy();}
            if(allowRefusal&&original.Phase<2&&(deaths<actor.Died||metadataOnly)&&RebirthRemoteResourceDurableOutcome.IsUnattemptedCookingOriginal(original))
            {
                int currentDeaths=actor.Died;
                // Attempt is persisted before every source effect. All-zero
                // attempts prove no effect; missing source receipts do not.
                if(!RebirthRemoteResourceDurableJournal.TryAdvance(original,value=>{
                    value.SetAttributeValue("phase",3);value.SetAttributeValue("outcome",2);
                    value.Element("reason").Value=deaths<currentDeaths?"Original cooking pull ended before any source attempt.":"Original cooking pull is restricted to metadata recovery before any source attempt.";
                },()=>Scope(actor,identity.StorageKey,creation)&&actor.Died==currentDeaths,out var refused))return false;
                original=refused;root=original.Copy();
            }
            bool refusal=allowRefusal&&(RebirthRemoteResourceDurableOutcome.IsUnattemptedCookingRefusal(original)||RebirthRemoteResourceDurableOutcome.IsRestoredCookingRefusal(original));
            bool owed=allowRefusal&&deaths<actor.Died&&(original.Phase==2||original.Phase==3)&&root.Element("ownerAction")==null&&
                root.Element("ownerRefund")==null&&RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(root.Element("removed").Value)).Length>0;
            if(original.Phase!=4&&!refusal&&!owed)return false;
            if(!refusal&&!owed&&(string.IsNullOrEmpty((string)root.Attribute("disposition"))||string.IsNullOrEmpty((string)root.Attribute("ownerProof"))))return false;
            terminal=original;return true;
        }
        catch{return false;}
    }
}
