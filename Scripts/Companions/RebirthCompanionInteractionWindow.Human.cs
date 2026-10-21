using System;
using UnityEngine;

// Human hiring reuses the existing companion card. Dog state, actions and
// payment logic remain separate; only the established presentation is shared.
public sealed partial class XUiC_RebirthCompanionInteractionWindow
{
    private RebirthNpcInteractionProjection humanProjection;
    private int humanEntity=-1,humanGeneration;
    private long humanSequence;
    private bool humanPending;
    private string humanPrice;

    internal void PrepareHuman(int entityId,RebirthNpcInteractionProjection projection)
    {
        Prepare((EntityRebirthDogCompanion)null);
        humanEntity=entityId;humanProjection=projection;humanPrice=null;humanPending=false;
        if(cardOpen)RenderHuman();
    }
    private void CloseHuman()
    {
        humanGeneration++;
        if(humanProjection!=null)RebirthNpcInteractionNetworkClient.RequestClose(humanProjection.SessionKey);
        humanProjection=null;humanEntity=-1;humanPrice=null;humanPending=false;
    }
    private void RenderHuman()
    {
        var npc=xui.playerUI.entityPlayer.world.GetEntity(humanEntity) as EntityRebirthHumanoidNPC;
        Set(dogName,humanProjection.DisplayName);Set(dogBreed,Localization.Get("xuiRebirthSurvivorFollowerType"));Set(dogLevel,string.Empty);
        if(dogIcon!=null)dogIcon.IsVisible=false;
        Set(dogStatusLabel,humanProjection.StatusText);
        Set(dogHealth,npc==null?string.Empty:Localization.Get("xuiRebirthHealth")+": "+npc.Health+" / "+npc.GetMaxHealth());
        Set(dogDistance,npc==null?string.Empty:Localization.Get("xuiRebirthDistance")+": "+Vector3.Distance(npc.position,xui.playerUI.entityPlayer.position).ToString("0.0")+" m");
        SetVisible(renameText,false);SetVisible(btnRename,false);
        actions.Clear();
        if((humanProjection.Permissions&RebirthNpcInteractionPermissions.Hire)!=0)
            AddAction(DirectActionKind.Hire,"xuiRebirthNpcHire","ui_game_symbol_allies",!humanPending&&npc!=null&&!npc.IsDead());
        actionOffset=0;RenderActions();
    }
    private void HireHuman()
    {
        if(humanPending||humanProjection==null||(humanProjection.Permissions&RebirthNpcInteractionPermissions.Hire)==0)return;
        GameManager.Instance.SaveLocalPlayerData();
        SubmitHumanHire(humanPrice==null?"operation=quote":"operation=prepare;price="+humanPrice);
    }
    private void SubmitHumanHire(string payload)
    {
        var original=humanProjection;if(original==null)return;
        int generation=humanGeneration;humanPending=true;RenderHuman();
        var command=new RebirthNpcInteractionCommand{RequestId=original.SessionKey+":companion-hire:"+(++humanSequence),SessionKey=original.SessionKey,
            Kind=RebirthNpcInteractionCommandKind.Hire,Payload=payload,ExpectedRevision=original.RuntimeRevision};
        RebirthNpcInteractionNetworkClient.RequestCommand(command,delegate(RebirthNpcInteractionProjection refreshed,RebirthNpcInteractionCommandResponse response,string error)
        {
            if(generation!=humanGeneration||humanProjection==null||humanProjection.SessionKey!=original.SessionKey)return;
            humanPending=false;
            if(refreshed!=null)humanProjection=refreshed;
            Set(feedback,!string.IsNullOrEmpty(error)?error:response?.Detail??Localization.Get("xuiRebirthNpcTargetUnavailable"));
            if(response?.Status==RebirthNpcInteractionCommandStatus.Accepted)
            {
                var values=RebirthSurvivorHirePayment.Parse(response.ResponsePayload);
                if(values.TryGetValue("price",out var price))humanPrice=price;
                if(values.ContainsKey("prepared"))
                {
                    if(RebirthSurvivorHirePayment.TryPayLocal(xui.playerUI.entityPlayer,humanProjection,response.ResponsePayload,humanPrice,out var operation,out var paymentError))
                    {SubmitHumanHire("operation=commit;id="+operation);return;}
                    Set(feedback,paymentError);
                }
                if(values.ContainsKey("settled"))
                {
                    string selected="N:"+humanProjection.NpcId;
                    xui.playerUI.windowManager.Close((GUIWindow)windowGroup);
                    RebirthCompanionUiService.Open(xui,selected,"Overview");return;
                }
            }
            RenderHuman();
        });
    }
}
