using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

internal static class Program
{
    static int checks;
    static string managed;
    static int Main(string[] args)
    {
        managed = args.Length > 0 ? args[0] : @"C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die\7DaysToDie_Data\Managed";
        AssemblyLoadContext.Default.Resolving += (context, name) => {
            string path = Path.Combine(managed, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
        try { Run(); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    static void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); checks++; }
    static ItemStack Stack(int count = 4) => new ItemStack(new ItemValue { type = 1, Quality = 3, Seed = 12 }, count);
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Run()
    {
        var source = new[] { Stack(), Stack(7), (ItemStack)null };
        var pending = RebirthInventoryProjectionSnapshot.Update(null, source);
        Check(!ReferenceEquals(source, pending) && !ReferenceEquals(source[0], pending[0]) && !ReferenceEquals(source[0].itemValue, pending[0].itemValue), "Snapshot aliases native source");
        var unchanged = pending[1]; var previous = pending[0];
        Check(ReferenceEquals(pending, RebirthInventoryProjectionSnapshot.Update(pending, source)) && ReferenceEquals(previous, pending[0]) && ReferenceEquals(unchanged, pending[1]), "Duplicate notification allocated clones");
        source[0].count = 3;
        Check(pending[0].count == 4, "Source mutation leaked before notification");
        RebirthInventoryProjectionSnapshot.Update(pending, source);
        Check(pending[0].count == 3 && !ReferenceEquals(previous, pending[0]) && ReferenceEquals(unchanged, pending[1]), "Changed slot refresh or unchanged reuse failed");
        foreach (Action<ItemValue> mutate in new Action<ItemValue>[] {
            v => v.UseTimes = 2.5f, v => v.Quality = 5, v => v.Seed = 30,
            v => v.SelectedAmmoTypeIndex = 2, v => v.Flags = 128,
            v => v.Stats = new[] { new ItemValue.Stat(PassiveEffects.CarryCapacity, 5, 0) },
            v => v.Stats[0].value = 7,
            v => v.TextureFullArray = new TextureFullArray(123),
            v => v.Metadata = new Dictionary<string, TypedMetadataValue> { { "test", new TypedMetadataValue("one", TypedMetadataValue.TypeTag.String) } },
            v => v.Metadata["test"] = new TypedMetadataValue("two", TypedMetadataValue.TypeTag.String),
            v => v.modifications = new[] { new ItemValue { type = 1, Flags = 3 } },
            v => v.modifications[0].Flags = 64,
            v => v.cosmeticMods = new[] { new ItemValue { type = 1, TextureFullArray = new TextureFullArray(4) } },
            v => v.cosmeticMods[0].TextureFullArray = new TextureFullArray(5),
            v => v.Metadata["nullable"] = null }) {
            previous = pending[0]; mutate(source[0].itemValue);
            RebirthInventoryProjectionSnapshot.Update(pending, source);
            Check(!ReferenceEquals(previous, pending[0]), "Changed item content was suppressed");
        }
        Check(pending[0].itemValue.Metadata["test"].Equals(source[0].itemValue.Metadata["test"]), "Metadata content differs");
        Check(!ReferenceEquals(pending[0].itemValue.Metadata, source[0].itemValue.Metadata) && !ReferenceEquals(pending[0].itemValue.modifications, source[0].itemValue.modifications), "Nested item values alias");
        Check(RebirthInventoryProjectionSnapshot.Update(pending, new ItemStack[1]).Length == 1, "Resize failed");
        Check(RebirthInventoryProjectionSnapshot.Update(pending, null).Length == 0, "Null source failed");
        Bench(); Console.WriteLine("PASS " + checks + " checks with installed native ItemStack/ItemValue.");
    }
    static void Bench()
    {
        var bag = new ItemStack[176]; for (int i = 0; i < bag.Length; i++) bag[i] = Stack(100);
        // Warm both paths before comparing duplicate notifications; each batch is independent.
        for (int i = 0; i < 20; i++) { var p=Clone(bag); RebirthInventoryProjectionSnapshot.Update(p,bag); }
        const int batches=1000; long bytes=GC.GetAllocatedBytesForCurrentThread(); var clock=Stopwatch.StartNew();
        for (int i=0;i<batches;i++) { Clone(bag); Clone(bag); Clone(bag); }
        clock.Stop(); long baseline=GC.GetAllocatedBytesForCurrentThread()-bytes; double before=clock.Elapsed.TotalMilliseconds;
        bytes=GC.GetAllocatedBytesForCurrentThread();clock.Restart();
        for(int i=0;i<batches;i++){var p=RebirthInventoryProjectionSnapshot.Update(null,bag);RebirthInventoryProjectionSnapshot.Update(p,bag);RebirthInventoryProjectionSnapshot.Update(p,bag);}
        clock.Stop(); long after=GC.GetAllocatedBytesForCurrentThread()-bytes;
        Console.WriteLine($"176-slot, three-identical-notification batches={batches}; baselineBytes={baseline}; revisedBytes={after}; baselineMs={before:F2}; revisedMs={clock.Elapsed.TotalMilliseconds:F2}");
        Check(after < baseline / 2, "Expected repeated snapshot allocation reduction missing");
    }
    static ItemStack[] Clone(ItemStack[] source) { var result=new ItemStack[source.Length];for(int i=0;i<source.Length;i++)result[i]=source[i]?.Clone()??ItemStack.Empty.Clone();return result; }
}
