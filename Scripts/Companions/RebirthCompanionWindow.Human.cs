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
    // B03: reverse lesson. Same open-session + Teach command path as the forward lesson; the payload selects
    // the direction and TeachHandler (server) validates ownership, proximity, skill, Theory and cooldown.
    private bool humanLearnPending;
    private void LearnFromHumanInManager()
    {
        var selected=Selected();
        if(humanLearnPending||selected==null||selected.IsDog||selected.Kind!=RebirthCompanionTargetKind.Npc)return;
        int generation=requestGeneration;string target=selected.Id;humanLearnPending=true;
        RebirthNpcInteractionNetworkClient.RequestOpen(selected.EntityId,delegate(RebirthNpcInteractionProjection projection,RebirthNpcInteractionCommandResponse ignored,string error)
        {
            if(projection==null){humanLearnPending=false;Set(feedback,error);return;}
            if(!managementOpen||generation!=requestGeneration||selectedId!=target)
            {humanLearnPending=false;RebirthNpcInteractionNetworkClient.RequestClose(projection.SessionKey);return;}
            var command=new RebirthNpcInteractionCommand{RequestId=projection.SessionKey+":manager-learn:"+Guid.NewGuid().ToString("N"),SessionKey=projection.SessionKey,
                Kind=RebirthNpcInteractionCommandKind.Teach,ExpectedRevision=projection.RuntimeRevision,Payload="direction=npc-to-player"};
            RebirthNpcInteractionNetworkClient.RequestCommand(command,delegate(RebirthNpcInteractionProjection refreshed,RebirthNpcInteractionCommandResponse response,string commandError)
            {
                humanLearnPending=false;
                if(managementOpen&&generation==requestGeneration&&selectedId==target)
                    Set(feedback,!string.IsNullOrEmpty(commandError)?commandError:response?.Detail??string.Empty);
                RebirthNpcInteractionNetworkClient.RequestClose(projection.SessionKey);
            });
        });
    }
    private void FinishHumanDismiss(RebirthNpcInteractionProjection projection)
    {humanDismissPending=false;RebirthNpcInteractionNetworkClient.RequestClose(projection.SessionKey);}
}
