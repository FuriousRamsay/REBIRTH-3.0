using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

#nullable disable

public static class RebirthTeachingUiService
{
    public const string WindowGroupName="rebirthTeaching";
    private static long nextSubjectsRequestId = 1L;
    public static bool Open(XUi xui,int targetEntityId,string targetName)
    {
        if(xui==null||targetEntityId<0)return false;
        XUiController group=xui.FindWindowGroupByName(WindowGroupName);
        XUiC_RebirthTeaching controller=group!=null?group.GetChildByType<XUiC_RebirthTeaching>():null;
        if(controller==null)return false;
        long requestId=nextSubjectsRequestId++;if(nextSubjectsRequestId<=0L)nextSubjectsRequestId=1L;
        controller.Prepare(targetEntityId,targetName,requestId);
        xui.playerUI.windowManager.Open((GUIWindow)controller.windowGroup,true);
        EntityPlayerLocal player=xui.playerUI!=null?xui.playerUI.entityPlayer:null;if(player==null)return true;
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(c!=null&&c.IsServer)RebirthTeachingService.ProcessSubjectsRequest(player.entityId,targetEntityId,requestId);
        else if(c!=null)c.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthTeachingSubjectsRequest>().Setup(player.entityId,targetEntityId,requestId));
        return true;
    }
    public static void ReceiveSubjects(long requestId,int studentId,string targetName,string[] skillIds)
    {
        World w=GameManager.Instance!=null?GameManager.Instance.World:null;EntityPlayerLocal p=w!=null?w.GetPrimaryPlayer():null;XUi x=p!=null&&p.PlayerUI!=null?p.PlayerUI.xui:null;if(x==null)return;
        XUiController group=x.FindWindowGroupByName(WindowGroupName);XUiC_RebirthTeaching c=group!=null?group.GetChildByType<XUiC_RebirthTeaching>():null;if(c!=null)c.ApplySubjects(requestId,studentId,targetName,skillIds);
    }
}

[Preserve]
public sealed class XUiC_RebirthTeaching:XUiController
{
    private sealed class Row{public XUiController Root;public XUiController Button;public XUiV_Label Label;public XUiV_Sprite Selected;public string SkillId=string.Empty;}
    private readonly Row[] rows=new Row[48];private long subjectsRequestId;private int studentId=-1;private string studentName=string.Empty;private string selectedSkill=string.Empty;private XUiV_Label targetLabel,selectedLabel,statusLabel;private XUiController offerButton;
    public override void Init(){base.Init();targetLabel=Label("teachingTargetName");selectedLabel=Label("teachingSelectedSubject");statusLabel=Label("teachingStatus");offerButton=GetChildById("btnTeachingOffer");if(offerButton!=null)offerButton.OnPress+=delegate{Offer();};XUiController close=GetChildById("btnTeachingClose");if(close!=null)close.OnPress+=delegate{Close();};for(int i=0;i<rows.Length;i++){string id="teachSubjectRow"+i.ToString("00");XUiController root=GetChildById(id);Row row=rows[i]=new Row{Root=root};if(root==null)continue;row.Button=root.GetChildById("teachSubjectButton"+i.ToString("00"));XUiController l=root.GetChildById("teachSubjectLabel"+i.ToString("00"));row.Label=l!=null?l.ViewComponent as XUiV_Label:null;XUiController s=root.GetChildById("teachSubjectSelected"+i.ToString("00"));row.Selected=s!=null?s.ViewComponent as XUiV_Sprite:null;int captured=i;if(row.Button!=null)row.Button.OnPress+=delegate{Select(captured);};}}
    public void Prepare(int targetId,string targetName,long requestId){subjectsRequestId=requestId;studentId=targetId;studentName=targetName??string.Empty;selectedSkill=string.Empty;Set(targetLabel,studentName);Set(selectedLabel,"--");Set(statusLabel,Localization.Get("xuiRebirthTeachingLoading"));for(int i=0;i<rows.Length;i++){rows[i].SkillId=string.Empty;if(rows[i].Root!=null&&rows[i].Root.ViewComponent!=null)rows[i].Root.ViewComponent.IsVisible=false;if(rows[i].Selected!=null)rows[i].Selected.IsVisible=false;}SetOffer(false);}
    public void ApplySubjects(long requestId,int targetId,string targetName,string[] ids){if(requestId!=subjectsRequestId||targetId!=studentId)return;if(!string.IsNullOrEmpty(targetName)){studentName=targetName;Set(targetLabel,studentName);}List<string> list=new List<string>();if(ids!=null)for(int i=0;i<ids.Length;i++)if(!string.IsNullOrEmpty(ids[i]))list.Add(ids[i]);list.Sort(delegate(string a,string b){return string.Compare(RebirthTeachingService.FriendlySkill(a),RebirthTeachingService.FriendlySkill(b),StringComparison.CurrentCultureIgnoreCase);});for(int i=0;i<rows.Length;i++){Row r=rows[i];bool show=i<list.Count;if(r.Root!=null&&r.Root.ViewComponent!=null)r.Root.ViewComponent.IsVisible=show;if(!show){r.SkillId=string.Empty;if(r.Selected!=null)r.Selected.IsVisible=false;continue;}r.SkillId=list[i];Set(r.Label,RebirthTeachingService.FriendlySkill(r.SkillId));}Set(statusLabel,list.Count==0?Localization.Get("xuiRebirthTeachingNoSubjects"):Localization.Get("xuiRebirthTeachingReady"));}
    private void Select(int index){if(index<0||index>=rows.Length||string.IsNullOrEmpty(rows[index].SkillId))return;selectedSkill=rows[index].SkillId;for(int i=0;i<rows.Length;i++)if(rows[i].Selected!=null)rows[i].Selected.IsVisible=i==index;Set(selectedLabel,RebirthTeachingService.FriendlySkill(selectedSkill));SetOffer(true);}
    private void Offer(){if(studentId<0||string.IsNullOrEmpty(selectedSkill)||xui==null||xui.playerUI==null)return;EntityPlayerLocal p=xui.playerUI.entityPlayer;if(p==null)return;ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;if(c!=null&&c.IsServer)RebirthTeachingService.ProcessOfferRequest(p.entityId,studentId,selectedSkill);else if(c!=null)c.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthTeachingOfferRequest>().Setup(p.entityId,studentId,selectedSkill));Set(statusLabel,Localization.Get("xuiRebirthTeachingOfferSent"));}
    private void Close(){if(xui!=null&&xui.playerUI!=null&&windowGroup!=null)xui.playerUI.windowManager.Close((GUIWindow)windowGroup);}
    private XUiV_Label Label(string id){XUiController c=GetChildById(id);return c!=null?c.ViewComponent as XUiV_Label:null;}private static void Set(XUiV_Label l,string v){if(l!=null)l.Text=v??string.Empty;}private void SetOffer(bool enabled){if(offerButton!=null&&offerButton.ViewComponent!=null)offerButton.ViewComponent.Enabled=enabled;}
}

