using System;
using System.IO;

internal sealed class RebirthPoiWorldBinding
{
    public readonly Guid WorldId;
    public readonly string NativeWorldId, SaveDirectory, Directory;
    public readonly object Scope;
    private readonly Func<bool> current;
    internal RebirthPoiWorldBinding(object scope,string nativeWorldId,string saveDirectory,Func<bool> stillCurrent)
    {
        Guid id;
        if(scope==null || stillCurrent==null || nativeWorldId==null || nativeWorldId.Length!=32 ||
            !Guid.TryParseExact(nativeWorldId,"N",out id) || id==Guid.Empty ||
            nativeWorldId!=id.ToString("N").ToUpperInvariant()) throw new ArgumentException("Invalid native saved-world identity.");
        if(string.IsNullOrWhiteSpace(saveDirectory)) throw new ArgumentException("Missing native save directory.");
        Scope=scope; NativeWorldId=nativeWorldId; WorldId=id; SaveDirectory=Path.GetFullPath(saveDirectory);
        Directory=Path.Combine(SaveDirectory,"RebirthData","Purge","Clearance"); current=stillCurrent;
    }
    public bool IsCurrent { get { try { return current(); } catch { return false; } } }
    public static bool TryCapture(World world,out RebirthPoiWorldBinding binding)
    {
        binding=null;
        try
        {
            var game=GameManager.Instance; var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
            var state=world==null?null:world.worldState;
            if(!ThreadManager.IsMainThread() || game==null || manager==null || !manager.IsServer || world==null ||
                world.IsRemote() || !ReferenceEquals(game.World,world) || state==null) return false;
            string native=state.Guid,save=Path.GetFullPath(GameIO.GetSaveGameDir());
            binding=new RebirthPoiWorldBinding(world,native,save,()=>ThreadManager.IsMainThread() &&
                ReferenceEquals(GameManager.Instance,game) && ReferenceEquals(game.World,world) &&
                ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance,manager) && manager.IsServer && !world.IsRemote() &&
                ReferenceEquals(world.worldState,state) && state.Guid==native && Path.GetFullPath(GameIO.GetSaveGameDir())==save);
            return binding.IsCurrent;
        }
        catch { binding=null; return false; }
    }
}