using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Scripting;

public sealed class RebirthJournalEntry {
    public string Id, Title, Type, Body, Created, Atlas, Image;
    public bool Authored;
}
public static class RebirthJournalStore {
    public static string ResolvePath() {
        RebirthHudTrackingPreferenceContext context;string error;
        if(!RebirthHudTrackingPreferenceStore.TryResolveCurrentContext(out context,out error))throw new IOException(error);
        return Path.Combine(GameIO.GetUserGameDataDir(),"RebirthData","Journal",context.WorldKey,context.PlayerStorageKey+".xml");
    }
    public static List<RebirthJournalEntry> Read(string path) {
        var result=new List<RebirthJournalEntry>();
        if(!File.Exists(path)&&!File.Exists(path+".bak"))return result;
        XDocument doc;bool recovered;string error;
        if(!RebirthAtomicXmlFile.TryLoadFinalThenBackup(path,out doc,out recovered,out error))throw new IOException(error);
        if(doc.Root?.Name!="journal")throw new IOException("Invalid journal document");
        var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var e in doc.Root.Elements("entry")) {
            string id=(string)e.Attribute("id");Guid parsed;
            if(!Guid.TryParse(id,out parsed))throw new IOException("Invalid personal entry identity or count");
            string canonicalId=parsed.ToString("N");
            if(!ids.Add(canonicalId)||result.Count>=500)throw new IOException("Invalid personal entry identity or count");
            if(((string)e.Element("title")??"").Length>80||((string)e.Element("body")??"").Length>4000)throw new IOException("Personal entry exceeds text limits");
            var type=(string)e.Attribute("type");
            if(type!="Notes"&&type!="Places"&&type!="Plans")throw new IOException("Invalid personal entry type");
            result.Add(new RebirthJournalEntry{Id=canonicalId,Type=type,Title=(string)e.Element("title")??"",Body=(string)e.Element("body")??"",Created=(string)e.Attribute("created")??""});
        }
        return result;
    }
    public static bool Save(string path,List<RebirthJournalEntry> entries,out string error) {
        error=string.Empty;
        var personal=entries.Where(e=>e!=null&&!e.Authored).ToList();
        if(personal.Count>500){error="The journal exceeds the 500 personal entry limit.";return false;}
        var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var root=new XElement("journal",new XAttribute("version",1));
        foreach(var e in personal){
            Guid parsed;if(!Guid.TryParse(e.Id,out parsed)){error="Journal entry identity is invalid.";return false;}
            string id=parsed.ToString("N");if(!ids.Add(id)){error="Journal entry identity is duplicated.";return false;}
            string title=e.Title??string.Empty,body=e.Body??string.Empty,type=e.Type??string.Empty;
            if(title.Length>80||body.Length>4000){error="Journal entry exceeds text limits.";return false;}
            if(type!="Notes"&&type!="Places"&&type!="Plans"){error="Journal entry type is invalid.";return false;}
            root.Add(new XElement("entry",new XAttribute("id",id),new XAttribute("type",type),new XAttribute("created",e.Created??string.Empty),new XElement("title",title),new XElement("body",body)));
        }
        return RebirthAtomicXmlFile.TryWrite(path,new XDocument(root),out error);
    }
}
[Preserve]
public sealed class XUiC_RebirthJournal : XUiController {
    private List<RebirthJournalEntry> entries=new List<RebirthJournalEntry>(), visible=new List<RebirthJournalEntry>();
    private RebirthJournalEntry selected;
    private RebirthScreenLayout layout;
    private XUiC_RebirthCharacterOverviewList list;
    private XUiC_RebirthJournalTypeDropdown typeDropdown;
    private string path, filter="All", sort="Newest", editId;
    private int typeIndex;
    private bool resetReader;
    private bool editing,deleteArmed,writable;
    private int guideRevision=-1;
    private Texture2D guideTexture;
    private string imageName;
    private bool imageOpen;
    private float imageZoom=1f;
    private float imageZoomTarget=1f, imageZoomVelocity;
    private Vector2i imagePan=Vector2i.zero;
    private readonly string[] types={"Notes","Places","Plans"};
    public override void Init(){base.Init();
        Wire("journalClose",()=>xui.playerUI.windowManager.Close("rebirthJournal"));Wire("journalNew",()=>Edit(null));Wire("journalEdit",()=>Edit(selected));Wire("journalCancel",()=>{editing=false;Render();});Wire("journalSave",Save);Wire("journalDelete",Delete);
        typeDropdown=GetChildById("journalTypeDropdown") as XUiC_RebirthJournalTypeDropdown;
        Wire("journalReadToggle",()=>{if(selected!=null){RebirthJournalGuideService.Mark(selected,!RebirthJournalGuideService.IsRead(selected));Render();}});
        Wire("journalReadAll",()=>{RebirthJournalGuideService.MarkAll(entries);Render();});
        Wire("journalImageOpen",OpenImage);Wire("journalArt",OpenImage);
        Wire("journalImageClose",()=>SetImageOpen(false));
        var imageInput=GetChildById("journalImageInput");
        imageInput.OnScroll += (sender,delta)=> { if(!imageOpen || RebirthConsoleInputGuardRuntime.BlocksGameplayInput())return; imageZoomTarget=Mathf.Clamp(imageZoomTarget*Mathf.Pow(1.08f,Mathf.Clamp(delta*10f,-3f,3f)),1f,4f); };
        imageInput.OnDrag += DragImage;
        Wire("journalSort",()=>{sort=sort=="Newest"?"Type":sort=="Type"?"Title":"Newest";list?.ResetPosition();Render();});
        foreach(var kind in new[]{"All","Notes","Places","Plans","Lore","Unread"}){var k=kind;Wire("journalFilter"+k,()=>{if(editing)return;filter=k;list?.ResetPosition();Render();});}
        for(int i=0;i<12;i++){int index=i;Wire("journalRow"+i,()=>{if(editing)return;int n=list.FirstDataIndex+index;if(n<visible.Count){selected=visible[n];RebirthJournalGuideService.Mark(selected,true);SetImageOpen(false);resetReader=true;deleteArmed=false;Render();}});}
        list=GetChildById("journalListScroll") as XUiC_RebirthCharacterOverviewList;list.DataRangeChanged+=RenderRows;
        Input("journalTitleInput").SupportBbCode=false;Input("journalBodyInput").SupportBbCode=false;
    }
    private void Wire(string id,Action action){GetChildById(id+"Hit").OnPress+=(s,b)=>{if((b==0||b==-1)&&!RebirthConsoleInputGuardRuntime.BlocksGameplayInput())action();};}
    private XUiC_TextInput Input(string id)=>(XUiC_TextInput)GetChildById(id);
    private void Label(string id,string text)=>(GetChildById(id).ViewComponent as XUiV_Label)?.SetTextImmediately(text);
    private void Show(string id,bool show)=>GetChildById(id).ViewComponent.IsVisible=show;
    public override void OnOpen(){base.OnOpen();layout=layout??new RebirthScreenLayout(this);layout.Open();GetChildByType<XUiC_RebirthCraftingTopTabs>()?.SetActiveDestination(RebirthCraftingNavigationService.Destination.Journal);editing=false;list?.ResetPosition();resetReader=true;deleteArmed=false;filter="All";
        try{path=RebirthJournalStore.ResolvePath();entries=RebirthJournalStore.Read(path);writable=true;Label("journalMessage","");}
        catch(Exception ex){entries=new List<RebirthJournalEntry>();writable=false;Label("journalMessage",Localization.Get("xuiRebirthJournalLoadError"));Log.Warning("[REBIRTH Journal] "+ex.Message);}
        RebirthJournalGuideService.Poll(xui.playerUI.entityPlayer,true);entries.AddRange(RebirthJournalGuideService.Entries);guideRevision=RebirthJournalGuideService.Revision;
        selected=null;SetImageOpen(false);Render();
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        // OnOpen refreshes entries and reader state. Hidden windows must not
        // rebuild labels or reload artwork when global guide revisions change.
        if(!xui.playerUI.windowManager.IsWindowOpen("rebirthJournal"))return;
        RebirthJournalGuideService.Poll(xui.playerUI.entityPlayer);
        AnimateImageZoom();
        layout?.Apply();
        if(guideRevision!=RebirthJournalGuideService.Revision&&!editing)
        {
            entries.RemoveAll(e=>e.Authored);
            entries.AddRange(RebirthJournalGuideService.Entries);
            if(selected?.Authored==true)
            {
                var refreshed=entries.FirstOrDefault(e=>e.Authored&&e.Id==selected.Id);
                if(!ReferenceEquals(selected,refreshed))
                {
                    selected=refreshed;
                    SetImageOpen(false); resetReader=true;
                }
            }
            guideRevision=RebirthJournalGuideService.Revision; Render();
        }
        if(resetReader)
        {
            var view=GetChildById("journalBodyViewport")?.ViewComponent as XUiV_ScrollView;
            if(view?.scrollView!=null){view.scrollView.ResetPosition();resetReader=false;}
        }
    }
    public override void OnClose(){SetImageOpen(false);if(guideTexture!=null)UnityEngine.Object.Destroy(guideTexture);guideTexture=null;imageName=null;layout?.Close();base.OnClose();}
    private static List<string> Paginate(string text,int maxLines){
        var lines=new List<string>();
        foreach(var paragraph in text.Replace("\r", "").Split('\n')){
            string rest=paragraph;
            while(rest.Length>90){int cut=rest.LastIndexOf(' ',89,90);if(cut<30)cut=90;if(char.IsHighSurrogate(rest[cut-1]))cut--;lines.Add(rest.Substring(0,cut));rest=rest.Substring(cut).TrimStart();}
            lines.Add(rest);
        }
        var pages=new List<string>();for(int i=0;i<lines.Count;i+=maxLines)pages.Add(string.Join("\n",lines.Skip(i).Take(maxLines)));return pages;
    }
    private void Edit(RebirthJournalEntry entry){if(!writable||entry?.Authored==true)return;editId=entry?.Id;editing=true;deleteArmed=false;typeIndex=Math.Max(0,Array.IndexOf(types,entry?.Type));Input("journalTitleInput").Text=entry?.Title??"";Input("journalBodyInput").Text=entry?.Body??"";Render();}
    private void Save(){if(!editing||!writable)return;typeIndex=Array.IndexOf(types,typeDropdown.Text);if(typeIndex<0){Label("journalMessage",Localization.Get("xuiRebirthJournalChooseType"));return;}string title=Input("journalTitleInput").Text.Trim(),body=Input("journalBodyInput").Text.Trim();
        if(title.Length==0||body.Length==0){Label("journalMessage",Localization.Get("xuiRebirthJournalNeedText"));return;}
        if(title.Length>80||body.Length>4000){Label("journalMessage",Localization.Get("xuiRebirthJournalTextLimit"));return;}
        if(editId==null&&entries.Count(e=>!e.Authored)>=500){Label("journalMessage",Localization.Get("xuiRebirthJournalFull"));return;}
        var old=entries.FirstOrDefault(e=>e.Id==editId&&!e.Authored);var entry=new RebirthJournalEntry{Id=old?.Id??Guid.NewGuid().ToString("N"),Title=title,Body=body,Type=types[typeIndex],Created=old?.Created??DateTime.UtcNow.ToString("o")};
        var next=entries.Where(e=>e!=old).ToList();next.Add(entry);string error;
        if(!RebirthJournalStore.Save(path,next,out error)){Label("journalMessage",Localization.Get("xuiRebirthJournalNoteSaveError"));return;}
        entries=next;selected=entry;filter="All";list?.ResetPosition();resetReader=true;editing=false;Label("journalMessage",Localization.Get("xuiRebirthJournalNoteSaved"));Render();
    }
    private void Delete(){if(selected==null||selected.Authored||!writable)return;if(!deleteArmed){deleteArmed=true;Label("journalMessage",Localization.Get("xuiRebirthJournalDeleteConfirm"));return;}
        var next=entries.Where(e=>e!=selected).ToList();string error;if(!RebirthJournalStore.Save(path,next,out error)){Label("journalMessage",Localization.Get("xuiRebirthJournalDeleteError"));return;}entries=next;selected=null;deleteArmed=false;Label("journalMessage",Localization.Get("xuiRebirthJournalDeleted"));Render();}
    private void RenderRows(){if(list==null)return;for(int i=0;i<12;i++){int n=list.FirstDataIndex+i;Show("journalRow"+i,n<visible.Count);if(n<visible.Count){Label("journalRowTitle"+i,visible[n].Title);bool read=RebirthJournalGuideService.IsRead(visible[n]);Label("journalRowType"+i,Localization.Get(read?"xuiRebirthJournalRead":"xuiRebirthJournalUnread")+" / "+(visible[n].Authored?Localization.Get("xuiRebirthJournalGuide"):visible[n].Type));var label=GetChildById("journalRowTitle"+i).ViewComponent as XUiV_Label;label.Color=read?new Color32(210,210,215,255):new Color32(255,208,96,255);}}}
    private void Render(){
        var query=entries.Where(e=>filter=="All"||e.Type==filter||(filter=="Unread"&&!RebirthJournalGuideService.IsRead(e)));visible=(sort=="Type"?query.OrderBy(e=>e.Type).ThenBy(e=>e.Title):sort=="Title"?query.OrderBy(e=>e.Title):query.OrderByDescending(e=>e.Created)).ToList();
        Label("journalSortLabel","Sort: "+sort);Label("journalFilterLabel",filter+" • "+visible.Count+" entries");
        list.SetItemCount(visible.Count, Localization.Get("xuiRebirthJournalEmptyFilter"));RenderRows();
        Show("journalReader",!editing);Show("journalEditor",editing);Show("journalNew",!editing&&writable);Show("journalEdit",!editing&&selected!=null&&!selected.Authored&&writable);Show("journalDelete",!editing&&selected!=null&&!selected.Authored&&writable);
        Show("journalReadToggle",!editing&&selected!=null);Show("journalReadAll",!editing);
        Label("journalReadToggleLabel",Localization.Get(selected!=null&&RebirthJournalGuideService.IsRead(selected)?"xuiRebirthJournalMarkUnread":"xuiRebirthJournalMarkRead"));
        if(RebirthJournalGuideService.Error!=null)Label("journalMessage",Localization.Get("xuiRebirthJournalSaveError"));
        if(editing){typeDropdown.Text=types[typeIndex];return;}
        Label("journalEntryTitle",selected?.Title??Localization.Get("xuiRebirthJournalSelectEntry"));Label("journalEntryMeta",selected==null?"":(selected.Authored?Localization.Get("xuiRebirthJournalGuide"):selected.Type)+" • "+(selected.Authored?Localization.Get("xuiRebirthJournalFieldTitle"):DateTime.TryParse(selected.Created,out var created)?created.ToLocalTime().ToString("g"):selected.Created));
        bool art=selected?.Authored==true&&!string.IsNullOrEmpty(selected.Image);Show("journalArt",art);
        Show("journalImageOpen",art);Show("journalArtHit",art);
        if(art)LoadImage(selected.Image);
        var body=(XUiV_Label)GetChildById("journalEntryBody").ViewComponent;body.Position=new Vector2i(24,art?-442:-100);body.Size=new Vector2i(1180,560);
        string text=selected?.Body??Localization.Get("xuiRebirthJournalEmptyHelp");
        body.Overflow=UILabel.Overflow.ResizeHeight;
        body.SetTextImmediately(text);
        body.Update(0f);
        int measured=body.label!=null?UnityEngine.Mathf.CeilToInt(body.label.printedSize.y):560;
        body.Size=new Vector2i(1180,Math.Max(40,measured+8));
        GetChildById("journalReaderContent").ViewComponent.Size=new Vector2i(1240,(art?442:100)+body.Size.y+20);
        int readerHeight=742;
        GetChildById("journalBodyScroll").ViewComponent.Size=new Vector2i(1268,readerHeight);
        GetChildById("journalBodyViewport").ViewComponent.Size=new Vector2i(1240,readerHeight);
    }
    private void LoadImage(string name)
    {
        if(imageName==name)return;
        if(guideTexture!=null)UnityEngine.Object.Destroy(guideTexture);guideTexture=null;imageName=name;
        try{if(Path.GetFileName(name)!=name)throw new IOException("Invalid guide image");
            guideTexture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!guideTexture.LoadImage(File.ReadAllBytes(Path.Combine(RebirthJournalGuideService.Root,"Images",name))))throw new IOException("Invalid guide PNG");
            guideTexture.wrapMode=TextureWrapMode.Clamp;guideTexture.filterMode=FilterMode.Bilinear;
            ((XUiV_Texture)GetChildById("journalArt").ViewComponent).Texture=guideTexture;
        }catch(Exception ex){Log.Warning("[REBIRTH Journal] "+ex.Message);Label("journalMessage",Localization.Get("xuiRebirthJournalImageError"));}
    }
    private void SetImageOpen(bool open)
    {
        imageDragging=false;imageOpen=open;Show("journalImageOverlay",open);Show("journalIndex",!open);Show("journalContent",!open);
    }
    private void OpenImage()
    {
        if(guideTexture==null)return;
        imageZoom=imageZoomTarget=1;imageZoomVelocity=0;imagePan=Vector2i.zero;SetImageOpen(true);RenderImage();
    }
    private Vector2 imageDragStart;
    private Vector2i imageDragPan;
    private bool imageDragging;
    private void DragImage(XUiController sender, EDragType type, Vector2 delta)
    {
        if(!imageOpen || RebirthConsoleInputGuardRuntime.BlocksGameplayInput())return;
        var t=sender.ViewComponent.UiTransform; var cam=UICamera.currentCamera;
        if(t==null || cam==null)return;
        Vector2 mouse=UICamera.currentTouch!=null?UICamera.currentTouch.pos:(Vector2)UnityEngine.Input.mousePosition;
        var ray=cam.ScreenPointToRay(mouse); float distance;
        if(!new Plane(t.forward,t.position).Raycast(ray,out distance))return;
        Vector2 point=t.InverseTransformPoint(ray.GetPoint(distance));
        if(type==EDragType.DragStart){imageDragStart=point;imageDragPan=imagePan;imageDragging=true;}
        if(imageDragging){var shift=point-imageDragStart;imagePan=new Vector2i(imageDragPan.x+Mathf.RoundToInt(shift.x),imageDragPan.y+Mathf.RoundToInt(shift.y));RenderImage();}
        if(type==EDragType.DragEnd)imageDragging=false;
    }
    private void RenderImage()
    {
        if(!imageOpen||guideTexture==null)return;
        var image=GetChildById("journalLargeImage").ViewComponent as XUiV_Texture;image.Texture=guideTexture;
        float fit=Mathf.Min(1800f/guideTexture.width,690f/guideTexture.height);
        int w=Mathf.RoundToInt(guideTexture.width*fit*imageZoom),h=Mathf.RoundToInt(guideTexture.height*fit*imageZoom);
        imagePan.x=Mathf.Clamp(imagePan.x,-Math.Max(0,(w-1800)/2),Math.Max(0,(w-1800)/2));
        imagePan.y=Mathf.Clamp(imagePan.y,-Math.Max(0,(h-690)/2),Math.Max(0,(h-690)/2));
        image.Size=new Vector2i(w,h);image.Position=new Vector2i((1800-w)/2+imagePan.x,-(690-h)/2+imagePan.y);image.TryUpdatePosition();
    }
    private void AnimateImageZoom()
    {
        if (!imageOpen || Mathf.Abs(imageZoom-imageZoomTarget)<0.0001f) return;
        imageZoom=Mathf.SmoothDamp(imageZoom,imageZoomTarget,ref imageZoomVelocity,0.10f,100f,Mathf.Min(Time.unscaledDeltaTime,0.05f));
        if (Mathf.Abs(imageZoom-imageZoomTarget)<0.0001f) { imageZoom=imageZoomTarget; imageZoomVelocity=0; }
        RenderImage();
    }
}
