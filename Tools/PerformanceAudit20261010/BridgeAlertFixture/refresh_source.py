from pathlib import Path
root=Path(__file__).resolve().parents[3]
s=(root/'Scripts/GameBridge/RebirthGameBridgeUi.cs').read_text();a=s.index('    private static void WalkAlertTexts(');b=s.index('    /// <summary>All visible label texts',a)
p=Path(__file__).with_name('Program.cs');t=p.read_text();x=t.index('    private static void WalkAlertTexts(');y=t.index('// Reference inclusion/text rules',x);p.write_text(t[:x]+s[a:b]+'\n'+t[y:])
print('Refreshed production text walker')
