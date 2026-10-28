"""Single guarded independent shape comparison; no texturing or installation."""
import os,sys,json,time,argparse,hashlib
from pathlib import Path
trial=Path('E:/_Haven/shared/REBIRTHTemp/ForgeImage3DTrial')
os.environ['HF_HUB_OFFLINE']='1';os.environ['HF_HOME']=str(trial/'hf-cache');os.environ['TORCH_HOME']=str(trial/'torch-cache');os.environ['HY3DGEN_MODELS']=str(trial/'weights')
sys.path.insert(0,str(trial/'Hunyuan3D-2'))
parser=argparse.ArgumentParser();parser.add_argument('--attempt',type=int,default=1);args=parser.parse_args();assert args.attempt>=1
import psutil
games=[p.pid for p in psutil.process_iter(['name']) if (p.info.get('name') or '').lower()=='7daystodie.exe']
assert not games,f'Defer GPU trial while game PIDs {games} are active'
folder=trial/'weights/HunyuanMini/hunyuan3d-dit-v2-mini';checkpoint=folder/'model.fp16.safetensors'
inputPath=trial/'forge-cutout-v1.png';out=trial/'Outputs'/('hunyuan-mini-forge-seed42'+(f'-attempt{args.attempt}' if args.attempt>1 else ''));out.mkdir(parents=True,exist_ok=True)
assert not (out/'trial-state.json').exists(),'Preserve earlier attempt; increment --attempt'
started=time.time();state={'status':'INITIALIZING','generator':'Hunyuan3D-2mini','sourceCommit':'f8db63096c8282cb27354314d896feba5ba6ff8a','inputSha256':hashlib.sha256(inputPath.read_bytes()).hexdigest(),'seed':42,'steps':50,'octreeResolution':384,'extractor':'mc/skimage','textures':False,'fidelity':'NOT_REVIEWED'}
def save(): (out/'trial-state.json').write_text(json.dumps(state,indent=2))
save()
try:
    import torch
    from PIL import Image
    from hy3dgen.shapegen import Hunyuan3DDiTFlowMatchingPipeline
    assert torch.cuda.is_available()
    free,total=torch.cuda.mem_get_info();assert free>=5.5*1024**3,f'GPU slot needs5.5GiB free; actual{free/1024**3:.2f}'
    budget=min(5.2*1024**3,free-.75*1024**3);torch.cuda.set_per_process_memory_fraction(budget/total)
    pipe=Hunyuan3DDiTFlowMatchingPipeline.from_single_file(str(checkpoint),str(folder/'config.yaml'),device='cpu',dtype=torch.float16,use_safetensors=True)
    # This upstream standalone pipeline omits the Diffusers components property.
    pipe.components={name:getattr(pipe,name) for name in ('conditioner','model','vae')}
    pipe.enable_model_cpu_offload()
    # Upstream __call__ reads self.device; synchronize it to the hook execution device without moving all components.
    pipe.device=pipe._execution_device;assert pipe.device.type=='cuda'
    image=Image.open(inputPath);assert image.mode=='RGBA' and image.getchannel('A').getextrema()==(0,255)
    state.update(status='GENERATING',cudaBudgetGiB=budget/1024**3);save()
    with torch.no_grad():
        mesh=pipe(image=image,num_inference_steps=50,guidance_scale=5.0,generator=torch.Generator(device='cuda').manual_seed(42),octree_resolution=384,mc_algo='mc',num_chunks=8000)[0]
    assert mesh is not None and len(mesh.vertices)>0 and len(mesh.faces)>0
    mesh.export(str(out/'forge-generated.glb'))
    state.update(status='MESH_SAVED_VISUAL_REVIEW_PENDING',vertices=len(mesh.vertices),faces=len(mesh.faces),elapsedSeconds=time.time()-started);save();print(json.dumps(state))
except Exception as error:
    state.update(status='FAILED',error=repr(error),elapsedSeconds=time.time()-started);save();raise
