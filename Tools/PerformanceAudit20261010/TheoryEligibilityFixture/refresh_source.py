from pathlib import Path
root=Path(__file__).resolve().parents[3]
p=Path(__file__).with_name('Program.cs');text=p.read_text()
for filename,name in [('_Documentation/PerformanceAudit_20261010/before_theory_eligibility/RebirthTheorySoloService.cs','Before'),('Scripts/Survivor/Progression/TheorySolo/RebirthTheorySoloService.cs','After')]:
 s=(root/filename).read_text();method=s[s.index('    private static bool Eligible('):s.index('    internal static bool ValidateOutcome')].replace('bool Eligible(','bool '+name+'(')
 a=text.index('    private static bool '+name+'(');b=text.index('    private static bool After(',a) if name=='Before' else text.index('static void Main()',a)
 text=text[:a]+method+text[b:]
p.write_text(text)
print('Refreshed old and current eligibility methods')