public static class RebirthTeachingOfferUiService
{
    public const string WindowGroupName="rebirthTeachingOffer";
    public static void Receive(long offerId,int instructorId,string instructorName,string skillId){World w=GameManager.Instance!=null?GameManager.Instance.World:null;EntityPlayerLocal p=w!=null?w.GetPrimaryPlayer():null;XUi x=p!=null&&p.PlayerUI!=null?p.PlayerUI.xui:null;if(x==null)return;XUiController group=x.FindWindowGroupByName(WindowGroupName);XUiC_RebirthTeachingOffer c=group!=null?group.GetChildByType<XUiC_RebirthTeachingOffer>():null;if(c==null)return;c.Prepare(offerId,instructorId,instructorName,skillId);x.playerUI.windowManager.Open((GUIWindow)c.windowGroup,true);}
}

[Preserve]
public sealed class XUiC_RebirthTeachingOffer:XUiController
{
    private long offerId;private bool responded;private XUiV_Label body;
    public override void Init(){base.Init();XUiController b=GetChildById("teachingOfferBody");body=b!=null?b.ViewComponent as XUiV_Label:null;XUiController accept=GetChildById("btnTeachingOfferAccept");if(accept!=null)accept.OnPress+=delegate{Respond(true);};XUiController decline=GetChildById("btnTeachingOfferDecline");if(decline!=null)decline.OnPress+=delegate{Respond(false);};}
    public void Prepare(long id,int instructorId,string instructorName,string skillId){offerId=id;responded=false;string template=Localization.Get("xuiRebirthTeachingOfferBody");string text;try{text=string.Format(template,instructorName??"Another survivor",RebirthTeachingService.FriendlySkill(skillId));}catch{text=(instructorName??"Another survivor")+" wants to teach you "+RebirthTeachingService.FriendlySkill(skillId)+".";}if(body!=null)body.Text=text;}
    public override void OnClose(){if(!responded&&offerId>0L)Send(false);offerId=0L;base.OnClose();}
    private void Respond(bool accept){if(responded||offerId<=0L)return;responded=true;Send(accept);if(xui!=null&&xui.playerUI!=null&&windowGroup!=null)xui.playerUI.windowManager.Close((GUIWindow)windowGroup);}
    private void Send(bool accept){if(xui==null||xui.playerUI==null)return;EntityPlayerLocal p=xui.playerUI.entityPlayer;if(p==null)return;ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;if(c!=null&&c.IsServer)RebirthTeachingService.ProcessOfferResponse(p.entityId,offerId,accept);else if(c!=null)c.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthTeachingOfferResponse>().Setup(p.entityId,offerId,accept));}
}
