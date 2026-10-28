"""Validate downloaded tensor containers and instantiate pipeline without GPU/network."""
import os,sys,json,time
from pathlib import Path
trial=Path('E:/_Haven/shared/REBIRTHTemp/ForgeImage3DTrial')
os.environ['CUDA_VISIBLE_DEVICES']='-1';os.environ['HF_HUB_OFFLINE']='1';os.environ['HF_HOME']=str(trial/'hf-cache')
sys.path.insert(0,str(trial/'TripoSG'))
out=trial/'cpu-validation.json';started=time.time()
report={'status':'VALIDATING','gpuVisible':False,'networkAllowed':False,'files':[],'fidelity':'NOT_TESTED'}
def save():out.write_text(json.dumps(report,indent=2))
save()
try:
    import torch
    from safetensors import safe_open
    assert not torch.cuda.is_available(),'GPU must be hidden for this CPU-only check'
    weights=trial/'weights/TripoSG'
    files=list(weights.rglob('*.safetensors'));assert len(files)==3
    for path in files:
        with safe_open(str(path),framework='pt',device='cpu') as container:
            keys=list(container.keys());assert keys
            shapes={key:list(container.get_slice(key).get_shape()) for key in keys[:3]}
        report['files'].append({'path':str(path.relative_to(weights)),'bytes':path.stat().st_size,'tensors':len(keys),'sampleShapes':shapes});save()
    report['status']='LOADING_PIPELINE_CPU';save()
    from triposg.pipelines.pipeline_triposg import TripoSGPipeline
    pipe=TripoSGPipeline.from_pretrained(str(weights),torch_dtype=torch.float16,local_files_only=True)
    components=[]
    for name,component in pipe.components.items():
        if isinstance(component,torch.nn.Module):
            devices=sorted({str(p.device) for p in component.parameters()});assert devices==['cpu'],(name,devices)
            components.append({'name':name,'class':type(component).__name__,'parameters':sum(p.numel() for p in component.parameters()),'devices':devices})
    report.update(status='CPU_PIPELINE_LOAD_PASS_INFERENCE_PENDING',components=components,elapsedSeconds=time.time()-started);save();print(json.dumps(report))
except Exception as error:
    report.update(status='FAILED',error=repr(error),elapsedSeconds=time.time()-started);save();raise
