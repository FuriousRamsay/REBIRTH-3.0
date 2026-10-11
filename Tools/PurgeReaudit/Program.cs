using System; using System.Linq; using Mono.Cecil;
class Program { static void Main(string[] args) {
using var assembly=AssemblyDefinition.ReadAssembly("../../7DaysToDie_Data/Managed/Assembly-CSharp.dll");
foreach(var type in assembly.MainModule.Types.Where(t=>args.Length==0 ? t.Name=="SpawnManagerBiomes" : t.Name.Contains("Map") || t.Name.Contains("NavObject")))
foreach(var method in type.Methods.Where(m=>m.HasBody && (args.Length==0 ? m.Name=="SpawnUpdate" || m.Name=="SpawnEntity" : m.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name=="hiddenOnCompass")))) {
Console.WriteLine(method.FullName);
foreach(var instruction in method.Body.Instructions)Console.WriteLine(instruction);
}
} }
