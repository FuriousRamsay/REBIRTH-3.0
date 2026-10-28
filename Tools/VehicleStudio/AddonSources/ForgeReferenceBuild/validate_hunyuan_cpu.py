"""Load the official Mini shape checkpoint on CPU, with GPU/network disabled."""
import os,sys,json,time
from pathlib import Path
trial=Path('E:/_Haven/shared/REBIRTHTemp/ForgeImage3DTrial')
os.environ['CUDA_VISIBLE_DEVICES']='-1';os.environ['HF_HUB_OFFLINE']='1'
os.environ['HF_HOME']=str(trial/'hf-cache');os.environ['TORCH_HOME']=str(trial/'torch-cache');os.environ['HY3DGEN_MODELS']=str(trial/'weights')
sys.path.insert(0,str(trial/'Hunyuan3D-2'))
folder=trial/'weights/HunyuanMini/hunyuan3d-dit-v2-mini';checkpoint=folder/'model.fp16.safetensors'
assert checkpoint.is_file(),'Wait for the verified weight-download job to finish'
output=trial/'hunyuan-cpu-validation.json';started=time.time()
report={'status':'VALIDATING_CPU','gpuVisible':False,'networkAllowed':False,'fidelity':'NOT_TESTED'}
def save():output.write_text(json.dumps(report,indent=2))
save()
try:
    import torch
    from safetensors import safe_open
    assert not torch.cuda.is_available()
    with safe_open(str(checkpoint),framework='pt',device='cpu') as container:
        keys=list(container.keys());assert keys
        report['tensors']=len(keys);report['checkpointBytes']=checkpoint.stat().st_size;save()
    from hy3dgen.shapegen import Hunyuan3DDiTFlowMatchingPipeline
    pipe=Hunyuan3DDiTFlowMatchingPipeline.from_single_file(str(checkpoint),str(folder/'config.yaml'),device='cpu',dtype=torch.float16,use_safetensors=True)
    components=[]
    for name in ['model','vae','conditioner']:
        component=getattr(pipe,name);devices=sorted({str(p.device) for p in component.parameters()});assert devices==['cpu'],(name,devices)
        components.append({'name':name,'class':type(component).__name__,'parameters':sum(p.numel() for p in component.parameters()),'devices':devices})
    report.update(status='CPU_PIPELINE_LOAD_PASS_INFERENCE_PENDING',components=components,elapsedSeconds=time.time()-started);save();print(json.dumps(report))
except Exception as error:
    report.update(status='FAILED',error=repr(error),elapsedSeconds=time.time()-started);save();raise
