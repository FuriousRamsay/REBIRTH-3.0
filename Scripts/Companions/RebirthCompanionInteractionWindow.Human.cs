using System;
using UnityEngine;

// Human hiring shares the existing world-interaction lifecycle and payment
// flow, while its read-only inspection panel uses the full Rebirth window shell.
public sealed partial class XUiC_RebirthCompanionInteractionWindow
{
    private RebirthNpcInteractionProjection humanProjection;
    private int humanEntity=-1,humanGeneration;
    private long humanSequence;
    private bool humanPending;
    private string humanPrice;
    private bool humanQuoteRequested;
    private float humanQuoteRetryAt;
    private byte humanTab;
    private XUiController humanHirePanel, humanBackgroundPanel, humanSkillsPanel, humanEquipmentPanel, humanInventoryPanel;
    private readonly XUiController[] humanTabButtons = new XUiController[4];
    private XUiV_Label humanHireName, humanHireType, humanHireStatus, humanHireHealth, humanHirePrice, humanHireFeedback;
    private XUiV_Label humanBackgroundName, humanBackgroundDescription, humanSignatureName, humanSignatureDescription, humanTraitsEmpty, humanSkillsAttributes;
    private readonly XUiController[] humanSkillRows = new XUiController[12];
    private readonly XUiV_Label[] humanSkillTexts = new XUiV_Label[12], humanSkillPracticalTexts = new XUiV_Label[12], humanSkillTheoryTexts = new XUiV_Label[12];
    private readonly XUiV_Sprite[] humanSkillPracticalFills = new XUiV_Sprite[12], humanSkillTheoryFills = new XUiV_Sprite[12];
    private readonly XUiController[] humanAttrCells = new XUiController[5], humanTraitRows = new XUiController[10], humanTabRules = new XUiController[4];
    private readonly XUiV_Label[] humanAttrNames = new XUiV_Label[5], humanAttrValues = new XUiV_Label[5], humanTraitTexts = new XUiV_Label[10];
    private readonly XUiV_Sprite[] humanAttrFills = new XUiV_Sprite[5], humanTraitBars = new XUiV_Sprite[10];
    private XUiV_Label humanSkillSelectedName, humanSkillSelectedDetails, humanSkillSelPracticalValue, humanSkillSelTheoryValue;
    private XUiV_Sprite humanSkillSelPracticalFill, humanSkillSelTheoryFill;
    private static readonly string[] HumanAttributeIds = { "strength", "dexterity", "constitution", "intelligence", "charisma" };
    private XUiV_Sprite HumanSprite(string id) => GetChildById(id)?.ViewComponent as XUiV_Sprite;
    private RebirthHumanProgressionProjection humanSkillProfile;
    private string[] humanSkillIds = Array.Empty<string>();
    private string humanSkillSelectedId = string.Empty;
    private int humanSkillOffset;
    private XUiController btnHumanHire;
    // Hire eligibility is the P5 Leadership gate. It scans owned humans, so it is refreshed on a timer and
    // after each hire reply, never on every 0.35 s render.
    private bool humanGateOk=true,humanHasReply,humanLayoutDone;
    private string humanGateReason=string.Empty,humanSlotsText=string.Empty;
    private float humanGateAt;
    private XUiV_Label humanHireSlots, humanInventoryTabLabel, humanHeldNotice;
    // C2C-009: an unsettled paid hire ends in a hire or a refund. Host reads it; a remote client learns the
    // operation from "held=<op>" in a quote/commit/resolve reply.
    private string humanHeldOp, humanRefundOp;
    private float humanRefundRetryAt;
    private int humanRefundRetries;
    private XUiController btnHumanResolve;

