from pathlib import Path
root=Path(__file__).resolve().parents[2]
p=Path(__file__).with_name('Program.cs');template=p.read_text()
s=(root/'Scripts/Purge/Network/RebirthPurgeSupplyTargets.cs').read_text()
a=s.index('internal sealed class RebirthPurgeSupplyTarget');b=s.index('internal static class RebirthPurgeSupplyTargets',a)
cls=s[a:b]
s=(root/'Scripts/UI/XUiC_RebirthPurgeSupplyButton.cs').read_text()
a=s.index('    internal static string CounterText(');b=s.index('    private World scope;',a)
helper=s[a:b]
checks=template[template.index(' static int checks;'):]
p.write_text('using System;using System.Collections.Generic;\nstruct Vector3 {}\n'+cls+'class Program {\n'+helper+checks)
