"""Downsample generated challenge PNGs for the game's UI atlas; preserve alpha."""
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[2]
for path in (ROOT/'UIAtlases/UIAtlas').glob('rb_challenge_*.png'):
    with Image.open(path) as src:
        if src.size==(128,128):
            continue
        assert src.mode=='RGBA', f'{path}: generated alpha missing'
        image=src.resize((128,128),Image.Resampling.LANCZOS)
    image.save(path,optimize=True)
print('Prepared existing challenge icons at 128x128 with alpha preserved.')
