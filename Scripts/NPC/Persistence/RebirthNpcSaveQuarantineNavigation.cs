using System;
using System.Xml;

public static partial class RebirthNpcWorkNavigationService
{
    internal static bool CanRetireEmptyNavigation
    {get{lock(Sync)return ThreadManager.IsMainThread()&&States.Count==0&&Order.Count==0;}}
    internal static RebirthNpcSaveQuarantineDomain PrepareEmptyNavigationQuarantine(RebirthNpcSaveQuarantineScope scope=null)
    {
        if(scope!=null)return CapturePackageCustody(scope).Domain;
        if(!CanRetireEmptyNavigation)throw new InvalidOperationException("Native movement owner/adapter has no qualified detach contract");
        bool enabled=RebirthNpcWorkReleaseGate.Enabled;var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("navigation");doc.AppendChild(root);root.SetAttribute("emptyOnly","1");root.SetAttribute("workEnabled",enabled?"1":"0");
        return new RebirthNpcSaveQuarantineDomain("work-navigation-empty",doc,()=>CanRetireEmptyNavigation&&RebirthNpcWorkReleaseGate.Enabled==enabled,()=>{if(!CanRetireEmptyNavigation)throw new InvalidOperationException("Navigation changed after empty preflight");},()=>{});
    }
}