    private void InitHumanHireUi()
    {
        RebirthHireInfoActionsPatch.Install();
        humanHirePanel=GetChildById("humanHirePanel");
        humanBackgroundPanel=GetChildById("humanBackgroundPanel");humanSkillsPanel=GetChildById("humanSkillsPanel");
        humanEquipmentPanel=GetChildById("humanEquipmentPanel");humanInventoryPanel=GetChildById("humanInventoryPanel");
        humanHireName=Label("humanHireName");humanHireType=Label("humanHireType");humanHireStatus=Label("humanHireStatus");humanHireHealth=Label("humanHireHealth");
        humanHirePrice=Label("humanHirePrice");humanHireFeedback=Label("humanHireFeedback");
        humanBackgroundName=Label("humanBackgroundName");humanBackgroundDescription=Label("humanBackgroundDescription");
        humanSignatureName=Label("humanSignatureName");humanSignatureDescription=Label("humanSignatureDescription");
        humanHireSlots=Label("humanHireSlots");humanInventoryTabLabel=Label("humanInventoryTabLabel");
        humanTraitsEmpty=Label("humanTraitsEmpty");humanSkillsAttributes=Label("humanSkillsAttributes");
        humanSkillSelectedName=Label("humanSkillSelectedName");humanSkillSelectedDetails=Label("humanSkillSelectedDetails");
        humanSkillSelPracticalValue=Label("humanSkillSelPracticalValue");humanSkillSelTheoryValue=Label("humanSkillSelTheoryValue");
        humanSkillSelPracticalFill=HumanSprite("humanSkillSelPracticalFill");humanSkillSelTheoryFill=HumanSprite("humanSkillSelTheoryFill");
        for(int i=0;i<humanAttrCells.Length;i++)
        {humanAttrCells[i]=GetChildById("humanAttrCell"+i);humanAttrNames[i]=Label("humanAttrName"+i);humanAttrValues[i]=Label("humanAttrValue"+i);humanAttrFills[i]=HumanSprite("humanAttrFill"+i);}
        for(int i=0;i<humanTraitRows.Length;i++){humanTraitRows[i]=GetChildById("humanTraitRow"+i);humanTraitTexts[i]=Label("humanTraitText"+i);humanTraitBars[i]=HumanSprite("humanTraitBar"+i);}
        for(int i=0;i<humanTabRules.Length;i++)humanTabRules[i]=GetChildById("humanTabRule"+i);
        for(int i=0;i<humanSkillRows.Length;i++)
        {
            int row=i;humanSkillRows[i]=GetChildById("humanSkillRow"+i);humanSkillTexts[i]=Label("humanSkillText"+i);
            humanSkillPracticalTexts[i]=Label("humanSkillPractical"+i);humanSkillTheoryTexts[i]=Label("humanSkillTheory"+i);
            humanSkillPracticalFills[i]=HumanSprite("humanSkillPracticalFill"+i);humanSkillTheoryFills[i]=HumanSprite("humanSkillTheoryFill"+i);
            if(humanSkillRows[i]!=null)humanSkillRows[i].OnPress+=delegate{SelectHumanSkill(row);};
        }
        var previous=GetChildById("humanSkillPrev");var next=GetChildById("humanSkillNext");
        if(previous!=null)previous.OnPress+=delegate{humanSkillOffset=Math.Max(0,humanSkillOffset-12);RenderHumanSkillRows();};
        if(next!=null)next.OnPress+=delegate{humanSkillOffset=Math.Min(Math.Max(0,humanSkillIds.Length-12),humanSkillOffset+12);RenderHumanSkillRows();};
        btnHumanHire=GetChildById("btnHumanHire");if(btnHumanHire!=null)btnHumanHire.OnPress+=delegate{HireHuman();};
        humanHeldNotice=Label("humanHeldNotice");
        btnHumanResolve=GetChildById("btnHumanResolve");if(btnHumanResolve!=null)btnHumanResolve.OnPress+=delegate{ResolveHeldPayment();};
        string[] ids={"btnHumanBackground","btnHumanSkills","btnHumanEquipment","btnHumanInventory"};
        for(int i=0;i<ids.Length;i++){humanTabButtons[i]=GetChildById(ids[i]);int tab=i;if(humanTabButtons[i]!=null)humanTabButtons[i].OnPress+=delegate{humanTab=(byte)tab;RenderHumanTabs();};}
    }
    // Hire equipment layout (panel-relative units; the slot rects live in equipment_windows.xml and are placed here
    // so that the read-only slot controller and its ids stay untouched). Worn gear sits left of the silhouette,
    // weapons/pack form a separate boxed column (Lockpicks directly below Backpack), ids 4..10 map to rows 0..6.
    private static void PlaceHireSlot(XUiController rect,XUiController stack,int x,int y,int w,int h,int display,int labelX)
    {
        var view=rect?.ViewComponent;if(view==null)return;
        view.Size=new Vector2i(w,h);view.Position=new Vector2i(x,y);view.TryUpdatePosition();
        if(rect.Children!=null&&rect.Children.Count>0&&rect.Children[0]?.ViewComponent is XUiV_Label label)
        {label.Size=new Vector2i(w-labelX-4,24);label.Position=new Vector2i(labelX,-24);label.TryUpdatePosition();}
        if(stack?.ViewComponent!=null){stack.ViewComponent.Position=new Vector2i(4,-4);stack.ViewComponent.TryUpdatePosition();}
        if(stack is XUiC_RebirthReadonlyItemSlot native)native.DisplaySize=display;
    }
    private void LayoutHireEquipment()
    {
        for(int i=0;i<11;i++)
        {
            var rect=GetChildById("hireGearSlot"+i);var stack=GetChildById("hireGearStack"+i);
            if(i<4)PlaceHireSlot(rect,stack,40,-150-110*i,250,72,64,80);
            else PlaceHireSlot(rect,stack,580,-132-80*(i-4),394,72,64,78);
        }
        // Inventory grid: 13 columns, 54-unit cells on a 58-unit pitch (no overlap), inside the NPC Backpack card.
        for(int i=0;i<117;i++)
        {
            var rect=GetChildById("hireInventorySlot"+i);var stack=GetChildById("hireInventoryStack"+i);
            var view=rect?.ViewComponent;if(view==null)continue;
            view.Size=new Vector2i(54,54);view.Position=new Vector2i(40+58*(i%13),-130-58*(i/13));view.TryUpdatePosition();
            if(stack?.ViewComponent!=null){stack.ViewComponent.Position=new Vector2i(0,0);stack.ViewComponent.TryUpdatePosition();}
            if(stack is XUiC_RebirthReadonlyItemSlot native)native.DisplaySize=54;
        }
        if(GetChildById("hireNativeSummary")?.ViewComponent is XUiV_Label summary)
        {summary.Size=new Vector2i(200,24);summary.Position=new Vector2i(744,-84);summary.Alignment=NGUIText.Alignment.Right;summary.TryUpdatePosition();}
    }
    // The standard itemInfoPanel (hosted in this window group, see xui.xml) is placed over the Selected Item card.
    private XUiC_ItemInfoWindow HireInfo=>windowGroup?.Controller?.GetChildByType<XUiC_ItemInfoWindow>();
    private void PlaceHireInfo()
    {
        var info=HireInfo;if(info?.ViewComponent==null||ViewComponent==null)return;
        if(humanTab<2){info.ClearSelectedStacks();info.ViewComponent.IsVisible=false;return;}
        var origin=ViewComponent.Position;int cardX=humanTab==2?994:972;
        info.ViewComponent.Position=new Vector2i(origin.x+343+cardX+8,origin.y-(141+70+56));
        info.ViewComponent.TryUpdatePosition();
    }
    private void RefreshHireGate()
    {
        if(Time.unscaledTime<humanGateAt)return;
        humanGateAt=Time.unscaledTime+2f;
        humanGateOk=true;humanGateReason=string.Empty;humanSlotsText=string.Empty;
        var player=xui?.playerUI?.entityPlayer;
        // A remote client has no authoritative ownership registry: the server decides at prepare/commit.
        if(player==null||humanProjection==null||player.world==null||player.world.IsRemote())return;
        humanHeldOp=null;
        var held=RebirthSurvivorHirePayment.GetHeldPayments(player);
        if(held!=null&&held.Count>0)
        {
            var pick=held[0];
            foreach(var entry in held)if(entry.Npc.Equals(humanProjection.NpcId)){pick=entry;break;}
            humanHeldOp=pick.Operation;
        }
        int owned,reservedSlots,limit;bool unrestricted;
        humanGateOk=RebirthCompanionLeadership.GetHireAvailability(player,humanProjection.NpcId,out owned,out reservedSlots,out limit,out unrestricted,out humanGateReason);
        humanSlotsText=RebirthSurvivorUiText.L("xuiRbNpcUiHireSlots","Hired survivors")+": "+(owned+reservedSlots)+" / "+
            (unrestricted?RebirthSurvivorUiText.L("xuiRbNpcUiHireSlotsUnrestricted","Unrestricted"):limit.ToString())+
            (reservedSlots>0?" "+string.Format(RebirthSurvivorUiText.L("xuiRbNpcUiHireSlotsPending","({0} pending)"),reservedSlots):string.Empty);
    }
    private void SelectHumanSkill(int row)
    {
        int index=humanSkillOffset+row;if(index<0||index>=humanSkillIds.Length)return;
        humanSkillSelectedId=humanSkillIds[index];RenderHumanSkillRows();
    }
    private void RenderHumanSkillRows()
    {
        for(int i=0;i<humanSkillRows.Length;i++)
        {
            int index=humanSkillOffset+i;bool present=index>=0&&index<humanSkillIds.Length;
            SetVisible(humanSkillRows[i],present);
            float practical=0f,theory=0f,practicalMax=100f,theoryMax=100f;
            if(present){humanSkillProfile.Skills.TryGetValue(humanSkillIds[index],out practical);humanSkillProfile.Theory.TryGetValue(humanSkillIds[index],out theory);HumanSkillMaxes(humanSkillIds[index],out practicalMax,out theoryMax);}
            Set(humanSkillTexts[i],present?RebirthSkillDisplayNames.Get(humanSkillIds[index]):string.Empty);
            Set(humanSkillPracticalTexts[i],present?practical.ToString("0.#",System.Globalization.CultureInfo.CurrentCulture):string.Empty);
            Set(humanSkillTheoryTexts[i],present?theory.ToString("0.#",System.Globalization.CultureInfo.CurrentCulture):string.Empty);
            HumanFill(humanSkillPracticalFills[i],practical,practicalMax);HumanFill(humanSkillTheoryFills[i],theory,theoryMax);
            if(humanSkillRows[i]?.ViewComponent is XUiV_Button button)button.Selected=present&&humanSkillIds[index]==humanSkillSelectedId;
        }
        bool has=humanSkillProfile!=null&&!string.IsNullOrEmpty(humanSkillSelectedId)&&humanSkillProfile.Skills.ContainsKey(humanSkillSelectedId);
        float selPractical=0f,selTheory=0f,selPracticalMax=100f,selTheoryMax=100f;
        if(has){humanSkillProfile.Skills.TryGetValue(humanSkillSelectedId,out selPractical);humanSkillProfile.Theory.TryGetValue(humanSkillSelectedId,out selTheory);HumanSkillMaxes(humanSkillSelectedId,out selPracticalMax,out selTheoryMax);}
        Set(humanSkillSelectedName,has?RebirthSkillDisplayNames.Get(humanSkillSelectedId):string.Empty);
        Set(humanSkillSelPracticalValue,has?selPractical.ToString("0.#",System.Globalization.CultureInfo.CurrentCulture)+" / "+selPracticalMax.ToString("0"):string.Empty);
        Set(humanSkillSelTheoryValue,has?selTheory.ToString("0.#",System.Globalization.CultureInfo.CurrentCulture)+" / "+selTheoryMax.ToString("0"):string.Empty);
        HumanFill(humanSkillSelPracticalFill,selPractical,selPracticalMax);HumanFill(humanSkillSelTheoryFill,selTheory,selTheoryMax);
        bool paged=humanSkillIds!=null&&humanSkillIds.Length>humanSkillRows.Length;
        SetVisible(GetChildById("humanSkillPrev"),paged);SetVisible(GetChildById("humanSkillNext"),paged);
        // The numbers are shown by the bars; the detail text keeps the primary attribute and the training/theory sources.
        Set(humanSkillSelectedDetails,has?HumanSkillDetailText(humanSkillSelectedId):string.Empty);
    }
    private string HumanSkillDetailText(string id)
    {
        if(!RebirthSurvivorDefinitionRegistry.TryGetSkill(id,out var definition)||definition==null)return string.Empty;
        string attribute=RebirthAttributeProgressionService.GetPrimaryAttributeForSkill(id);
        return RebirthSurvivorUiText.L("xuiRebirthProgressionPrimaryAttribute","Primary Attribute")+": "+RebirthSurvivorUiText.ResolveDefinitionName(attribute)+"\n\n"+
            RebirthSurvivorUiText.L("xuiRbNpcUiPracticalTraining","Practical training")+": "+RebirthCharacterUiSnapshotBuilder.SkillTrainingSource(definition)+"\n\n"+
            RebirthSurvivorUiText.L("xuiRbNpcUiTheorySources","Theory sources")+": "+RebirthCharacterUiSnapshotBuilder.TheorySource(id);
    }
    // Bar scale: the same definitions the player Skills window and the Companion Skills tab use (skill Max, theory SkillKnowledgeMax).
    private static void HumanSkillMaxes(string id,out float practicalMax,out float theoryMax)
    {
        practicalMax=theoryMax=100f;
        if(RebirthSurvivorDefinitionRegistry.TryGetSkill(id,out var definition)&&definition!=null&&definition.Max>0f)practicalMax=definition.Max;
        var progression=RebirthSurvivorDefinitionRegistry.Bundle?.Progression;
        if(progression!=null&&progression.SkillKnowledgeMax>0f)theoryMax=progression.SkillKnowledgeMax;
    }
    private static void HumanFill(XUiV_Sprite sprite,float value,float max)
    {
        if(sprite==null)return;
        float fill=Mathf.Clamp01(value/Mathf.Max(1f,max));
        if(!Mathf.Approximately(sprite.Fill,fill))sprite.Fill=fill;
    }
    private void RenderHumanAttributes(RebirthHumanProgressionProjection profile)
    {
        bool ok=profile?.Attributes!=null&&profile.AttributePotentials!=null&&profile.Attributes.Count>0;
        for(int i=0;ok&&i<HumanAttributeIds.Length;i++)ok=profile.Attributes.ContainsKey(HumanAttributeIds[i])&&profile.AttributePotentials.ContainsKey(HumanAttributeIds[i]);
        for(int i=0;i<HumanAttributeIds.Length;i++)
        {
            SetVisible(humanAttrCells[i],ok);if(!ok)continue;
            float current=profile.Attributes[HumanAttributeIds[i]],potential=profile.AttributePotentials[HumanAttributeIds[i]];
            Set(humanAttrNames[i],RebirthSurvivorUiText.ResolveDefinitionName(HumanAttributeIds[i]));
            Set(humanAttrValues[i],current.ToString("0.#",System.Globalization.CultureInfo.CurrentCulture)+" / "+potential.ToString("0.#",System.Globalization.CultureInfo.CurrentCulture));
            if(humanAttrFills[i]!=null)humanAttrFills[i].Fill=potential>0f?Mathf.Clamp01(current/potential):0f;
        }
        Set(humanSkillsAttributes,ok?string.Empty:RebirthHumanProfileUiText.Unavailable);
    }
    // One boxed row per saved trait (green positive, red negative, lavender neutral). An NPC with no saved traits
    // shows a localized empty state instead of a blank card: NPC origins are generated without trait selections.
    private void RenderHumanTraits(RebirthHumanProgressionProjection profile)
    {
        int shown=0;
        foreach(string id in profile?.TraitIds??Array.Empty<string>())
        {
            if(shown>=humanTraitRows.Length)break;
            if(!RebirthSurvivorDefinitionRegistry.TryGetTrait(id,out var definition)||definition==null)continue;
            SetVisible(humanTraitRows[shown],true);Set(humanTraitTexts[shown],RebirthSurvivorUiText.TraitDisplayName(definition));
            if(humanTraitBars[shown]!=null)humanTraitBars[shown].Color=definition.Polarity==RebirthTraitPolarity.Positive?new Color32(122,190,122,255):
                definition.Polarity==RebirthTraitPolarity.Negative?new Color32(204,107,100,255):new Color32(181,140,255,255);
            if(humanTraitTexts[shown]!=null)humanTraitTexts[shown].Color=humanTraitBars[shown]!=null?humanTraitBars[shown].Color:new Color32(235,235,240,255);
            shown++;
        }
        for(int i=shown;i<humanTraitRows.Length;i++){SetVisible(humanTraitRows[i],false);Set(humanTraitTexts[i],string.Empty);}
        Set(humanTraitsEmpty,profile==null?string.Empty:shown==0?RebirthSurvivorUiText.L("xuiRbNpcUiNoTraits","No notable traits."):string.Empty);
    }
    // Never show the raw entity class id (e.g. npcRebirthSurvivorManSDCS) as the survivor's name.
    private string HireDisplayName(EntityRebirthHumanoidNPC npc,RebirthHumanProgressionProjection profile)
    {
        string name=humanProjection?.DisplayName;
        string classKey=npc?.EntityClass?.entityClassName;
        bool raw=string.IsNullOrWhiteSpace(name)||string.Equals(name,classKey,StringComparison.Ordinal)||string.Equals(name,humanProjection?.ProfileId,StringComparison.Ordinal)||
            (name.StartsWith("npc",StringComparison.Ordinal)&&name.IndexOf(' ')<0);
        if(!raw)return name;
        if(!string.IsNullOrEmpty(classKey))
        {
            string localized=Localization.Get(classKey);
            if(!string.IsNullOrEmpty(localized)&&!string.Equals(localized,classKey,StringComparison.Ordinal))return localized;
        }
        return RebirthSurvivorUiText.L("xuiRbNpcUiTypeSurvivor","Survivor");
    }
    private void RenderHumanTabs()
    {
        for(int i=0;i<humanTabRules.Length;i++)SetVisible(humanTabRules[i],i==humanTab);
        PlaceHireInfo();
        for(int i=0;i<humanTabButtons.Length;i++)if(humanTabButtons[i]?.ViewComponent is XUiV_Button button)button.Selected=i==humanTab;
        SetVisible(humanBackgroundPanel,humanTab==0);SetVisible(humanSkillsPanel,humanTab==1);
        SetVisible(humanEquipmentPanel,humanTab==2);SetVisible(humanInventoryPanel,humanTab==3);
    }

