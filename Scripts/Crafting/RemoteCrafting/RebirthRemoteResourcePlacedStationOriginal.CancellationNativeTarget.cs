using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;

internal static partial class RebirthRemoteResourcePlacedStationOriginal
{
    private static bool cancellationNativeLifecycleQualified;
    private static void RequireCancellationNativeMethod(Type owner,string name,Type result,params Type[] parameters)
    {
        var method=AccessTools.DeclaredMethod(owner,name,parameters);
        if(method==null||method.ReturnType!=result)throw new MissingMethodException(owner.FullName,name);
    }
    // Source qualification for this exact native serializer/load/save/join
    // ordering. A different installation requires fresh source review; it may
    // not inherit financial reconstruction permission from compatible names.
    private static void QualifyCancellationNativeLifecycle()
    {
        if(cancellationNativeLifecycleQualified)return;
        var assembly=typeof(RegionFileManager).Assembly;
        Guid module=assembly.ManifestModule.ModuleVersionId;
        // Each identity is a reviewed image pair, never an independent MVID/hash allowlist.
        // 3.4b6 guarded methods match the qualified 3.3 source; see Compatibility34 evidence.
        // 3.4b9 lifecycle was reviewed separately; see Compatibility34b9/NativeReview.
        string expectedImage=module==new Guid("1a9a4203-3d95-4c90-b094-8926dec1ee9c")
            ? "FCEEC27300FFD3A1F97B097E43B60F3B07597F441E7ECBC6E1B59EEBB234C705"
            : module==new Guid("47c87570-1f44-4ad3-872b-95a9f98d316c")
                ? "874515D3B7DBE6E920D773887ECCE9DDF4C51D225C01E0A3E2EC394C02ED95F4"
                : module==new Guid("b181c006-3dca-4211-80fe-9efc360eeb10")
                    ? "086755F825EEE6ABD4A13789F2F428D9043D36C8C54EBB3B38A9518B4A5146FC" : null;
        if(expectedImage==null)
            throw new NotSupportedException("Unqualified placed cancellation native lifecycle module.");
        using(var input=File.OpenRead(assembly.Location))using(var sha=SHA256.Create())
            if(BitConverter.ToString(sha.ComputeHash(input)).Replace("-","")!=expectedImage)
                throw new NotSupportedException("Unqualified placed cancellation native lifecycle image.");
        if(AccessTools.Constructor(typeof(RegionFileManager),new[]{typeof(string),typeof(string),typeof(int),typeof(bool)})==null)
            throw new MissingMethodException(typeof(RegionFileManager).FullName,".ctor");
        RequireCancellationNativeMethod(typeof(RegionFileManager),nameof(RegionFileManager.DoSaveChunks),typeof(bool));
        RequireCancellationNativeMethod(typeof(RegionFileManager),nameof(RegionFileManager.SaveChunkSnapshot),typeof(void),typeof(Chunk),typeof(bool));
        RequireCancellationNativeMethod(typeof(ChunkSnapshotUtil),nameof(ChunkSnapshotUtil.LoadChunk),typeof(Chunk),typeof(string),typeof(long));
        RequireCancellationNativeMethod(typeof(RegionFileChunkReader),nameof(RegionFileChunkReader.readIntoLoadStream),typeof(PooledBinaryReader),
            typeof(string),typeof(int),typeof(int),typeof(string),typeof(uint).MakeByRefType());
        RequireCancellationNativeMethod(typeof(Chunk),nameof(Chunk.load),typeof(void),typeof(PooledBinaryReader),typeof(uint));
        RequireCancellationNativeMethod(typeof(Chunk),nameof(Chunk.Reset),typeof(void));
        RequireCancellationNativeMethod(typeof(TileEntity),nameof(TileEntity.InstantiateFromRead),typeof(TileEntity),typeof(PooledBinaryReader),typeof(global::StreamModeRead),
            typeof(TileEntityType),typeof(Chunk),typeof(int[]),typeof(Func<int,int,int,BlockValue>));
        RequireCancellationNativeMethod(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.read),typeof(void),typeof(PooledBinaryReader),typeof(global::StreamModeRead));
        RequireCancellationNativeMethod(typeof(NetPackagePlayerId),nameof(NetPackagePlayerId.ProcessPackage),typeof(void),typeof(World),typeof(GameManager));
        RequireCancellationNativeMethod(typeof(GameManager),nameof(GameManager.PlayerId),typeof(void),typeof(int),typeof(int),typeof(PlayerDataFile),typeof(int));
        RequireCancellationNativeMethod(typeof(PlayerDataFile),nameof(PlayerDataFile.ToPlayer),typeof(void),typeof(EntityPlayer),typeof(global::StreamModeRead));
        RequireCancellationNativeMethod(typeof(GameManager),nameof(GameManager.SaveLocalPlayerData),typeof(void));
        RequireCancellationNativeMethod(typeof(PlayerDataFile),nameof(PlayerDataFile.FromPlayer),typeof(void),typeof(EntityPlayer),typeof(global::StreamModeWrite));
        RequireCancellationNativeMethod(typeof(PlayerDataFile),nameof(PlayerDataFile.Save),typeof(void),typeof(string),typeof(string));
        RequireCancellationNativeMethod(typeof(BlockWorkstation),nameof(BlockWorkstation.OnBlockActivated),typeof(bool),typeof(string),typeof(WorldBase),typeof(Vector3i),typeof(BlockValue),typeof(EntityPlayerLocal));
        RequireCancellationNativeMethod(typeof(BlockWorkstation),nameof(BlockWorkstation.OnBlockRemoved),typeof(void),typeof(WorldBase),typeof(Chunk),typeof(Vector3i),typeof(BlockValue));
        RequireCancellationNativeMethod(typeof(Block),nameof(Block.TakeItemWithTimerDone),typeof(void),typeof(TimerEventData));
        RequireCancellationNativeMethod(typeof(GameManager),nameof(GameManager.PickupBlockServer),typeof(void),typeof(Vector3i),typeof(BlockValue),typeof(int),typeof(PlatformUserIdentifierAbs));
        cancellationNativeLifecycleQualified=true;
    }
}
