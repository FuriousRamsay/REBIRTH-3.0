from pathlib import Path
p=Path('Config/XUi_InGame/windows.xml')
s=p.read_text(encoding='utf-8-sig')
start=s.index('<window name="rebirthCreativeRoot"')
end=s.index('</window>',start)
c=s[start:end]
c=c.replace('height="813"','height="889"')
c=c.replace('name="searchInput" focus_on_open','name="searchInput" justify="left" focus_on_open')
c=c.replace('height="380"','height="456"').replace('clippingsize="384,380"','clippingsize="384,456"').replace('clippingcenter="192,-190"','clippingcenter="192,-228"')
# Last toolbar button has center x=128 and half-width 13; slot edge is x=394.
c=c.replace('pos="206,-346" width="226" controller="ContainerStandardControls"','pos="253,-346" width="141" controller="ContainerStandardControls"')
s=s[:start]+c+s[end:]
p.write_text(s,encoding='utf-8')
