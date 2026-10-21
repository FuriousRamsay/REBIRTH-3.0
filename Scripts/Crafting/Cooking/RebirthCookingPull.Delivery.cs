using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

public static partial class RebirthCookingLegacyPull
{
    // Cold originals have no live UI callback. Preserve their saved result and
    // let only an existing native grid claim or historical return retire it.
    private static void DeliverOriginalResult(Original original,List<ItemStack> items)
    {
        if(!CurrentDeliverySession(original))
        {
            if(CurrentPullSession(original)&&original.Deaths>=0&&original.Deaths<original.Player.Died&&items?.Count>0)
                RebirthCookingGridCustody.ReturnHistoricalFetched(original.Player,original.Request,items,original.Deaths);
            return;
        }
        try
        {
            if(RebirthCookingGridCustody.HasFetchClaim(original.Player,original.Request,out bool complete))
            {
                if(complete)RetireLocalResult(original);
                return;
            }
        }
        catch(Exception error)
        {
            // Saved request/result bytes and any original grid claim remain
            // the recovery source. No new withdrawal or fallback grant here.
            RemoteResourceDiagnostics.Write("Cooking pull delivery retained: "+error.GetType().Name);
        }
    }

    private static bool RetireTerminalResult(Original original,string resourceWorld,string resourceOperation,string resourceBinding,
        string gridChild="",string gridHash="",string ownerProof="",string disposition="")
    {
        if(!CurrentPullSession(original))return false;
        try
        {
            var operation=Operation(original.Player,original.Owner,original.Request);var known=Results().Read(operation);
            if(known==null||known.OwnerCreation!=original.Creation||!known.Before.SequenceEqual(known.After))return false;
            var proof=XElement.Parse(Encoding.UTF8.GetString(known.Before));
            if(proof.Name!="cookingPullOriginal"||ReadRequestDeaths(proof)!=original.Deaths||(string)proof.Attribute("creation")!=original.Creation||
                known.Operation!=operation||(string)proof.Attribute("world")!=original.World.worldState.Guid||(string)proof.Attribute("owner")!=original.Owner||
                (long?)proof.Attribute("request")!=original.Request||(string)proof.Attribute("sourceWorld")!=resourceWorld||
                (string)proof.Attribute("sourceOperation")!=resourceOperation||(string)proof.Attribute("sourceBinding")!=resourceBinding)return false;
            bool noPayment=(bool?)proof.Attribute("noPayment")==true&&!string.IsNullOrEmpty((string)proof.Attribute("error"))&&
                RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(proof.Element("items").Value)).Length==0;
            if(!noPayment&&(!RebirthCookingGridCustody.HasFetchClaim(original.Player,original.Request,out bool complete)||!complete)&&
                !RebirthCookingGridCustody.RetireFetchedTerminal(original.Player,original.Request,resourceWorld,resourceOperation,resourceBinding,gridChild,gridHash,ownerProof,disposition))return false;
            return RetireLocalResult(original);
        }
        catch(Exception error){RemoteResourceDiagnostics.Write("Cooking pull terminal retained: "+error.GetType().Name);return false;}
    }
    private static bool RetireLocalResult(Original original)
    {
        if(!CurrentPullSession(original))return false;
        try
        {
            CompleteGridOriginal(original.Player,original.Request);
            // An interrupted result-then-request metadata publication is never a
            // second delivery permission. Both journals must positively retire.
            var operation=Operation(original.Player,original.Owner,original.Request);
            var result=Results().Read(operation);var request=Requests().Read(operation);
            if(result?.State!=RebirthCamperTransferJournal.Phase.Applied||request?.State!=RebirthCamperTransferJournal.Phase.Applied)return false;
            waiting.Remove(original.Request);return true;
        }
        catch(Exception error){RemoteResourceDiagnostics.Write("Cooking pull local retirement retained: "+error.GetType().Name);return false;}
    }
}
