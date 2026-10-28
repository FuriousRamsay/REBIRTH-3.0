using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Text.Json;

// Offline inventory of the actual built assembly, never executes mod/game code.
// Call graph is static evidence, not proof a path is running. Reflection,
// interface dispatch and native game I/O need separate source/runtime review.
if (args.Length != 2) throw new ArgumentException("Usage: FileIoAudit assembly output.json");
using var assembly = AssemblyDefinition.ReadAssembly(args[0], new ReaderParameters { ReadSymbols = File.Exists(Path.ChangeExtension(args[0], ".pdb")) });
var methods = Types(assembly.MainModule.Types).SelectMany(t => t.Methods).Where(m => m.HasBody).ToArray();
var own = methods.ToDictionary(m => m.FullName);
var callers = new Dictionary<string, HashSet<string>>();
foreach (var m in methods)
foreach (var instruction in m.Body.Instructions)
    if (instruction.Operand is MethodReference call && own.ContainsKey(call.GetElementMethod().FullName))
    {
        string key = call.GetElementMethod().FullName;
        if (!callers.TryGetValue(key, out var set)) callers[key] = set = new();
        set.Add(m.FullName);
    }
var rows = new List<object>();
foreach (var m in methods)
{
    var calls = m.Body.Instructions.Where(i => i.Operand is MethodReference r && IsIo(r)).ToArray();
    if (calls.Length == 0) continue;
    var source = m.DebugInformation.SequencePoints.FirstOrDefault(p => !p.IsHidden);
    var seen = new HashSet<string> { m.FullName };
    var frontier = new Queue<(string Name,int Depth)>(); frontier.Enqueue((m.FullName,0));
    var recurring = new HashSet<string>();
    while (frontier.Count > 0)
    {
        var node = frontier.Dequeue();
        var name = own[node.Name].Name;
        if (name.Contains("Update") || name.Contains("Tick") || name.Contains("Pump")) recurring.Add(node.Name);
        if (node.Depth >= 8 || !callers.TryGetValue(node.Name, out var parents)) continue;
        foreach (var parent in parents) if (seen.Add(parent)) frontier.Enqueue((parent,node.Depth+1));
    }
    rows.Add(new {
        method=m.FullName, source=source?.Document.Url, line=source?.StartLine,
        operations=calls.Select(i=>new {offset=i.Offset, call=((MethodReference)i.Operand).FullName,
            category=IoCategory((MethodReference)i.Operand),
            forcedFlush=((MethodReference)i.Operand).Name=="Flush" && ((MethodReference)i.Operand).Parameters.Count==1 && i.Previous?.OpCode==OpCodes.Ldc_I4_1}).ToArray(),
        callers=callers.TryGetValue(m.FullName,out var direct)?direct.Order().ToArray():Array.Empty<string>(),
        recurringAncestors=recurring.Order().ToArray(),
        literals=m.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ldstr).Select(i=>(string)i.Operand)
            .Where(s=>s.Length>0 && s.Length<180).Distinct().ToArray()
    });
}
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
File.WriteAllText(args[1],JsonSerializer.Serialize(new {
    assembly=Path.GetFullPath(args[0]),mvid=assembly.MainModule.Mvid,utc=DateTime.UtcNow,
    methodCount=methods.Length,ioMethodCount=rows.Count,
    scope="Direct filesystem APIs, native SdFile/SdDirectory wrappers and file-capable XML/stream APIs; stream overloads may be memory-only. Static recurring ancestry depth8, not runtime frequency. Other native/reflection/interface paths require review.",
    methods=rows
},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Inspected {methods.Length} compiled methods; {rows.Count} methods have direct I/O or file-capable API calls. Wrote {args[1]}");
static bool IsIo(MethodReference r)
{
    string t=r.DeclaringType.FullName;
    if(t is "SdFile" or "SdDirectory") return true;
    if(t is "System.IO.File" or "System.IO.Directory" or "System.IO.FileInfo" or "System.IO.DirectoryInfo" or "System.IO.FileStream" or "System.IO.FileSystemWatcher") return true;
    if(t is "System.IO.StreamReader" or "System.IO.StreamWriter") return r.Name==".ctor";
    if(t.StartsWith("System.Xml.") && r.Name is "Load" or "Save" or "Create") return true;
    return false;
}
static string IoCategory(MethodReference r)
{
    string t=r.DeclaringType.FullName;
    if(t is "SdFile" or "SdDirectory") return "native-file-wrapper";
    if(t.StartsWith("System.Xml.") || t is "System.IO.StreamReader" or "System.IO.StreamWriter") return "file-capable-stream";
    return "filesystem";
}
static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> roots)
{
    foreach(var t in roots){yield return t;foreach(var nested in Types(t.NestedTypes))yield return nested;}
}
