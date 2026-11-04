from pathlib import Path
root=Path(__file__).resolve().parents[3]
source=(root/'Scripts/Spawning/RebirthSpawnCompositionRuntimeIntegration.cs').read_text(encoding='utf-8-sig')
a=source.index('    public static void Postfix(string _sEntityGroupName')
b=source.index('\n}\n',a)
Path(__file__).with_name('Resolver.cs').write_text('using System;\nstatic class Resolver {\n'+source[a:b]+'\n}\n')
