using System;
using System.Collections.Generic;
using Mono.Cecil;

class Program
{
    static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> types)
    { foreach(var type in types) { yield return type; foreach(var child in Types(type.NestedTypes)) yield return child; } }
    static void Main(string[] args)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(args[0]);
        foreach(var type in assembly.MainModule.Types)
            if(type.Name == "EnumGamePrefs") foreach(var field in type.Fields)
                if(field.HasConstant && (field.Name.IndexOf("Volume", StringComparison.OrdinalIgnoreCase) >= 0 || field.Name.IndexOf("Mute", StringComparison.OrdinalIgnoreCase) >= 0))
                    Console.WriteLine("PREF " + field.Name + "=" + field.Constant);
        foreach(var type in Types(assembly.MainModule.Types))
        foreach(var method in type.Methods)
        {
            if(!method.HasBody) continue;
            bool match = false;
            foreach(var instruction in method.Body.Instructions)
                if(instruction.Operand is MethodReference called && called.DeclaringType.FullName == "UnityEngine.AudioListener" && called.Name == "set_volume") match = true;
            if(!match) continue;
            Console.WriteLine(method.FullName);
            foreach(var instruction in method.Body.Instructions) Console.WriteLine(instruction);
        }
    }
}
