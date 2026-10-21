using System;
using System.Text;

public sealed partial class XUiC_RebirthCompanions
{
    private bool humanDismissPending;
    private void DismissHumanFromManager(RebirthCompanionListEntry selected)
    {
        if(humanDismissPending)return;
        int generation=requestGeneration;string target=selected.Id;humanDismissPending=true;
        RebirthNpcInteractionNetworkClient.RequestOpen(selected.EntityId,delegate(RebirthNpcInteractionProjection projection,RebirthNpcInteractionCommandResponse ignored,string error)
        {
            if(!managementOpen||generation!=requestGeneration||selectedId!=target)
            {humanDismissPending=false;if(projection!=null)RebirthNpcInteractionNetworkClient.RequestClose(projection.SessionKey);return;}
            if(projection==null){humanDismissPending=false;Set(feedback,error);return;}
            SubmitHumanDismiss(projection,"operation=prepare",generation,target);
        });
    }
    private void SubmitHumanDismiss(RebirthNpcInteractionProjection projection,string payload,int generation,string target)
    {
        var command=new RebirthNpcInteractionCommand{RequestId=projection.SessionKey+":manager-dismiss:"+Guid.NewGuid().ToString("N"),SessionKey=projection.SessionKey,
            Kind=RebirthNpcInteractionCommandKind.Dismiss,ExpectedRevision=projection.RuntimeRevision,Payload=payload};
        RebirthNpcInteractionNetworkClient.RequestCommand(command,delegate(RebirthNpcInteractionProjection refreshed,RebirthNpcInteractionCommandResponse response,string error)
        {
            if(!managementOpen||generation!=requestGeneration||selectedId!=target)
            {FinishHumanDismiss(projection);return;}
            Set(feedback,!string.IsNullOrEmpty(error)?error:response?.Detail??string.Empty);
            if(response?.Status==RebirthNpcInteractionCommandStatus.Accepted)
            {
                var values=RebirthSurvivorHirePayment.Parse(response.ResponsePayload);
                if(values.TryGetValue("return",out var encoded))
                {ReturnHumanLegacy(refreshed??projection,Encoding.UTF8.GetString(Convert.FromBase64String(encoded)).Split('\n'),0,generation,target);return;}
            }
            FinishHumanDismiss(projection);refreshTimer=0.05f;
        });
    }
    private void ReturnHumanLegacy(RebirthNpcInteractionProjection projection,string[] rows,int index,int generation,string target)
    {
        if(!managementOpen||generation!=requestGeneration||selectedId!=target){FinishHumanDismiss(projection);return;}
        if(index>=rows.Length){SubmitHumanDismiss(projection,"operation=commit",generation,target);return;}
        LogisticsTransferService.Request(QuickStackRadialAction.CompanionInventoryPull,target,rows[index],()=>ReturnHumanLegacy(projection,rows,index+1,generation,target));
    }
    private void FinishHumanDismiss(RebirthNpcInteractionProjection projection)
    {humanDismissPending=false;RebirthNpcInteractionNetworkClient.RequestClose(projection.SessionKey);}
}
