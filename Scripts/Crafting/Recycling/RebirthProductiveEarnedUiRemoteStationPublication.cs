using System;
using System.IO;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiRemote
{
    // A positive earlier station/viewer publication survives that viewer leaving.
    // It grants no authority over a replacement station or a reconstructed world.
    internal sealed class TypedStationPublication
    {
        private readonly Registration original;private readonly World world;
        private readonly byte[] receipt;private readonly string path;
        internal TypedStationPublication(object issuer,Registration source,byte[] image,string file)
        {
            if(!ReferenceEquals(issuer,terminalIssuer))throw new InvalidOperationException();
            original=source;world=source.Actor.world;receipt=image;path=file;
        }
        internal Guid Operation=>original.Frame.Operation;
        internal Guid Birth=>original.Frame.Birth;
        internal World World=>world;
        internal TileEntityWorkstation Station=>original.Station;
        internal bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(GameManager.Instance?.World,world)&&
            original.Frame!=null&&ReferenceEquals(world.GetTileEntity(Station.ToWorldPos()),Station)&&
            original.Save==Path.GetFullPath(GameIO.GetSaveGameDir())&&original.Frame.World==RebirthProductiveEarnedArtifact.WorldIdentity(world)&&
            RebirthLogisticsEndpointIdentity.TryGetIncarnation(world,Station,out var birth)&&birth==Birth.ToString("N")&&
            RebirthProductiveEarnedArtifact.FileMatches(original.Path,original.Image)&&RebirthProductiveEarnedArtifact.FileMatches(path,receipt);
    }
    internal static bool TryGetStationPublication(Registration original,out TypedStationPublication publication)
    {
        publication=null;
        if(original?.StationPublication!=null)
        {if(!original.StationPublication.Current())return false;publication=original.StationPublication;return true;}
        if(original?.Terminal?.Owner?.Saved()!=true||!published.TryGetValue(original.Frame.Operation,out var proof)||!proof.Current()||
            !proof.Watch.TryReadPublishedTileDescriptor(out var native,out long offset,out int length,out string tile)||!proof.Current())return false;
        var node=new XElement("uiNativeStationViewerSaved",new XAttribute("operation",original.Frame.Operation.ToString("N")),
            new XAttribute("uio",original.Hash),new XAttribute("nonce",original.Nonce.ToString("N")),new XAttribute("birth",original.Frame.Birth.ToString("N")),
            new XAttribute("native",Hash(original.TerminalBody)),new XAttribute("viewerNative",Hash(original.Terminal.Owner.Final)),
            new XAttribute("nativePath",native.Path),new XAttribute("payload",native.PayloadDigest),new XAttribute("offset",offset),
            new XAttribute("length",length),new XAttribute("tile",tile));
        byte[] bytes=Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting));string path=original.Path+".StationViewerSaved";
        if(!RebirthProductiveEarnedUiCoordinator.WriteRemoteExact(path,bytes))return false;
        var value=new TypedStationPublication(terminalIssuer,original,bytes,path);
        if(!value.Current())return false;original.StationPublication=value;publication=value;return true;
    }
}
