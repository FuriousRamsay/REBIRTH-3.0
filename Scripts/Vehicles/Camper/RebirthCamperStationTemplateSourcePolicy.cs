using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using HarmonyLib;

// Captures merged client-evaluated native XML even on dedicated servers, where
// XUiFromXml.Load deliberately does not instantiate a UI. No views are created.
internal static class RebirthCamperStationTemplateSourcePolicy
{
    private static readonly Dictionary<string,XElement> roots=new Dictionary<string,XElement>(StringComparer.Ordinal);
    private static readonly HashSet<string> views=new HashSet<string>("window,panel,rect,sprite,filledsprite,texture,label,textlist,grid,table,button,gamepad_icon,scrollbar,scrollview,video".Split(','),StringComparer.Ordinal);
    internal static void Clear()=>roots.Clear();
    internal static void Capture(XmlFile file)
    {
        string path=file?.Filename?.Replace('\\','/');
        if(path==null)return;
        string[] ids={"XUi_Common/templates","XUi_InGame/templates","XUi_InGame/windows","XUi_InGame/xui"};
        foreach(string id in ids)
            if(path.EndsWith(id+".xml",StringComparison.OrdinalIgnoreCase)||path.Equals(id,StringComparison.OrdinalIgnoreCase))
            {
                roots.Remove(id);var root=file.XmlDoc?.Root;
                if(root!=null&&root.ToString(SaveOptions.DisableFormatting).Length<=8*1024*1024)roots[id]=new XElement(root);
                return;
            }
    }
    internal static bool TryGet(string windowName,out bool actorSource)
    {
        actorSource=false;
        try
        {
            if(roots.Count!=4||!roots.TryGetValue("XUi_InGame/xui",out var xui)||!roots.TryGetValue("XUi_InGame/windows",out var windows))return false;
            var groups=xui.Elements("window_group").Where(e=>(string)e.Attribute("name")==windowName).ToArray();
            if(groups.Length!=1)return false;
            string controller=(string)groups[0].Attribute("controller");
            var type=string.IsNullOrEmpty(controller)?null:ReflectionHelpers.GetTypeWithPrefix("XUiC_",controller);
            if(type==null||!typeof(XUiC_WorkstationWindowGroup).IsAssignableFrom(type))return false;
            var templates=new Dictionary<string,XElement>(StringComparer.Ordinal);
            foreach(string id in new[]{"XUi_Common/templates","XUi_InGame/templates"})
                foreach(var entry in roots[id].Elements())
                {if(entry.Elements().Count()!=1)return false;templates[entry.Name.LocalName]=entry.Elements().Single();}
            int budget=8192,inputs=0;
            foreach(var offered in groups[0].Elements("window"))
            {
                XElement node=offered;
                if(!node.HasElements)
                {
                    var matches=windows.Elements("window").Where(e=>(string)e.Attribute("name")== (string)offered.Attribute("name")).ToArray();
                    if(matches.Length!=1)return false;node=matches[0];
                }
                if(!Inspect(node,templates,new HashSet<string>(StringComparer.Ordinal),0,ref budget,ref inputs))return false;
            }
            if(!groups[0].Elements("window").Any())return false;
            actorSource=inputs==0;return inputs<=1;
        }
        catch{return false;}
    }
    private static bool Inspect(XElement node,Dictionary<string,XElement> templates,HashSet<string> chain,int depth,ref int budget,ref int inputs)
    {
        if(node==null||depth>64||--budget<0)return false;
        if(node.Attribute("platform")!=null)
        {
            // Platform-only decoration cannot change payment custody. A
            // conditionally present input controller remains ambiguous.
            var unconditional=new XElement(node);unconditional.Attribute("platform").Remove();
            int previous=inputs;
            return Inspect(unconditional,templates,chain,depth+1,ref budget,ref inputs)&&inputs==previous;
        }
        string name=node.Name.LocalName;
        if(!views.Contains(name))
        {
            if(!templates.TryGetValue(name,out var template)||node.HasElements||!chain.Add(name))return false;
            bool result=Inspect(template,templates,chain,depth+1,ref budget,ref inputs);chain.Remove(name);return result;
        }
        string controller=(string)node.Attribute("controller");
        if(!string.IsNullOrEmpty(controller))
        {
            // Native controller resolution is static. Expressions in controller
            // identity cannot qualify this detached policy inspector.
            if(controller.IndexOfAny(new[]{'{','}','@','$'})>=0)return false;
            var type=ReflectionHelpers.GetTypeWithPrefix("XUiC_",controller);
            if(type==null)return false;
            if(typeof(XUiC_WorkstationInputGrid).IsAssignableFrom(type))inputs++;
        }
        foreach(var child in node.Elements())
        {
            if(child.Name.LocalName=="tween"||child.Name.LocalName=="params")continue;
            if(!Inspect(child,templates,chain,depth+1,ref budget,ref inputs))return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(XUiFromXml),nameof(XUiFromXml.Load))]
internal static class RebirthCamperStationTemplateSourceCapture
{
    private static void Prefix(XmlFile _xmlFile)=>RebirthCamperStationTemplateSourcePolicy.Capture(_xmlFile);
}
[HarmonyPatch(typeof(XUiFromXml),nameof(XUiFromXml.ClearData))]
internal static class RebirthCamperStationTemplateSourceClear
{
    private static void Prefix()=>RebirthCamperStationTemplateSourcePolicy.Clear();
}
