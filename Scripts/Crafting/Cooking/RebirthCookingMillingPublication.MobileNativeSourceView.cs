using System;

internal static partial class RebirthCookingMillingPublication
{
    // Read-only view of the existing actual native vehicles.dat -> ECD ->
    // factory -> Persistency Read lineage. It creates no owner or setter grant.
    internal sealed class NativeMobileLoadSource
    {
        private readonly MobileLoadedSource source;
        private readonly string modules;
        private NativeMobileLoadSource(MobileLoadedSource value){source=value;modules=value.Modules;}
        internal EntityVehicle Vehicle=>Current()?source.Vehicle:null;
        internal string Identity=>source.Identity;
        internal string NativePath=>source.Record.Read.Path;
        internal string NativeFileDigest=>source.Record.Read.Digest;
        internal string NativeEntityDigest=>source.Record.Entity;
        internal string NativeBagDigest=>source.Record.Bag;
        internal bool Current()=>source.Current()&&mobileLoadedSources.TryGetValue(source.Vehicle,out var actual)&&ReferenceEquals(actual,source);
        internal bool OriginalCurrent()=>Current()&&source.Modules==modules&&MobileNativeModulesCurrent(source)&&MobileLoadedBagMatches(source.Vehicle,source.Record);
        internal bool TryCopyStationBody(Guid installation,out byte[] body)
        {
            body=null;if(!OriginalCurrent()||source.Bodies==null||!source.Bodies.TryGetValue(installation,out var original))return false;
            body=(byte[])original.Clone();if(OriginalCurrent())return true;body=null;return false;
        }
        internal static NativeMobileLoadSource Observe(EntityVehicle vehicle)
        {
            if(vehicle==null||!mobileLoadedSources.TryGetValue(vehicle,out var source))return null;
            var result=new NativeMobileLoadSource(source);return result.OriginalCurrent()?result:null;
        }
    }
    internal static bool TryObserveNativeLoadedVehicle(EntityVehicle vehicle,out NativeMobileLoadSource source)
    {source=NativeMobileLoadSource.Observe(vehicle);return source!=null;}
}
