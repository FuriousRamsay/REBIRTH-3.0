using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

public static partial class RebirthDogLifecycleService
{
    private static ItemStack DecodeHireOriginalItem(XElement row,string name,string count)
    {
        var encoded=row.Element(name);if(encoded==null||encoded.HasElements||encoded.HasAttributes)throw new InvalidDataException();
        var bytes=Convert.FromBase64String(encoded.Value);if(bytes.Length<1||bytes.Length>65536||Convert.ToBase64String(bytes)!=encoded.Value)throw new InvalidDataException();
        int quantity=(int)row.Attribute(count);if(quantity<0||quantity>1000000)throw new InvalidDataException();
        if(!RebirthNativeItemConformanceReader.TryReadCanonicalV9Prefix(bytes,0,out var item,out int consumed)||consumed!=bytes.Length||item==null&&quantity!=0)throw new InvalidDataException();
        return new ItemStack(item??ItemValue.None,quantity);
    }
    private static float HireOriginalFloat(XElement image,string name)
    {float value=float.Parse((string)image.Attribute(name),System.Globalization.CultureInfo.InvariantCulture);if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidDataException();return value;}
    private static void TryReconstructUnattemptedHire(XElement image,string text,string directory,string control,HireNativeDogLoad body,HireNativeOwnerLoad loaded)
    {
        // The positive current control record is written BEFORE any native permission.
        // Every native financial/ownership boundary advances it to Uncertain first.
        // A missing stage, old backup or equality with an unissued plan is never used.
        if(image.Attributes().Count()!=14||image.Elements().Any(row=>row.Name!="source"))return;
        string creation=(string)image.Attribute("creation");if(string.IsNullOrEmpty(creation)||RebirthCamperCharacterAccess.CurrentCreation(loaded.Actor)!=creation)return;
        bool debit=control=="AwaitingBaseline"||control=="ReadyDebit",refund=control=="ReadyRefund",ownedSaved=control=="OwnedSaved";
        string phase=debit?"before":"after",leg=ownedSaved?"ownership":refund?"reversal":"before";
        if(!HasHireStage(directory,"prepared",text)||!HasHireStage(directory,"owner-"+phase+"-saved",text)||!HireColdOwnerIntegrity(directory,phase)||
            !File.ReadAllBytes(Path.Combine(directory,"owner-"+phase+".native")).SequenceEqual(loaded.Native)||
            (int?)image.Attribute("price")!=HireItemCount||(int?)image.Attribute("deaths")!=loaded.Deaths||loaded.Actor.Died!=loaded.Deaths||
            !TryResolveOwnerId(loaded.Actor,out var owner)||owner!=(string)image.Attribute("owner"))return;
        if(control!="AwaitingBaseline"&&!MatchesHireSavedOwnership(directory,leg,text,body.Save))return;
        if(debit||!refund&&!ownedSaved){if(body.Owner!=string.Empty)return;}else if(body.Owner!=string.Empty&&body.Owner!=owner)return;
        if(!debit&&!HasHireStage(directory,"debited",text))return;
        if(refund&&!HasHireStage(directory,"reversal-saved",text))return;
        if(ownedSaved&&!HasHireStage(directory,"ownership-saved",text))return;
        if(!ReferenceEquals(loaded.Actor.inventory?.ItemGrid.items,loaded.BeltGrid)||!ReferenceEquals(loaded.Actor.bag?.ItemGrid.items,loaded.BagGrid))return;
        if(!Enum.TryParse((string)image.Attribute("order"),false,out RebirthNpcOrderState order)||!Enum.IsDefined(typeof(RebirthNpcOrderState),order))return;
        var original=new HireSettlement{World=loaded.World,Player=loaded.Actor,Dog=body.Dog,State=body.Dog.RebirthRuntimeState,
            Inventory=loaded.Actor.inventory,Bag=loaded.Actor.bag,Owner=owner,Account=loaded.Owner,Creation=creation,Deaths=loaded.Deaths,Save=body.Save,Directory=directory,Original=text,
            PreviousOrder=order,PreviousHasGuard=(bool)image.Attribute("guard"),PreviousGuard=new Vector3(HireOriginalFloat(image,"x"),HireOriginalFloat(image,"y"),HireOriginalFloat(image,"z")),
            RecoveryGeneration=RebirthNpcRecoveryGeneration.RequireCurrent(),Debited=!debit,EnteredDebit=!debit,OwnershipAttempted=refund||ownedSaved,OwnershipSaved=ownedSaved,OwnershipArchived=ownedSaved,ReversalSaved=refund,ReversalArchived=refund};
        int removed=0;
        foreach(var row in image.Elements("source"))
        {
            if(row.Attributes().Count()!=4||row.Elements().Count()!=2||row.Elements().Any(node=>node.Name!="beforeItem"&&node.Name!="afterItem"))return;
            bool belt=(bool)row.Attribute("belt");int slot=(int)row.Attribute("slot");var grid=belt?loaded.BeltGrid:loaded.BagGrid;var references=belt?loaded.BeltRefs:loaded.BagRefs;
            if(grid==null||references==null||slot<0||slot>=grid.Length||slot>=references.Length||!ReferenceEquals(grid[slot],references[slot]))return;
            var before=DecodeHireOriginalItem(row,"beforeItem","beforeCount");var after=DecodeHireOriginalItem(row,"afterItem","afterCount");
            if(before.count<=after.count||before.itemValue.type!=ItemClass.GetItem(HireItemName).type||!RebirthStationGridIngredients.IsSameStackSnapshot(grid[slot],debit?before:after))return;
            removed=checked(removed+before.count-after.count);
            if(original.Sources.Any(source=>source.Belt==belt&&source.Slot==slot))return;
            original.Sources.Add(new HireDebitSource{Belt=belt,Slot=slot,Grid=grid,Before=before,After=after,OriginalReference=grid[slot],AppliedReference=debit?null:grid[slot]});
        }
        if(IsRemoteHire(original)&&!ReadRemoteHireRequest(original,loaded.Data))return;
        if(removed!=HireItemCount||original.Sources.Count<1||original.Sources.Count>HireItemCount||!HireCurrent(original)||!ReserveHireOwner(original))return;
        lock(HireOwnerGate)
        {
            HireOwnerFiles[original][phase]=new HireOwnerCheckpoint{Original=original,Phase=phase,Root=Path.GetDirectoryName(loaded.Path),Name=loaded.Owner,Path=loaded.Path,Native=loaded.Native,Accepted=true,Saved=true,Archived=true};
            if(phase=="after")HireOwnerFiles[original]["before"]=new HireOwnerCheckpoint{Original=original,Phase="before",Archived=true};
        }
        HireSettlements.Add(original.State.StableId,original);TransferHireNativeLoad(loaded,body,(string)image.Attribute("operation"));
        if(ownedSaved)
        {
            if(original.State.OwnershipKind!=RebirthNpcOwnershipKind.None&&(original.State.OwnershipKind!=RebirthNpcOwnershipKind.Player||original.State.OwnerId!=owner))return;
            var prior=CurrentHireMutation;CurrentHireMutation=original;original.Busy=true;
            try
            {
                // Reconstruct the already-saved owner projection only. The saved full
                // ownership generation and native dog/player load witnesses are required;
                // no debit, reversal or refund is replayed by this projection.
                if(!ApplyHireOwner(original,RebirthNpcOwnershipKind.Player,owner).Succeeded||!ApplyHireOrder(original,RebirthNpcOrderState.Follow,Vector3.zero,false).Succeeded)return;
                SynchronizeLegacyOwnerProjection(original.Dog);
            }
            finally{original.Busy=false;CurrentHireMutation=prior;}
        }
        FinishHireSettlement(original,out var reason);
    }
}
