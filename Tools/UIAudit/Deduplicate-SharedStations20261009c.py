from pathlib import Path
import xml.etree.ElementTree as E,copy,shutil
p=Path('Config/XUi_InGame/station_templates.xml');backup=Path('_Documentation/UiFeedback_20261009c/before')/p;backup.parent.mkdir(parents=True,exist_ok=True)
if not backup.exists():backup.write_bytes(p.read_bytes())
t=E.parse(p).getroot();app=t.find('append')
w=E.parse('Config/XUi_InGame/station_workspace.xml').getroot();cache={};serial=0
for win in w.iter('window'):
 root=win.find('rect');right=next(x for x in root.iter() if x.get('name')=='rebirthCraftingRightZone')
 right.append(E.Element('label',{'name':'stationName','pos':'14,-8','width':'460','height':'28','font_size':'24','color':'181,140,255,255'}))
 for parent in [root,next(x for x in root if x.get('name')=='rebirthCraftingBodyZone'),right]:
  for child in list(parent):
   if child.get('name') in ['rebirthCraftingBodyZone','rebirthCraftingRightZone']:continue
   data=E.tostring(child,encoding='unicode').strip()
   if data not in cache:
    tag='rebirth_station_shared_'+str(serial);serial+=1;cache[data]=tag;holder=E.SubElement(app,tag);holder.append(copy.deepcopy(child))
   idx=list(parent).index(child);parent.remove(child);parent.insert(idx,E.Element(cache[data]))
E.indent(t,space='  ');p.write_text(E.tostring(t,encoding='unicode'),encoding='utf-8')
E.indent(w,space='  ');Path('Config/XUi_InGame/station_workspace.xml').write_text(E.tostring(w,encoding='unicode'),encoding='utf-8')
p=Path('Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('''        SetRect(owner.GetChildById("rebirthCraftingQueueRegion"), 0, 0, width, height);''','''        int top = 0;
        if (!string.IsNullOrEmpty(owner.Workstation))
        {
            var tools = owner.GetChildById("windowToolsForge");
            var fuel = owner.GetChildById("windowFuel");
            SetRect(owner.GetChildById("stationName"),14,-8,width-28,28);
            var title = owner.GetChildById("stationName")?.ViewComponent as XUiV_Label;
            if(title!=null)title.Text=Localization.Get(owner.Workstation);
            SetRect(tools,8,-42,228,121);
            SetRect(fuel,tools==null?8:width-236,-42,228,166);
            top = fuel!=null?218:tools!=null?175:46;
            var input=owner.GetChildById("windowForgeInput");
            if(input!=null){SetRect(input,8,-top,228,204);top+=214;}
            SetRect(owner.GetChildById("windowOutput"),8,-(height-198),width-16,198);
            height=Math.Max(100,height-top-208);
        }
        SetRect(owner.GetChildById("rebirthCraftingQueueRegion"), 0, -top, width, height);''');p.write_text(s,encoding='utf-8-sig')
