$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/XUiC_RebirthCamperStationPicker.cs')).Replace('using System;','').Replace('using UnityEngine.Scripting;','')
$fixture=@'
using System;using System.Collections.Generic;
public class PreserveAttribute:Attribute {}
public class GUIWindow {}
public class View {public bool IsVisible=true;}
public class XUiV_Label:View {public string Text;}
public class Manager {public void Close(GUIWindow w){}}
public class PlayerUI {public Manager windowManager=new Manager();}
public class XUi {public PlayerUI playerUI=new PlayerUI();}
public class XUiController {
 public Dictionary<string,XUiController> Children=new Dictionary<string,XUiController>();public View ViewComponent=new View();public GUIWindow windowGroup=new GUIWindow();public XUi xui=new XUi();
 public event Action<XUiController,int> OnPress;
 public XUiController GetChildById(string id){if(Children.TryGetValue(id,out var value))return value;foreach(var child in Children.Values){var nested=child.GetChildById(id);if(nested!=null)return nested;}return null;}
 public virtual void Init(){}public virtual void OnOpen(){}public virtual void OnClose(){}public virtual void Update(float dt){}
 // Matches the real native XUiController.Pressed: only this controller's event,
 // no ancestor event propagation. The clickable child must own subscription.
 public void Pressed(int button){if(button==-1)OnPress?.Invoke(this,button);}
}
public static class PickerFixture {
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 public static void Run(){
  var picker=new XUiC_RebirthCamperStationPicker();var choice=new XUiController[10];var clickable=new XUiController[10];
  for(int i=0;i<10;i++){choice[i]=new XUiController();clickable[i]=new XUiController();choice[i].Children["clickable"]=clickable[i];choice[i].Children["camperStationName"+i]=new XUiController{ViewComponent=new XUiV_Label()};picker.Children["camperStationChoice"+i]=choice[i];}
  picker.Init();var ids=new Guid[10];var names=new string[10];ids[0]=Guid.NewGuid();ids[1]=Guid.NewGuid();names[0]=names[1]="Forge";Guid selected=Guid.Empty;int releases=0;
  picker.Bind(ids,names,()=>true,id=>selected=id,()=>releases++);picker.OnOpen();
  choice[1].Pressed(-1);Check(selected==Guid.Empty,"Nonclickable rect accepted native press");clickable[1].Pressed(-1);Check(selected==ids[1],"Identical station types did not select exact second installation");
  Check(!choice[2].ViewComponent.IsVisible,"Empty choice left name/background visible");
  picker.RetainLeaseForSelection();picker.OnClose();Check(releases==0,"Picker handoff released native lease");clickable[0].Pressed(-1);Check(selected==ids[1],"Closed picker retained selection callback");
  picker.Bind(ids,names,()=>true,id=>selected=id,()=>releases++);picker.OnOpen();picker.OnClose();Check(releases==1,"Normal picker close did not release exact session");
  ids[1]=ids[0];try{picker.Bind(ids,names,()=>true,id=>{},()=>{});throw new Exception("Duplicate installation identity accepted");}catch(ArgumentException){}
 }
}
'@
Add-Type -TypeDefinition ($fixture+"`n"+$source)
[PickerFixture]::Run()
Write-Output 'PASS actual picker controller: native child-button press, duplicate station types resolve distinct installation GUIDs, empty rows hidden, handoff retains lease, normal close releases and duplicate GUID refused. Native event semantics checked against installed assembly; visual gameplay unverified.'
