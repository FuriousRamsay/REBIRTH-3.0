"""Decode maps through Blender without Pillow's RGB8 conversion; numerical review only."""
import bpy,numpy as np,json,sys,hashlib
from pathlib import Path
args=sys.argv[sys.argv.index('--')+1:];folder=Path(args[0]);names=args[1:]
report={'status':'FLOAT_MAP_DATA_INSPECTION_NOT_VISUAL_ACCEPTANCE','maps':{}}
for name in names:
 path=folder/(name+'.exr' if name=='emission' else name+'.png')
 if not path.exists():continue
 image=bpy.data.images.load(str(path));image.colorspace_settings.name='Non-Color'
 rgba=np.empty(len(image.pixels),dtype=np.float32);image.pixels.foreach_get(rgba)
 pixels=rgba.reshape((-1,image.channels))[:,:3]
 item={'size':list(image.size),'decoder':'Blender Non-Color float pixels','channelMin':pixels.min(axis=0).tolist(),'channelMax':pixels.max(axis=0).tolist(),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
 if path.suffix=='.png':
  with path.open('rb') as stream:header=stream.read(25)
  item['pngBitDepth']=header[24]
 if name=='normal':
  lengths=np.linalg.norm(pixels*2-1,axis=1)
  item['decodedLengthPercentiles']=np.percentile(lengths,[0,1,50,99,100]).tolist()
  item['note']='Does not establish correct tangent orientation, seams, filtering or visual fidelity.'
 report['maps'][name]=item;bpy.data.images.remove(image)
(folder/'float-map-data-inspection.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
