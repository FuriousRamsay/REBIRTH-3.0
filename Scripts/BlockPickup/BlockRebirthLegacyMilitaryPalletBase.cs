using System;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine.Scripting;

// Save compatibility only: existing composite pallets retain their native storage;
// newly added base/cap blocks retain the ordinary non-container lifecycle.
[Preserve]
public sealed class BlockRebirthLegacyMilitaryPalletBase : BlockCompositeTileEntity
{
    private delegate void NativeBlockAdded(Block block, WorldBase world, Chunk chunk,
        Vector3i position, BlockValue value, PlatformUserIdentifierAbs addedBy);
    private delegate void NativeTransformActivated(Block block, WorldBase world, Vector3i position,
        BlockValue value, BlockEntityData entityData);
    private static readonly NativeBlockAdded AddOrdinaryBlock = (NativeBlockAdded)CreateBaseCall(
        nameof(OnBlockAdded), typeof(NativeBlockAdded), new[] { typeof(WorldBase), typeof(Chunk),
        typeof(Vector3i), typeof(BlockValue), typeof(PlatformUserIdentifierAbs) });
    private static readonly NativeTransformActivated ActivateOrdinaryModel = (NativeTransformActivated)CreateBaseCall(
        nameof(OnBlockEntityTransformAfterActivated), typeof(NativeTransformActivated), new[] {
        typeof(WorldBase), typeof(Vector3i), typeof(BlockValue), typeof(BlockEntityData) });

    private static Delegate CreateBaseCall(string name, Type delegateType, Type[] nativeArguments)
    {
        var original = typeof(Block).GetMethod(name, BindingFlags.Public | BindingFlags.Instance |
            BindingFlags.DeclaredOnly, null, nativeArguments, null);
        if (original == null) throw new MissingMethodException(typeof(Block).FullName, name);
        var arguments = new Type[nativeArguments.Length + 1];
        arguments[0] = typeof(Block);
        Array.Copy(nativeArguments, 0, arguments, 1, nativeArguments.Length);
        var call = new DynamicMethod("RebirthLegacyPallet_" + name, typeof(void), arguments,
            typeof(BlockRebirthLegacyMilitaryPalletBase), false);
        var il = call.GetILGenerator();
        for (short i = 0; i < arguments.Length; i++) il.Emit(OpCodes.Ldarg, i);
        il.Emit(OpCodes.Call, original); // Nonvirtual grandparent call, not recursive override dispatch.
        il.Emit(OpCodes.Ret);
        return call.CreateDelegate(delegateType);
    }

    public override void Init()
    {
        // Runtime instance policy: do not make this an inherited XML override on
        // the military tile variants, which use their own native composite class.
        Properties.Values[RebirthBlockPickupClassifier.PropertyPickup] = "Deny";
        base.Init();
    }

    public override void OnBlockAdded(WorldBase world, Chunk chunk, Vector3i position,
        BlockValue value, PlatformUserIdentifierAbs addedBy)
    {
        if (value.ischild || !(world.GetTileEntity(position) is TileEntityComposite))
        {
            // Nonvirtual dispatch to Block preserves shape/multiblock/temporary
            // callbacks without creating a new storage entity or generating loot.
            AddOrdinaryBlock(this, world, chunk, position, value, addedBy);
            return;
        }
        base.OnBlockAdded(world, chunk, position, value, addedBy);
    }

    public override void OnBlockEntityTransformAfterActivated(WorldBase world, Vector3i position,
        BlockValue value, BlockEntityData entityData)
    {
        if (entityData == null) return; // Preserve the native composite callback admission.
        if (!(world.GetTileEntity(position) is TileEntityComposite))
        {
            // Native composite model activation is the other TE creation path.
            ActivateOrdinaryModel(this, world, position, value, entityData);
            return;
        }
        base.OnBlockEntityTransformAfterActivated(world, position, value, entityData);
    }
}
