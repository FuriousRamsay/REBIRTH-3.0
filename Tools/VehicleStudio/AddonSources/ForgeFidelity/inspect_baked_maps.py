"""Read-only image data inspection; never a visual fidelity/seam acceptance test."""
import sys,json,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
folder=Path(sys.argv[1]);report={'status':'MAP_DATA_INSPECTION_NOT_VISUAL_ACCEPTANCE','maps':{}}
for name in ['albedo','normal','roughness','metallic','emission']:
 path=folder/(name+'.png')
 if not path.exists():continue
 with Image.open(path) as image:
  pixels=np.asarray(image.convert('RGB'),dtype=np.float32)/255
  file_data=path.read_bytes()
  item={'size':list(image.size),'mode':image.mode,'pngBitDepth':file_data[24] if file_data[:8]==b'\x89PNG\r\n\x1a\n' else None,'sha256':hashlib.sha256(file_data).hexdigest(),
   'channelMin':pixels.min(axis=(0,1)).tolist(),'channelMax':pixels.max(axis=(0,1)).tolist()}
  item['statisticsPrecision']='Pillow RGB preview converted to8bits; header bit depth is authoritative, statistics are not full16bit measurements.'
  if name=='normal':
   decoded=pixels*2-1;length=np.linalg.norm(decoded,axis=2)
   item['decodedLengthPercentiles']=np.percentile(length,[0,1,50,99,100]).tolist()
   item['note']='Values do not prove correct UV frame, convention, seam sampling or target-engine shading.'
  report['maps'][name]=item
(folder/'map-data-inspection.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
