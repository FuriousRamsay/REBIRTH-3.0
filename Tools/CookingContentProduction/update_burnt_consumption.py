"""Apply the agreed burnt-food names and consumption properties without changing other localization rows."""
import csv, io, xml.etree.ElementTree as E
from content_model import ROOT

path=ROOT/'Config/_Cooking/burnt_items.xml'
tree=E.parse(path)
for item in tree.findall('.//item'):
    prop=item.find("property[@name='RebirthHydrationCostMl']")
    if prop is None: prop=E.SubElement(item,'property',name='RebirthHydrationCostMl')
    prop.set('value','100')
E.indent(tree,space='  ')
path.write_text(E.tostring(tree.getroot(),encoding='unicode')+'\n',encoding='utf-8')

path=ROOT/'Config/Localization.csv'
lines=path.read_text(encoding='utf-8-sig').splitlines(keepends=True)
for i,line in enumerate(lines):
    row=next(csv.reader([line]))
    if len(row)<2: continue
    if row[0].startswith('rebirthBurnt_'):
        name=row[1]
        if name.startswith('Burnt '): name=name[6:]
        if not name.endswith(' (Burnt)'): name+=' (Burnt)'
        row[1]=name
    elif row[0]=='rebirthBurntFoodDesc':
        row[1]='Ruined by prolonged heat. Very little nutrition remains. Eating it increases thirst and lowers mood.'
    else: continue
    out=io.StringIO(); csv.writer(out,lineterminator='\n').writerow(row); lines[i]=out.getvalue()
path.write_text(''.join(lines),encoding='utf-8')
print('Updated burnt item names and 100 mL hydration cost.')
