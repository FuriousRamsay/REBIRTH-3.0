from pathlib import Path
import xml.etree.ElementTree as E
p=Path('Config/_Purge/map_popup.xml');t=E.parse(p);card=t.find(".//rect[@name='rebirthPurgePoiPopup']");card.tag='panel';card.set('depth','2000');card.set('clipping','None')
for n in card:
 if n.tag=='sprite' and n.get('pos')=='14,-174':n.set('pos','14,-232')
for name,x,color,icon,atlas in [('Areas',14,'181,140,255,255','ui_game_symbol_map','UIAtlas'),('Entities',316,'235,210,118,255','ui_game_symbol_skull','UIAtlas')]:
 E.SubElement(card,'sprite',name='poiPopup'+name+'Background',pos=f'{x},-174',width='290',height='48',sprite='menu_empty3px',type='sliced',color='30,32,39,255',depth='501',globalopacitymod='0')
 E.SubElement(card,'sprite',name='poiPopup'+name+'Icon',pos=f'{x+10},-184',width='28',height='28',sprite=icon,atlas=atlas,color=color,depth='503',globalopacitymod='0')
 E.SubElement(card,'label',name='poiPopup'+name+'Caption',pos=f'{x+48},-198',width='188',height='28',font_size='17',text_key='xuiRebirthPurgePopup'+name,color='225,225,232,255',pivot='left',justify='left',upper_case='true',depth='503')
 E.SubElement(card,'label',name='poiPopup'+name+'Value',pos=f'{x+238},-198',width='42',height='34',font_size='27',color=color,pivot='left',justify='right',depth='503')
for kind in ['Discoverer','Contributors']:
 label=next(n for n in card if n.get('name')=='poiPopup'+kind);label.set('width','272')
 E.SubElement(card,'label',name='poiPopup'+kind+'Caption',width='108',height='30',font_size='18',text_key='xuiRebirthPurgePopup'+kind+'Label',color='225,225,232,255',pivot='left',justify='left',depth='503')
E.indent(t,space='  ');t.write(p,encoding='unicode')
p=Path('Scripts/Purge/Network/RebirthPoiMapProjection.cs');s=p.read_text();s=s.replace('internal readonly int Tier;','internal readonly int Tier;\n    internal readonly int AreasLeft, EstimatedEntities;\n    internal readonly bool Approximate;')
s=s.replace('ulong purgedAt=0)','ulong purgedAt=0,int areasLeft=-1,int estimatedEntities=-1,bool approximate=true)')
s=s.replace('Identity = identity; Epoch = epoch;', 'if(areasLeft < -1 || areasLeft > 4096 || estimatedEntities < -1 || estimatedEntities > 1000000)throw new ArgumentException("Invalid map counters.");\n        AreasLeft=areasLeft;EstimatedEntities=estimatedEntities;Approximate=approximate;\n        Identity = identity; Epoch = epoch;')
s=s.replace('&&Discoverer==other.Discoverer','&&AreasLeft==other.AreasLeft&&EstimatedEntities==other.EstimatedEntities&&Approximate==other.Approximate\n        &&Discoverer==other.Discoverer')
s=s.replace('writer.Write(0x504d5033);','writer.Write(0x504d5034);')
s=s.replace('writer.Write(record.PurgedAt);','writer.Write(record.PurgedAt);writer.Write(record.AreasLeft);writer.Write(record.EstimatedEntities);writer.Write(record.Approximate);')
s=s.replace('version != 0x504d5033)','version != 0x504d5033 && version != 0x504d5034)')
s=s.replace('version==0x504d5033?', 'version>=0x504d5033?')
s=s.replace('records[i] = new RebirthPoiMapRecord(id, epoch, state, tier,discoverer,contributors,discoveredAt,purgedAt);','int areas=version>=0x504d5034?reader.ReadInt32():-1,entities=version>=0x504d5034?reader.ReadInt32():-1;\n                    bool approximate=version>=0x504d5034?reader.ReadBoolean():true;\n                    records[i] = new RebirthPoiMapRecord(id, epoch, state, tier,discoverer,contributors,discoveredAt,purgedAt,areas,entities,approximate);')
p.write_text(s)
p=Path('Scripts/Purge/Network/RebirthPoiMapSync.cs');s=p.read_text();s=s.replace('var projected = new RebirthPoiMapRecord(', 'int areas,entities;bool approximate;ReadCounters(record,out areas,out entities,out approximate);\n                var projected = new RebirthPoiMapRecord(');s=s.replace('history?.PurgedAt??0);','history?.PurgedAt??0,areas,entities,approximate);')
pos=s.index('    private static void CompleteProjection()')
helper='''    private static void ReadCounters(RebirthPoiClearanceRecord record,out int areas,out int entities,out bool approximate)
    {
        areas=entities=-1;approximate=true;
        if(record.State==RebirthPoiClearanceState.Cleared&&!record.ResetOnly){areas=entities=0;approximate=false;return;}
        if(record.State==RebirthPoiClearanceState.ResetPending)return;
        var id=record.Identity;
        var prefab=GameManager.Instance?.GetDynamicPrefabDecorator()?.GetPrefabFromWorldPosInside(id.X+id.SizeX/2,id.Z+id.SizeZ/2);
        RebirthPoiNativeManifest manifest;
        if(!RebirthPoiNativeManifest.TryResolve(GameManager.Instance?.World,prefab,out manifest)||manifest.Identity.Key!=id.Key)return;
        areas=entities=0;approximate=false;int observed=0;
        foreach(var room in prefab.sleeperVolumes)
        {
            if(room==null)continue;observed++;
            if(room.wasCleared)continue;
            areas++;entities=(int)Math.Min(1000000L,(long)entities+Math.Max(1,room.numSpawned));
            if(room.numSpawned<=0||room.isSpawning)approximate=true;
        }
        int missing=Math.Max(0,prefab.prefab.SleeperVolumeList.Count-observed);
        areas+=missing;entities=(int)Math.Min(1000000L,(long)entities+missing);if(missing>0)approximate=true;
    }
'''
s=s[:pos]+helper+s[pos:];p.write_text(s)
p=Path('Config/Localization.csv');s=p.read_text();s+='\nxuiRebirthPurgePopupAreas,Areas Left\nxuiRebirthPurgePopupEntities,Estimated Entities\nxuiRebirthPurgePopupDiscovererLabel,Discovered by\nxuiRebirthPurgePopupContributorsLabel,Purged by\n';p.write_text(s)
