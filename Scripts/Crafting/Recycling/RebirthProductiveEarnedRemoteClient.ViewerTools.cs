using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedRemoteClient
{
    private sealed class ViewerToolRequest
    {internal EntityPlayerLocal Actor;internal TileEntityWorkstation Station;internal WeakReference<XUiC_WorkstationWindowGroup> Window;internal long Session;internal Guid Operation=Guid.NewGuid();internal double Next;internal int Deaths;internal string Creation;}
    private static readonly System.Collections.Generic.List<ViewerToolRequest> viewerToolRequests=new System.Collections.Generic.List<ViewerToolRequest>();
    private static void ObserveViewerToolRequest(EntityPlayerLocal actor,TileEntityWorkstation station,XUiC_WorkstationWindowGroup window,long session)
    {
        viewerToolRequests.RemoveAll(value=>ReferenceEquals(value.Station,station));
        if(viewerToolRequests.Count>=4)return;
        viewerToolRequests.Add(new ViewerToolRequest{Actor=actor,Station=station,Window=new WeakReference<XUiC_WorkstationWindowGroup>(window),Session=session,Deaths=actor.Died,Creation=RebirthCamperCharacterAccess.CurrentCreation(actor)});
    }
    private static void PumpViewerToolRequests()
    {
        double now=DateTime.UtcNow.Ticks/(double)TimeSpan.TicksPerSecond;
        foreach(var value in viewerToolRequests.ToArray())
        {
            if(!ReferenceEquals(value.Actor.world,observedWorld)||value.Actor.IsDead()||value.Actor.Died!=value.Deaths||
                RebirthCamperCharacterAccess.CurrentCreation(value.Actor)!=value.Creation||!value.Window.TryGetTarget(out var window)||
                !ReferenceEquals(window.WorkstationData?.TileEntity,value.Station)||!RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(window,out var session)||session!=value.Session)
            {viewerToolRequests.Remove(value);continue;}
            if(originals.TryGetValue(value.Station,out var original))
            {if(original.ViewerToolsOnly)viewerToolRequests.Remove(value);continue;}
            if(now<value.Next)continue;value.Next=now+1;
            RebirthProductiveEarnedWire.Send(value.Actor,value.Station,RebirthProductiveEarnedWire.Kind.NativeViewerTools,value.Operation,Guid.Empty,null,null);
        }
    }
    // Invoked only inside the authenticated existing native-echo receiver.
    // This is a tool payer scope, never an earned-work Original producer.
    private static Original BindViewerToolDescription(XElement header,World world)
    {
        var actor=world.GetPrimaryPlayer() as EntityPlayerLocal;
        if(actor==null||!RebirthStablePlayerIdentity.TryFromLocalPlatform(actor,out var owner)||(string)header.Attribute("payer")!=owner.StorageKey||
            (string)header.Attribute("creation")!=RebirthCamperCharacterAccess.CurrentCreation(actor)||(int?)header.Attribute("deaths")!=actor.Died)return null;
        string originalText=(string)header.Attribute("original"),sourceText=(string)header.Attribute("source");
        if(originalText==null||sourceText==null||originalText.Length>2L*RebirthProductiveEarnedRowProof.MaximumArtifactBytes||sourceText.Length>2L*RebirthProductiveEarnedRowProof.MaximumArtifactBytes)return null;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
        {
            if(decoder==null)return null;byte[] image=Convert.FromBase64String(originalText);
            var frame=RebirthProductiveEarnedRowProof.Decode(image);
            if(!decoder.Admit(frame,image.Length)||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*(originalText.Length+sourceText.Length)+65536),out var bootstrapMemory))return null;
            using(bootstrapMemory)
            {
            byte[] source=Convert.FromBase64String(sourceText);
            if(Hash(image)!=(string)header.Attribute("digest")||frame.World!=RebirthProductiveEarnedArtifact.WorldIdentity(world)||
                frame.Operation.ToString("N")!=(string)header.Attribute("operation")||frame.Birth.ToString("N")!=(string)header.Attribute("birth")||
                !Guid.TryParseExact((string)header.Attribute("nonce"),"N",out var nonce)||nonce==Guid.Empty||
                !RebirthProductiveEarnedViewerToolSource.TryRead(null,image,source,nonce,out var paid,false))return null;
            // Locate only the actual observed open native card. A matching row
            // or a request position cannot manufacture a card/read witness.
            var position=new Vector3i((int)header.Attribute("x"),(int)header.Attribute("y"),(int)header.Attribute("z"));
            var station=world.GetTileEntity(position) as TileEntityWorkstation;
            if(station==null||!toolWindows.TryGetValue(station,out var observed)||!observed.Window.TryGetTarget(out var window)||
                !ReferenceEquals(window.WorkstationData?.TileEntity,station)||!ReferenceEquals(window.xui?.playerUI?.entityPlayer,actor)||
                !RebirthProductiveEarnedUiDeferredTransport.TryGetCurrentNativeUiSession(window,out var session)||session!=observed.Session)return null;
            if(originals.TryGetValue(station,out var existing))return existing.ViewerToolsOnly&&existing.Nonce==nonce&&existing.Image.SequenceEqual(image)?existing:null;
            if(originals.Count>=4)return null;
            string root=System.IO.Path.GetFullPath(GameIO.GetPlayerDataDir()),path=System.IO.Path.Combine(root,"RebirthProductiveViewerToolsOwner",frame.World.Replace('/','_').Replace('+','-'),owner.StorageKey,frame.Operation.ToString("N")+".rew");
            var value=new Original{Actor=actor,World=world,Station=station,Frame=frame,Image=image,Owner=owner.StorageKey,Creation=RebirthCamperCharacterAccess.CurrentCreation(actor),
                Deaths=actor.Died,WorldKey=frame.World,Root=root,Path=path,Digest=Hash(image),Nonce=nonce,ViewerToolsOnly=true};
            value.Memory=RebirthProductiveEarnedObservationBudget.TryOpenOwner(actor,frame,image);if(value.Memory==null)return null;
            if(!WriteExact(path,image,RebirthProductiveEarnedRowProof.MaximumArtifactBytes)||!WriteExact(path+".scope",Scope(value),2048)||
                !WriteExact(path+".nativeViewerSource",source,RebirthProductiveEarnedRowProof.MaximumArtifactBytes))
            {value.Memory.Dispose();return null;}
            value.Output=new NativeOutput{Original=value,Plan=paid,Saved=true};
            if(File.Exists(path+".nativeTools")&&!LoadNativeToolsDescription(value,decoder)){value.Memory.Dispose();return null;}
            originals.Add(station,value);return value;
            }
        }
    }
    private static void PumpViewerTools(Original source,double now)
    {
        source.NextQuery=now+1;
        if(!source.Identity())
        {
            if(source.Tools==null)
            {
                // This warm scope never entered even its first tool gesture.
                // Closing an observing card retires only its transient graph.
                if(originals.TryGetValue(source.Station,out var unused)&&ReferenceEquals(unused,source))originals.Remove(source.Station);
                source.Output?.Dispose();source.Output=null;source.Memory?.Dispose();source.Memory=null;return;
            }
            QueueViewerToolsClosure(source.Path,source.Actor);return;
        }
        if(RetireSavedViewerTools(source))return;
        if(!source.Current())
        {
            if(source.Tools?.Record!=null)PumpNativeTools(source); // Status only until an actual returned echo renews Current.
            if(RetireSavedViewerTools(source))return;
            RebirthProductiveEarnedWire.Send(source.Actor,source.Station,RebirthProductiveEarnedWire.Kind.NativeViewerTools,source.Frame.Operation,source.Nonce,null,source.Digest);return;
        }
        PumpNativeTools(source);
        RetireSavedViewerTools(source);
        // No output, pending timer, earned upload or reward is run by this
        // scope. The actual host native pending coordinator retains the work.
    }
    private static bool RetireSavedViewerTools(Original source)
    {
        if(source.Tools?.Saved!=true||!Stage(source,"nativeViewerReleased",source.Nonce))return false;
        QueueViewerToolsClosure(source.Path,source.Actor); // Retain bounded compact metadata observation after this graph leaves.
        if(originals.TryGetValue(source.Station,out var current)&&ReferenceEquals(current,source))originals.Remove(source.Station);
        source.Tools.Dispose();source.Tools=null;source.Output?.Dispose();source.Output=null;source.Memory?.Dispose();source.Memory=null;return true;
    }
}
