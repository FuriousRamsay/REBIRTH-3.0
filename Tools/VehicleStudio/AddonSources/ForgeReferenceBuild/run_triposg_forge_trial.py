"""One isolated free shape trial; no texture/fidelity or production-install claims."""
import os,sys,json,time,argparse,hashlib,re
from pathlib import Path
trial=Path('E:/_Haven/shared/REBIRTHTemp/ForgeImage3DTrial')
os.environ['HF_HOME']=str(trial/'hf-cache');os.environ['TORCH_HOME']=str(trial/'torch-cache')
os.environ['HF_HUB_DISABLE_TELEMETRY']='1'
sys.path.insert(0,str(trial/'TripoSG'));sys.path.insert(0,str(trial/'TripoSG/scripts'))
parser=argparse.ArgumentParser();parser.add_argument('--download-only',action='store_true');parser.add_argument('--depth',type=int,default=8)
parser.add_argument('--image-input',type=Path,default=trial/'forge-cutout-v1.png');parser.add_argument('--asset-name',default='forge')
parser.add_argument('--seed',type=int,default=42);parser.add_argument('--attempt',type=int,default=1);args=parser.parse_args()
assert re.fullmatch(r'[a-z][a-z0-9-]{0,47}',args.asset_name),'Use a short lowercase asset name, without paths'
assert args.attempt>=1 and args.depth in (7,8,9),'Invalid attempt or decoder depth'
from huggingface_hub import snapshot_download
weights=trial/'weights/TripoSG'
required=['model_index.json','image_encoder_dinov2/model.safetensors','transformer/diffusion_pytorch_model.safetensors','vae/diffusion_pytorch_model.safetensors']
if not all((weights/name).exists() for name in required):
    snapshot_download(repo_id='VAST-AI/TripoSG',local_dir=str(weights),allow_patterns=['*.json','*.safetensors'],max_workers=2)
if args.download_only:
    print('WEIGHTS_DOWNLOADED_NO_INFERENCE');sys.exit(0)
import psutil
activeGames=[p.pid for p in psutil.process_iter(['name']) if (p.info.get('name') or '').lower()=='7daystodie.exe']
if activeGames:
    raise RuntimeError(f'GPU trial deferred: game still running, PIDs {activeGames}. Do not close/control it from this script.')
import torch,numpy as np,trimesh
from PIL import Image
from triposg.pipelines.pipeline_triposg import TripoSGPipeline
from image_process import prepare_image
inputPath=args.image_input.resolve();assert inputPath.is_file(),str(inputPath)
folderName=f'{args.asset_name}-depth{args.depth}-seed{args.seed}'+(f'-attempt{args.attempt}' if args.attempt>1 else '')
output=trial/'Outputs'/folderName;output.mkdir(parents=True,exist_ok=True)
outputPath=output/f'{args.asset_name}-generated.glb'
assert not outputPath.exists(),'Preserve previous trial output; choose a separate output directory before rerunning'
assert not (output/'trial-state.json').exists(),'Preserve previous attempt evidence; increment --attempt'
started=time.time();state={'status':'INITIALIZING','asset':args.asset_name,'attempt':args.attempt,'input':str(inputPath),'inputSha256':hashlib.sha256(inputPath.read_bytes()).hexdigest(),'seed':args.seed,'steps':50,'decoder':'upstream hierarchical, no DISO','depth':args.depth,'textures':False,'fidelity':'NOT_REVIEWED'}
def save(): (output/'trial-state.json').write_text(json.dumps(state,indent=2))
save()
try:
    assert torch.cuda.is_available(),'CUDA unavailable'
    freeMemory,totalMemory=torch.cuda.mem_get_info()
    assert totalMemory>=8*1024**3,'Upstream minimum is an8GiB GPU'
    assert freeMemory>=5.5*1024**3,f'CPU-offloaded trial deferred: only {freeMemory/1024**3:.2f}GiB free'
    memoryBudget=min(5.2*1024**3,freeMemory-.75*1024**3)
    torch.cuda.set_per_process_memory_fraction(memoryBudget/totalMemory)
    state['cudaAllocationBudgetGiB']=memoryBudget/1024**3
    with Image.open(inputPath) as image:
        assert image.mode=='RGBA' and image.getchannel('A').getextrema()==(0,255),'Valid supplied alpha required; no background-removal model used'
    image=prepare_image(str(inputPath),bg_color=np.array([1.,1.,1.]),rmbg_net=None)
    pipe=TripoSGPipeline.from_pretrained(str(weights),torch_dtype=torch.float16)
    pipe.model_cpu_offload_seq='image_encoder_dinov2->transformer->vae'
    pipe.enable_model_cpu_offload()
    state['status']='GENERATING';state['gpu']=torch.cuda.get_device_name(0);save()
    with torch.no_grad():
        result=pipe(image=image,generator=torch.Generator(device='cuda').manual_seed(args.seed),num_inference_steps=50,guidance_scale=7.,use_flash_decoder=False,dense_octree_depth=args.depth-1,hierarchical_octree_depth=args.depth).samples[0]
    assert result[0] is not None and result[1] is not None,'Decoder returned no mesh'
    mesh=trimesh.Trimesh(result[0].astype(np.float32),np.ascontiguousarray(result[1]),process=False)
    assert len(mesh.vertices)>0 and len(mesh.faces)>0
    mesh.export(str(outputPath))
    state.update(status='MESH_SAVED_VISUAL_REVIEW_PENDING',vertices=len(mesh.vertices),faces=len(mesh.faces),elapsedSeconds=time.time()-started);save()
    print(json.dumps(state))
except Exception as error:
    state.update(status='FAILED',error=repr(error),elapsedSeconds=time.time()-started);save();raise
