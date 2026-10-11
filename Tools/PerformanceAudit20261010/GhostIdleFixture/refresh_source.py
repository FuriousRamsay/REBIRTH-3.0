from pathlib import Path
root=Path(__file__).resolve().parents[3]
s=(root/'Scripts/NPC/Dog/RebirthDogLifecycleService.cs').read_text()
method=s[s.index('    public static void TickGhostRepairRetries()'):s.index('    public static void ResetGhostRepairRetries()')]
p=Path(__file__).with_name('Program.cs');text=p.read_text();a=text.index('    public static void TickGhostRepairRetries()');b=text.index(' static void Check(',a)
p.write_text(text[:a]+method+'\n'+text[b:])
print('Refreshed callback from current production source')
