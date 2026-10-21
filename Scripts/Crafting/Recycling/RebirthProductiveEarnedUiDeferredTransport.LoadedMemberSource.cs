using System;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    internal sealed partial class HostContribution
    {
        internal AuthorityLocalLoadedSource AuthorityLoadedOriginal;
        internal LoadedMemberSource LoadedMemberSource=>LoadedOwnerSource!=null?new LoadedMemberSource(LoadedOwnerSource):
            AuthorityLoadedOriginal!=null?new LoadedMemberSource(AuthorityLoadedOriginal):null;
        internal bool LoadedIdentityCurrent()=>LoadedOwnerSource!=null?LoadedOwnerSource.Current()&&LoadedOwnerSource.Native?.IdentityCurrent()==true:
            AuthorityLoadedOriginal?.IdentityCurrent()==true;
    }
    // Shared immutable member grammar, with separate real native-load issuers.
    internal sealed class LoadedMemberSource
    {
        private readonly RebirthWorkstationCompletionPipeline.RemoteOutputHostSource remote;
        private readonly AuthorityLocalLoadedSource local;
        internal RebirthWorkstationCompletionPipeline.RemoteOutputHostSource Remote=>remote;
        internal AuthorityLocalLoadedSource Local=>local;
        internal LoadedMemberSource(RebirthWorkstationCompletionPipeline.RemoteOutputHostSource value){remote=value;Native=new NativeSource(value.Native);}
        internal LoadedMemberSource(AuthorityLocalLoadedSource value){local=value;Native=new NativeSource(value.Native);}
        internal World World=>remote?.World??local.World;
        internal string Save=>remote?.Save??local.Save;
        internal string Path=>remote?.Path??local.Path;
        internal Guid Birth=>remote?.Birth??local.Birth;
        internal Vector3i Position=>remote?.Position??local.Position;
        internal XElement DeferredMembership=>remote?.DeferredMembership??local?.Membership;
        internal XElement Request=>remote?.Request;
        internal byte[] DeferredAddBody=>remote?.DeferredAddBody??local?.Body;
        internal readonly NativeSource Native;
        internal bool Current()=>remote?.Current()??local.InputsCurrent();
        internal sealed class NativeSource
        {
            private readonly RebirthRecipeOutputIntegration.RemoteOutputHostNativeSource remote;
            private readonly RebirthRecipeOutputIntegration.AuthorityLocalNativeSource local;
            internal NativeSource(RebirthRecipeOutputIntegration.RemoteOutputHostNativeSource value){remote=value;}
            internal NativeSource(RebirthRecipeOutputIntegration.AuthorityLocalNativeSource value){local=value;}
            internal bool Current()=>remote?.Current()??local?.Current()==true;
            internal bool IdentityCurrent()=>remote?.IdentityCurrent()??local?.IdentityCurrent()==true;
            internal TileEntityWorkstation Tile=>remote?.Tile??local?.Tile;
        }
    }
}