    internal void PrepareHuman(int entityId,RebirthNpcInteractionProjection projection)
    {
        Prepare((EntityRebirthDogCompanion)null);
        humanEntity=entityId;humanProjection=projection;humanPrice=null;humanPending=false;humanQuoteRequested=false;humanQuoteRetryAt=0f;humanTab=0;humanHasReply=false;humanGateAt=0f;humanLayoutDone=false;
        humanHeldOp=null;humanRefundOp=null;humanRefundRetries=0;
        humanSkillProfile=null;humanSkillIds=Array.Empty<string>();humanSkillSelectedId=string.Empty;humanSkillOffset=0;
        if(cardOpen){RenderHuman();RequestHumanQuote();}
    }
    private void CloseHuman()
    {
        humanGeneration++;
        {var info=HireInfo;if(info!=null){info.ClearSelectedStacks();if(info.ViewComponent!=null)info.ViewComponent.IsVisible=false;}}
        if(humanProjection!=null)RebirthNpcInteractionNetworkClient.RequestClose(humanProjection.SessionKey);
        humanProjection=null;humanEntity=-1;humanPrice=null;humanPending=false;humanQuoteRequested=false;humanQuoteRetryAt=0f;
        humanHeldOp=null;humanRefundOp=null;
    }
    private void RenderHuman()
    {
        if(ViewComponent.Size!=new Vector2i(1872,935))
        {ViewComponent.Size=new Vector2i(1872,935);ViewComponent.Position=new Vector2i(-936,467);ViewComponent.TryUpdatePosition();}
        var npc=xui.playerUI.entityPlayer.world.GetEntity(humanEntity) as EntityRebirthHumanoidNPC;
        SetVisible(GetChildById("rebirthInteractionHeader"),false);SetVisible(GetChildById("body"),false);
        SetVisible(humanHirePanel,true);RenderHumanTabs();
        Set(humanHireName,HireDisplayName(npc,null));
        if(!humanLayoutDone){LayoutHireEquipment();humanLayoutDone=true;}
        RefreshHireGate();
        bool canHire=(humanProjection.Permissions&RebirthNpcInteractionPermissions.Hire)!=0&&humanGateOk;
        Set(humanHireStatus,RebirthSurvivorUiText.L(canHire?"xuiRbNpcUiAvailableToHire":"xuiRbNpcUiNotAvailableToHire",canHire?"Available to hire":"Not available to hire"));
        Set(humanHireSlots,humanSlotsText);
        if(!humanGateOk)Set(humanHireFeedback,humanGateReason);
        else if(!humanHasReply)Set(humanHireFeedback,string.Empty);
        Set(humanHireHealth,npc==null?string.Empty:npc.Health+" / "+npc.GetMaxHealth());
        Set(humanHirePrice,string.IsNullOrEmpty(humanPrice)?RebirthSurvivorUiText.L("xuiRbNpcUiPriceEllipsis","..."):
            humanPrice+" "+RebirthSurvivorUiText.L("xuiRbNpcUiDukes","Dukes"));
        if(btnHumanHire?.ViewComponent is XUiV_Button hireButton)hireButton.Enabled=!humanPending&&npc!=null&&!npc.IsDead()&&
            canHire&&!string.IsNullOrEmpty(humanPrice);
        bool held=!string.IsNullOrEmpty(humanHeldOp);
        SetVisible(btnHumanResolve,held);
        Set(humanHeldNotice,held?RebirthSurvivorUiText.L("xuiRbNpcUiHeldPayment","You have an unsettled payment"):string.Empty);
        if(btnHumanResolve?.ViewComponent is XUiV_Button resolveButton)resolveButton.Enabled=held&&!humanPending;
        RebirthHumanProgressionProjection profile;
        if(!RebirthHumanProgressionClientCache.TryGet(humanProjection.NpcId,out profile))
        {
            string unavailable=RebirthHumanProfileUiText.Unavailable;
            Set(humanBackgroundName,unavailable);Set(humanBackgroundDescription,string.Empty);Set(humanSignatureName,string.Empty);
            Set(humanSignatureDescription,string.Empty);RenderHumanTraits(null);RenderHumanAttributes(null);Set(humanSkillsAttributes,unavailable);
            humanSkillProfile=null;humanSkillIds=Array.Empty<string>();humanSkillSelectedId=string.Empty;humanSkillOffset=0;
        }
        else
        {
            Set(humanBackgroundName,RebirthHumanProfileUiText.BackgroundName(profile));
            Set(humanBackgroundDescription,RebirthHumanProfileUiText.BackgroundDescription(profile));
            Set(humanSignatureName,RebirthSurvivorUiText.SignatureBonusName(profile.BackgroundId));
            Set(humanSignatureDescription,RebirthHumanProfileUiText.SignatureDescription(profile));
            RenderHumanTraits(profile);
            RenderHumanAttributes(profile);
            humanSkillProfile=profile;humanSkillIds=RebirthHumanProfileUiText.SkillIds(profile);
            if(Array.IndexOf(humanSkillIds,humanSkillSelectedId)<0){humanSkillSelectedId=humanSkillIds.Length>0?humanSkillIds[0]:string.Empty;humanSkillOffset=0;}
        }
        // Type line: "Survivor" plus the saved background once its profile has arrived.
        string typeLine=RebirthSurvivorUiText.L("xuiRbNpcUiTypeSurvivor","Survivor");
        if(profile!=null){string background=RebirthHumanProfileUiText.BackgroundName(profile);if(!string.IsNullOrEmpty(background)&&background!=RebirthHumanProfileUiText.Unavailable&&background!=RebirthHumanProfileUiText.NoBackground)typeLine+=" - "+background;}
        Set(humanHireType,typeLine);
        RenderHumanSkillRows();
        var inventoryView=GetChildById("humanHireInventoryReadOnly") as XUiC_RebirthHumanEquipmentReadOnly;
        Set(humanInventoryTabLabel,inventoryView!=null&&inventoryView.SnapshotLoaded
            ?string.Format(RebirthSurvivorUiText.L("xuiRbNpcUiInventoryTabCount","Inventory {0} / {1}"),inventoryView.SnapshotUsed,inventoryView.SnapshotCapacity)
            :RebirthSurvivorUiText.L("xuiRbNpcUiInventoryTab","Inventory"));
        (GetChildById("humanHireEquipmentReadOnly") as XUiC_RebirthHumanEquipmentReadOnly)?.PresentHire(humanProjection.NpcId,humanEntity,true);
        (GetChildById("humanHireInventoryReadOnly") as XUiC_RebirthHumanEquipmentReadOnly)?.PresentHire(humanProjection.NpcId,humanEntity,false);
        actions.Clear();
        actionOffset=0;RenderActions();
        // The server marks a refund settled only after the client's refund is saved: poll, bounded.
        if(humanRefundOp!=null&&!humanPending&&Time.unscaledTime>=humanRefundRetryAt)
        {
            if(humanRefundRetries++<20){humanRefundRetryAt=Time.unscaledTime+1.5f;SubmitHumanHire("operation=refunded;id="+humanRefundOp);}
            else humanRefundOp=null;
        }
    }
    private void RequestHumanQuote()
    {
        if(humanQuoteRequested||!humanGateOk||Time.unscaledTime<humanQuoteRetryAt||humanProjection==null||
           (humanProjection.Permissions&RebirthNpcInteractionPermissions.Hire)==0)return;
        humanQuoteRequested=true;humanQuoteRetryAt=Time.unscaledTime+5f;SubmitHumanHire("operation=quote");
    }
    private void ResolveHeldPayment()
    {
        if(humanPending||humanProjection==null||string.IsNullOrEmpty(humanHeldOp))return;
        SubmitHumanHire("operation=resolve;id="+humanHeldOp);
    }
    private void HireHuman()
    {
        if(humanPending||humanProjection==null||(humanProjection.Permissions&RebirthNpcInteractionPermissions.Hire)==0)return;
        if(!humanGateOk)return;
        if(string.IsNullOrEmpty(humanPrice)){RequestHumanQuote();return;}
        GameManager.Instance.SaveLocalPlayerData();
        SubmitHumanHire("operation=prepare;price="+humanPrice);
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
            humanPending=false;humanHasReply=true;humanGateAt=0f;
            if(payload=="operation=quote"&&string.IsNullOrEmpty(humanPrice))humanQuoteRequested=false;
            if(refreshed!=null)humanProjection=refreshed;
            bool quoteOk=payload=="operation=quote"&&string.IsNullOrEmpty(error)&&response?.Status==RebirthNpcInteractionCommandStatus.Accepted;
            // An accepted quote only fills the price; its confirmation sentence is not shown until the player presses Hire.
            Set(humanHireFeedback,quoteOk?string.Empty:!string.IsNullOrEmpty(error)?error:response?.Detail??Localization.Get("xuiRebirthNpcTargetUnavailable"));
            // Held-payment keys can ride on rejected replies too ("held=<op>"), so parse before the status check.
            var heldValues=RebirthSurvivorHirePayment.Parse(response?.ResponsePayload);
            if(heldValues.TryGetValue("held",out var heldOp)&&!string.IsNullOrEmpty(heldOp))humanHeldOp=heldOp;
            if(heldValues.ContainsKey("refunded"))
            {
                humanHeldOp=null;humanRefundOp=null;
                if(string.IsNullOrEmpty(response.Detail))Set(humanHireFeedback,Localization.Get("xuiRbNpcHireRefunded"));
            }
            else if(humanRefundOp!=null)humanRefundRetryAt=Time.unscaledTime+1.5f; // server has not seen the saved refund yet: retry
            if(response?.Status==RebirthNpcInteractionCommandStatus.Accepted)
            {
                var values=heldValues;
                if(values.TryGetValue("refund",out _))
                {
                    if(RebirthSurvivorHirePayment.TryRefundLocal(xui.playerUI.entityPlayer,humanProjection,response.ResponsePayload,out var refundOp,out var refundError))
                    {humanRefundOp=refundOp;humanRefundRetries=0;SubmitHumanHire("operation=refunded;id="+refundOp);return;}
                    Set(humanHireFeedback,refundError); // carries xuiRbNpcHireRefundRoom when the backpack is full
                }
                if(values.TryGetValue("price",out var price))humanPrice=price;
                if(values.ContainsKey("prepared"))
                {
                    if(RebirthSurvivorHirePayment.TryPayLocal(xui.playerUI.entityPlayer,humanProjection,response.ResponsePayload,humanPrice,out var operation,out var paymentError))
                    {SubmitHumanHire("operation=commit;id="+operation);return;}
                    Set(humanHireFeedback,paymentError);
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
