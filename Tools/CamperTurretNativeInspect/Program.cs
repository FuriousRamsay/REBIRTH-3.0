using System;
using System.Linq;
using Mono.Cecil;
class P
{
    static void Main(string[] args)
    {
        if(args.Contains("armor"))
        {
            using var own=AssemblyDefinition.ReadAssembly("RebirthUtils.dll");
            foreach(var type in own.MainModule.Types)foreach(var method in type.Methods.Where(m=>m.HasBody))foreach(var instruction in method.Body.Instructions)
                if(instruction.Operand is string value&&value.IndexOf("armor",StringComparison.OrdinalIgnoreCase)>=0)Console.WriteLine(type.FullName+"::"+method.Name+" "+value);
            return;
        }
        if(args.Contains("unitymesh"))
        {
            using var editor=AssemblyDefinition.ReadAssembly("C:/Program Files/Unity/Hub/Editor/2022.3.62f2/Editor/Data/Managed/UnityEngine/UnityEditor.CoreModule.dll");
            foreach(var type in editor.MainModule.Types.Where(t=>t.Name=="MeshUtility"))foreach(var method in type.Methods.Where(m=>m.Name.Contains("MeshData")||m.Name.Contains("Optimize")))Console.WriteLine(method.FullName+" public="+method.IsPublic);
            return;
        }
        foreach(var file in new[]{"Assembly-CSharp.dll","Assembly-CSharp-firstpass.dll"})
        {
            using var a=AssemblyDefinition.ReadAssembly("../../7DaysToDie_Data/Managed/"+file);
            foreach(var t in a.MainModule.Types)
            {
                if(args.Contains("vehiclemods"))
                {
                    if((t.Name.IndexOf("Mod",StringComparison.OrdinalIgnoreCase)>=0||t.Name.IndexOf("Part",StringComparison.OrdinalIgnoreCase)>=0||t.Name.IndexOf("Attach",StringComparison.OrdinalIgnoreCase)>=0)&&t.Name.StartsWith("XUiC_"))Console.WriteLine("MODTYPE "+t.FullName);
                    if(t.Name=="Vehicle"||t.Name=="EntityVehicle")foreach(var m in t.Methods.Where(m=>m.Name.IndexOf("Mod",StringComparison.OrdinalIgnoreCase)>=0||m.Name.IndexOf("ItemValue",StringComparison.OrdinalIgnoreCase)>=0))Console.WriteLine(m.FullName);
                    continue;
                }
                if(args.Contains("purgestart"))
                {
                    foreach(var m in t.Methods.Where(m=>m.HasBody&&m.Body.Instructions.Any(i=>i.Operand is string s&&s.IndexOf("splat3",StringComparison.OrdinalIgnoreCase)>=0)))Console.WriteLine("SPLATOWNER "+m.FullName);
                    if(t.Name.IndexOf("Road",StringComparison.OrdinalIgnoreCase)>=0||t.Name.IndexOf("Splat",StringComparison.OrdinalIgnoreCase)>=0)
                    {Console.WriteLine("ROADTYPE "+t.FullName);foreach(var m in t.Methods)Console.WriteLine(m.FullName);}
                    if(t.Name.IndexOf("Spawn",StringComparison.OrdinalIgnoreCase)>=0||t.Name.IndexOf("Town",StringComparison.OrdinalIgnoreCase)>=0||t.Name=="World"||t.Name=="EntityPlayerLocal"||t.Name=="EntityVehicle"||t.Name=="BiomeProviderFromImage")
                    {
                        Console.WriteLine("TYPE "+t.FullName);
                        foreach(var m in t.Methods.Where(m=>m.Name.IndexOf("Spawn",StringComparison.OrdinalIgnoreCase)>=0||m.Name.IndexOf("Road",StringComparison.OrdinalIgnoreCase)>=0||m.Name.IndexOf("Town",StringComparison.OrdinalIgnoreCase)>=0||m.Name.IndexOf("Wake",StringComparison.OrdinalIgnoreCase)>=0||m.Name.IndexOf("Attach",StringComparison.OrdinalIgnoreCase)>=0))Console.WriteLine(m.FullName);
                        foreach(var f in t.Fields.Where(f=>f.Name.IndexOf("Road",StringComparison.OrdinalIgnoreCase)>=0||f.Name.IndexOf("Town",StringComparison.OrdinalIgnoreCase)>=0))Console.WriteLine(f.FullName);
                    }
                    continue;
                }
                if(args.Contains("nativelaser"))
                {
                    if(t.Name.IndexOf("Laser",StringComparison.OrdinalIgnoreCase)>=0||t.Name=="EntityTurret")foreach(var m in t.Methods.Where(m=>m.HasBody))
                    {Console.WriteLine(m.FullName);foreach(var i in m.Body.Instructions)Console.WriteLine(i);}
                    continue;
                }
                if(args.Contains("aimsettings"))
                {
                    if(t.Name=="PlayerMoveController")foreach(var m in t.Methods.Where(m=>m.HasBody&&(m.Name=="GetCameraInputSensitivity"||m.Body.Instructions.Any(i=>i.Operand is FieldReference r&&r.Name=="mouseLookSensitivity"))))
                    {Console.WriteLine(m.FullName);foreach(var i in m.Body.Instructions)Console.WriteLine(i);}
                    continue;
                }
                if(args.Contains("relayapi"))
                {
                    if(new[]{"TileEntity","TileEntityPowered","ItemActionConnectPower","PowerItem","NetPackageManager"}.Contains(t.Name))
                    {
                        Console.WriteLine("TYPE "+t.FullName);
                        foreach(var f in t.Fields)Console.WriteLine("FIELD "+f.FullName+" public="+f.IsPublic);
                        foreach(var p in t.Properties)Console.WriteLine("PROPERTY "+p.FullName);
                        foreach(var nested in t.NestedTypes)if(nested.Name=="ConnectPowerData")foreach(var f in nested.Fields)Console.WriteLine("NESTED "+f.FullName+" public="+f.IsPublic);
                        if(t.Name=="NetPackageManager")foreach(var m in t.Methods)Console.WriteLine("METHOD "+m.FullName);
                    }
                    continue;
                }
                if(args.Contains("zoom"))
                {
                    if(t.Name=="ItemActionZoom")
                    {
                        foreach(var field in t.Fields)Console.WriteLine("FIELD "+field.FullName+" public="+field.IsPublic);
                        foreach(var method in t.Methods.Where(m=>m.HasBody&&(m.Name=="ReadFrom"||m.Name=="OnHoldingUpdate")))
                        {Console.WriteLine(method.FullName);foreach(var instruction in method.Body.Instructions)Console.WriteLine(instruction);}
                    }
                    continue;
                }
                if(args.Contains("sinkinventory"))
                {
                    if(t.Name=="PlayerActionsLocal"){foreach(var field in t.Fields)Console.WriteLine("INPUT "+field.FullName);foreach(var property in t.Properties)Console.WriteLine("PROPERTY "+property.FullName);}
                    if(t.Name=="Inventory"||t.Name=="ItemStackGrid")
                        foreach(var m in t.Methods.Where(m=>m.HasBody&&(m.Name=="SetItem"||m.Name=="SetSlot"||m.Name=="Execute")))
                        {Console.WriteLine(m.FullName);foreach(var instruction in m.Body.Instructions)Console.WriteLine(instruction);}
                    continue;
                }
                if(args.Contains("camperwire"))
                {
                    if(t.Name=="ItemActionConnectPower"||t.Name=="TileEntityPoweredBlock"||t.Name=="PowerItem")
                    {
                        Console.WriteLine("TYPE "+t.FullName);
                        foreach(var f in t.Fields)Console.WriteLine("FIELD "+f.FullName+" public="+f.IsPublic);
                        foreach(var m in t.Methods)
                        {
                            Console.WriteLine("METHOD "+m.FullName);
                            if(t.Name=="ItemActionConnectPower"&&m.HasBody)
                                foreach(var i in m.Body.Instructions)Console.WriteLine(i);
                        }
                    }
                    continue;
                }
                if(args.Contains("turretsounds"))
                {
                    if(t.Name=="EntityTurret"||t.Name=="AutoTurretFireController")
                        foreach(var m in t.Methods.Where(m=>m.HasBody&&m.Body.Instructions.Any(i=>
                            i.Operand is string s&&(s.IndexOf("sound",StringComparison.OrdinalIgnoreCase)>=0||s.Contains("turret_")))))
                        {Console.WriteLine(m.FullName);foreach(var i in m.Body.Instructions)Console.WriteLine(i);}
                    continue;
                }
                if(args.Contains("cursor"))
                {
                    if(t.Name.Contains("Cursor"))foreach(var m in t.Methods.Where(m=>m.HasBody&&new[]{"GetScreenPosition","HandleMovement","GetMouseButton"}.Contains(m.Name)))
                    {Console.WriteLine(m.FullName);foreach(var instruction in m.Body.Instructions)Console.WriteLine(instruction);}
                    continue;
                }
                if(args.Contains("windowflow"))
                {
                    if(t.Name=="XUiWindowGroup"||t.Name=="XUiView")Console.WriteLine("METHODS "+t.Name+" "+string.Join(",",t.Methods.Select(m=>m.Name)));
                    if(t.Name.Contains("WindowManager")||new[]{"XUiController","XUiWindowGroup","GUIWindow","XUiView","XUiV_Window"}.Contains(t.Name))
                        foreach(var m in t.Methods.Where(m=>m.HasBody&&(new[]{"OnOpen","get_IsOpen","Open","Show","SetVisible"}.Contains(m.Name)||m.Name.Contains("Open")||m.Name.Contains("Visible")||m.Name=="openInternal"||m.Name=="Update")))
                        {Console.WriteLine(m.FullName);foreach(var instruction in m.Body.Instructions)Console.WriteLine(instruction);}
                    continue;
                }
                if(args.Contains("radial"))
                {
                    if(t.Name=="XUiC_Radial")foreach(var m in t.Methods.Where(m=>m.HasBody&&(m.Name=="Update"||m.Name=="OnOpen"||m.Name=="Init"||m.Name.Contains("InputStyle")||m.Name=="CalculateSelectionFromController"||m.Name=="radialButtonPressed"||m.Name=="Open"||m.Name=="Close")))
                    {Console.WriteLine(m.FullName);foreach(var instruction in m.Body.Instructions)Console.WriteLine(instruction);}
                    continue;
                }
                if(args.Contains("itemslot"))
                {
                    if(t.Name=="XUiC_ItemStack"){Console.WriteLine("METHODS "+string.Join(",",t.Methods.Select(m=>m.Name)));foreach(var f in t.Fields.Where(f=>f.Name.Contains("Snap")))Console.WriteLine("FIELD "+f.FullName+" public="+f.IsPublic);}
                    if(t.Name=="XUiC_ItemStack")foreach(var m in t.Methods.Where(m=>m.HasBody&&(m.IsConstructor||m.Name.Contains("Drag")||m.Name.Contains("Press")||m.Name.Contains("Mouse")||m.Name=="SwapItem"||m.Name=="HandleStackSwap"||m.Name=="Update"||m.Name=="Init"||m.Name.Contains("ParseAttribute"))))
                    {Console.WriteLine(m.FullName);foreach(var instruction in m.Body.Instructions)Console.WriteLine(instruction);}
                    continue;
                }
                if(args.Contains("visibility"))
                {
                    if(t.Name.StartsWith("Vehicle"))foreach(var method in t.Methods.Where(m=>m.HasBody&&m.Body.Instructions.Any(i=>i.Operand is MethodReference target&&(target.Name=="set_enabled"||target.Name=="SetActive"))))
                    {Console.WriteLine(method.FullName);foreach(var instruction in method.Body.Instructions)Console.WriteLine(instruction);}
                    continue;
                }
                if(args.Contains("gui"))
                {
                    if(t.Name.IndexOf("GUI",StringComparison.OrdinalIgnoreCase)>=0&&t.Fields.Any(f=>f.FieldType.Name=="PlayerAction"))
                    {Console.WriteLine(t.FullName);foreach(var field in t.Fields)Console.WriteLine(" FIELD "+field.Name);foreach(var method in t.Methods.Where(m=>m.HasBody&&(m.IsConstructor||t.Name=="PlayerActionsGUI")))foreach(var instruction in method.Body.Instructions)Console.WriteLine(instruction);}
                    continue;
                }
                if(args.Contains("presentation"))
                {
                    if(t.FullName=="Audio.Manager")foreach(var m in t.Methods.Where(m=>new[]{"Play","Stop","GetAudioClip"}.Contains(m.Name)))Console.WriteLine(m.FullName+" parameters="+string.Join(",",m.Parameters.Select(p=>p.Name)));
                    if(t.Name=="AutoTurretFireController")foreach(var f in t.Fields.Where(f=>f.Name=="fireSound"))Console.WriteLine("FIELD "+f.FullName+" public="+f.IsPublic);
                    if(t.Name=="AutoTurretFireController"||t.Name=="EntityTurret")foreach(var m in t.Methods.Where(m=>m.HasBody&&(m.Name=="GetRange"||m.Name=="Fire"||m.Name.Contains("Sound"))))
                    {Console.WriteLine(m.FullName);foreach(var instruction in m.Body.Instructions)Console.WriteLine("  "+instruction);}
                    continue;
                }
                if(args.Contains("protocol"))
                {
                    if(t.Name!="NetPackageManager"&&t.Name!="NetPackage")continue;
                    Console.WriteLine(file+" "+t.FullName);
                    foreach(var m in t.Methods.Where(m=>m.HasBody&&(t.Name=="NetPackageManager"||m.Name=="ValidEntityIdForSender")))
                    {
                        Console.WriteLine(m.FullName);
                        foreach(var instruction in m.Body.Instructions)Console.WriteLine("  "+instruction);
                    }
                }
                else if(t.Name.Contains("NetPackage")&&(t.Name.Contains("Inventory")||t.Name.Contains("PlayerData")))Console.WriteLine(file+" "+t.FullName);
            }
        }
    }
}








