using System;
using System.Linq;
using System.Text;
using UnityEngine.Scripting;
#nullable disable
public static class RebirthNpcProgressionUiService
{
    public const string WindowGroupName="rebirthNpcProgression";
    public static bool Open(XUi xui,RebirthNpcStableId id,out string error){error=string.Empty;RebirthNpcProgressionProjection v;var c=SingletonMonoBehaviour<ConnectionManager>.Instance;if(c!=null&&!c.IsServer){if(!RebirthNpcProgressionClientCache.TryGet(id,out v)){error="Authoritative progression has not been replicated yet.";return false;}}else v=RebirthNpcProgressionProjectionService.Capture(id,true);XUiController g=xui==null?null:xui.FindWindowGroupByName(WindowGroupName);var controller=g==null?null:g.GetChildByType<XUiC_RebirthNpcProgression>();if(controller==null){error="The REBIRTH NPC progression window is not registered.";return false;}controller.Prepare(v);xui.playerUI.windowManager.Open((GUIWindow)controller.windowGroup,false);return true;}
}
[Preserve]
public sealed class XUiC_RebirthNpcProgression:XUiController
{
    private XUiV_Label title,attributes,specialties,professions,certifications,specialization,mentorship;private RebirthNpcProgressionProjection value;
    public override void Init(){base.Init();title=L("title");attributes=L("attributes");specialties=L("specialties");professions=L("professions");certifications=L("certifications");specialization=L("specialization");mentorship=L("mentorship");var close=GetChildById("btnClose");if(close!=null)close.OnPress+=delegate{xui.playerUI.windowManager.Close((GUIWindow)windowGroup);};}
    public void Prepare(RebirthNpcProgressionProjection v){value=v;Render();}
    public override void OnOpen(){base.OnOpen();Render();}
    private void Render(){if(value==null)return;Set(title,"NPC Progression — "+value.NpcId+" — revision "+value.Revision);Set(attributes,Lines((byte)RebirthNpcProgressionTrackKind.Attribute));Set(specialties,Lines((byte)RebirthNpcProgressionTrackKind.WeaponSpecialty));Set(professions,Lines((byte)RebirthNpcProgressionTrackKind.Profession));Set(certifications,"Certifications: "+(value.Certifications.Length==0?"None":string.Join(", ",value.Certifications)));Set(specialization,"Specializations: "+(value.Specializations.Length==0?"None":string.Join(", ",value.Specializations)));Set(mentorship,"Mentorship: "+(value.Mentorships.Length==0?"None":string.Join(", ",value.Mentorships)));}
    private string Lines(byte k){var a=value.Entries.Where(x=>x.Kind==k).OrderBy(x=>x.Id).Select(x=>x.Id+"  Level "+x.Level+"/"+x.MaximumLevel+"  XP "+x.Xp+"  Next "+x.XpToNext).ToArray();return a.Length==0?"None":string.Join("\n",a);}
    private XUiV_Label L(string id){var c=GetChildById(id);return c==null?null:c.ViewComponent as XUiV_Label;}private static void Set(XUiV_Label l,string s){if(l!=null)l.Text=s??string.Empty;}
}
