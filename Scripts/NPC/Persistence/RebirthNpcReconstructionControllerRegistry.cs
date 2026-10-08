using System;
using System.Collections.Generic;

// Required fragment handlers are explicit; unknown required versions are never defaulted.
internal interface IRebirthNpcReconstructionController
{
    string TypeId { get; }
    int Version { get; }
    bool TryPreflight(byte[] bytes);
    bool TryRestore(EntityRebirthHumanoidNPC npc,byte[] bytes);
    bool TryVerify(EntityRebirthHumanoidNPC npc,byte[] bytes);
}
internal static class RebirthNpcReconstructionControllerRegistry
{
    private static readonly Dictionary<string,IRebirthNpcReconstructionController> Handlers=new Dictionary<string,IRebirthNpcReconstructionController>(StringComparer.Ordinal);
    internal static void Register(IRebirthNpcReconstructionController handler)
    {
        if(handler==null || string.IsNullOrWhiteSpace(handler.TypeId) || handler.Version<=0 || Handlers.ContainsKey(handler.TypeId))
            throw new ArgumentException("Invalid or duplicate NPC reconstruction controller.");
        Handlers.Add(handler.TypeId,handler);
    }
    internal static bool Supports(string type,int version)=>Handlers.TryGetValue(type,out var handler)&&handler.Version==version;
    internal static bool TryPreflight(RebirthNpcControllerFragmentSet fragments)
    {
        foreach(var fragment in fragments?.Fragments??new RebirthNpcControllerFragment[0])
        {
            if(fragment==null)return false;
            if(!Handlers.TryGetValue(fragment.ControllerTypeId??"",out var handler)||handler.Version!=fragment.FragmentVersion)
            {if(fragment.Required)return false;continue;}
            if(fragment.Bytes==null||!handler.TryPreflight((byte[])fragment.Bytes.Clone()))return false;
        }
        return true;
    }
    internal static bool TryRestore(EntityRebirthHumanoidNPC npc,RebirthNpcControllerFragmentSet fragments)
    {
        foreach(var fragment in fragments?.Fragments??new RebirthNpcControllerFragment[0])
        {
            if(!Handlers.TryGetValue(fragment.ControllerTypeId??"",out var handler)||handler.Version!=fragment.FragmentVersion)
            {if(fragment.Required)return false;continue;}
            if(!handler.TryRestore(npc,(byte[])fragment.Bytes.Clone())||!handler.TryVerify(npc,(byte[])fragment.Bytes.Clone()))return false;
        }
        return true;
    }
}
