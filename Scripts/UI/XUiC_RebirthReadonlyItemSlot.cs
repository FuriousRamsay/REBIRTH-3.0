using UnityEngine;
using UnityEngine.Scripting;

// Presentation-only native item_stack, shared by cassette cells, selection and the drag image.
[Preserve]
public sealed class XUiC_RebirthReadonlyItemSlot : XUiC_ItemStack
{
    private readonly XUiV_Sprite[] backgrounds=new XUiV_Sprite[3];
    public XUiController GeometrySource;
    private bool collidersCleared;
    private bool IsHireSlot{get{string id=ViewComponent?.ID;return id!=null&&id.StartsWith("hire",System.StringComparison.Ordinal)&&id.IndexOf("Mirror",System.StringComparison.Ordinal)<0;}}
    public float DisplaySize=75;
    public override bool ParseAttribute(string name,string value){if(name=="display_size"){DisplaySize=float.Parse(value,System.Globalization.CultureInfo.InvariantCulture);return true;}return base.ParseAttribute(name,value);}
    public override void Init()
    {
        base.Init();DisableInput(this);
        backgrounds[0]=GetChildById("backgroundMain")?.ViewComponent as XUiV_Sprite;
        backgrounds[1]=GetChildById("background")?.ViewComponent as XUiV_Sprite;
        backgrounds[2]=GetChildById("highlightOverlay")?.ViewComponent as XUiV_Sprite;
    }
    private static void DisableInput(XUiController node)
    {
        if(node.ViewComponent!=null){node.ViewComponent.EventOnPress=false;node.ViewComponent.EventOnHover=false;node.ViewComponent.EventOnDrag=false;node.ViewComponent.EventOnScroll=false;}
        if(node.Children!=null)foreach(var child in node.Children)DisableInput(child);
    }
    public void Show(ItemStack stack,XUiController source=null)
    {
        GeometrySource=source;
        if(!RebirthStationGridIngredients.IsSameStackSnapshot(ItemStack,stack??global::ItemStack.Empty))ItemStack=stack?.Clone()??global::ItemStack.Empty.Clone();
    }
    public override void Update(float dt)
    {
        base.Update(dt);
        // Hire preview slots keep the native slot background so empty cells are visible; all other users stay presentation-only.
        bool slotBackground=IsHireSlot;
        for(int i=0;i<backgrounds.Length;i++)if(backgrounds[i]!=null)backgrounds[i].IsVisible=slotBackground&&i==0;
        if(!collidersCleared&&ViewComponent?.UiTransform!=null)
        {
            // The nested stack must not intercept the parent rect's press (selection is wired on the rect): switch its colliders off.
            foreach(var c in ViewComponent.UiTransform.GetComponentsInChildren<Collider>(true))c.enabled=false;
            collidersCleared=true;
        }
        if(ViewComponent?.UiTransform!=null){float scale=DisplaySize/System.Math.Max(1,ViewComponent.Size.x);ViewComponent.UiTransform.localScale=new Vector3(scale,scale,1);}
        if(GeometrySource!=null)RebirthSelectedDurability.CopyNativePresentation(this,GeometrySource,false,ItemStack);
    }
}
