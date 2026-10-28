using System;
using System.IO;
using System.Reflection;

// PREPARED, NOT RUN. Bounded sink checks do not invoke native death/network or
// mutate inventories. Real detached builder cases require captured native data.
internal static class mpA_logistics_native_body_bound_fixture
{
    internal static void Verify(Assembly production)
    {
        var owner=production.GetType("RebirthLogisticsPhysicalLossWitness",true);
        var counter=owner.GetNestedType("NativeBodyCounter",BindingFlags.NonPublic);
        Stream New(long maximum)=>(Stream)Activator.CreateInstance(counter,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{maximum},null);
        using(var stream=New(ushort.MaxValue))
        {
            stream.Write(new byte[ushort.MaxValue],0,ushort.MaxValue);
            if(stream.Length!=ushort.MaxValue)throw new Exception("Exact native maximum rejected.");
            try{stream.WriteByte(1);throw new Exception("Native ushort body overflow was accepted.");}
            catch(IOException){if(stream.Length!=ushort.MaxValue)throw new Exception("Rejected byte changed counted image.");}
        }
        using(var stream=New(ushort.MaxValue))
        {
            try{stream.Write(new byte[65536],0,65536);throw new Exception("Oversized native body accepted.");}
            catch(IOException){if(stream.Length!=0)throw new Exception("Oversized write partly consumed.");}
        }
    }
    internal static void VerifyActualDetachedBuilder(Assembly production,EntityBackpack detached,bool expected)
    {
        // The supplied builder must already contain the native preferences,
        // since dropBackpack copies those AFTER all AddItem callbacks.
        var method=production.GetType("RebirthLogisticsPhysicalLossWitness",true).GetMethod("OriginalBodyFitsNative",BindingFlags.Static|BindingFlags.NonPublic);
        if((bool)method.Invoke(null,new object[]{detached})!=expected)throw new Exception("Actual detached native body byte classification changed.");
    }
}
