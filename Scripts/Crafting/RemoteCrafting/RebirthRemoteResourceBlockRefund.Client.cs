using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourceBlockRefund
{
    private static RebirthRemoteResourceClientOriginalJournal ClientJournal(string name)=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH",name));
    private static bool ClientRead(string name,RebirthRemoteResourceClientOriginal.Intent original,out XElement body,out RebirthCamperTransferJournal.Phase phase)
    {
        body=null;phase=default;var record=ClientJournal(name).Read(original.Operation);
        if(record==null||record.Vehicle!=original.Operation||record.OwnerCreation!=original.Creation||record.Revision!=0||record.Before.Length>24*1024*1024||!record.Before.SequenceEqual(record.After))return false;
        body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),24*1024*1024);phase=record.State;return true;
    }
    private static bool ClientPublish(string name,RebirthRemoteResourceClientOriginal.Intent original,XElement body,bool terminal)
    {
        byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));if(bytes.Length>24*1024*1024)return false;var journal=ClientJournal(name);
        var record=journal.Prepare(original.Operation,original.Operation,original.Creation,0,bytes,bytes);
        if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes))return false;
        if(terminal&&record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
        if(terminal&&record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
        return record.State==(terminal?RebirthCamperTransferJournal.Phase.Applied:RebirthCamperTransferJournal.Phase.Prepared);
    }
    private static XElement Claim(RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string noUse,string command,string decline)
        =>new XElement("blockOwed",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("source",source.ToString("N")),new XAttribute("binding",binding),
            new XAttribute("noUse",noUse),new XAttribute("command",command??string.Empty),new XAttribute("decline",decline??string.Empty));
    private static bool ClaimPayload(RebirthRemoteResourceClientOriginal.Intent original,XElement claim,Guid source,string binding,out ItemStack[] payload,out string noUse)
    {
        payload=null;noUse=null;
        if(!RebirthRemoteResourceBlockPlan.Validate(original)||!RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out _,out var paidSource,out var paidBinding,out var remote)||source!=paidSource||binding!=paidBinding)return false;
        string command=(string)claim.Attribute("command"),decline=(string)claim.Attribute("decline");noUse=(string)claim.Attribute("noUse");
        if(string.IsNullOrEmpty(command)&&string.IsNullOrEmpty(decline))
        {
            bool unissued=noUse==RebirthRemoteResourceBlockRejection.UnissuedProof(original,binding);
            bool issued=RebirthRemoteResourceBlockOriginal.TryReadPreparedRejection(original,out var prepared)&&
                RebirthRemoteResourceBlockOriginal.TryRead(original,out var action,out _)&&noUse==RebirthRemoteResourceBlockRejection.IssuedProof(original,binding,action)&&
                XNode.DeepEquals(prepared,RebirthRemoteResourceBlockOriginal.PreparedRejectionBody(original,action));
            bool ended=RebirthRemoteResourceBlockOriginal.TryRead(original,out var endedAction,out _)&&noUse==RebirthRemoteResourceBlockAdoption.EndedOwnerProof(original,binding,endedAction);
            if(!unissued&&!issued&&!ended)return false;
            if(ended)
            {if(!RebirthRemoteResourceBlockPlan.Expected(original,remote,out _,out _,out var local))return false;payload=local.Concat(remote).Select(stack=>stack.Clone()).ToArray();}
            else payload=remote.Select(stack=>stack.Clone()).ToArray();
        }
        else
        {
            if(command?.Length!=64||decline?.Length!=64||command.Any(c=>!Uri.IsHexDigit(c))||decline.Any(c=>!Uri.IsHexDigit(c))||
                !RebirthRemoteResourceBlockOriginal.TryReadCapturedCommand(original,out _,out _)||!RebirthRemoteResourceBlockPlan.Expected(original,remote,out _,out var after,out var local))return false;
            string owner2=RebirthRemoteResourceDurableJournal.Digest("native-block-owner2|"+original.Digest+"|"+source.ToString("N")+"|"+binding+"|"+RebirthRemoteResourceClientOriginal.ActorImage(after));
            var typed=new XElement("blockHostDeclined",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("command",command),
                new XAttribute("source",source.ToString("N")),new XAttribute("binding",binding),new XAttribute("ownerProof",owner2));
            if(RebirthRemoteResourceDurableJournal.Digest(typed.ToString(SaveOptions.DisableFormatting))!=decline||
                noUse!=RebirthRemoteResourceDurableJournal.Digest("native-block-declined-unused|"+original.Digest+"|"+binding+"|"+decline))return false;
            payload=local.Concat(remote).Select(stack=>stack.Clone()).ToArray();
        }
        return XNode.DeepEquals(claim,Claim(original,source,binding,noUse,command,decline));
    }
    internal static bool HasClientOwed(RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,out ItemStack[] payload,out string noUse)
    {
        payload=null;noUse=null;
        try{return ClientRead("RemoteBlockOwed",original,out var claim,out var phase)&&phase==RebirthCamperTransferJournal.Phase.Applied&&ClaimPayload(original,claim,source,binding,out payload,out noUse);}
        catch{return false;}
    }
    internal static bool TryReadClientRequest(RebirthRemoteResourceClientOriginal.Intent original,out XElement request)
        =>ClientRead("RemoteBlockRefundChildren",original,out request,out _);
    internal static bool AcceptOwed(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string noUse,string command,string decline,Func<bool> authenticated)
    {
        try
        {
            var claim=Claim(original,source,binding,noUse,command,decline);
            if(!RebirthRemoteResourceBlockAdoption.QueryScope(player,original)||authenticated?.Invoke()!=true||!ClaimPayload(original,claim,source,binding,out _,out _)||
                !ClientPublish("RemoteBlockOwed",original,claim,true))return false;
            return ClientRead("RemoteBlockRefundChildren",original,out _,out _)||RebirthRemoteResourceOwnerAction.ReleaseBlockOwed(player,original,source,binding);
        }
        catch{return false;}
    }
    internal static bool IsClientPrepared(RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,RebirthCamperEnergyActorImage before,RebirthCamperEnergyActorImage after)
    {
        try{return HasClientOwed(original,source,binding,out var payload,out var noUse)&&ClientRead("RemoteBlockRefundChildren",original,out var body,out _)&&
            Plan(before,payload,body.Element("beltPolicy"),out var planned)&&after.Matches(planned)&&XNode.DeepEquals(body,Body(original,source,binding,noUse,before,after,(int?)body.Attribute("deaths")??original.Deaths,body.Element("beltPolicy")));}
        catch{return false;}
    }
    internal static bool PrepareClient(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,Func<bool> authenticated,out XElement request)
    {
        request=null;
        try
        {
            if(!Current(player,original)||authenticated?.Invoke()!=true||!HasClientOwed(original,source,binding,out var payload,out var noUse))return false;
            if(!ClientRead("RemoteBlockRefundChildren",original,out request,out _))
            {
                if(!RebirthRemoteResourceOwnerAction.CanPrepareNativeOriginal(player))return false;
                var before=RebirthCamperEnergyActorImage.Capture(player);var policy=CaptureBeltPolicy(player);if(!Plan(before,payload,policy,out var after))return false;
                request=Body(original,source,binding,noUse,before,after,player.Died,policy);if(!ClientPublish("RemoteBlockRefundChildren",original,request,false))return false;
            }
            var initial=RebirthRemoteResourceClientOriginal.ReadActor(request.Element("beforeActor").Value);var final=RebirthRemoteResourceClientOriginal.ReadActor(request.Element("afterActor").Value);
            if(!IsClientPrepared(original,source,binding,initial,final)||!RebirthRemoteResourceOwnerAction.TryHoldBlockRefund(player,original,source,binding,initial,final,authenticated))return false;
            if(initial.Matches(RebirthCamperEnergyActorImage.Capture(player)))
            {player.Buffs.SetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(Child(original)),1f,true);GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);}
            return true;
        }
        catch{request=null;return false;}
    }
    internal static bool ApplyClient(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,XElement request,Func<bool> actualHostPermission)
    {
        try
        {
            if(!Current(player,original)||DeliveryDeaths(original)!=player.Died||actualHostPermission?.Invoke()!=true||!Guid.TryParseExact((string)request.Attribute("source"),"N",out var source))return false;
            var before=RebirthRemoteResourceClientOriginal.ReadActor(request.Element("beforeActor").Value);var after=RebirthRemoteResourceClientOriginal.ReadActor(request.Element("afterActor").Value);string binding=(string)request.Attribute("binding");
            if(!IsClientPrepared(original,source,binding,before,after)||!RebirthRemoteResourceOwnerAction.TryHoldBlockRefund(player,original,source,binding,before,after,actualHostPermission))return false;
            var journal=ClientJournal("RemoteBlockRefundChildren");var record=journal.Read(original.Operation);
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State!=RebirthCamperTransferJournal.Phase.Uncertain)return false;
            for(int slot=0;slot<after.Belt.Length;slot++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(before.Belt[slot],after.Belt[slot])&&
                !RebirthRemoteResourceOwnerAction.WriteOriginalSlot(player,original.Operation,player.inventory.ItemGrid,slot,after.Belt[slot]))return false;
            for(int slot=0;slot<after.Bag.Length;slot++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(before.Bag[slot],after.Bag[slot])&&
                !RebirthRemoteResourceOwnerAction.WriteOriginalSlot(player,original.Operation,player.bag.ItemGrid,slot,after.Bag[slot]))return false;
            if(!after.Matches(RebirthCamperEnergyActorImage.Capture(player)))return false;
            player.Buffs.SetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(Child(original)),2f,true);GameManager.Instance.SaveLocalPlayerData();GameManager.Instance.TriggerSendOfLocalPlayerDataFile(0f);return true;
        }
        catch{return false;}
    }
    internal static bool IsClientApplied(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,XElement request)
    {
        try
        {
            if(!RebirthRemoteResourceBlockAdoption.QueryScope(player,original)||!Guid.TryParseExact((string)request.Attribute("source"),"N",out var source)||
                !ClientRead("RemoteBlockRefundChildren",original,out var retained,out var phase)||phase!=RebirthCamperTransferJournal.Phase.Uncertain||!XNode.DeepEquals(request,retained))return false;
            var before=RebirthRemoteResourceClientOriginal.ReadActor(request.Element("beforeActor").Value);var after=RebirthRemoteResourceClientOriginal.ReadActor(request.Element("afterActor").Value);
            int deaths=(int?)request.Attribute("deaths")??original.Deaths;
            return deaths<=player.Died&&IsClientPrepared(original,source,(string)request.Attribute("binding"),before,after)&&(deaths<player.Died||after.Matches(RebirthCamperEnergyActorImage.Capture(player)))&&
                player.Buffs.GetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(Child(original)))==2f;
        }
        catch{return false;}
    }
    internal static bool HasClientTerminal(RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string proof)
    {
        try
        {
            if(!HasClientOwed(original,source,binding,out _,out var noUse)||!ClientRead("RemoteBlockRefundChildren",original,out var request,out _)||
                !ClientRead("RemoteBlockRefundTerminals",original,out var terminal,out var phase)||phase!=RebirthCamperTransferJournal.Phase.Applied||
                TerminalProof(binding,noUse,request)!=proof)return false;
            var expected=new XElement("blockRefundTerminal",new XAttribute("original",original.Digest),new XAttribute("source",source.ToString("N")),new XAttribute("binding",binding),
                new XAttribute("refundDigest",RebirthRemoteResourceDurableJournal.Digest(request.ToString(SaveOptions.DisableFormatting))),new XAttribute("proof",proof));
            return XNode.DeepEquals(terminal,expected);
        }
        catch{return false;}
    }
    internal static bool CompleteClient(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string proof,Func<bool> authenticatedTerminal)
    {
        try
        {
            if(!RebirthRemoteResourceBlockAdoption.QueryScope(player,original)||authenticatedTerminal?.Invoke()!=true||!HasClientOwed(original,source,binding,out _,out var noUse)||
                !ClientRead("RemoteBlockRefundChildren",original,out var request,out _)||TerminalProof(binding,noUse,request)!=proof)return false;
            var terminal=new XElement("blockRefundTerminal",new XAttribute("original",original.Digest),new XAttribute("source",source.ToString("N")),new XAttribute("binding",binding),
                new XAttribute("refundDigest",RebirthRemoteResourceDurableJournal.Digest(request.ToString(SaveOptions.DisableFormatting))),new XAttribute("proof",proof));
            if(!ClientPublish("RemoteBlockRefundTerminals",original,terminal,true))return false;
            var journal=ClientJournal("RemoteBlockRefundChildren");var record=journal.Read(original.Operation);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(original.Operation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            if(record.State!=RebirthCamperTransferJournal.Phase.Applied)return false;
            return RebirthRemoteResourceOwnerAction.ReleaseBlockRefundTerminal(player,original,source,binding,proof);
        }
        catch{return false;}
    }
}
