"""Download only official Mini shape weights/config and license; no inference."""
import os,json,time
from pathlib import Path
trial=Path('E:/_Haven/shared/REBIRTHTemp/ForgeImage3DTrial')
os.environ['HF_HOME']=str(trial/'hf-cache');os.environ['TORCH_HOME']=str(trial/'torch-cache')
from huggingface_hub import snapshot_download
out=trial/'weights/HunyuanMini';started=time.time()
snapshot_download(repo_id='tencent/Hunyuan3D-2mini',local_dir=str(out),allow_patterns=['hunyuan3d-dit-v2-mini/config.yaml','hunyuan3d-dit-v2-mini/model.fp16.safetensors','LICENSE','README.md'],max_workers=2)
files=[{'name':str(p.relative_to(out)),'bytes':p.stat().st_size} for p in out.rglob('*') if p.is_file() and '.cache' not in p.parts]
report={'status':'SHAPE_WEIGHTS_DOWNLOADED_NO_INFERENCE','repository':'tencent/Hunyuan3D-2mini','subfolder':'hunyuan3d-dit-v2-mini','files':files,'elapsedSeconds':time.time()-started,'texturesDownloaded':False,'fidelity':'NOT_TESTED'}
(trial/'hunyuan-download-receipt.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
