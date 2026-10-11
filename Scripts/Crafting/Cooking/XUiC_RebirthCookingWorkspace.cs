using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed partial class XUiC_RebirthCookingWorkspace : XUiController
{
    private static readonly string[] HeatControls = { "toolsTitle", "cookingTools", "fuelTitle", "burnTimeLeft", "cookingFuel" };
    private readonly RebirthCraftSkillPreview outcomeSkillPreview = new RebirthCraftSkillPreview();

    public readonly RebirthCookingPreparation Preparation = new RebirthCookingPreparation();
    private readonly List<Recipe> catalogue = new List<Recipe>();
    private readonly List<Recipe> matchingCatalogue = new List<Recipe>();
    private readonly List<RemoteResourceTransactions.ReturnReceipt> ingredientOrigins = new List<RemoteResourceTransactions.ReturnReceipt>();
    private readonly ItemStack[] ghosts = new ItemStack[12];
    private readonly XUiC_RebirthCookingSlot[] slots = new XUiC_RebirthCookingSlot[12];
    private readonly Dictionary<int,int> resolvedAmounts = new Dictionary<int,int>();
    private readonly Dictionary<int,int> inventoryCounts = new Dictionary<int,int>();
    private readonly Dictionary<string,int> feasibilityCache = new Dictionary<string,int>(StringComparer.Ordinal);
    private readonly Dictionary<Recipe,int> millingFrameFeasibility = new Dictionary<Recipe,int>();
    private readonly Dictionary<Recipe,int> renderRecipeFeasibility = new Dictionary<Recipe,int>();
    private readonly Dictionary<Recipe,string> renderMissingTools = new Dictionary<Recipe,string>();
    private bool millingFrameActive;
    private int feasibilityFingerprint = int.MinValue;
    private int? renderFeasibilityFingerprint;
    private int inventoryStateFingerprint;
    private bool rendering;
    private Recipe selected, result;
    private XUiC_RebirthCookingStation station;
    private int batch = 1, offset, substitutionSlot;
    private int variation;
    private bool hiddenRecipe, usePot;
    private int lastFreeInputHash;
    private XUiC_TextInput quantityInput;
    private float refresh;
    private bool open;
    public static XUiC_RebirthCookingWorkspace ActiveInstance { get; private set; }
    public bool IsCookingOpen => open;
    private int category;
    private const int RecipeRows=8;
    private const int RecipeCapacity=160;
    private List<Recipe> visibleRecipes=new List<Recipe>();
    private float recipePixels,recipeTarget;
    private readonly Dictionary<string,XUiController> viewCache=new Dictionary<string,XUiController>();
    private XUiController Find(string id){if(!viewCache.TryGetValue(id,out var c)){c=GetChildById(id);viewCache[id]=c;}return c;}
    private const int RecipeTrackHeight=352;
    private float recipeDrag;
    private int recipeScrollFrame=-1;
    private string book, magazine;
    private string status = "";
    private string[] alternatives = Array.Empty<string>();
    private readonly Dictionary<XUiController, Vector2i> positions = new Dictionary<XUiController, Vector2i>();
    private readonly Dictionary<XUiController, bool> visibility = new Dictionary<XUiController, bool>();
    private bool pulling;
    private int pullUiGeneration;
    private ItemStack[] preparationInputs;
    private int preparationBatch;
    private string pendingBook,pendingMagazine;
    private World sessionWorld;
    private bool preparingRequest, submittingCook;
    private int prepGeneration;
    private string comfortPreview="", comfortReason="", comfortKey="";
    private float comfortRefresh;
    private readonly Dictionary<string,string> chosenMagazines=new Dictionary<string,string>();
    private RebirthCookingMemory.Ready Ready => RebirthCookingSessionService.Ready(xui.playerUI.entityPlayer,Key);
    private string ActiveMagazine => Ready?.Magazine;
    private bool ActiveBook => !string.IsNullOrEmpty(Ready?.Book);
    private bool IsCurrentSession(int generation,EntityPlayerLocal player)
        => open&&generation==pullUiGeneration&&ReferenceEquals(player,xui?.playerUI?.entityPlayer)
            &&ReferenceEquals(player?.world,sessionWorld);

    private void LoadPreparation(string response)
    {
        if(response.StartsWith("<cooking",StringComparison.Ordinal))
        { if(xui.playerUI.entityPlayer.world.IsRemote())RebirthCookingSessionService.ClientMemory=RebirthCookingMemory.Read(XElement.Parse(response)); }
        else status=response;
        preparingRequest=false;refresh=0;
    }
    private void CancelPreparation()
    {
        prepGeneration++;Preparation.Cancel();preparingRequest=false;preparationInputs=null;
        RebirthCookingSessionService.Request(xui.playerUI.entityPlayer,"cancel");
    }
    private double Now => Time.timeAsDouble;
    private string Key => result?.GetName();

    public override void Init()
    {
        base.Init();
        station = windowGroup.Controller as XUiC_RebirthCookingStation;
        RebirthPersonalCraftingHudSuppressionInstaller.EnsureInstalled();
        for (int i = 0; i < slots.Length; i++)
        {
            int slot = i;
            slots[i] = Find("cookingSlot" + i) as XUiC_RebirthCookingSlot;
            slots[i].SlotNumber = i;
            slots[i].ViewComponent.EventOnPress = true;
            slots[i].ViewComponent.EventOnDrag = true;
            slots[i].StackLocation = XUiC_ItemStack.StackLocationTypes.Workstation;
            slots[i].InfoWindow = xui.GetChildByType<XUiC_ItemInfoWindow>();
            Wire("sub" + i, () => ShowSubstitutes(slot));
        }
        if(station.UsesSharedMillingPresentation)return;
        Wire("pullIngredients", Pull);
        Wire("batchMinus", () => SetBatch(batch-1));
        Wire("batchPlus", () => SetBatch(batch+1));
        Wire("batchMin", () => SetBatch(1));
        Wire("batchMax", () => SetBatch(MaximumBatch()));
        Wire("cookingMethod",()=>{if(selected==null&&!Preparation.IsPreparing){usePot=!usePot;Render();}});
        quantityInput=Find("batchInput") as XUiC_TextInput;
        if(quantityInput!=null)quantityInput.OnChangeHandler+=(sender,text,fromCode)=>{if(!fromCode&&int.TryParse(text,out int value))SetBatch(value);};
        Wire("prepare", Prepare);
        Wire("cook", Cook);
        var magazineChoice=Find("magazineReferenceIcon");
        magazineChoice.ViewComponent.EventOnPress=true;
        magazineChoice.OnPress+=(sender,button)=>
        {
            if(Preparation.IsPreparing||preparingRequest)return;
            var choices=RebirthCookingCatalogue.Get(Key)?.Magazines.Where(id=>RebirthCookingCatalogue.Studied(xui.playerUI.entityPlayer,id)&&xui.PlayerInventory.GetItemCount(ItemClass.GetItem(id))>0).ToList();
            if(choices==null||choices.Count<2)return;
            chosenMagazines[Key]=choices[(choices.IndexOf(magazine)+1)%choices.Count];Render();
        };
        Wire("clearRecipe", () => Select(null));
        Wire("recipesUp", () => { offset = Math.Max(0, offset - 1); Render(); });
        Wire("recipesDown", () => { offset = Math.Min(Math.Max(0, catalogue.Count - RecipeRows), offset + 1); Render(); });
        Wire("closeSub", () => Show("substitutionPopup", false));
        for (int i = 0; i < RecipeCapacity; i++) { int row = i; Wire("dish" + i, () => { if (row < visibleRecipes.Count) Select(visibleRecipes[row]); }); }
        for (int i = 0; i < 6; i++) { int choice = i; Wire("choice" + i, () => Substitute(choice)); }
        for (int i=0;i<3;i++) { int choice=i; Wire("filter"+i,()=>{category=choice;offset=0;recipeTarget=0;Render();}); }
        WireRecipeScroll(Find("known"));
        var thumb=Find("recipeThumb");
        thumb.ViewComponent.EventOnDrag=true;thumb.ViewComponent.EventOnPress=true;
        thumb.OnDrag+=(sender,type,delta)=>
        {
            if(type==EDragType.DragStart){recipeDrag=recipePixels;return;}
            if(type!=EDragType.Dragging)return;
            int max=Math.Max(0,visibleRecipes.Count-RecipeRows)*44;
            recipeDrag-=delta.y*max/Math.Max(1,RecipeTrackHeight-thumb.ViewComponent.Size.y);
            recipeTarget=recipePixels=Mathf.Clamp(recipeDrag,0,max);ApplyRecipeScroll(0);
        };
        Find("recipeTrack").OnPress+=(sender,mouse)=>
        {
            var v=sender.ViewComponent;
            var point=v.UiTransform.InverseTransformPoint(UICamera.lastWorldPosition);
            int max=Math.Max(0,visibleRecipes.Count-RecipeRows)*44;
            recipeTarget=Mathf.Clamp(-point.y/RecipeTrackHeight*max,0,max);
        };

    }

    private void RefreshStationCatalogue(IList<Recipe> recipes)
    {
        catalogue.Clear();
        matchingCatalogue.Clear();
        foreach (Recipe r in recipes)
            if ((station.IsMilling || RebirthCookingCatalogue.IsCooking(r)) && RebirthCookingCatalogue.AtStation(r, station.Workstation))
            {
                Recipe candidate=RebirthCookingCatalogue.ForStation(r,station.Workstation);
                catalogue.Add(candidate);
                matchingCatalogue.Add(candidate); // Native order breaks experiment-matching ties.
            }
        catalogue.Sort((a,b) => string.Compare(Localization.Get(a.GetName()), Localization.Get(b.GetName()), StringComparison.CurrentCulture));
    }

    public override void OnOpen()
    {
        unchecked { pullUiGeneration++; }
        base.OnOpen(); ActiveInstance = this; open = true; Preparation.Cancel(); status = ""; variation = UnityEngine.Random.Range(1,5);
        if(sessionWorld!=xui.playerUI.entityPlayer.world){Preparation.Reset();sessionWorld=xui.playerUI.entityPlayer.world;}
        int fetchGeneration=pullUiGeneration,preparationGeneration=prepGeneration;
        EntityPlayerLocal fetchOwner=xui.playerUI.entityPlayer;
        RebirthCookingSessionService.Request(fetchOwner,"fetch",reply:response=>
        {
            if(!IsCurrentSession(fetchGeneration,fetchOwner)||preparationGeneration!=prepGeneration)return;
            LoadPreparation(response);
        });
        catalogue.Clear();
        RefreshStationCatalogue(XUiM_Recipes.GetRecipes());
        usePot=station.Workstation!="WorkbenchGasStove001_FR"||station.toolWindow.HasRequirement(new Recipe{craftingToolType=ItemClass.GetItem("toolCookingPot").type});
        Select(null); Layout(); Render();
    }
    public override void OnClose() { outcomeSkillPreview.Reset(); Leave(); base.OnClose(); }
    public void Leave()
    {
        CancelPreparation();
        if (!open) return;
        sharedCraftPending=false;
        open = false;
        unchecked { pullUiGeneration++; }
        if (ActiveInstance == this) ActiveInstance = null;
        ReturnIngredients();
        foreach (var pair in positions) if (pair.Key.ViewComponent != null) pair.Key.ViewComponent.Position = pair.Value;
        foreach (var pair in visibility) if (pair.Key.ViewComponent != null) pair.Key.ViewComponent.IsVisible = pair.Value;
        positions.Clear(); visibility.Clear();
    }
    public override void Update(float dt)
    {
        long childrenProfile=RebirthCookingDiagnostics.Begin();
        base.Update(dt);
        RebirthCookingDiagnostics.Section("workspace children",childrenProfile);
        if (!open) return;
        if (!windowGroup.isShowing || xui.playerUI.entityPlayer.IsDead()) { Leave(); return; }
        if((Preparation.IsPreparing||preparingRequest)&&!PreparationInputsMatch())CancelPreparation();
        if(Preparation.Tick(open, Key, Now))
        {
            preparingRequest=true;
            int generation=prepGeneration,finishUiGeneration=pullUiGeneration;string recipe=Key;
            EntityPlayerLocal finishOwner=xui.playerUI.entityPlayer;
            RebirthCookingSessionService.Request(finishOwner,"finish",recipe,pendingBook,pendingMagazine,reply:response=>
            {
                if(!IsCurrentSession(finishUiGeneration,finishOwner)||generation!=prepGeneration||Key!=recipe||!PreparationInputsMatch())return;
                LoadPreparation(response);
            });
        }
        if(station.UsesSharedMillingPresentation)
        {
            CompleteSharedCraft();
            if(!string.IsNullOrEmpty(status)&&status!=sharedLastStatus)
                GameManager.ShowTooltip(xui.playerUI.entityPlayer,status);
            sharedLastStatus=status;
            if((refresh-=dt)<=0){refresh=.75f;Render();}
            return;
        }
        var actions=xui.playerUI.playerInput?.GUIActions;
        if(actions!=null && UIInput.selection==null && !Find("substitutionPopup").ViewComponent.IsVisible && actions.DPad_Up.WasPressed && !RebirthConsoleInputGuardRuntime.BlocksGameplayInput())Cook();
        ApplyRecipeScroll(dt);
        if ((refresh -= dt) > 0) return;
        refresh = .75f;
        Layout(); Render();
    }
    // Coalesce native fuel/slot invalidations instead of forcing an entire recipe scan every frame.
    public void RefreshStationState(){refresh=Math.Min(refresh,.1f);}
    private void Wire(string id, Action action) { var c=Find(id); if(c!=null)c.OnPress+=(sender,button)=> { if(open && c.ViewComponent.Enabled)action(); }; }
    private void Text(string id,string value) { if(Find(id)?.ViewComponent is XUiV_Label label)if(label.Text!=(value??""))label.Text=value??""; }
    private void Caption(string id,string value) { if(Find(id)?.GetChildById("label")?.ViewComponent is XUiV_Label v)if(v.Text!=value)v.Text=value; }
    private void Enable(string id,bool value)
    {
        var c=Find(id);if(c?.ViewComponent==null)return;
        if(c.ViewComponent.Enabled!=value)c.ViewComponent.Enabled=value;
        if(id=="cook"||id=="pullIngredients"||id=="prepare")
        {
            Color color=value?new Color32(235,235,240,255):new Color32(120,120,126,255);
            foreach(var child in c.Children)
            {
                if(child.ViewComponent is XUiV_Label label && label.Color!=color)label.Color=color;
                if(child.ViewComponent is XUiV_Sprite icon && child!=c.GetChildById("actionFill") && icon.Color!=color)icon.Color=color;
            }
        }
    }
    private void Show(string id,bool value) { var c=Find(id); if(c?.ViewComponent!=null)if(c.ViewComponent.IsVisible!=value)c.ViewComponent.IsVisible=value; }
    private void Move(XUiController c,int x,int y)
    {
        if(c?.ViewComponent==null)return;
        if(!positions.ContainsKey(c))positions[c]=c.ViewComponent.Position;
        if(c.ViewComponent.Position.x!=x || c.ViewComponent.Position.y!=y) c.ViewComponent.Position=new Vector2i(x,y);
    }
    private void Hide(XUiController c)
    {
        if(c?.ViewComponent==null)return;
        if(!visibility.ContainsKey(c))visibility[c]=c.ViewComponent.IsVisible;
        c.ViewComponent.IsVisible=false; Move(c,-10000,-10000);
    }
    private void Layout()
    {
        if(station.UsesSharedMillingPresentation)return;
        Vector2i screen=xui.GetXUiScreenSize();
        float scale=Math.Min(1f,Math.Min((screen.x-4)/1872f,(screen.y-144)/900f));
        if(ViewComponent.UiTransform.localScale!=Vector3.one*scale)ViewComponent.UiTransform.localScale=Vector3.one*scale;
        var position=new Vector2i((int)(-1872*scale/2),(int)(screen.y/2-12));
        if(ViewComponent.Position!=position)ViewComponent.Position=position;
        RebirthPersonalCraftingLayoutService.ApplySharedTopLayout(this,1856);
        foreach(string id in new[]{"knownBg","ingredientsBg","resultBg","stationBg","rebirthCraftingQueueRegionBg","rebirthCraftingInventoryRegionBg"})
            if(Find(id)?.ViewComponent is XUiV_Sprite bg)
            {var color=bg.Color;color.a=RebirthPersonalCraftingPanelOpacity.Alpha;if(bg.Color!=color)bg.Color=color;}
        foreach(var item in new[]{new[]{"cookingTools","1"},new[]{"cookingFuel","1"},new[]{"cookingOutput","0.9"}})
        {
            var v=Find(item[0])?.ViewComponent;
            if(v?.UiTransform!=null)v.UiTransform.localScale=Vector3.one*float.Parse(item[1],System.Globalization.CultureInfo.InvariantCulture);
        }
        Hide(xui.FindWindowGroupByName("windowpaging")?.Controller);
        Text("stationName",Localization.Get(station.Workstation)); Text("stationTitle","");
        if(Find("stationIcon")?.ViewComponent is XUiV_Sprite icon)
        {icon.SpriteName=ItemClass.GetItem(station.Workstation).ItemClass?.GetIconName()??"campfire";}
        if (station.IsMilling) LayoutMilling();
    }
    private void WireRecipeScroll(XUiController c)
    {
        if(c==null)return;
        if(c.ViewComponent!=null)c.ViewComponent.EventOnScroll=true;
        c.OnScroll+=(sender,delta)=>{if(recipeScrollFrame==Time.frameCount)return;recipeScrollFrame=Time.frameCount;recipeTarget=Mathf.Clamp(recipeTarget+(delta>0?-44:44),0,Math.Max(0,visibleRecipes.Count-RecipeRows)*44);};
        foreach(var child in c.Children)WireRecipeScroll(child);
    }
    private void ApplyRecipeScroll(float dt)
    {
        int max=Math.Max(0,visibleRecipes.Count-RecipeRows)*44;
        recipeTarget=Mathf.Clamp(recipeTarget,0,max);
        recipePixels=Mathf.Clamp(Mathf.Lerp(recipePixels,recipeTarget,Mathf.Clamp01(dt*18)),0,max);
        if(Mathf.Abs(recipePixels-recipeTarget)<.2f)recipePixels=recipeTarget;
        var host=Find("recipeRows")?.ViewComponent;
        if(host!=null){var pos=new Vector2i(0,Mathf.RoundToInt(recipePixels));if(host.Position!=pos){host.Position=pos;host.TryUpdatePosition();}}
        if(Find("recipeViewport")?.ViewComponent is XUiV_Panel clip && clip.UiTransform!=null)
        {
            var panel=clip.UiTransform.GetComponent<UIPanel>();
            if(panel!=null){var region=new Vector4(165,-176,330,352);if(panel.baseClipRegion!=region)panel.baseClipRegion=region;panel.clipping=UIDrawCall.Clipping.SoftClip;panel.clipSoftness=Vector2.zero;}
        }
        var track=Find("recipeTrack");
        RebirthScrollbarPresentation.Render(track,Find("recipeThumb"),new Vector2i(350,-124),
            RecipeTrackHeight,RecipeRows*44,RecipeRows*44+max,recipePixels);
    }
    private List<Recipe> Filtered()
    {
        if(!rendering)ReadInventoryCounts();
        string search=(Find("cookingSearch") as XUiC_TextInput)?.Text??"";
        bool previous=rendering;rendering=true;
        try{return catalogue.Where(r=>station.IsMilling ? RebirthCapabilityService.EvaluateRecipe(xui.playerUI.entityPlayer,r.GetName()).IsAllowed : RebirthCookingCatalogue.Known(xui.playerUI.entityPlayer,r)).Where(r=>(category==0 || RebirthCookingCatalogue.IsDrink(r) == (category==2))).Where(r=>Localization.Get(r.GetName()).IndexOf(search,StringComparison.CurrentCultureIgnoreCase)>=0).OrderByDescending(r=>MissingTool(r)==null&&Feasible(r)>0).ToList();}
        finally{rendering=previous;}
    }
    private int Feasible(Recipe recipe)
    {
        if(recipe==null)return 0;
        if(!rendering)return EvaluateFeasible(recipe);
        if(renderRecipeFeasibility.TryGetValue(recipe,out var value))return value;
        value=EvaluateFeasible(recipe);
        renderRecipeFeasibility[recipe]=value;
        return value;
    }
    private int EvaluateFeasible(Recipe recipe)
    {
        if(recipe==null)return 0;
        if(station.IsMilling)
        {
            if(millingFrameActive&&millingFrameFeasibility.TryGetValue(recipe,out int cached))return cached;
            int current=Plan(recipe,out _);
            if(millingFrameActive)millingFrameFeasibility[recipe]=current;
            return current;
        }
        if (!rendering || !renderFeasibilityFingerprint.HasValue)
            renderFeasibilityFingerprint = FeasibilityInputFingerprint();
        int fingerprint = renderFeasibilityFingerprint.Value;
        if(fingerprint!=feasibilityFingerprint){feasibilityFingerprint=fingerprint;feasibilityCache.Clear();}
        string key=(recipe.GetName()??string.Empty)+":"+RecipeDefinitionFingerprint(recipe);int value;
        if(feasibilityCache.TryGetValue(key,out value))return value;
        value=Plan(recipe,out _);feasibilityCache[key]=value;return value;
    }
    private int FeasibilityInputFingerprint()
    {
        unchecked
        {
            int hash=17;var keys=new List<int>(inventoryCounts.Keys);keys.Sort();
            for(int i=0;i<keys.Count;i++){int key=keys[i];hash=hash*31+key;hash=hash*31+inventoryCounts[key];}
            hash=hash*31+inventoryStateFingerprint;
            for(int i=0;i<slots.Length;i++)
            {
                ItemStack stack=slots[i]?.ItemStack;
                if(stack==null||stack.IsEmpty())continue;
                hash=hash*31+FingerprintStack(stack);
            }
            return hash;
        }
    }
    private static readonly string[] CookingMetadataKeys = Array.ConvertAll(RebirthCookingItemStats.Keys, key => "rebirth.cooking." + key);
    private static int FingerprintStack(ItemStack stack)
    {
        if(stack==null||stack.IsEmpty()||stack.itemValue==null)return 0;
        unchecked
        {
            ItemValue v=stack.itemValue;int hash=17;
            hash=hash*31+v.type;hash=hash*31+v.Meta;hash=hash*31+v.Quality;hash=hash*31+v.UseTimes.GetHashCode();hash=hash*31+stack.count;
            for(int i=0;i<RebirthCookingItemStats.Keys.Length;i++)
            {
                string key=CookingMetadataKeys[i];
                hash=hash*31+StringComparer.Ordinal.GetHashCode(key);
                if(v.Metadata!=null&&v.Metadata.ContainsKey(key))hash=hash*31+(v.Metadata[key]?.GetHashCode()??0);
            }
            if(RebirthConsumableResolver.TryResolve(v,out var food)&&food.IsDrink)hash=hash*31+RebirthLiquidContainerService.GetRemainingMl(v,food).GetHashCode();
            return hash;
        }
    }
    private int RecipeDefinitionFingerprint(Recipe recipe)
    {
        if(recipe==null)return 0;
        if(station.IsMilling)
        {
            if(millingFrameActive&&millingFrameFeasibility.TryGetValue(recipe,out int cached))return cached;
            int current=Plan(recipe,out _);
            if(millingFrameActive)millingFrameFeasibility[recipe]=current;
            return current;
        }
        unchecked
        {
            int hash=17;hash=hash*31+recipe.itemValueType;hash=hash*31+recipe.count;hash=hash*31+recipe.craftingToolType;
            var dish=RebirthCookingCatalogue.Get(recipe.GetName());
            if(recipe.ingredients!=null)foreach(var ingredient in recipe.ingredients)
            {
                if(ingredient==null)continue;
                hash=hash*31+ingredient.itemValue.type;hash=hash*31+ingredient.count;
                string original=ingredient.itemValue.ItemClass?.GetItemName()??string.Empty;
                if(dish!=null&&dish.Substitutes.TryGetValue(original,out var options))
                    foreach(string option in options.OrderBy(x=>x,StringComparer.Ordinal))hash=hash*31+StringComparer.Ordinal.GetHashCode(option??string.Empty);
            }
            return hash;
        }
    }
    private string MissingTool(Recipe recipe)
    {
        if(recipe==null)return null;
        if(rendering&&renderMissingTools.TryGetValue(recipe,out var cached))return cached;
        string missing=recipe.craftingToolType!=0&&!(station.toolWindow?.HasRequirement(recipe)??false)
            ?ItemClass.GetForId(recipe.craftingToolType)?.GetLocalizedItemName():null;
        if(rendering)renderMissingTools[recipe]=missing;
        return missing;
    }

    // Explicit destination for Shift-click: the hidden native output is never an input container.
    public bool AcceptsIngredient(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty()) return true;
        if (!station.IsMilling) return RebirthCookingCatalogue.IsIngredient(stack);
        return catalogue.Any(r => r.ingredients != null && r.ingredients.Any(i => i.itemValue.type == stack.itemValue.type));
    }
    public bool MoveIngredient(ItemStack remaining)
    {
        if(Preparation.IsPreparing||preparingRequest||submittingCook||pulling||!AcceptsIngredient(remaining))return false;
        int start=!station.IsMilling&&RebirthCookingCatalogue.IsHerb(remaining)?9:0,end=start==9?12:9;
        int initial=remaining.count;
        for(int pass=0;pass<2;pass++)for(int i=start;i<end&&remaining.count>0;i++)
        {
            var current=slots[i].ItemStack;
            if(pass==0?current.IsEmpty()||!current.CanStackWith(remaining):!current.IsEmpty())continue;
            int capacity=remaining.itemValue.ItemClass.MaxCount-(current.IsEmpty()?0:current.count);
            int amount=Math.Min(remaining.count,Math.Max(0,capacity));if(amount==0)continue;
            var next=current.IsEmpty()?remaining.Clone():current.Clone();
            next.count=(current.IsEmpty()?0:current.count)+amount;
            slots[i].ItemStack=next;remaining.count-=amount;
        }
        RefreshStationState();return remaining.count!=initial;
    }
    private string Method => station.Workstation=="WorkbenchIronOven001_FR"?"Baked":station.Workstation=="WorkbenchGasStove001_FR"&&!usePot?"Pan":"Soup";
    private string UnknownIcon => station.IsMilling ? ItemClass.GetItem(station.Workstation).ItemClass?.GetIconName() ?? "FR_MortarPestle_icon" : "rebirthImprovised"+Method+variation.ToString("00");
    private void ReadInventoryCounts()
    {
        long profile=RebirthCookingDiagnostics.Begin();
        renderFeasibilityFingerprint = null;
        inventoryCounts.Clear();
        inventoryStateFingerprint=17;
        var player=xui.playerUI.entityPlayer;
        var available=new List<ItemStack>(player.bag.ItemGrid.items);
        var belt=player.inventory.ItemGrid.items;
        int beltLimit=RebirthToolbeltCapacity.GetOwnedSlotCount(player,belt.Length);
        for(int i=0;i<beltLimit;i++)available.Add(belt[i]);
        if(RemoteResourcesRuntimePolicy.Enabled)
        {
            if(player.world.IsRemote())RemoteResourceClientAvailability.AppendStacks(player,available);
            else RemoteResourceSnapshotCache.Get(player).AppendClonedStacks(available);
        }
        foreach(var stack in available)
        {
            if(stack==null||stack.IsEmpty()||!RebirthCookingItemStats.FullIngredient(stack.itemValue))continue;
            inventoryStateFingerprint=unchecked(inventoryStateFingerprint*31+FingerprintStack(stack));
            inventoryCounts.TryGetValue(stack.itemValue.type,out int count);
            long combined=(long)count+stack.count;inventoryCounts[stack.itemValue.type]=combined>int.MaxValue?int.MaxValue:(int)combined;
        }
        RebirthCookingDiagnostics.Inventory(profile);
    }
    private int InventoryCount(ItemValue item)
    {
        if(!rendering)ReadInventoryCounts();
        return inventoryCounts.TryGetValue(item.type,out int count)?count:0;
    }
    private int Available(ItemValue item)
    {
        int count=InventoryCount(item);
        for(int i=0;i<slots.Length;i++)
        {
            var stack=slots[i]?.ItemStack;
            if(stack!=null&&!stack.IsEmpty()&&stack.itemValue.type==item.type&&RebirthCookingItemStats.FullIngredient(stack.itemValue))count+=stack.count;
        }
        return count;
    }
    private int Plan(Recipe recipe,out int[] types)
    {
        bool previous = rendering;
        if (!previous) ReadInventoryCounts();
        rendering = true;
        try
        {
            if(station.IsMilling)
            {
                types=new int[recipe.ingredients.Count];
                var totals=new Dictionary<int,int>();
                for(int i=0;i<recipe.ingredients.Count;i++)
                {
                    var ingredient=recipe.ingredients[i];
                    if(!RebirthCraftingIngredientQuantity.TryResolvePerBatch(xui.playerUI.entityPlayer,recipe,ingredient,1,out int count))return 0;
                    types[i]=ingredient.itemValue.type;
                    totals.TryGetValue(types[i],out int prior);
                    long combined=(long)prior+count;if(combined>int.MaxValue)return 0;
                    totals[types[i]]=(int)combined;
                }
                if(totals.Count>9)return 0;
                int maximum=9999;
                foreach(var pair in totals)
                    if(pair.Value>0)maximum=Math.Min(maximum,Available(new ItemValue(pair.Key))/pair.Value);
                return maximum;
            }
            var dish=RebirthCookingCatalogue.Get(recipe.GetName());
            var roles=recipe.ingredients.Select(g=>
            {
                var names=new List<string>{g.itemValue.ItemClass.GetItemName()};
                if(dish!=null&&dish.Substitutes.TryGetValue(names[0],out var options))names.AddRange(options);
                return names.Distinct().Select(n=>ItemClass.GetItem(n)).Where(v=>!v.IsEmpty()).Select(v=>new RebirthCookingIngredientPlan.Choice{Type=v.type,Required=g.count,Available=Available(v)}).ToList();
            }).ToArray();
            return RebirthCookingIngredientPlan.Solve(roles,out types);
        }
        finally { rendering = previous; }
    }
    private int MaximumBatch()
    {
        if(selected!=null&&!station.IsMilling)return ghosts.Take(9).Where(g=>g!=null).GroupBy(g=>g.itemValue.type).Select(group=>Available(group.First().itemValue)/Math.Max(1,group.Sum(g=>g.count))).DefaultIfEmpty(1).Min();
        if(selected!=null)
        {
            int maximum=9999;
            for(int i=0;i<9;i++)
            {
                if(ghosts[i]==null)continue;
                if(!TryGhostQuantity(i,1,out int required))return 0;
                if(required>0)maximum=Math.Min(maximum,Available(ghosts[i].itemValue)/required);
            }
            return maximum;
        }
        var inputs=slots.Take(9).Where(s=>!s.ItemStack.IsEmpty()).Select(s=>s.ItemStack).ToList();
        if(inputs.Count==0)return 1;
        foreach(var r in StationRecipesInPriorityOrder(matchingCatalogue))
        {
            if(RebirthCookingCatalogue.IsCooking(r)
                &&(station.toolWindow==null||station.toolWindow.HasRequirement(r))&&RebirthCookingHeatRules.Method(r)==Method
                &&Matches(r,inputs,false,1))
                return inputs.Min(i=>i.count/Math.Max(1,resolvedAmounts[i.itemValue.type]));
        }
        return inputs.Select(i=>i.count).DefaultIfEmpty(1).Min();
    }
    private void SetBatch(int value)
    {
        if(Preparation.IsPreparing||submittingCook||pulling)return;
        int next=Math.Max(1,Math.Min(9999,Math.Min(Math.Max(1,MaximumBatch()),value)));
        if(next<batch)
        {
            for(int i=0;i<slots.Length;i++)
            {
                var loaded=slots[i].ItemStack;
                if(loaded.IsEmpty()||ghosts[i]==null||!TryGhostQuantity(i,next,out int required)||loaded.count<=required)continue;
                var excess=loaded.Clone();excess.count-=required;
                RefundIngredient(excess,false);
                var remaining=loaded.Clone();remaining.count=required+excess.count;
                slots[i].ItemStack=remaining;
            }
        }
        batch=next;Render();
    }
    private void RefundIngredient(ItemStack stack,bool allowDrop)
    {
        var player=xui.playerUI.entityPlayer;
        foreach(var receipt in ingredientOrigins)
        {
            if(stack.count<=0)break;
            try { stack.count-=receipt.Restore(player,stack); }
            catch(Exception ex) { Log.Warning("[REBIRTH Cooking] Original ingredient slot unavailable: "+ex.Message); }
        }
        ingredientOrigins.RemoveAll(r=>r.Remaining<=0);
        if(stack.count>0&&xui.PlayerInventory.AddItem(stack,true))stack.count=0;
        if(stack.count>0&&allowDrop)
        {
            GameManager.Instance.ItemDropServer(stack,player.position,Vector3.zero,player.entityId,120f,false);
            stack.count=0;
        }
    }
    private void ReconcileIngredientOrigins()
    {
        var held=new Dictionary<int,int>();
        foreach(var slot in slots)
        {
            var stack=slot?.ItemStack;
            if(stack==null||stack.IsEmpty())continue;
            held.TryGetValue(stack.itemValue.type,out int count);held[stack.itemValue.type]=count+stack.count;
        }
        foreach(var receipt in ingredientOrigins)
        {
            int type=receipt.Item.itemValue.type;
            held.TryGetValue(type,out int count);
            receipt.Remaining=Math.Min(receipt.Remaining,count);
            held[type]=count-receipt.Remaining;
        }
        ingredientOrigins.RemoveAll(r=>r.Remaining<=0);
    }
    private void Select(Recipe recipe)
    {
        if(Preparation.IsPreparing||preparingRequest)CancelPreparation();
        ReturnIngredients(); selected=recipe; status="";
        for(int i=0;i<ghosts.Length;i++)ghosts[i]=null;
        if(recipe!=null)
        {
            int maximum=Plan(recipe,out var types);
            if(station.IsMilling)
            {
                int slot=0;
                foreach(var ingredient in recipe.ingredients.GroupBy(g=>g.itemValue.type).Select(g=>g.First()))
                {
                    if(slot>=9)break;
                    // Milling amounts are resolved by type on demand, including repeated native roles.
                    ghosts[slot++]=new ItemStack(ingredient.itemValue.Clone(),1);
                }
            }
            else for(int i=0;i<Math.Min(9,recipe.ingredients.Count);i++)ghosts[i]=new ItemStack(new ItemValue(types[i]==0?recipe.ingredients[i].itemValue.type:types[i]),recipe.ingredients[i].count);
            batch=1;
        }
        else batch=1;
        var dish=station.IsMilling?null:RebirthCookingCatalogue.Get(recipe?.GetName());int herbSlot=9;
        foreach(var herb in new[]{new[]{"basil","foodCropBasil","B23"},new[]{"dill","foodCropDill","B24"},new[]{"sage","foodCropSage","B25"}})
            if(dish!=null&&dish.Herbs.Contains(herb[0])&&RebirthCookingCatalogue.Studied(xui.playerUI.entityPlayer,"rebirthCookingBook"+herb[2]))
            {var value=ItemClass.GetItem(herb[1]);if(!value.IsEmpty())ghosts[herbSlot++]=new ItemStack(value,1);}
        Show("substitutionPopup",false); Render();
    }
    private void ReturnIngredients()
    {
        for(int i=0;i<slots.Length;i++)
        {
            if(slots[i]==null||slots[i].ItemStack.IsEmpty())continue;
            var stack=slots[i].ItemStack.Clone(); slots[i].ItemStack=ItemStack.Empty.Clone();
            RefundIngredient(stack,true);
        }
    }
    private static bool TryBatchQuantity(int perBatch,int batches,out int total)
    {
        total=0;if(perBatch<=0||batches<=0)return false;long wide=(long)perBatch*batches;if(wide>int.MaxValue)return false;total=(int)wide;return true;
    }
    private bool TryGhostQuantity(int slot,int batches,out int total)
    {
        total=0;
        if(slot<0||slot>=ghosts.Length||ghosts[slot]==null)return false;
        if(station.IsMilling&&selected!=null&&slot<9)
        {
            long sum=0;bool found=false;
            foreach(var ingredient in selected.ingredients)
            {
                if(ingredient.itemValue.type!=ghosts[slot].itemValue.type)continue;
                found=true;
                if(!RebirthCraftingIngredientQuantity.TryResolveTotal(xui.playerUI.entityPlayer,selected,ingredient,1,batches,out int quantity))return false;
                sum+=quantity;if(sum>int.MaxValue)return false;
            }
            total=(int)sum;return found;
        }
        return TryBatchQuantity(ghosts[slot].count,batches,out total);
    }
    private string[] AvailableSubstitutes(int slot)
    {
        if(station.IsMilling||selected==null||slot<0||slot>=selected.ingredients.Count)return Array.Empty<string>();
        string original=selected.ingredients[slot].itemValue.ItemClass.GetItemName();
        var dish=RebirthCookingCatalogue.Get(selected.GetName());
        if(dish==null||!dish.Substitutes.TryGetValue(original,out var choices))return Array.Empty<string>();
        string current=ghosts[slot]?.itemValue.ItemClass.GetItemName()??original;
        if(!TryBatchQuantity(selected.ingredients[slot].count,batch,out int need))return Array.Empty<string>();
        return new[]{original}.Concat(choices).Distinct().Where(id=>id!=current&&
            !ghosts.Where((g,i)=>i!=slot&&g!=null).Any(g=>g.itemValue.ItemClass.GetItemName()==id)&&
            Available(ItemClass.GetItem(id))>=need).ToArray();
    }
    private void ShowSubstitutes(int slot)
    {
        if(station.IsMilling||Preparation.IsPreparing||selected==null||slot<0||slot>=selected.ingredients.Count)return;
        var dish=RebirthCookingCatalogue.Get(selected.GetName());
        if(dish==null)return;
        string ingredient=selected.ingredients[slot].itemValue.ItemClass.GetItemName();
        if(!dish.Substitutes.TryGetValue(ingredient,out var choices))return;
        substitutionSlot=slot; alternatives=AvailableSubstitutes(slot).Take(6).ToArray();
        if(alternatives.Length==0)return;
        Text("substitutionHelp",RebirthSurvivorUiText.L("xuiRebirthCookingSubstitutionHelp", "Choose a replacement. Pull ingredients after selecting it."));
        int popupHeight=118+alternatives.Length*42;
        var popup=Find("substitutionPopup").ViewComponent;
        popup.Size=new Vector2i(340,popupHeight);
        popup.Position=new Vector2i(435+(slot%3)*90,-Math.Min(700-popupHeight,150+(slot/3)*90));
        Find("popupBg").ViewComponent.Size=new Vector2i(340,popupHeight);
        Find("closeSub").ViewComponent.Position=new Vector2i(12,-(popupHeight-42));
        for(int i=0;i<6;i++)
        {
            bool valid=i<alternatives.Length; string id=valid?alternatives[i]:"";
            bool used=valid&&ghosts.Where((g,j)=>j!=slot&&g!=null).Any(g=>g.itemValue.type==ItemClass.GetItem(id).type);
            Caption("choice"+i, valid?used?string.Format(RebirthSurvivorUiText.L("xuiRebirthCookingSubstitutionUsed", "{0} — already used"),Localization.Get(id)):string.Format(RebirthSurvivorUiText.L("xuiRebirthCookingSubstitutionAvailable", "{0} ({1})"),Localization.Get(id),xui.PlayerInventory.GetItemCount(ItemClass.GetItem(id))):"");
            if(Find("choiceIcon"+i)?.ViewComponent is XUiV_Sprite icon){icon.SpriteName=valid?ItemClass.GetItem(id).ItemClass?.GetIconName()??"":"";icon.IsVisible=valid;}
            Show("choice"+i,valid); Enable("choice"+i,valid&&!used);
        }
        Show("substitutionPopup",true);
    }
    private void Substitute(int index)
    {
        if(station.IsMilling||index<0||index>=alternatives.Length||Preparation.IsPreparing)return;
        var value=ItemClass.GetItem(alternatives[index]);
        if(value.IsEmpty()||ghosts.Where((g,j)=>j!=substitutionSlot&&g!=null).Any(g=>g.itemValue.type==value.type))return;
        // Substitution never moves an item. Loaded ingredients must first be taken back by the player.
        if(!slots[substitutionSlot].ItemStack.IsEmpty()){status=RebirthSurvivorUiText.L("xuiRebirthCookingRemoveLoadedIngredient", "Remove the loaded ingredient before changing this slot.");return;}
        ghosts[substitutionSlot]=new ItemStack(value,selected.ingredients[substitutionSlot].count);
        batch=Math.Max(1,Math.Min(batch,MaximumBatch()));Show("substitutionPopup",false);Render();
    }
    private void Pull()
    {
        if(RebirthBackpackLibraryReservation.BlocksResourceUse(xui?.playerUI?.entityPlayer)){status=Localization.Get("xuiRebirthLibraryTransferPending");return;}
        if(Preparation.IsPreparing||pulling||selected==null)return;
        batch=Math.Max(1,Math.Min(batch,MaximumBatch()));
        var needs=new List<ItemStack>();
        for(int i=0;i<12;i++)
        {
            var g=ghosts[i];if(g==null)continue;
            var loaded=slots[i].ItemStack;
            if(!loaded.IsEmpty()&&loaded.itemValue.type!=g.itemValue.type){status=RebirthSurvivorUiText.L("xuiRebirthCookingRemoveDifferentIngredient", "Remove the different ingredient from the slot first.");return;}
            if(!TryGhostQuantity(i,batch,out int requested)){status=RebirthSurvivorUiText.L("xuiRebirthCookingInvalidIngredientQuantity", "Ingredient quantity is invalid.");return;}
            int amount=Math.Max(0,requested-(loaded.IsEmpty()?0:loaded.count));
            if(i>=9&&xui.PlayerInventory.GetItemCount(g.itemValue)<amount)continue;
            if(amount>0){var required=g.itemValue.Clone();required.SetMetadata("rebirth.cooking.fullIngredient",1);needs.Add(new ItemStack(required,amount));}
        }
        if(needs.Count==0)return;
        var removed=new List<ItemStack>();
        var player=xui.playerUI.entityPlayer;
        if(player.world.IsRemote())
        {
            var missing=new List<ItemStack>();
            foreach(var n in needs)
            {
                int local=RebirthCookingLocalIngredients.Count(player,n.itemValue);
                if(local<n.count)missing.Add(new ItemStack(n.itemValue.Clone(),n.count-local));
            }
            if(missing.Count>0)
            {
                pulling=true;var original=selected;int originalBatch=batch;
                int generation=pullUiGeneration;
                status=RebirthSurvivorUiText.L("xuiRebirthCookingCollectingIngredients", "Collecting ingredients…");
                if(!RebirthCookingPull.Request(player,missing,(items,error)=>
                    CompleteIngredientPull(player,items,error,generation,original,originalBatch)))
                {
                    pulling=false;
                    status=Localization.Get("xuiRebirthCookingConnectionUnavailable");
                }
                return;
            }
            if(!RebirthCookingLocalIngredients.Take(player,needs,removed,ingredientOrigins)){status=RebirthSurvivorUiText.L("xuiRebirthCookingNotEnoughFullIngredients", "Not enough usable ingredients. Drink containers must be full.");Render();return;}
            xui.PlayerInventory.dispatchBackpackItemsChanged();xui.PlayerInventory.dispatchToolbeltItemsChanged();
        }
        else
        {
            var transfer=RemoteResourceTransactions.TryConsume(player,needs,1,removed,returnReceipts:ingredientOrigins);
            if(!transfer.Success)
            {
                ReturnPulledIngredients(player, removed);
                status=RebirthSurvivorUiText.L("xuiRebirthCookingNotEnoughAvailableIngredients", "Not enough available ingredients.");Render();return;
            }
        }
        foreach(var stack in removed)
        {
            int index=Array.FindIndex(ghosts,g=>g!=null&&g.itemValue.type==stack.itemValue.type);
            if(index<0){RefundIngredient(stack,true);continue;}
            var loaded=slots[index].ItemStack;
            if(loaded.IsEmpty())slots[index].ItemStack=stack.Clone();
            else if(RebirthCookingItemStats.Compatible(loaded.itemValue,stack.itemValue)) {var merged=loaded.Clone();merged.count+=stack.count;slots[index].ItemStack=merged;}
            else if(!xui.PlayerInventory.AddItem(stack,true)&&stack.count>0)GameManager.Instance.ItemDropServer(stack,player.position,Vector3.zero,player.entityId,120f,false);
        }
        status="";Render();
    }
    private void CompleteIngredientPull(EntityPlayerLocal player, IList<ItemStack> items, string error,
        int generation, Recipe original, int originalBatch)
    {
        pulling = false;
        // The receipt belongs to the player even when its initiating window closed.
        ReturnPulledIngredients(player, items);
        if (!open || generation != pullUiGeneration ||
            !ReferenceEquals(original, selected) || batch != originalBatch) return;
        status = error ?? "";
        if (status.Length == 0) Pull();
        if (station.UsesSharedMillingPresentation) CompleteSharedCraft();
    }

    private void ReturnPulledIngredients(EntityPlayerLocal player, IList<ItemStack> items)
    {
        if (player == null || items == null) return;
        for (int i = 0; i < items.Count; i++)
        {
            ItemStack item = items[i];
            if (item == null || item.IsEmpty() || item.count <= 0) continue;
            if (!xui.PlayerInventory.AddItem(item, true) && item.count > 0)
                GameManager.Instance.ItemDropServer(item, player.position, Vector3.zero, player.entityId, 120f, false);
        }
    }

    private Recipe Resolve()
    {
        resolvedAmounts.Clear();hiddenRecipe=false;
        var inputs=slots.Take(9).Where(s=>s!=null&&!s.ItemStack.IsEmpty()).Select(s=>s.ItemStack).ToList();
        if(inputs.Count==0)return selected;
        // A larger requested batch must not reinterpret an already selected dish.
        if(selected!=null)
        {
            foreach(var requirement in ghosts.Take(9).Where(g=>g!=null))
                resolvedAmounts[requirement.itemValue.type]=requirement.count;
            return selected;
        }
        var knownRecipe = ResolveKnownRecipe(matchingCatalogue, inputs);
        if (knownRecipe != null) return knownRecipe;
        if (station.IsMilling) { status = Localization.Get("xuiRebirthMillingNoRecipe"); return null; }
        if(inputs.Where(i=>!RebirthCookingCatalogue.IsHerb(i)).Select(i=>i.itemValue.type).Distinct().Count()<3){status=RebirthSurvivorUiText.L("xuiRebirthCookingImprovisedMainIngredients", "An improvised meal needs three different main ingredients.");return null;}
        if(!RebirthCookingHeatRules.ValidIngredients(inputs,Method,out status))return null;
        string category=Method;
        var item=ItemClass.GetItem("rebirthImprovised"+category+variation.ToString("00"));
        if(item.IsEmpty())return null;
        string tool=category=="Baked"?"FuriousRamsayBakingPan":category=="Pan"?"rebirthCookingFryingPan":"toolCookingPot";
        foreach(var input in inputs)resolvedAmounts[input.itemValue.type]=Math.Max(1,input.count/batch);
        float inputNutrition=inputs.Sum(i=>RebirthConsumableResolver.TryResolve(i.itemValue,out var d)?d.NutritionUnits*Math.Max(1,i.count/batch):0);
        if(inputNutrition<=0){status=RebirthSurvivorUiText.L("xuiRebirthCookingAddNourishingIngredients", "Add nourishing ingredients to make a meal.");return null;}
        int servings=Math.Max(1,(int)Math.Ceiling(inputNutrition*1.1f/35f));
        return new Recipe{itemValueType=item.type,count=servings,craftingArea=station.Workstation,craftingToolType=ItemClass.GetItem(tool).type,craftingTime=RebirthCookingHeatRules.Duration(category,inputs.Count),craftExpGain=5,UseIngredientModifier=false,
            ingredients=inputs.Select(s=>new ItemStack(s.itemValue.Clone(),Math.Max(1,s.count/batch))).ToList()};
    }
    private IEnumerable<Recipe> StationRecipesInPriorityOrder(IList<Recipe> recipes)
    {
        // The native list order breaks ties. Avoid sorting the entire list on each preview.
        for (int toolPass = 1; toolPass >= 0; toolPass--)
        {
            for (int index = 0; index < recipes.Count; index++)
            {
                Recipe source = recipes[index];
                if ((source.craftingToolType != 0) != (toolPass == 1) ||
                    !RebirthCookingCatalogue.AtStation(source, station.Workstation)) continue;
                yield return RebirthCookingCatalogue.ForStation(source, station.Workstation);
            }
        }
    }

    private Recipe ResolveKnownRecipe(IList<Recipe> recipes, List<ItemStack> inputs)
    {
        foreach (Recipe candidate in StationRecipesInPriorityOrder(recipes))
        {
            if (RebirthCookingCatalogue.IsCooking(candidate) && Matches(candidate, inputs, false) &&
                (station.toolWindow == null || station.toolWindow.HasRequirement(candidate)) &&
                (selected != null || RebirthCookingHeatRules.Method(candidate) == Method))
            {
                hiddenRecipe = !RebirthCookingCatalogue.Known(xui.playerUI.entityPlayer, candidate);
                return candidate;
            }
        }
        return null;
    }

    private bool Matches(Recipe r,List<ItemStack> inputs,bool useGhosts,int requestedBatch=0)
    {
        if(station.IsMilling)
        {
            int count=requestedBatch>0?requestedBatch:batch;
            if(inputs.Select(i=>i.itemValue.type).Distinct().Count()!=inputs.Count||
                !RebirthStationGridIngredients.TryPlan(xui.playerUI.entityPlayer,r,inputs,count,1,out var exact))return false;
            var amounts=new Dictionary<int,int>();
            foreach(var input in exact.Inputs)
            {
                if(input.Count%count!=0)return false;
                var paid=input.Snapshot();amounts[paid.itemValue.type]=input.Count/count;
            }
            resolvedAmounts.Clear();foreach(var pair in amounts)resolvedAmounts[pair.Key]=pair.Value;
            return true;
        }
        var requirements=useGhosts?ghosts.Take(9).Where(g=>g!=null).ToList():r.ingredients;
        if(inputs.Select(i=>i.itemValue.type).Distinct().Count()!=inputs.Count||inputs.Count!=requirements.Count)return false;
        var assignment=new Dictionary<int,int>();
        var dish=station.IsMilling?null:RebirthCookingCatalogue.Get(r.GetName());
        if(!AssignRoles(0,requirements,inputs,assignment,useGhosts?null:dish,requestedBatch>0?requestedBatch:batch))return false;
        resolvedAmounts.Clear();foreach(var pair in assignment)resolvedAmounts[pair.Key]=pair.Value;
        return true;
    }
    private bool AssignRoles(int role,List<ItemStack> requirements,List<ItemStack> inputs,Dictionary<int,int> assigned,RebirthCookingCatalogue.Dish dish,int requestedBatch)
    {
        if(role==requirements.Count)return true;
        var required=requirements[role];
        foreach(var input in inputs)
        {
            int type=input.itemValue.type;
            if(!TryBatchQuantity(required.count,requestedBatch,out int requiredTotal))return false;
            if(assigned.ContainsKey(type)||input.count<requiredTotal)continue;
            bool allowed=type==required.itemValue.type;
            if(!allowed&&dish!=null&&dish.Substitutes.TryGetValue(required.itemValue.ItemClass.GetItemName(),out var choices))
                allowed=choices.Contains(input.itemValue.ItemClass.GetItemName());
            if(!allowed)continue;
            assigned[type]=required.count;
            if(AssignRoles(role+1,requirements,inputs,assigned,dish,requestedBatch))return true;
            assigned.Remove(type);
        }
        return false;
    }
    private void Prepare()
    {
        if(RebirthBackpackLibraryReservation.BlocksResourceUse(xui?.playerUI?.entityPlayer)){status=Localization.Get("xuiRebirthLibraryTransferPending");return;}
        if(!open||!windowGroup.isShowing)return;
        Render();
        if(result==null||Preparation.IsPreparing||(book==null&&magazine==null))return;
        pendingBook=book;pendingMagazine=magazine;
        preparationInputs=slots.Select(s=>s.ItemStack.Clone()).ToArray();preparationBatch=batch;preparingRequest=true;int generation=++prepGeneration;string recipe=Key;
        RebirthCookingSessionService.Request(xui.playerUI.entityPlayer,"begin",recipe,book,magazine,reply:error=>
        {
            if(!open||generation!=prepGeneration||Key!=recipe)return;
            if(!PreparationInputsMatch()){CancelPreparation();return;}
            preparingRequest=false;status=error;
            if(error.Length==0)Preparation.Begin(recipe,Now);
            Render();
        });
    }
    private bool PreparationInputsMatch()
    {
        if(preparationInputs==null||preparationInputs.Length!=slots.Length||preparationBatch!=batch)return false;
        for(int i=0;i<slots.Length;i++)
        {
            ItemStack current=slots[i]?.ItemStack,admitted=preparationInputs[i];
            if(current==null||admitted==null||current.count!=admitted.count||current.itemValue==null
                ||!current.itemValue.Equals(admitted.itemValue))return false;
        }
        return true;
    }
    private int InputFingerprint(){int hash=batch;foreach(var s in slots)hash=unchecked(hash*31+s.ItemStack.itemValue.type*7+s.ItemStack.count);return hash;}
    private Recipe BatchRecipe(string preparedMagazine, bool ghostsForPreview = false)
    {
        var recipe=new Recipe{itemValueType=result.itemValueType,count=result.count,craftingArea=station.Workstation,
            craftingToolType=result.craftingToolType,craftingTime=XUiM_Recipes.GetRecipeCraftTime(xui,result)*RebirthCookingRules.TechniqueTime(RebirthCookingCatalogue.Effect(preparedMagazine)),
            craftExpGain=result.craftExpGain,UseIngredientModifier=false,craftingTier=1};
        if(station.IsMilling)
        {
            foreach(var ingredient in result.ingredients)
            {
                if(!RebirthCraftingIngredientQuantity.TryResolvePerBatch(xui.playerUI.entityPlayer,result,ingredient,1,out int count))continue;
                if(count>0)recipe.ingredients.Add(new ItemStack(ingredient.itemValue.Clone(),count));
            }
            return recipe;
        }
        for(int i=0;i<12;i++)
        {
            var loaded=slots[i].ItemStack;
            if(ghostsForPreview&&ghosts[i]!=null)
            {
                if(!TryBatchQuantity(ghosts[i].count,batch,out int previewCount))continue;
                loaded=new ItemStack(!loaded.IsEmpty()&&loaded.itemValue.type==ghosts[i].itemValue.type?loaded.itemValue.Clone():ghosts[i].itemValue.Clone(),previewCount);
            }
            if(loaded.IsEmpty())continue;
            int perBatch=i<9&&resolvedAmounts.TryGetValue(loaded.itemValue.type,out var amount)?amount:Math.Max(1,loaded.count/batch);
            recipe.ingredients.Add(new ItemStack(loaded.itemValue.Clone(),perBatch));
        }
        return recipe;
    }
    private long cookingReadinessRevision,cachedCookingReadinessRevision=-1;
    private Recipe cachedCookingReadinessRecipe;
    private string cachedCookingReadinessBlocker;
    private bool cachedCookingReadinessValid;
    // A revision is one complete Render; never retain native quantity effects across renders.
    // Cook always rebuilds: presentation cache cannot authorize payment or enqueue.
    private bool TryCookingPreflight(bool fresh,out Recipe recipe,out string blocker)
    {
        if(fresh)return BuildCookingPreflight(out recipe,out blocker);
        if(cachedCookingReadinessRevision!=cookingReadinessRevision)
        {
            cachedCookingReadinessValid=BuildCookingPreflight(out cachedCookingReadinessRecipe,out cachedCookingReadinessBlocker);
            cachedCookingReadinessRevision=cookingReadinessRevision;
        }
        recipe=cachedCookingReadinessRecipe;blocker=cachedCookingReadinessBlocker;
        return cachedCookingReadinessValid;
    }
    private sealed class CookingPreflightWitness
    {
        internal EntityPlayerLocal Owner;internal World World;internal Recipe Result,Selected;internal object Station,Queue,Xui,PlayerUi,WindowGroup;internal object[] QueueEntries;internal Recipe[] QueueRecipes;internal int UiGeneration;
        internal int Batch;internal string Magazine,ResultImage,SelectedImage;internal object ResultEffects,SelectedEffects;internal ItemStack[] Inputs;
    }
    private CookingPreflightWitness lastBuiltCookingWitness;
    // Exact native recipe image plus omitted native catalogue fields; comparison only, no grant authority.
    private static string CookingRecipeImage(Recipe source)
    {
        if(source==null)return null;
        using(var bytes=new System.IO.MemoryStream())
        {
            using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                writer.SetBaseStream(bytes);source.Write(writer);writer.Write(source.craftingToolType);
                writer.Write(source.UseIngredientModifier);writer.Write(source.tags.ToString());
                writer.Write(source.materialBasedRecipe);writer.Write(source.wildcardForgeCategory);writer.Write(source.wildcardCampfireCategory);
            }
            return Convert.ToBase64String(bytes.ToArray());
        }
    }
    private bool CookingPreflightContextCurrent(CookingPreflightWitness witness)
    {
        return witness!=null&&open&&windowGroup!=null&&windowGroup.isShowing&&
            ReferenceEquals(xui,witness.Xui)&&ReferenceEquals(xui?.playerUI,witness.PlayerUi)&&ReferenceEquals(windowGroup,witness.WindowGroup)&&pullUiGeneration==witness.UiGeneration&&
            !preparingRequest&&!submittingCook&&!Preparation.IsPreparing&&!RebirthBackpackLibraryReservation.BlocksResourceUse(witness.Owner)&&
            ReferenceEquals(xui?.playerUI?.entityPlayer,witness.Owner)&&witness.Owner!=null&&
            ReferenceEquals(witness.Owner.world,witness.World)&&ReferenceEquals(GameManager.Instance?.World,witness.World)&&
            ReferenceEquals(witness.World.GetEntity(witness.Owner.entityId),witness.Owner)&&
            ReferenceEquals(result,witness.Result)&&ReferenceEquals(selected,witness.Selected)&&batch==witness.Batch&&ActiveMagazine==witness.Magazine&&
            ReferenceEquals(station,witness.Station)&&ReferenceEquals(station?.craftingQueue,witness.Queue);
    }
    private bool CookingPreflightStillCurrent(CookingPreflightWitness witness)
    {
        if(!CookingPreflightContextCurrent(witness)||witness.Inputs==null||witness.Inputs.Length!=slots.Length)return false;
        // Bounded repeated readback catches a later-cell serialization reentry changing an earlier cell.
        for(int pass=0;pass<2;pass++)
        {
            if(!CookingPreflightContextCurrent(witness)||CookingRecipeImage(result)!=witness.ResultImage||CookingRecipeImage(selected)!=witness.SelectedImage||
                !ReferenceEquals(result?.Effects,witness.ResultEffects)||!ReferenceEquals(selected?.Effects,witness.SelectedEffects))return false;
            var currentQueue=station.craftingQueue.GetRecipesToCraft().ToArray();
            if(witness.QueueEntries==null||witness.QueueRecipes==null||currentQueue.Length!=witness.QueueEntries.Length||currentQueue.Length!=witness.QueueRecipes.Length)return false;
            if(station.IsMilling&&!currentQueue.Any(entry=>entry.GetRecipe()==null))return false;
            for(int i=0;i<currentQueue.Length;i++)if(!ReferenceEquals(currentQueue[i],witness.QueueEntries[i])||!ReferenceEquals(currentQueue[i].GetRecipe(),witness.QueueRecipes[i])||(!station.IsMilling&&currentQueue[i].GetRecipe()!=null))return false;
            for(int i=0;i<slots.Length;i++)if(!RebirthStationGridIngredients.IsSameStackSnapshot(slots[i].ItemStack,witness.Inputs[i]))return false;
        }
        return CookingPreflightContextCurrent(witness);
    }
    private bool BuildCookingPreflight(out Recipe recipe,out string blocker)
    {
        recipe=null;blocker=null;
        if(RebirthBackpackLibraryReservation.BlocksResourceUse(xui?.playerUI?.entityPlayer)){blocker=Localization.Get("xuiRebirthLibraryTransferPending");return false;}
        if(Preparation.IsPreparing||preparingRequest){blocker=RebirthSurvivorUiText.L("xuiRebirthCookingPreparing","Preparing");return false;}
        if(submittingCook){blocker=RebirthSurvivorUiText.L("xuiRebirthCookingSubmitting","Waiting for the cooking request…");return false;}
        if(result==null){blocker=RebirthSurvivorUiText.L("xuiRebirthCookingAddIngredients","Add ingredients");return false;}
        var originalOwner=xui?.playerUI?.entityPlayer;var originalWorld=originalOwner?.world;
        var originalResult=result;var originalSelected=selected;int originalBatch=batch;string originalMagazine=ActiveMagazine;
        var inputSnapshot=slots.Select(s=>s.ItemStack.Clone()).ToArray();
        var witness=new CookingPreflightWitness{Owner=originalOwner,World=originalWorld,Result=originalResult,Selected=originalSelected,Batch=originalBatch,Magazine=originalMagazine,Inputs=inputSnapshot,Xui=xui,PlayerUi=xui.playerUI,WindowGroup=windowGroup,UiGeneration=pullUiGeneration,Station=station,Queue=station.craftingQueue,QueueEntries=station.craftingQueue.GetRecipesToCraft().Cast<object>().ToArray(),QueueRecipes=station.craftingQueue.GetRecipesToCraft().Select(entry=>entry.GetRecipe()).ToArray(),ResultImage=CookingRecipeImage(originalResult),SelectedImage=CookingRecipeImage(originalSelected),ResultEffects=originalResult?.Effects,SelectedEffects=originalSelected?.Effects};
        var inputs=inputSnapshot.Take(9).Where(s=>!s.IsEmpty()).ToList();
        if(inputs.Count==0){blocker=RebirthSurvivorUiText.L("xuiRebirthCookingPullOrAddFirst", "Pull or add ingredients first.");return false;}
        if(selected!=null&&!Matches(selected,inputs,true)){blocker=RebirthSurvivorUiText.L("xuiRebirthCookingLoadRequestedServings", "Load all ingredients for the requested servings.");return false;}
        if(inputs.Any(i=>!RebirthCookingItemStats.FullIngredient(i.itemValue))){blocker=RebirthSurvivorUiText.L("xuiRebirthCookingFullContainersOnly", "Drink ingredients must be in full containers.");return false;}
        if(inputSnapshot.Any(s=>!s.IsEmpty()&&s.count<batch)||inputSnapshot.Where(s=>!s.IsEmpty()).GroupBy(s=>s.itemValue.type).Any(g=>g.Count()>1)){blocker=RebirthSurvivorUiText.L("xuiRebirthCookingOneSlotPerIngredient", "Use each ingredient type in one slot, with enough for the batch.");return false;}
        recipe=BatchRecipe(ActiveMagazine);
        if(station.IsMilling)
        {
            RebirthStationGridIngredients.Plan exact;
            if(inputSnapshot.Skip(9).Any(s=>!s.IsEmpty())||
                !RebirthStationGridIngredients.TryPlan(xui.playerUI.entityPlayer,result,
                    inputSnapshot.Take(9).ToArray(),batch,recipe.craftingTier,out exact))
            {blocker=Localization.Get("xuiRebirthStationExactMaterialsRequired");return false;}
            // The live queue must pay the same native-adjusted quantities admitted by the grid.
            // Milling currently permits one slot per type, so each fragment is a whole per-batch requirement.
            recipe.ingredients.Clear();
            foreach(var allocation in exact.Inputs)
            {
                if(allocation.Count%batch!=0){blocker=Localization.Get("xuiRebirthStationExactMaterialsRequired");return false;}
                var paid=allocation.Snapshot();paid.count=allocation.Count/batch;recipe.ingredients.Add(paid);
            }
        }
        if(!station.CraftingRequirementsValid(recipe)){blocker=station.CraftingRequirementsInvalidMessage(recipe);return false;} // Outcome already names the missing requirement.

        if(!station.IsMilling&&station.craftingQueue.GetRecipesToCraft().Any(e=>e.GetRecipe()!=null)){blocker=RebirthSurvivorUiText.L("xuiRebirthCookingTakeCurrentBatch","Take the current batch before starting another.");return false;}
        if(!CookingPreflightStillCurrent(witness)){recipe=null;blocker=Localization.Get("xuiRebirthStationExactMaterialsRequired");return false;}
        lastBuiltCookingWitness=witness;return true;
    }

    private void Cook()
    {
        if(RebirthBackpackLibraryReservation.BlocksResourceUse(xui?.playerUI?.entityPlayer)){status=Localization.Get("xuiRebirthLibraryTransferPending");return;}
        if (!open || !windowGroup.isShowing) return;
        Render(); // Resolve the current grid, including a last-moment drag or batch change.
        Recipe recipe;string blocker;if(!TryCookingPreflight(true,out recipe,out blocker)){status=blocker;return;}
        var paidWitness=lastBuiltCookingWitness;
        RebirthCookingBatch.Stamp(recipe,ActiveBook,xui.playerUI.entityPlayer,ActiveMagazine,batch,true);
        RebirthCookingHeat.Mark(recipe,batch,hiddenRecipe,RebirthCookingHeatRules.BurnMethod(recipe),UnknownIcon);
        if(!CookingPreflightStillCurrent(paidWitness)){status=Localization.Get("xuiRebirthStationExactMaterialsRequired");return;}
        submittingCook=true;int fingerprint=InputFingerprint();int quantity=batch;
        var submittedInputs=slots.Select(s=>s.ItemStack.Clone()).ToArray();
        int cookGeneration=pullUiGeneration;EntityPlayerLocal cookOwner=xui.playerUI.entityPlayer;
        RebirthCookingSessionService.Request(cookOwner,"register",recipe.GetName(),magazine:RebirthCookingHeat.Position(station.WorkstationData.TileEntity.ToWorldPos()),item:RebirthCookingBatch.Receipt(recipe),count:recipe.count,reply:error=>
        {
            if(!IsCurrentSession(cookGeneration,cookOwner))
            {
                // Abandon only this submitting owner's receipt; never touch a new window's pending state.
                RebirthCookingSessionService.Request(cookOwner,"abandonRegistration",item:RebirthCookingBatch.Receipt(recipe));
                return;
            }
            submittingCook=false;
            if(error.Length>0){status=error;return;}
            if(!open||fingerprint!=InputFingerprint()||quantity!=batch){AbandonRegistration(recipe);return;}
            if(slots.Where((s,i)=>!RebirthStationGridIngredients.IsSameStackSnapshot(s.ItemStack,submittedInputs[i])).Any()){AbandonRegistration(recipe);return;}
            Enqueue(recipe);
        });
    }
    private void AbandonRegistration(Recipe recipe)
    {
        RebirthCookingSessionService.Request(xui.playerUI.entityPlayer, "abandonRegistration", item: RebirthCookingBatch.Receipt(recipe));
    }
    private void Enqueue(Recipe recipe)
    {
        // The native queue stores the concrete ingredient list, so cancellation refunds substitutions.
        if(!station.IsMilling&&station.craftingQueue.GetRecipesToCraft().Any(e=>e.GetRecipe()!=null)){status=RebirthSurvivorUiText.L("xuiRebirthCookingTakeCurrentBatch", "Take the current batch before starting another.");AbandonRegistration(recipe);return;}
        var totals=new List<int>(recipe.ingredients.Count);
        foreach(var ingredient in recipe.ingredients)
        {
            if(!TryBatchQuantity(ingredient.count,batch,out int total)){status=RebirthSurvivorUiText.L("xuiRebirthCookingInvalidIngredientQuantity", "Ingredient quantity is invalid.");AbandonRegistration(recipe);return;}
            totals.Add(total);
        }
        if(!station.AddItemToQueue(recipe,batch,recipe.craftingTime*batch)){status=RebirthSurvivorUiText.L("xuiRebirthCookingQueueFull", "Cooking queue is full.");AbandonRegistration(recipe);return;}
        for(int ingredientIndex=0;ingredientIndex<recipe.ingredients.Count;ingredientIndex++)
        {
            var ingredient=recipe.ingredients[ingredientIndex];
            int needed=totals[ingredientIndex];
            foreach(var slot in slots)
            {
                var s=slot.ItemStack;if(s.IsEmpty()||s.itemValue.type!=ingredient.itemValue.type)continue;
                int take=Math.Min(needed,s.count);var remaining=s.Clone();remaining.count-=take;
                slot.ItemStack=remaining.count==0?ItemStack.Empty.Clone():remaining;needed-=take;if(needed==0)break;
            }
        }
        if(RebirthCookingBatch.NeedsHeat(recipe))station.fuelWindow?.TurnOn(); station.syncTEfromUI(); variation=UnityEngine.Random.Range(1,5); status="";Render();
    }
    private void Render()
    {
        unchecked{cookingReadinessRevision++;} // At most one native plan for this render revision.

        if(!open)return;
        long profile=RebirthCookingDiagnostics.Begin();
        ReconcileIngredientOrigins();
        ReadInventoryCounts();renderRecipeFeasibility.Clear();renderMissingTools.Clear();rendering=true;
        millingFrameFeasibility.Clear();millingFrameActive=true;
        try {
            if(station.UsesSharedMillingPresentation){result=Resolve();book=null;magazine=null;}
            else RenderContents();
        }
        finally { millingFrameActive=false;millingFrameFeasibility.Clear();renderRecipeFeasibility.Clear();renderMissingTools.Clear();rendering=false;RebirthCookingDiagnostics.Render(profile); }
    }
    private void RenderContents()
    {
        // Native GetRecipesToCraft allocates a list. Reuse this render's result
        // for both the guide and action state; Cook still rechecks at submission.
        bool queueOccupied = station.craftingQueue.GetRecipesToCraft().Any(e=>e.GetRecipe()!=null);
        Show("cookingGuide",!queueOccupied);
        bool milling = station.IsMilling;
        Show("herbsLabel", !milling);
        Show("ingredientGuide", !milling);
        for (int herb = 9; herb < 12; herb++) Show("ingredient" + herb, !milling);
        foreach (string id in HeatControls)
            Show(id, !milling);
        Text("stationQueueTitle", milling ? Localization.Get("xuiRebirthStationProcessingArea") : RebirthSurvivorUiText.L("xuiRebirthCookingAreaTitle", "COOKING AREA"));
        Caption("cook", milling ? Localization.Get("xuiRebirthStationProcess") : RebirthSurvivorUiText.L("xuiRebirthCookingCookButton", "COOK"));
        Text("outcomeStatusTitle", milling ? Localization.Get("xuiRebirthStationProcessStatus") : RebirthSurvivorUiText.L("xuiRebirthCookingStatusTitle", "COOK STATUS"));
        Text("cookingGuideTitle", Localization.Get(milling ? "xuiRebirthMillingGuideTitle" : "rbCookingGuideTitle"));
        Text("cookingGuideBody", Localization.Get(milling ? "xuiRebirthMillingGuideBody" : "rbCookingGuideBody"));
        if(selected==null&&!submittingCook)
        {
            int hash=17;foreach(var s in slots.Take(9))hash=unchecked(hash*31+s.ItemStack.itemValue.type*7+s.ItemStack.count);
            if(hash!=lastFreeInputHash){lastFreeInputHash=hash;batch=1;}
        }
        result=Resolve();
        var dish=hiddenRecipe?null:RebirthCookingCatalogue.Get(Key);
        book=hiddenRecipe?null:RebirthCookingCatalogue.Reference(xui,dish?.Books);magazine=hiddenRecipe?null:RebirthCookingCatalogue.Reference(xui,dish?.Magazines);
        if(!hiddenRecipe&&Key!=null&&chosenMagazines.TryGetValue(Key,out var chosen)&&dish!=null&&dish.Magazines.Contains(chosen)&&RebirthCookingCatalogue.Studied(xui.playerUI.entityPlayer,chosen)&&InventoryCount(ItemClass.GetItem(chosen))>0)magazine=chosen;
        for(int c=0;c<3;c++)if(Find("filter"+c)?.ViewComponent is XUiV_Button filter)filter.DefaultSpriteColor=c==category?new Color32(220,198,87,255):Color.white;
        var list=Filtered();visibleRecipes=list;
        for(int i=0;i<RecipeCapacity;i++)
        {
            var r=i<list.Count?list[i]:null;
            if(Find("dishBg"+i)?.ViewComponent is XUiV_Sprite bg){string sprite=r!=null&&r==selected?"ui_game_select_row":"menu_empty2px";Color color=r!=null&&r==selected?Color.white:new Color32(20,20,24,225);if(bg.SpriteName!=sprite)bg.SpriteName=sprite;if(bg.Color!=color)bg.Color=color;}
            string missingTool=MissingTool(r);
            bool available=r!=null&&missingTool==null&&Feasible(r)>0;
            Text("dishStatus"+i,r==null?"":missingTool!=null?"[FF9696]"+string.Format(Localization.Get("xuiRebirthCookingMissingToolFormat"),missingTool)+"[-]":available?"[70C47E]"+Localization.Get("xuiRebirthCookingReady")+"[-]":Localization.Get("xuiRebirthCookingMissingIngredients"));
            Text("dishSkill"+i, r == null ? "" : milling ? RebirthSkillDisplayNames.Get(RebirthServiceCraftSkillService.ClassifyRecipe(r)) : RebirthServiceCraftSkillService.ClassifyRecipe(r)=="skill.drink_preparation" ? RebirthSurvivorUiText.L("xuiRebirthCookingDrinksCategory", "DRINKS") : RebirthSurvivorUiText.L("xuiRebirthCookingCookingCategory", "COOKING"));
            Caption("dish"+i,r==null?"":Localization.Get(r.GetName()));Show("dish"+i,r!=null);
            if(Find("dish"+i)?.GetChildById("label")?.ViewComponent is XUiV_Label title)title.Color=available?Color.white:new Color32(180,180,185,255);
            if(Find("dishIcon"+i)?.ViewComponent is XUiV_Sprite icon){string sprite=r?.GetIcon()??"";if(icon.SpriteName!=sprite)icon.SpriteName=sprite;if(icon.IsVisible!=(r!=null))icon.IsVisible=r!=null;}
        }
        ApplyRecipeScroll(0);
        if(quantityInput!=null&&UIInput.selection!=quantityInput.uiInput&&quantityInput.Text!=batch.ToString())quantityInput.Text=batch.ToString();
        Show("cookingMethod",selected==null&&station.Workstation=="WorkbenchGasStove001_FR");Caption("cookingMethod",Method=="Soup"?RebirthSurvivorUiText.L("xuiRebirthCookingPotMethod", "COOKING POT"):RebirthSurvivorUiText.L("xuiRebirthCookingSkilletMethod", "SKILLET"));
        var craftAction=xui.playerUI.playerInput?.GUIActions?.DPad_Up;
        Text("cookShortcut",craftAction?.GetBindingString(false,_emptyStyle:XUiUtils.EmptyBindingStyle.EmptyString,_displayStyle:XUiUtils.DisplayStyle.KeyboardWithAngleBrackets)?.Trim('<','>')??"");
        for(int i=0;i<12;i++)
        {
            var g=ghosts[i];var loaded=slots[i].ItemStack;
            slots[i].ViewComponent.Position=Vector2i.zero;
            if(Find("ghost"+i)?.ViewComponent is XUiV_Sprite icon){icon.SpriteName=g?.itemValue?.ItemClass?.GetIconName()??"";icon.IsVisible=g!=null&&loaded.IsEmpty();}
            var value=!loaded.IsEmpty()?loaded.itemValue:g?.itemValue;
            Text("millingIngredientName"+i, value?.ItemClass == null ? "" : Localization.Get(value.ItemClass.GetItemName()));
            Show("millingIngredientName"+i, milling && value != null);
            int have=value==null?0:InventoryCount(value)+(loaded.IsEmpty()?0:loaded.count),need=g==null?(!loaded.IsEmpty()?Math.Max(1,loaded.count/batch)*batch:0):(TryGhostQuantity(i,batch,out int requiredTotal)?requiredTotal:int.MaxValue);
            Text("need"+i,value==null?"":need+" ["+(have>=need?"70C47E":"E05D5D")+"]("+have+")[-]");
            var sub=!station.IsMilling&&selected!=null&&i<selected.ingredients.Count?RebirthCookingCatalogue.Get(selected.GetName()):null;
            Show("sub"+i,AvailableSubstitutes(i).Length>0);
            string tip=value==null?"":string.Format(RebirthSurvivorUiText.L("xuiRebirthCookingIngredientTooltip", "{0}\nRequired: {1} | Available: {2}"),Localization.Get(value.ItemClass.GetItemName()),need,have);
            if(sub!=null&&sub.Substitutes.TryGetValue(selected.ingredients[i].itemValue.ItemClass.GetItemName(),out var swaps))
                tip+=string.Format(RebirthSurvivorUiText.L("xuiRebirthCookingSubstitutionsTooltip", "\nSubstitutions: {0}\nUse the swap button to choose."),string.Join(", ",swaps.Select(s=>Localization.Get(s))));
            if(slots[i].SuggestedTooltip!=tip){slots[i].SuggestedTooltip=tip;slots[i].IsDirty=true;}
        }
        Text("resultName",result==null?RebirthSurvivorUiText.L("xuiRebirthCookingAddIngredients", "Add ingredients"):(hiddenRecipe?(milling ? Localization.Get("xuiRebirthStationUnfamiliarRecipe") : RebirthSurvivorUiText.L("xuiRebirthCookingUnfamiliarDish", "Unfamiliar dish")):Localization.Get(result.GetName())));
        if(Find("resultIcon")?.ViewComponent is XUiV_Sprite resultIcon)resultIcon.SpriteName=hiddenRecipe?UnknownIcon:result?.GetIcon()??"";
        foreach(string stat in new[]{"Nutrition","Water","Comfort","Time"})Text("stat"+stat,"—");
        Text("outcomeName",result==null?(milling ? Localization.Get("xuiRebirthStationNoRecipe") : RebirthSurvivorUiText.L("xuiRebirthCookingNoMealSelected", "No meal selected")):(hiddenRecipe?(milling ? Localization.Get("xuiRebirthStationUnfamiliarRecipe") : RebirthSurvivorUiText.L("xuiRebirthCookingUnfamiliarDish", "Unfamiliar dish")):Localization.Get(result.GetName()))+" ×"+(result.count*batch));
        string skillId = result == null ? "skill.cooking" : RebirthServiceCraftSkillService.ClassifyRecipe(result.GetName());
        bool hasSkill = result != null && !string.IsNullOrEmpty(skillId);
        RebirthCraftSkillPreview.Snapshot skillPreview = hasSkill
            ? outcomeSkillPreview.Get(xui.playerUI.entityPlayer, result, batch, ActiveBook) : null;
        Show("outcomeXpTitle", hasSkill); Show("outcomeXp", hasSkill);
        Text("outcomeXp", hasSkill ? RebirthCraftSkillPreview.GainText(skillPreview) : "");
        bool loadedIngredients=selected!=null?Matches(selected,slots.Take(9).Where(s=>!s.ItemStack.IsEmpty()).Select(s=>s.ItemStack).ToList(),true):slots.Take(9).Any(slot=>!slot.ItemStack.IsEmpty());
        string missing=MissingTool(result);
        bool missingFuel=result!=null&&RebirthCookingBatch.NeedsHeat(result)&&station.fuelWindow?.WorkstationData!=null&&!station.fuelWindow.HasRequirement(result);
        Show("requiredToolIcon",missing!=null); Show("requiredToolText",missing!=null);
        Show("requiredFuelIcon",missingFuel); Show("requiredFuelText",missingFuel);
        Show("outcomeHint",missing==null&&!missingFuel);
        if(missing!=null)
        {
            if(Find("requiredToolIcon")?.ViewComponent is XUiV_Sprite toolIcon)
                toolIcon.SpriteName=ItemClass.GetForId(result.craftingToolType)?.GetIconName()??"";
            Text("requiredToolText",Localization.Get("xuiTools")+": "+missing);
        }

        Recipe readyRecipe;string readyBlocker;bool ready=TryCookingPreflight(false,out readyRecipe,out readyBlocker);
        Text("outcomeStatus",missing!=null?"[FF9696]"+Localization.Get("xuiTools")+": "+missing+"[-]":missingFuel?"[F07070]"+Localization.Get("xuiRebirthMissingFuel")+"[-]":!ready?"[F07070]"+readyBlocker+"[-]":milling?"[70C47E]"+Localization.Get("xuiRebirthStationReadyToProcess")+"[-]":"[70C47E]"+RebirthSurvivorUiText.L("xuiRebirthCookingReadyToCook","Ready to cook")+"[-]");
        Text("outcomeHint",missing!=null?"":result==null?RebirthSurvivorUiText.L("xuiRebirthCookingChooseRecipeHint", "Choose a recipe or assemble ingredients."):!loadedIngredients?RebirthSurvivorUiText.L("xuiRebirthCookingPullOrPlaceHint", "Pull ingredients or place them in the grid."):station.CraftingRequirementsValid(result)&&!RebirthCookingBatch.NeedsHeat(result)?milling ? Localization.Get("xuiRebirthMillingNoFuel") : RebirthSurvivorUiText.L("xuiRebirthCookingColdPreparation", "Cold preparation — no fuel required."):"");
        Text("ingredientGuide",selected==null?milling ? Localization.Get("xuiRebirthMillingIngredientGuide") : RebirthSurvivorUiText.L("xuiRebirthCookingDiscoveryGuide", "Combine ingredients to discover a dish or make an improvised meal."):Enumerable.Range(0,9).Any(i=>AvailableSubstitutes(i).Length>0)?RebirthSurvivorUiText.L("xuiRebirthCookingSwapGuide", "Select a swap icon to choose an available alternative before pulling ingredients."):"");
        string stats="";
        bool showEnergy=false;
        Text("mealQuality","");Text("actualComfort","");
        if(result!=null)
        {
            var preview=BatchRecipe(ActiveMagazine,!loadedIngredients);RebirthCookingBatch.Stamp(preview,ActiveBook,xui.playerUI.entityPlayer,ActiveMagazine);
            var item=RebirthCookingBatch.Preview(preview);
            bool hasFood = RebirthConsumableResolver.TryResolve(item,out var food);
            if(hasFood){Text("statNutrition",food.NutritionUnits.ToString("0.#"));Text("statWater",(food.IsDrink?food.InitialVolumeMl:food.FoodWaterMl).ToString("0")+" mL");} 
            // Owner display policy: energy belongs to metabolism/digestion, not item/result stats.
            // Keep the underlying batch energy metadata and consumption calculations unchanged.
            Text("statEnergy", "");
            if(hasFood)stats=string.Format(RebirthSurvivorUiText.L("xuiRebirthCookingNutritionWaterStats", "Nutrition: {0}    Water: {1} mL"),food.NutritionUnits.ToString("0.#"),(food.IsDrink?food.InitialVolumeMl:food.FoodWaterMl).ToString("0"));
            bool hasMood = RebirthFoodMoodResolver.TryResolve(item,out var mood);
            if(hasMood)Text("statComfort",mood.BaseMoodInfluence.ToString("0.#"));
            Text("statTime",RebirthCookingHeat.FormatDuration(preview.craftingTime*batch));
            string requestKey=Key+":"+InputFingerprint()+":"+(ActiveMagazine??"");
            if(hasMood&&(requestKey!=comfortKey||Time.realtimeSinceStartup>comfortRefresh))
            {
                comfortKey=requestKey;comfortRefresh=Time.realtimeSinceStartup+2;
                int previewGeneration=pullUiGeneration;EntityPlayerLocal previewOwner=xui.playerUI.entityPlayer;
                RebirthCookingSessionService.Request(previewOwner,"preview",Key,item:item,reply:response=>
                {
                    if(!IsCurrentSession(previewGeneration,previewOwner)||comfortKey!=requestKey)return;
                    var fields=response.Split('|');comfortPreview=fields[0];comfortReason=fields.Length>1?fields[1]:"";
                });
            }
            if(!hasMood){comfortKey="";comfortPreview="";comfortReason="";}
            Text("actualComfort",!hasMood?"":string.IsNullOrEmpty(comfortPreview)?RebirthSurvivorUiText.L("xuiRebirthCookingComfortPending", "Your comfort: …"):string.Format(RebirthSurvivorUiText.L("xuiRebirthCookingComfortPreview", "Your comfort: {0} — {1}"),comfortPreview,comfortReason));
            if(hasMood)stats+=string.Format(RebirthSurvivorUiText.L("xuiRebirthCookingBaseComfortStats", "\nBase comfort: {0}"),mood.BaseMoodInfluence.ToString("0.#"));
            stats+=string.Format(RebirthSurvivorUiText.L("xuiRebirthCookingOutputTimeStats", "\nOutput: {0}    Cook time: {1}"),result.count*batch,RebirthCookingHeat.FormatDuration(preview.craftingTime*batch));
        }
        foreach(string suffix in new[]{"Icon","Label",""})
        {
            Show("statEnergy"+suffix,showEnergy);
            var time=Find("statTime"+suffix)?.ViewComponent;
            if(time!=null)time.Position=new Vector2i(time.Position.x,showEnergy?-211:-179);
        }
        Text("resultStats",stats);
        bool foodResult = !milling && result != null && (skillId == "skill.cooking" || skillId == "skill.drink_preparation");
        bool hasReferences = dish != null && (dish.Books.Count > 0 || dish.Magazines.Count > 0);
        Show("resultDescriptionReader", result != null && !foodResult);
        Text("resultDescription", result != null && !foodResult ? RebirthRecipeDescriptionText.GetForRecipe(result) : "");
        foreach (string stat in new[] { "statNutrition", "statWater", "statComfort", "statTime" })
            foreach (string suffix in new[] { "Icon", "Label", "" }) Show(stat + suffix, result == null || foodResult);
        Show("nonFoodStats", result != null && !foodResult && (!hasReferences || milling));
        if (result != null && !foodResult)
        {
            var output = new ItemStack(new ItemValue(result.itemValueType), result.count);
            var rows = UIDisplayInfoManager.Current.GetDisplayStatsForTag(output.itemValue.ItemClass.DisplayType);
            var lines = new List<string>();
            if (RebirthWeaponDetailRows.TryGet(xui, output, null, 0, out _, out _))
            {
                for (int row = 0; row < 7; row++)
                    if (RebirthWeaponDetailRows.TryGet(xui, output, null, row, out var title, out var value) && !string.IsNullOrEmpty(title))
                        lines.Add(title + ": " + value);
            }
            else if (rows != null) foreach (var stat in rows.DisplayStats)
            {
                if (stat == null) continue;
                lines.Add((stat.TitleOverride ?? UIDisplayInfoManager.Current.GetLocalizedName(stat.StatType)) + ": " +
                    RebirthItemStatColors.Format(RebirthItemStatColors.NativeValue(output, xui.playerUI.entityPlayer, stat)));
            }
            lines.Add(Localization.Get("xuiRebirthBatchSize") + ": " + result.count * batch);
            lines.Add(Localization.Get("xuiRebirthCraftTime") + ": " + RebirthCookingHeat.FormatDuration(result.craftingTime * batch));
            Text("nonFoodStats", string.Join("\n", lines));
        }

        void ReferenceIcon(string id,string available,string candidate)
        {
            if(Find(id)?.ViewComponent is XUiV_Sprite icon){string item=available??candidate;icon.UIAtlas=available!=null?"ItemIconAtlas":"ItemIconAtlasGreyscale";icon.SpriteName=item==null?"":ItemClass.GetItem(item).ItemClass?.GetIconName()??"";icon.IsVisible=item!=null;icon.Color=available!=null?Color.white:new Color32(130,130,136,255);}
        }
        ReferenceIcon("bookReferenceIcon",book,dish?.Books.FirstOrDefault());
        ReferenceIcon("magazineReferenceIcon",magazine,dish?.Magazines.FirstOrDefault());
        string ReferenceText(string selectedId,List<string> candidates,string kind)
        {
            if(selectedId!=null)return "[B58CFF]"+kind+"[-]\n"+Localization.Get(selectedId);
            string candidate=candidates?.FirstOrDefault();
            if(candidate==null)return "[B58CFF]"+kind+"[-]\n"+RebirthSurvivorUiText.L("xuiRebirthCookingNoReference", "No reference");
            return "[B58CFF]"+kind+"[-]\n"+Localization.Get(candidate);
        }
        Text("referenceStatus",ReferenceText(book,dish?.Books,RebirthSurvivorUiText.L("xuiRebirthCookingBookLabel", "Book"))+(dish?.Books.Count>0?"\n[70C47E]"+RebirthSurvivorUiText.L("xuiRebirthCookingBookPracticeBonus", "+20% skill gain")+"[-]":""));
        string magazineId=magazine??dish?.Magazines.FirstOrDefault();
        Text("magazineReferenceText",ReferenceText(magazine,dish?.Magazines,RebirthSurvivorUiText.L("xuiRebirthCookingMagazineLabel", "Magazine"))+(magazineId!=null?"\n[70C47E]"+RebirthCookingCatalogue.Benefit(magazineId)+"[-]":""));
        string benefitBook=book??dish?.Books.FirstOrDefault();
        string benefitMagazine=magazine??dish?.Magazines.FirstOrDefault();
        string benefits=(benefitBook!=null?RebirthSurvivorUiText.L("xuiRebirthCookingBookBenefit", "Book: +20% skill gain"):"")+(benefitBook!=null&&benefitMagazine!=null?"\n":"")+(benefitMagazine!=null?string.Format(RebirthSurvivorUiText.L("xuiRebirthCookingMagazineBenefit", "Magazine: {0}"),RebirthCookingCatalogue.Benefit(benefitMagazine)):"");
        if(Ready!=null)benefits=string.Format(RebirthSurvivorUiText.L("xuiRebirthCookingActiveBenefits", "Active: {0}"),(ActiveBook?RebirthSurvivorUiText.L("xuiRebirthCookingBookPracticeBonus", "+20% skill gain"):"")+(ActiveBook&&!string.IsNullOrEmpty(ActiveMagazine)?" • ":"")+(!string.IsNullOrEmpty(ActiveMagazine)?RebirthCookingCatalogue.Benefit(ActiveMagazine):""));
        Text("referenceBenefits",benefits);
        Text("cookingSkillValue", string.IsNullOrEmpty(skillId) ? RebirthSurvivorUiText.L("xuiRebirthCraftNoSkill", "No associated skill")
            : RebirthCraftSkillPreview.SkillText(xui.playerUI.entityPlayer, skillId, skillPreview));
        Show("cookingSkillIcon", !string.IsNullOrEmpty(skillId));
        if(!string.IsNullOrEmpty(skillId) && Find("cookingSkillIcon")?.ViewComponent is XUiV_Sprite skillIcon)
            skillIcon.SpriteName = RebirthSkillAptitudeTraitFactory.SkillIconKey(skillId);
        float remaining=Ready?.Remaining??0;
        Text("prepStatus",preparingRequest?RebirthSurvivorUiText.L("xuiRebirthCookingConfirmingPreparation", "Confirming preparation…"):Preparation.IsPreparing?string.Format(RebirthSurvivorUiText.L("xuiRebirthCookingPreparationProgress", "Preparing… {0}%"),(int)(Preparation.Progress(Now)*100)):remaining>0?string.Format(RebirthSurvivorUiText.L("xuiRebirthCookingPreparationActive", "Preparation active for {0}; you can refresh it."),TimeSpan.FromSeconds(remaining).ToString(@"mm\:ss")):RebirthSurvivorUiText.L("xuiRebirthCookingPreparationOptional", "Optional: prepare for 10 seconds; benefits last 5 minutes."));
        if(Find("prepProgress")?.ViewComponent is XUiV_Sprite bar)bar.Fill=Preparation.Progress(Now);
        Caption("prepare",remaining>0?RebirthSurvivorUiText.L("xuiRebirthCookingPrepareAgain", "PREPARE AGAIN"):RebirthSurvivorUiText.L("xuiRebirthCookingPrepare", "PREPARE"));
        Enable("prepare",!Preparation.IsPreparing&&!preparingRequest&&result!=null&&(book!=null||magazine!=null));
        foreach (string id in new[] { "referenceTitle", "referenceStatus", "prepStatus", "prepTrack", "prepProgress", "prepare", "bookReferenceIcon", "magazineReferenceIcon", "magazineReferenceText" })
            Show(id, hasReferences);

        Enable("pullIngredients",!pulling&&!Preparation.IsPreparing&&selected!=null);
        Enable("cook",!Preparation.IsPreparing&&!preparingRequest&&!submittingCook&&result!=null&&missing==null&&loadedIngredients&&!queueOccupied);
        int maximum=Math.Max(1,MaximumBatch());
        bool adjust=!Preparation.IsPreparing&&!preparingRequest&&!submittingCook&&!pulling;
        foreach(string id in new[]{"batchMin","batchMinus","batchPlus","batchMax"})
        {
            bool allowed=adjust&&(id=="batchMin"||id=="batchMinus"?batch>1:batch<maximum);
            Enable(id,allowed);
            if(Find(id)?.ViewComponent is XUiV_Sprite arrow)arrow.Color=allowed?Color.white:new Color32(100,100,106,255);
        }
        Text("cookStatus",missing!=null?"":Preparation.IsPreparing?RebirthSurvivorUiText.L("xuiRebirthCookingFinishPreparation", "Finish preparation before cooking."):status);
    }
}
