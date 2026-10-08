from pathlib import Path
import csv, io, sys
root=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(root/'Tools/CookingContentProduction'))
from build_xml import cooking_food_description
path=root/'Config/Localization.csv'
raw=path.read_bytes();text=raw.decode('utf-8-sig');lines=text.splitlines(keepends=True)
replacement={'rebirthCookingFood'+key+'Desc':cooking_food_description(key) for key in ['P01','P02','P03','P04']}
reader=csv.reader(io.StringIO(text));previous=0;output=[];found=set()
for row in reader:
    original=''.join(lines[previous:reader.line_num]);previous=reader.line_num
    if len(row)==2 and row[0] in replacement:
        found.add(row[0]);buf=io.StringIO(newline='');csv.writer(buf,lineterminator='\r\n').writerow([row[0],replacement[row[0]]]);output.append(buf.getvalue())
    else:output.append(original)
assert found==set(replacement),found
path.write_bytes((b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b'')+''.join(output).encode('utf-8'))
assert all('cooked serving' not in v for v in replacement.values())
print('PASS: four component descriptions match generator; other CSV records preserved')

