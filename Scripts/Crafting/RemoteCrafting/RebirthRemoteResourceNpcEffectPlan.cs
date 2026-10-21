using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

// Detached view of an already retained fetch original. Native NPC services obtain
// this through the durable journal reader; a caller-created snapshot grants no
// mutation permission and is never accepted as an argument to an effect service.
internal sealed class RebirthRemoteResourceNpcEffectPlan
{
    internal Guid Operation { get; }
    internal Guid MutationId { get; }
    internal RebirthNpcStableId NpcId { get; }
    internal string World { get; }
    internal string Owner { get; }
    internal string Creation { get; }
    internal string SourceId { get; }
    internal string Binding { get; }
    internal uint Revision { get; }
    internal int Deaths { get; }
    internal int Attempted { get; }
    internal bool DogStorage { get; }
    internal IReadOnlyDictionary<string,int> BeforeQuantities { get; }
    internal IReadOnlyDictionary<string,int> AfterQuantities { get; }
    internal IReadOnlyDictionary<string,int> Reservations { get; }
    internal string BeforeNative { get; }
    internal string AfterNative { get; }
    private readonly ItemStack[] beforeSlots,afterSlots;
    private readonly bool[] locks;
    private readonly Func<bool> current;

    internal RebirthRemoteResourceNpcEffectPlan(Guid operation,Guid mutationId,RebirthNpcStableId npcId,
        string world,string owner,string creation,string sourceId,string binding,uint revision,int deaths,int attempted,bool dogStorage,
        IDictionary<string,int> before,IDictionary<string,int> after,IDictionary<string,int> reservations,
        string beforeNative,string afterNative,ItemStack[] beforeSlots,ItemStack[] afterSlots,bool[] locks,Func<bool> current)
    {
        Operation=operation;MutationId=mutationId;NpcId=npcId;World=world;Owner=owner;Creation=creation;SourceId=sourceId;Binding=binding;
        Revision=revision;Deaths=deaths;Attempted=attempted;DogStorage=dogStorage;
        BeforeQuantities=Map(before);AfterQuantities=Map(after);Reservations=Map(reservations);
        BeforeNative=beforeNative??"";AfterNative=afterNative??"";
        this.beforeSlots=beforeSlots==null?null:ItemStack.Clone(beforeSlots);
        this.afterSlots=afterSlots==null?null:ItemStack.Clone(afterSlots);this.locks=locks==null?null:(bool[])locks.Clone();this.current=current;
    }
    private static IReadOnlyDictionary<string,int> Map(IDictionary<string,int> values)
        =>new ReadOnlyDictionary<string,int>(values==null?new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase):new Dictionary<string,int>(values,StringComparer.OrdinalIgnoreCase));
    internal ItemStack[] CopyBeforeSlots()=>beforeSlots==null?null:ItemStack.Clone(beforeSlots);
    internal ItemStack[] CopyAfterSlots()=>afterSlots==null?null:ItemStack.Clone(afterSlots);
    internal bool[] CopyLocks()=>locks==null?null:(bool[])locks.Clone();
    internal bool IsCurrent()=>current?.Invoke()==true;
}
