using Mono.Cecil;

// Inspect the installed game's exact ABI/IL without loading Unity assemblies.
// Usage: dotnet run --project ... -- TypeName [MethodName ...]
using var assembly = AssemblyDefinition.ReadAssembly("../../7DaysToDie_Data/Managed/Assembly-CSharp.dll");
Console.WriteLine("MVID " + assembly.MainModule.Mvid);
foreach (var type in Types(assembly.MainModule.Types).Where(t => args.Length > 0 && (t.Name == args[0] || t.FullName == args[0])))
{
    foreach (var field in type.Fields) Console.WriteLine("FIELD " + field.FullName + (field.HasConstant ? " = " + field.Constant : ""));
    foreach (var method in type.Methods)
    {
        Console.WriteLine("METHOD " + method.FullName);
        if (args.Skip(1).Contains(method.Name) && method.HasBody)
            foreach (var instruction in method.Body.Instructions) Console.WriteLine(instruction);
    }
}

static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> roots)
{
    foreach (var type in roots)
    {
        yield return type;
        foreach (var nested in Types(type.NestedTypes)) yield return nested;
    }
}
