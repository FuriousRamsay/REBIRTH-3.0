using System;using System.Collections.Generic;using System.Linq;
class GameObject {public bool activeInHierarchy=true;}
class Transform {public GameObject gameObject=new();}
class XUiView {public bool IsVisible=true,EventOnPress;public Transform UiTransform=new();}
class XUiV_Label:XUiView {public string Text;}
class XUiController {public XUiView ViewComponent;public List<XUiController> Children=new();}
class XUiC_SimpleButton:XUiController {public string Text;}
class XUiC_TextInput:XUiController {}
class XUiC_ItemStack:XUiController {}
class XUiC_RecipeEntry:XUiController {public object Recipe;}
class XUiC_RecipeStack:XUiController {public object recipe;}
class RebirthGameBridgePlayer {public static string StripColors(string t)=>t.Replace("[FFFFFF]","").Replace("[-]","");}
class Program {const int MaxNodes=2500;
    private static void WalkAlertTexts(XUiController controller, List<string> texts, ref int count)
    {
        if (controller == null || count >= MaxNodes) return;
        XUiView view = controller.ViewComponent;
        if (view != null && (!view.IsVisible || view.UiTransform == null || !view.UiTransform.gameObject.activeInHierarchy)) return;
        string text = (view as XUiV_Label)?.Text;
        var button = controller as XUiC_SimpleButton;
        if (button != null && !string.IsNullOrEmpty(button.Text)) text = button.Text;
        bool interesting = !string.IsNullOrEmpty(text) || (view != null && view.EventOnPress) ||
            controller is XUiC_TextInput || controller is XUiC_ItemStack ||
            (controller is XUiC_RecipeEntry entry && entry.Recipe != null) ||
            (controller is XUiC_RecipeStack queued && queued.recipe != null);
        if (interesting)
        {
            ++count;
            if (!string.IsNullOrEmpty(text)) texts.Add(RebirthGameBridgePlayer.StripColors(text));
        }
        if (controller.Children == null) return;
        foreach (XUiController child in controller.Children) WalkAlertTexts(child, texts, ref count);
    }


// Reference inclusion/text rules from pre-change Describe(includeAll:false).
static void Reference(XUiController c,List<string> result,ref int count){if(c==null||count>=MaxNodes)return;var v=c.ViewComponent;if(v!=null&&(!v.IsVisible||v.UiTransform==null||!v.UiTransform.gameObject.activeInHierarchy))return;
 bool interesting=false;string text=null;if(v is XUiV_Label l&&!string.IsNullOrEmpty(l.Text)){text=RebirthGameBridgePlayer.StripColors(l.Text);interesting=true;}if(v!=null&&v.EventOnPress)interesting=true;if(c is XUiC_SimpleButton b&&!string.IsNullOrEmpty(b.Text)){text=RebirthGameBridgePlayer.StripColors(b.Text);interesting=true;}if(c is XUiC_TextInput||c is XUiC_ItemStack)interesting=true;if(c is XUiC_RecipeEntry e&&e.Recipe!=null)interesting=true;if(c is XUiC_RecipeStack q&&q.recipe!=null)interesting=true;if(interesting){count++;if(text!=null)result.Add(text);}if(c.Children!=null)foreach(var child in c.Children)Reference(child,result,ref count);
}
static void Check(XUiController root){var a=new List<string>();var b=new List<string>();int ac=0,bc=0;Reference(root,a,ref ac);WalkAlertTexts(root,b,ref bc);if(ac!=bc||!a.SequenceEqual(b))throw new Exception("projection mismatch");}
static void Main(){var rng=new Random(413);for(int n=0;n<1000;n++){var root=new XUiController();for(int i=0;i<100;i++){XUiController c=rng.Next(6) switch{0=>new XUiC_SimpleButton{Text=rng.Next(2)==0?"READY TO TAKE":null},1=>new XUiC_ItemStack(),2=>new XUiC_TextInput(),3=>new XUiC_RecipeEntry{Recipe=rng.Next(2)==0?new object():null},4=>new XUiC_RecipeStack{recipe=new object()},_=>new XUiController()};c.ViewComponent=rng.Next(2)==0?new XUiV_Label{Text="[FFFFFF]01:12 to burning[-]"}:new XUiView();c.ViewComponent.IsVisible=rng.Next(4)!=0;c.ViewComponent.EventOnPress=rng.Next(2)==0;c.ViewComponent.UiTransform.gameObject.activeInHierarchy=rng.Next(4)!=0;root.Children.Add(c);if(i%8==0)c.Children.Add(new XUiC_SimpleButton{Text="TAKE NOW"});}Check(root);}
var limit=new XUiController();for(int i=0;i<2501;i++)limit.Children.Add(new XUiC_ItemStack());limit.Children.Add(new XUiC_SimpleButton{Text="TAKE NOW"});Check(limit);int count=0;var texts=new List<string>();WalkAlertTexts(limit,texts,ref count);if(texts.Count!=0||count!=2500)throw new Exception("node cap");Console.WriteLine("PASS actual text walker:1000 generated visibility/control/button-text/color/order projections plus2500 non-text node boundary. Reference inclusion model and native doubles; no rendered/native equivalence claim.");}
}
