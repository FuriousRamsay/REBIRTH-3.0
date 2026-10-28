"""Downsampled diagnostic plot of actual texture data; preserves the original map."""
import sys
from pathlib import Path
from PIL import Image
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
source=Path(sys.argv[1]);output=Path(sys.argv[2])
if output.exists():raise RuntimeError('Preserve prior plot')
with Image.open(source) as img:
 fig,axis=plt.subplots(figsize=(8,8),dpi=150)
 axis.imshow(img.convert('RGB'),interpolation='nearest');axis.axis('off')
 axis.set_title('Actual 4K albedo — downsampled inspection only',fontsize=10)
 fig.tight_layout();fig.savefig(output);plt.close(fig)
