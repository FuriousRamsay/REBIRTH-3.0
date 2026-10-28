// Deferred offline fixture: compile with the two production upload patch classes
// extracted verbatim, Harmony attributes removed. Do not substitute their bodies.
using System;
using System.Reflection;
class World {}
class NetPackage {}
class NetPackagePlayerInventory : NetPackage {}
class NetPackagePlayerData : NetPackage {}
static class RebirthRepairOriginalCorrection
{
    internal static int Calls;
    internal static NetPackage Package;
    internal static World World;
    internal static void NativeUpload(NetPackage package, World world)
    { Calls++; Package=package; World=world; }
}
static class RebirthRepairUploadHookFixture
{
    static void Check(bool condition,string reason)
    { if(!condition)throw new Exception(reason); }
    public static void Main()
    {
        var world=new World();
        Verify(typeof(RebirthRepairOriginalNativeUpload),new NetPackagePlayerInventory(),world);
        Verify(typeof(RebirthRepairOriginalNativePlayerData),new NetPackagePlayerData(),world);
    }
    static void Verify(Type type,NetPackage package,World world)
    {
        var hook=type.GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic);
        Check(hook!=null,"Missing actual upload hook");
        int before=RebirthRepairOriginalCorrection.Calls;
        hook.Invoke(null,new object[]{package,world,false});
        Check(RebirthRepairOriginalCorrection.Calls==before,"Skipped native original refreshed repair upload proof");
        hook.Invoke(null,new object[]{package,world,true});
        Check(RebirthRepairOriginalCorrection.Calls==before+1&&
            ReferenceEquals(RebirthRepairOriginalCorrection.Package,package)&&
            ReferenceEquals(RebirthRepairOriginalCorrection.World,world),
            "Executed native original did not forward its exact package/world");
    }
}
