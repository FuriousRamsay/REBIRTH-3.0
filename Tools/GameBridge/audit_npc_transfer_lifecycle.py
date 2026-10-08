from pathlib import Path
root=Path(__file__).resolve().parents[2]
s=(root/'Scripts/NPC/Persistence/RebirthNpcPersistenceCoordinator.cs').read_text(encoding='utf-8-sig')
a=s.index('public static bool ResetAfterSave()'); b=s.index('public static void ResetForUnexpectedWorldBoundary()',a)
safe=s[a:b]
assert safe.index('if (!lastSaveSucceeded)') < safe.index('return false;') < safe.index('RebirthNpcExternalInventoryTransferCoordinator.Reset();')
assert safe.count('RebirthNpcExternalInventoryTransferCoordinator.Reset();')==1
boundary=s[b:s.index('public static RebirthNpcPersistenceFileSnapshot[]',b)]
assert boundary.count('RebirthNpcExternalInventoryTransferCoordinator.Reset();')==1
print('PASS: transfer coordinator included in both lifecycle reset paths, after failed-save refusal. Source integration check, not runtime concurrency test.')
