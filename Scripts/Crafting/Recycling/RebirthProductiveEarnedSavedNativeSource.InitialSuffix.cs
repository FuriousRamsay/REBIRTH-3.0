using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedSavedNativeSource
{
    private static bool TryInitialEarnedBody(string original,byte[] image,RebirthProductiveEarnedRowProof.Frame frame,
        out byte[] body,out int offset,out NativeChildDependency suffix)
    {
        body=frame.AfterNative;offset=frame.AfterElapsedOffset;suffix=null;
        string path=original+".initial-native-suffix";
        if(!File.Exists(path)&&!File.Exists(path+".tmp"))return true;
        if(!RebirthProductiveEarnedCoordinator.TryReadInitialNativeSuffix(original,image,out body,out offset))return false;
        byte[] record=Read(path,RebirthProductiveEarnedCoordinator.MaximumInitialNativeSuffix);
        suffix=NativeChildDependencyFor(path,record);
        return RebirthProductiveEarnedCoordinator.TryReadInitialNativeSuffix(original,image,out var repeated,out int repeatedOffset)&&
            repeatedOffset==offset&&repeated.SequenceEqual(body)&&NativeChildDependenciesCurrent(new[]{suffix});
    }
}
