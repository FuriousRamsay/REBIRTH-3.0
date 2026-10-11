using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
public sealed class XUiC_RebirthCraftingSkillHelp : XUiController
{
    private XUiC_RebirthCraftingRecipeDetails details;
    private Recipe recipe;
    private readonly RebirthCapabilityRequirementEvaluation[] targets = new RebirthCapabilityRequirementEvaluation[3];
    private RebirthCapabilityRequirementEvaluation active;
    private readonly List<Entry> entries = new List<Entry>();
    private const int RowCapacity=256;
    private int contentHeight=-1;
    private bool treatment, expanded;
    private float refreshAt, rowsAt;

    private int generation;
    private readonly Dictionary<string,XUiController> controls=new Dictionary<string,XUiController>();
    private void Cache(XUiController c){if(c.ViewComponent!=null && !string.IsNullOrEmpty(c.ViewComponent.ID))controls[c.ViewComponent.ID]=c;if(c.Children!=null)foreach(var child in c.Children)Cache(child);}
    private XUiController Find(string id)=>controls.TryGetValue(id,out var c)?c:null;
    private sealed class Entry
    {
        public string Name, Status, Icon, Key;
        public string TreatmentGain="...";
        public float NextPreview;
        public bool Priority;
        public Recipe Recipe;
        public readonly List<Recipe> Variants = new List<Recipe>();
        public ItemValue Item;
        public bool OtherOnly;
        public readonly RebirthCraftSkillPreview Preview = new RebirthCraftSkillPreview();
    }
    public override void Init()
    {
        base.Init();
        Cache(this);
        details = GetParentByType<XUiC_RebirthCraftingRecipeDetails>();
        for (int i=0;i<3;i++)
        {
            int row=i;
            Find("skillHelpHit"+i).OnHover += (sender, over) =>
            {
                var background = sender.ViewComponent as XUiV_Sprite;
                if(background != null) { background.SpriteName = over ? "ui_game_select_row" : "menu_empty2px"; background.Color = over ? Color.white : new Color32(240,240,244,255); }
            };
            Find("skillHelpHit"+i).OnPress += (s, button) => { if(targets[row]!=null) { if(active?.Id==targets[row].Id) Hide(); else Open(targets[row]); } };
        }
        Find("skillHelpCraftHit").OnPress += (s,b)=>Expand(false);
        Find("skillHelpTreatHit").OnPress += (s,b)=>Expand(true);
        foreach (string id in new[]{"skillHelpCraftHit","skillHelpTreatHit"})
            Find(id).OnHover += (sender,over)=>
            {
                var background=sender.ViewComponent as XUiV_Sprite;
                if(background!=null){background.SpriteName=over?"ui_game_select_row":"menu_empty";background.Color=over?Color.white:new Color32(42,39,49,255);}
            };
        foreach(string id in new[]{"skillHelpMainBackground","skillHelpListBackground"})
        {
            var background=Find(id).ViewComponent as XUiV_Texture;
            background.AutoUnload=false;
            background.Texture=Texture2D.whiteTexture;
            background.Color=new Color32(18,18,23,255);
        }
        Hide();
    }
    public override void OnClose() { Hide(); recipe=null; base.OnClose(); }
    public override void Update(float dt)
    {
        if(active!=null)base.Update(dt);
        else
        {
            for(int i=0;i<3;i++)Find("skillHelpTarget"+i)?.Update(dt);
            Find("skillHelpRecipeRow")?.Update(dt);
            Find("skillHelpMet")?.Update(dt);
        }
        if(details==null || xui?.playerUI?.entityPlayer==null) return;
        if(recipe!=details.SelectedRecipe) {Hide();recipe=details.SelectedRecipe;refreshAt=0;}
        float now=Time.realtimeSinceStartup;
        if(now>=refreshAt) {refreshAt=now+1;BindTargets();if(expanded)RefreshAvailability();}
        if(active==null)return;
        if(Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            bool inside=Over("skillHelpMain") || Over("skillHelpList");
            for(int i=0;i<3;i++)inside |= Over("skillHelpHit"+i);
            if(!inside){Hide();return;}
        }
        if(expanded && now>=rowsAt) {rowsAt=now+.5f;RenderRows();}
    }
    private void BindTargets()
    {
        Array.Clear(targets,0,targets.Length);
        var label=details.GetChildById("rebirthCraftingSelectedRecipeKnowledge")?.ViewComponent;
        float scale=Math.Min(1f,Math.Max(480,details.ViewComponent.Size.x-24)/852f);
        foreach(string id in new[]{"skillHelpMain","skillHelpList"})
        {
            var panel=Find(id).ViewComponent;
            if(panel.UiTransform!=null)panel.UiTransform.localScale=new Vector3(scale,scale,1);
            panel.Position=new Vector2i(id=="skillHelpMain"?8:8+(int)(432*scale),-140);
        }
        int line=0;
        RebirthCapabilityRequirementEvaluation knowledge=null;
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var evaluation=recipe==null?null:RebirthCapabilityService.EvaluateRecipe(xui.playerUI.entityPlayer,recipe.GetName());
        if(evaluation!=null)foreach(var r in evaluation.Requirements)
        {
            if(r==null || r.WarningOnly)continue;
            if(r.Kind==RebirthCapabilityKinds.Knowledge || r.Kind==RebirthCapabilityKinds.Discipline){knowledge=r;}
            else if(r.Kind==RebirthCapabilityKinds.Skill && seen.Add(r.Id))
            {if(line<3)targets[line]=r;line++;}
        }
        for(int i=0;i<3;i++)
        {
            bool visible=targets[i]!=null && label!=null && label.IsVisible;
            Show("skillHelpTarget"+i,visible);
            if(!visible)continue;
            var hit=Find("skillHelpTarget"+i).ViewComponent;
            hit.Position=new Vector2i(label.Position.x,label.Position.y-(knowledge!=null?28+i*48:i*48));
            int width=label.Size.x;
            Find("skillHelpTarget"+i).ViewComponent.Size=new Vector2i(width,44);
            // Only the chevron is an interactive button; status symbols are informational.
            foreach(string id in new[]{"skillHelpHit","skillHelpTargetFrame"})
            {
                Find(id+i).ViewComponent.Position=new Vector2i(width-25,-8);
                Find(id+i).ViewComponent.Size=new Vector2i(24,25);
            }
            Find("skillHelpTargetText"+i).ViewComponent.Size=new Vector2i(width-90,22);
            Find("skillHelpTargetValue"+i).ViewComponent.Position=new Vector2i(57,-19);
            Find("skillHelpTargetValue"+i).ViewComponent.Size=new Vector2i(width-90,22);
            Find("skillHelpTargetArrow"+i).ViewComponent.Position=new Vector2i(width-19,-13);
            Find("skillHelpTargetTrack"+i).ViewComponent.Size=new Vector2i(width-140,5);
            Find("skillHelpTargetProgress"+i).ViewComponent.Size=new Vector2i(Math.Max(1,(int)((width-140)*Math.Min(1,targets[i].CurrentValue/Math.Max(1,targets[i].RequiredValue)))),5);
            Find("skillHelpTargetPercent"+i).ViewComponent.Position=new Vector2i(width-77,-31);
            Text("skillHelpTargetPercent"+i,(100*Math.Min(1,targets[i].CurrentValue/Math.Max(1,targets[i].RequiredValue))).ToString("0.#",CultureInfo.InvariantCulture)+"%");
            Text("skillHelpTargetValue"+i,F(targets[i].CurrentValue)+" / "+F(targets[i].RequiredValue));
            Icon("skillHelpTargetIcon"+i,RebirthSkillAptitudeTraitFactory.SkillIconKey(targets[i].Id));
            RebirthSkillDefinition definition;
            RebirthSurvivorDefinitionRegistry.TryGetSkill(targets[i].Id,out definition);
            Text("skillHelpTargetText"+i,(definition==null?targets[i].Id:Localization.Get(definition.NameKey)));
            Icon("skillHelpTargetMarker"+i,targets[i].Allowed?"ui_game_symbol_check":"ui_game_symbol_x");
            ((XUiV_Sprite)Find("skillHelpTargetMarker"+i).ViewComponent).Color=targets[i].Allowed?new Color32(112,196,126,255):new Color32(255,120,120,255);
        }
        Show("skillHelpRecipeRow",knowledge!=null && label!=null && label.IsVisible);
        bool hasRequirements=evaluation!=null&&evaluation.Requirements.Any(r=>r!=null&&!r.WarningOnly);
        var title=details.GetChildById("rebirthCraftingKnowledgeTitle")?.ViewComponent;if(title!=null)title.IsVisible=hasRequirements&&label!=null&&label.IsVisible;
        Show("skillHelpMet",hasRequirements && label!=null && label.IsVisible);
        if(label!=null){
            var met=Find("skillHelpMet").ViewComponent;
            met.Position=new Vector2i(label.Position.x+label.Size.x-80,label.Position.y+21);
            if(evaluation!=null){var required=evaluation.Requirements.Where(r=>r!=null&&!r.WarningOnly).ToList();Text("skillHelpMet",required.Count(r=>r.Allowed)+" / "+required.Count+" MET");}
        }
        if(knowledge!=null && label!=null){
            Find("skillHelpRecipeRow").ViewComponent.Position=label.Position;
            Find("skillHelpRecipeRow").ViewComponent.Size=new Vector2i(label.Size.x,24);
            Find("skillHelpRecipeText").ViewComponent.Size=new Vector2i(label.Size.x-90,24);
            Find("skillHelpRecipeStatus").ViewComponent.Position=new Vector2i(label.Size.x-66,-4);
            Text("skillHelpRecipeText",knowledge.Kind==RebirthCapabilityKinds.Discipline?knowledge.Message:"Recipe: "+RebirthKnowledgeService.GetDisplayName(knowledge.Id).Replace("Recipe: ",""));
            Text("skillHelpRecipeStatus", "");
            Icon("skillHelpRecipeMarker",knowledge.Allowed?"ui_game_symbol_check":"ui_game_symbol_x");
            ((XUiV_Sprite)Find("skillHelpRecipeMarker").ViewComponent).Color=knowledge.Allowed?new Color32(112,196,126,255):new Color32(255,120,120,255);
        }
        if(active!=null)
        {
            var updated=targets.FirstOrDefault(r=>r!=null && r.Id==active.Id);
            if(updated==null)Hide();else {active=updated;RenderMain();}
        }
    }
    private bool Over(string id)
    {
        var v=Find(id)?.ViewComponent;
        var t=UICamera.hoveredObject?.transform;
        return v!=null && v.IsVisible && t!=null && (t==v.UiTransform || t.IsChildOf(v.UiTransform));
    }
    private void Open(RebirthCapabilityRequirementEvaluation r)
    {
        if(active==null || active.Id!=r.Id){Hide();active=r;RenderMain();}
        Show("skillHelpMain",true);
    }
    private void RenderMain()
    {
        RebirthSkillDefinition def;
        RebirthSurvivorDefinitionRegistry.TryGetSkill(active.Id,out def);
        Text("skillHelpName",def==null?active.Id:Localization.Get(def.NameKey));
        Icon("skillHelpIcon",RebirthSkillAptitudeTraitFactory.SkillIconKey(active.Id));
        Text("skillHelpLevel",F(active.CurrentValue));
        Text("skillHelpStatus",(active.Allowed?"[70C47E]":"[FF9696]")+F(active.RequiredValue)+"[-]");
        Icon("skillHelpRequiredMarker",active.Allowed?"ui_game_symbol_check":"ui_game_symbol_x");
        var marker=Find("skillHelpRequiredMarker").ViewComponent as XUiV_Sprite;
        marker.Color=active.Allowed?new Color32(112,196,126,255):new Color32(255,120,120,255);
        Text("skillHelpProgressText",F(active.CurrentValue)+" / "+F(active.RequiredValue));
        var bar=Find("skillHelpProgress").ViewComponent;
        bar.Size=new Vector2i(Math.Max(1,(int)(376*Math.Min(1,active.CurrentValue/Math.Max(1,active.RequiredValue)))),12);
        bool medicine=active.Id=="skill.medicine";
        Text("skillHelpCraftLabel",medicine?"Craft medical supplies":"Craft related recipes");
        Text("skillHelpCraftDescription",medicine?"Complete medical recipes to gain skill practice.":"Complete related recipes to gain skill practice.");
        string source=def==null?"":(string.IsNullOrEmpty(def.SourceKey)?def.LearnByDoingSource:Localization.Get(def.SourceKey));
        source=string.IsNullOrWhiteSpace(source)?"":char.ToUpper(source[0],CultureInfo.CurrentCulture)+source.Substring(1);
        Text("skillHelpSource",source);
        Show("skillHelpSource",!medicine);
        Show("skillHelpTreat",medicine);
    }
    private void Expand(bool medical)
    {
        if(active==null)return;
        if(expanded && treatment==medical){expanded=false;generation++;entries.Clear();Show("skillHelpList",false);Show("skillHelpTreatSelected",false);Show("skillHelpCraftSelected",false);return;}
        treatment=medical;expanded=true;entries.Clear();generation++;contentHeight=-1;
        var player=xui.playerUI.entityPlayer;
        if(!medical)
        {
            foreach(var r in XUiM_Recipes.GetRecipes())
            {
                if(r==null || RebirthServiceCraftSkillService.ClassifyRecipe(r)!=active.Id)continue;
                var item=r.GetOutputItemClass();if(item==null)continue;
                bool unlocked=XUiM_Recipes.GetRecipeIsUnlocked(xui,r);
                var entry=entries.FirstOrDefault(e=>e.Recipe.itemValueType==r.itemValueType);
                if(entry==null)
                {
                    entry=new Entry{Name=item.GetLocalizedItemName(),Icon=item.GetIconName(),Key=item.GetItemName(),Recipe=r,Priority=unlocked,Status=unlocked?"Unlocked":"Locked"};
                    entries.Add(entry);
                }
                entry.Variants.Add(r);
                if(unlocked && !entry.Priority){entry.Recipe=r;entry.Priority=true;entry.Status="Unlocked";}
            }
        }
        else
        {
            foreach(var item in ItemClass.list)
            {
                if(item?.Actions==null)continue;
                if(!RebirthDifficultyPractice.HasTreatment(item.GetItemName()))continue;
                bool self=true;
                var value=ItemClass.GetItem(item.GetItemName());
                int local=player.bag.GetItemCount(value)+player.inventory.GetItemCount(value);
                int total=xui.PlayerInventory.GetItemCount(value);
                entries.Add(new Entry{Name=item.GetLocalizedItemName(),Icon=item.GetIconName(),Key=item.GetItemName(),Item=value,OtherOnly=!self,Priority=total>0,Status=(local>0?"Inventory":total>0?"Nearby storage":"Not available")+(self?"":" - Treat others")});
            }

        }
        entries.Sort((a,b)=>{int c=b.Priority.CompareTo(a.Priority);if(c==0)c=StringComparer.CurrentCultureIgnoreCase.Compare(a.Name,b.Name);return c!=0?c:StringComparer.Ordinal.Compare(a.Key,b.Key);});
        Show("skillHelpList",true);
        Text("skillHelpListTitle",medical?"Treat injuries":active.Id=="skill.medicine"?"Craft medical supplies":"Craft related recipes");
        Icon("skillHelpListIcon",medical?"rb_skill_medicine":"rb_ui_craft_medicine");
        Text("skillHelpListHint",medical?"PRACTICE PER EFFECTIVE USE":"PRACTICE PER CRAFT BATCH");
        Show("skillHelpCraftSelected",!medical);Show("skillHelpTreatSelected",medical);
        Text("skillHelpListFoot",medical?"Full useful effect shown. Partial recovery gives partial practice.":"Learning bonuses included. Materials and station still required.");
        RenderRows();
        RebirthNativeScrollbarUtil.TrySetValue(Find("skillHelpScrollView"),0f);
    }
    private void RefreshAvailability()
    {
        var player=xui.playerUI.entityPlayer;
        foreach(var e in entries)
        {
            if(e.Recipe!=null)
            {
                var available=e.Variants.FirstOrDefault(r=>XUiM_Recipes.GetRecipeIsUnlocked(xui,r));
                e.Priority=available!=null;
                e.Recipe=available??e.Variants[0];
                e.Status=e.Priority?"Unlocked":"Locked";
            }
            else
            {
                int local=player.bag.GetItemCount(e.Item)+player.inventory.GetItemCount(e.Item);
                e.Priority=xui.PlayerInventory.GetItemCount(e.Item)>0;
                e.Status=(local>0?"Inventory":e.Priority?"Nearby storage":"Not available")+(e.OtherOnly?" - Treat others":"");
            }
        }
        entries.Sort((a,b)=>{int c=b.Priority.CompareTo(a.Priority);if(c==0)c=StringComparer.CurrentCultureIgnoreCase.Compare(a.Name,b.Name);return c!=0?c:StringComparer.Ordinal.Compare(a.Key,b.Key);});
    }
    // The entire ordered list is laid out once; the native scrollview moves it continuously.
    private List<Entry> DisplayRows()
    {
        var rows=new List<Entry>();
        foreach(bool available in new[]{true,false})
        {
            var group=entries.Where(e=>e.Priority==available).ToList();
            if(group.Count==0)continue;
            string title=treatment?(available?"AVAILABLE TO YOU":"NOT AVAILABLE"):(available?"UNLOCKED":"LOCKED");
            rows.Add(new Entry{Name=title,Priority=available});
            foreach(var entry in group)
            {
                rows.Add(entry);
            }
        }
        return rows;
    }
    private void RenderRows()
    {
        var rows=DisplayRows();
        int height=Math.Max(448,rows.Count*52);
        var scroll=Find("skillHelpScrollView");
        bool boundsChanged=contentHeight!=height;
        if(boundsChanged){contentHeight=height;Find("skillHelpContent").ViewComponent.Size=new Vector2i(370,height);}
        float value;RebirthNativeScrollbarUtil.TryGetValue(scroll,out value);
        int first=Math.Max(0,(int)(value*Math.Max(0,height-448)/52)-1);
        for(int i=0;i<RowCapacity;i++)
        {
            int n=i;Show("skillHelpRow"+i,n<rows.Count);
            if(n>=rows.Count)continue;
            var e=rows[n];bool heading=e.Key==null;
            Show("skillHelpGroup"+i,heading);Show("skillHelpGroupSort"+i,heading && !string.IsNullOrEmpty(e.Name));
            Show("skillHelpItem"+i,!heading);Show("skillHelpItemName"+i,!heading);
            Show("skillHelpItemStatus"+i,!heading);Show("skillHelpGain"+i,!heading);
            Show("skillHelpRowBg"+i,!heading && i%2==0);
            Text("skillHelpGroup"+i,(e.Priority?"[70C47E]":"[FF9696]")+e.Name+"[-]");
            if(heading)continue;
            Icon("skillHelpItem"+i,e.Icon);Text("skillHelpItemName"+i,e.Name);
            string status=treatment?e.Status+" - "+RebirthMedicalPractice.Purpose(e.Key):e.Priority?"":"Requirements not met";
            Text("skillHelpItemStatus"+i,status);
            if(i>=first && i<=first+11)
            {
                if(treatment && Time.realtimeSinceStartup>=e.NextPreview)
                {
                    e.NextPreview=Time.realtimeSinceStartup+3f;int token=generation;
                    RebirthCookingSessionService.Request(xui.playerUI.entityPlayer,"treatmentSkillPreview",e.Key,reply:reply=>
                    {if(token==generation && float.TryParse(reply,NumberStyles.Float,CultureInfo.InvariantCulture,out float gain))e.TreatmentGain="+"+gain.ToString("0.####",CultureInfo.InvariantCulture);});
                }
                Text("skillHelpGain"+i,treatment?e.TreatmentGain:RebirthCraftSkillPreview.GainText(e.Preview.Get(xui.playerUI.entityPlayer,e.Recipe,1,false)));
            }
        }
        if(boundsChanged)RebirthNativeScrollbarUtil.Refresh(scroll);
    }
    private void Hide(){active=null;expanded=false;generation++;entries.Clear();Show("skillHelpMain",false);Show("skillHelpList",false);Show("skillHelpCraftSelected",false);Show("skillHelpTreatSelected",false);}
    private void Show(string id,bool value){var v=Find(id)?.ViewComponent;if(v!=null){if(v.IsVisible!=value)v.IsVisible=value;if(v.UiTransform!=null && v.UiTransform.gameObject.activeSelf!=value)v.UiTransform.gameObject.SetActive(value);}}
    private void Text(string id,string value){var v=Find(id)?.ViewComponent as XUiV_Label;if(v!=null && v.Text!=value)v.Text=value;}
    private void Icon(string id,string value){var v=Find(id)?.ViewComponent as XUiV_Sprite;if(v!=null && v.SpriteName!=value)v.SpriteName=value;}
    private static string F(float value)=>value.ToString("0.#",CultureInfo.InvariantCulture);
}
