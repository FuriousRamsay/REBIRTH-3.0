from pathlib import Path
root=Path(__file__).resolve().parents[2]
src=(root/'Scripts/Purge/Runtime/RebirthPurgeScanner.cs').read_text(encoding='utf-8-sig')
p=Path(__file__).with_name('Program.cs')
template=p.read_text()
a=src.index('    internal static bool SetCommandMarkers(');b=src.index('    private static void ClearMarkers()',a)
method=src[a:b]
a=src.index('    internal static bool Threshold(');b=src.index('    private static bool Eligible(',a)
threshold=src[a:b]
a=src.index('        RoomHistory history;',src.index('    private static View Build('));b=src.index('        history.Maximum=',a)
history=src[a:b]
assignment=next(x.strip() for x in src.splitlines() if 'result.Assisted=history.CommandMarkers||' in x)
header=template[:template.index('    internal static bool SetCommandMarkers(')]
checks=template[template.index(' static int checks;'):]
project=""" static bool Project(EntityPlayer player,RebirthPoiIdentity identity) {
 var result=new View{Epoch=RebirthPoiWorldLifecycle.Instance.Store.Published.Record.Epoch};
 object world=null,prefab=null;
"""+history+assignment+"\n return result.Assisted;\n }\n"
p.write_text(header+method+threshold+project+checks)
print('Refreshed production scanner grant/scope/projection/threshold before fixture run')
