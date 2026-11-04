"""Prepare or activate four creative-only comparison modifiers/localizations."""
import argparse, copy, csv, io, json, shutil, xml.etree.ElementTree as ET
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
DOC=ROOT/'_Documentation/WrapComparison20261104'
BASE=Path('F:/REBIRTH_Wrap_Optimization/2026-11-04/Comparison')

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--activate',action='store_true');args=ap.parse_args()
    result=json.loads((BASE/'comparison.json').read_text());rows=result['variants'];DOC.mkdir(parents=True,exist_ok=True)
    original=ET.parse(ROOT/'Config/_Vehicles/camper_wrap_collection_item_modifiers.xml')
    template=next(n for n in original.iter('item_modifier') if n.attrib['name']=='modVehicleWrapNavezganeExplorer')
    xml=ET.Element('configs');parent=ET.SubElement(xml,'append',xpath='/item_modifiers');localized=[]
    for letter,row in zip('ABCD',rows):
        item=row['item'];node=copy.deepcopy(template);node.set('name',item)
        values={n.attrib['name']:n for n in node.findall('property')}
        values['DescriptionKey'].set('value',item+'Desc');values['RebirthCamperWrapId'].set('value',row['id'])
        values['EconomicValue'].set('value','0');values['CreativeMode'].set('value','Player')
        ET.SubElement(node,'property',name='SellableToTrader',value='false');parent.append(node)
        mb=row['bytes']/1e6;reduction=row['reductionPercent']
        label=f'Wrap Test {letter} - Explorer {row["percent"]}%'
        desc=f'Navezgane Explorer comparison {letter}. {mb:.2f} MB; {reduction:.0f}% smaller than the current wrap. Install in the vehicle cosmetic slot to compare. Creative test copy; not found in loot or sold by traders.'
        localized.extend([(item,label),(item+'Desc',desc)])
    ET.indent(xml,space='  ')
    staged=DOC/'camper_wrap_comparison_item_modifiers.xml';ET.ElementTree(xml).write(staged,encoding='utf-8',xml_declaration=False)
    (DOC/'localization.json').write_text(json.dumps(localized,indent=2))
    if not args.activate:print('Staged 4 modifiers, 8 localization rows; inactive');return
    assert (BASE/'gpu-comparison-PASS.txt').exists(),'GPU verification required before activation'
    assert (DOC/'build/RebirthUtils.dll').exists(),'Build required before activation'
    shutil.copyfile(staged,ROOT/'Config/_Vehicles/camper_wrap_comparison_item_modifiers.xml')
    path=ROOT/'Config/_Vehicles/camper_pattern_item_modifiers.xml';data=path.read_bytes()
    include=b'  <include filename="camper_wrap_comparison_item_modifiers.xml" />'
    if include not in data:
        assert data.count(b'</configs>')==1;data=data.replace(b'</configs>',include+b'\r\n</configs>');path.write_bytes(data)
    for filename in ('Localization.csv','Localization.txt'):
        path=ROOT/'Config'/filename;data=path.read_bytes();existing=list(csv.reader(io.StringIO(data.decode('utf-8-sig'))))
        keys={r[0] for r in existing if r};add=[]
        for key,value in localized:
            if key in keys:raise RuntimeError('Comparison localization already exists; do not duplicate '+key)
            if filename.endswith('.csv'):add.append([key,value])
            else:
                sample=next(r for r in existing if r and r[0]=='modVehicleWrapNavezganeExplorer')
                row=sample.copy();row[0]=key;row[4]=value;add.append(row)
        output=io.StringIO(newline='');csv.writer(output,lineterminator='\r\n').writerows(add)
        with path.open('ab') as f:
            if not data.endswith(b'\n'):f.write(b'\r\n')
            f.write(output.getvalue().encode('utf-8'))
    print('Activated 4 creative-only modifiers; no loot/trader/recipe changes')
if __name__=='__main__':main()
