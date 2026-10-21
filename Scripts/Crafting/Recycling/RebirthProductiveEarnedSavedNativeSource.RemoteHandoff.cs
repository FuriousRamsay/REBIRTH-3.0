internal static partial class RebirthProductiveEarnedSavedNativeSource
{
    internal static bool TryRemoteHandoffToolDependencies(string original,byte[] image,byte[] after,int offset,out NativeChildDependency[] dependencies)
    {
        dependencies=null;
        if(!RebirthProductiveEarnedRemoteToolRebaseRecord.TryRead(original,image,after,offset,out var source,out var record))return false;
        using(source)
            dependencies=RemoteToolDependencies(original,source,record);
        return NativeChildDependenciesCurrent(dependencies);
    }
    private static NativeChildDependency[] RemoteToolDependencies(string original,RebirthProductiveEarnedRemoteToolRebaseRecord.Description source,byte[] record)
    {
        var values=new System.Collections.Generic.List<NativeChildDependency>{NativeChildDependencyFor(original+".remote-tools-rebased",record),
            NativeChildDependencyFor(source.Header+".nativeTools.ownerBefore",source.BeforeOwner),NativeChildDependencyFor(source.Header+".nativeTools.ownerAfter",source.AfterOwner)};
        if(source.ViewerSource!=null)
        {values.Add(NativeChildDependencyFor(source.Header+".nativeViewerSource",source.ViewerSource));values.Add(NativeChildDependencyFor(original+".native-terminal",source.ViewerTerminal));values.Add(NativeChildDependencyFor(source.Header,source.HeaderImage));}
        return values.ToArray();
    }
    internal static bool RemoteHandoffDependenciesCurrent(NativeChildDependency[] dependencies)=>NativeChildDependenciesCurrent(dependencies);
}
