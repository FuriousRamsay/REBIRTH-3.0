from pathlib import Path
import sys
root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('.')
p=root/'Scripts/Survivor/Support/RebirthSurvivorGearService.cs'
s=p.read_text(encoding='utf-8')
checks=[
 ('test flag remains runtime-enabled', 'public static readonly bool ForceHundredSlotBackpackForPersonalCraftingTest = true;' in s),
 ('test helper exists', 'private static bool UseHundredSlotBackpackForPersonalCraftingTest()' in s),
 ('helper returns test flag', 'return ForceHundredSlotBackpackForPersonalCraftingTest;' in s),
 ('player capacity branch uses helper', 'if (UseHundredSlotBackpackForPersonalCraftingTest())\n        {' in s),
 ('record capacity branch uses helper', 'if (UseHundredSlotBackpackForPersonalCraftingTest())\n            return PersonalCraftingTestPhysicalBagSlots;' in s),
 ('no direct constant-style branch remains', 'if (ForceHundredSlotBackpackForPersonalCraftingTest)' not in s),
]
failed=[]
for name,ok in checks:
 print(('PASS' if ok else 'FAIL')+': '+name)
 if not ok: failed.append(name)
print(f'RESULT: {len(checks)-len(failed)}/{len(checks)} PASS')
sys.exit(1 if failed else 0)
