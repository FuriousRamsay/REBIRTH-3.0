using System;
using System.IO;
using System.Globalization;
using System.Xml;
using UnityEngine.Scripting;

internal static class RebirthNpcBaselineFraming
{
    internal struct Frame{internal uint Epoch,Connection;internal ulong Generation,Sequence;internal int First,Second;}
    internal static void Validate(Frame f){if(f.Epoch==0||f.Connection==0||f.Sequence==0||f.First<0||f.First>RebirthNpcNetworkProtocol.MaxBaselineActors||f.Second<0||(long)f.First+f.Second>int.MaxValue)throw new InvalidDataException("Invalid explicit baseline V2 frame");}
    internal static void Write(BinaryWriter writer,Frame f){Validate(f);writer.Write((ushort)1);writer.Write(f.Epoch);writer.Write(f.Connection);writer.Write(f.Generation);writer.Write(f.Sequence);writer.Write(f.First);writer.Write(f.Second);}
    internal static Frame Read(BinaryReader reader){if(reader.ReadUInt16()!=1)throw new InvalidDataException("Unknown baseline V2 codec");var f=new Frame{Epoch=reader.ReadUInt32(),Connection=reader.ReadUInt32(),Generation=reader.ReadUInt64(),Sequence=reader.ReadUInt64(),First=reader.ReadInt32(),Second=reader.ReadInt32()};Validate(f);return f;}
    internal static bool SameHeader(Frame a,Frame b)=>a.Epoch==b.Epoch&&a.Connection==b.Connection&&a.Generation==b.Generation&&a.Sequence==b.Sequence&&a.First==b.First&&a.Second==b.Second;
}
[Preserve]
public sealed class NetPackageRebirthNpcBaselineStartV2:NetPackage
{
    private uint epoch,connection;private ulong generation,sequence;private int expected,initialSkipped;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthNpcBaselineStartV2 Setup(uint worldEpoch,uint recipientConnection,ulong recoveryGeneration,ulong baselineSequence,int actors,int skipped)
    {epoch=worldEpoch;connection=recipientConnection;generation=recoveryGeneration;sequence=baselineSequence;expected=actors;initialSkipped=skipped;Validate();return this;}
    private void Validate(){if(epoch==0||connection==0||sequence==0||expected<0||expected>RebirthNpcNetworkProtocol.MaxBaselineActors||initialSkipped<0||(long)expected+initialSkipped>int.MaxValue)throw new InvalidDataException("Invalid V2 baseline start");}
    public override void read(PooledBinaryReader reader){var f=RebirthNpcBaselineFraming.Read(reader);epoch=f.Epoch;connection=f.Connection;generation=f.Generation;sequence=f.Sequence;expected=f.First;initialSkipped=f.Second;Validate();}
    public override void write(PooledBinaryWriter writer){Validate();base.write(writer);RebirthNpcBaselineFraming.Write(writer,new RebirthNpcBaselineFraming.Frame{Epoch=epoch,Connection=connection,Generation=generation,Sequence=sequence,First=expected,Second=initialSkipped});}
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(RebirthNpcBaselineClientHeader.Start(world,epoch,connection,generation,sequence,expected,initialSkipped,false))
            RebirthNpcBaselineClientHeader.AcknowledgeInstalled(world,new RebirthNpcBaselineFraming.Frame{Epoch=epoch,Connection=connection,Generation=generation,Sequence=sequence,First=expected,Second=initialSkipped});
    }
    public int GetLength()=>34;
}
[Preserve]
public sealed class NetPackageRebirthNpcBaselineCompleteV2:NetPackage
{
    private uint epoch,connection;private ulong generation,sequence;private int sent,skipped;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    public NetPackageRebirthNpcBaselineCompleteV2 Setup(uint worldEpoch,uint recipientConnection,ulong recoveryGeneration,ulong baselineSequence,int actors,int omitted)
    {epoch=worldEpoch;connection=recipientConnection;generation=recoveryGeneration;sequence=baselineSequence;sent=actors;skipped=omitted;Validate();return this;}
    private void Validate(){if(epoch==0||connection==0||sequence==0||sent<0||sent>RebirthNpcNetworkProtocol.MaxBaselineActors||skipped<0||(long)sent+skipped>int.MaxValue)throw new InvalidDataException("Invalid V2 baseline completion");}
    public override void read(PooledBinaryReader reader){var f=RebirthNpcBaselineFraming.Read(reader);epoch=f.Epoch;connection=f.Connection;generation=f.Generation;sequence=f.Sequence;sent=f.First;skipped=f.Second;Validate();}
    public override void write(PooledBinaryWriter writer){Validate();base.write(writer);RebirthNpcBaselineFraming.Write(writer,new RebirthNpcBaselineFraming.Frame{Epoch=epoch,Connection=connection,Generation=generation,Sequence=sequence,First=sent,Second=skipped});}
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthNpcBaselineClientHeader.Complete(world,epoch,connection,generation,sequence,sent,skipped,false);
    public int GetLength()=>34;
}
// Header installation only: never row delivery or recovery convergence authority.
[Preserve]
public sealed class NetPackageRebirthNpcBaselineHeaderAckV2:NetPackage
{
    private RebirthNpcBaselineFraming.Frame frame;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthNpcBaselineHeaderAckV2 Setup(RebirthNpcBaselineFraming.Frame value){RebirthNpcBaselineFraming.Validate(value);frame=value;return this;}
    public override void read(PooledBinaryReader reader){frame=RebirthNpcBaselineFraming.Read(reader);}
    public override void write(PooledBinaryWriter writer){base.write(writer);RebirthNpcBaselineFraming.Write(writer,frame);}
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthNpcNetworkBaselineService.ReceiveHeaderAck(this,world,frame);
    public int GetLength()=>34;
}
// Baseline completion is a visual lifecycle event, NEVER recovery readback ACK,
// native actor presence, inventory/payment or body reconstruction authority.
internal static class RebirthNpcBaselineClientHeader
{
    private sealed class Header
    {internal World World;internal string Directory;internal uint Epoch,Connection;internal ulong Generation,Sequence;internal int Expected,InitialSkipped;internal bool Legacy,Completed,CallbackUnknown;}
    private static Header current;private static bool callbackActive;
    private static bool NativeCurrent(World world)=>ThreadManager.IsMainThread()&&world?.IsRemote()==true&&ReferenceEquals(world,GameManager.Instance?.World);
    internal static bool Start(World world,uint epoch,uint connection,ulong generation,ulong sequence,int expected,int initialSkipped,bool legacy)
    {
        if(!NativeCurrent(world)||epoch==0||connection==0||(!legacy&&sequence==0)||(legacy&&(sequence!=0||generation!=0))||expected<0||expected>RebirthNpcNetworkProtocol.MaxBaselineActors||initialSkipped<0||(long)expected+initialSkipped>int.MaxValue||callbackActive||current?.CallbackUnknown==true)return false;
        if(!RebirthNpcLifecycle.PrepareNativeEpochBoundary()||!NativeCurrent(world))return false;
        string save=GameIO.GetSaveGameDir();
        if((RebirthNpcNetworkEpoch.ClientEpoch!=0&&RebirthNpcNetworkEpoch.ClientEpoch!=epoch)||(RebirthNpcNetworkEpoch.ClientConnectionEpoch!=0&&RebirthNpcNetworkEpoch.ClientConnectionEpoch!=connection)||!RebirthNpcRecoveryProjectionScope.CanBeginBaseline(world,epoch,connection,generation))return false;
        if(current!=null)
        {
            if(!ReferenceEquals(current.World,world)||current.Directory!=save||current.Epoch!=epoch||current.Connection!=connection||!HeaderCompatible(current.Generation,current.Sequence,current.Legacy,current.Expected,current.InitialSkipped,generation,sequence,legacy,expected,initialSkipped))return false;
            if(sequence==current.Sequence)return true;
        }
        RebirthNpcNetworkEpoch.BeginClientEpoch(epoch,connection);
        if(!RebirthNpcNetworkEpoch.AcceptClientEpoch(epoch)||RebirthNpcNetworkEpoch.ClientConnectionEpoch!=connection)return false;
        if(!RebirthNpcRecoveryProjectionScope.Observe(generation))return false;
        current=new Header{World=world,Directory=save,Epoch=epoch,Connection=connection,Generation=generation,Sequence=sequence,Expected=expected,InitialSkipped=initialSkipped,Legacy=legacy};return true;
    }
    internal static void AcknowledgeInstalled(World world,RebirthNpcBaselineFraming.Frame f)
    {
        var h=current;var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!NativeCurrent(world)||manager==null||manager.IsServer||h==null||h.Legacy||callbackActive||h.CallbackUnknown||!ReferenceEquals(h.World,world)||h.Directory!=GameIO.GetSaveGameDir()||h.Epoch!=f.Epoch||h.Connection!=f.Connection||h.Generation!=f.Generation||h.Sequence!=f.Sequence||h.Expected!=f.First||h.InitialSkipped!=f.Second||!RebirthNpcNetworkEpoch.AcceptClientEpoch(f.Epoch)||RebirthNpcNetworkEpoch.ClientConnectionEpoch!=f.Connection||!RebirthNpcRecoveryProjectionScope.CanBeginBaseline(world,f.Epoch,f.Connection,f.Generation))return;
        manager.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthNpcBaselineHeaderAckV2>().Setup(f));
    }
    internal static void Complete(World world,uint epoch,uint connection,ulong generation,ulong sequence,int sent,int skipped,bool legacy)
    {
        var original=current;
        if(!NativeCurrent(world)||original==null||callbackActive||original.CallbackUnknown||original.Completed||!ReferenceEquals(original.World,world)||original.Directory!=GameIO.GetSaveGameDir()||original.Epoch!=epoch||original.Connection!=connection||original.Generation!=generation||original.Sequence!=sequence||original.Legacy!=legacy||!RebirthNpcNetworkEpoch.AcceptClientEpoch(epoch)||RebirthNpcNetworkEpoch.ClientConnectionEpoch!=connection||!RebirthNpcRecoveryProjectionScope.Observe(generation)||!CompletionCompatible(original.Expected,original.InitialSkipped,sent,skipped,legacy))return;
        original.Completed=true;callbackActive=true;
        try{RebirthNpcBaselineClientEvents.Receive(RebirthNpcNetworkProtocol.CurrentVersion,sent,skipped);}
        catch{original.CallbackUnknown=true;throw;}
        finally{callbackActive=false;}
    }
    internal static void LegacyComplete(World world,uint epoch,int sent,int skipped)
    {var original=current;if(original?.Legacy==true)Complete(world,epoch,original.Connection,0,0,sent,skipped,true);}
    internal static bool CanRetireEmpty=>ThreadManager.IsMainThread()&&current==null&&!callbackActive&&!RebirthNpcBaselineClientEvents.HasSubscribers;
    internal static RebirthNpcSaveQuarantineDomain PrepareClientHeaderQuarantine(World world,string directory,uint epoch,uint connection)
    {
        var original=current;if(!ThreadManager.IsMainThread()||world?.IsRemote()!=true||callbackActive||RebirthNpcBaselineClientEvents.HasSubscribers||original?.CallbackUnknown==true||(original!=null&&(!ReferenceEquals(original.World,world)||!RebirthNpcSaveQuarantineScope.SameDirectory(original.Directory,directory)||original.Epoch!=epoch||original.Connection!=connection)))throw new InvalidOperationException("Original client baseline header requires qualified retirement");
        var capturedWorld=original?.World;string capturedDirectory=original?.Directory;
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("baselineClientHeader");doc.AppendChild(root);string image=Image(original);root.InnerText=image;
        return new RebirthNpcSaveQuarantineDomain("client-baseline-header",doc,()=>ThreadManager.IsMainThread()&&!callbackActive&&!RebirthNpcBaselineClientEvents.HasSubscribers&&ReferenceEquals(current,original)&&ReferenceEquals(original?.World,capturedWorld)&&original?.Directory==capturedDirectory&&Image(current)==image,()=>current=null,()=>current=original);
    }
    private static string Image(Header h)=>h==null?"empty":string.Join("|",h.Epoch,h.Connection,h.Generation,h.Sequence,h.Expected,h.InitialSkipped,h.Legacy,h.Completed,h.CallbackUnknown);
    internal static bool HeaderCompatible(ulong oldGeneration,ulong oldSequence,bool oldLegacy,int oldExpected,int oldSkipped,ulong incomingGeneration,ulong incomingSequence,bool incomingLegacy,int expected,int skipped)
    {if(incomingGeneration<oldGeneration||incomingSequence<oldSequence||(!oldLegacy&&incomingLegacy))return false;return incomingSequence!=oldSequence||(incomingGeneration==oldGeneration&&oldLegacy==incomingLegacy&&oldExpected==expected&&oldSkipped==skipped);}
    internal static bool CompletionCompatible(int expected,int initialSkipped,int sent,int skipped,bool legacy)
    {return expected>=0&&initialSkipped>=0&&sent>=0&&sent<=expected&&skipped>=0&&(legacy||((long)sent+skipped==(long)expected+initialSkipped&&skipped>=initialSkipped));}
}
internal static partial class RebirthNpcRecoveryProjectionScope
{
    internal static bool CanRetireEmptyClientGeneration=>ThreadManager.IsMainThread()&&(world==null||world.IsRemote()!=true);
    internal static bool CanBeginBaseline(World target,uint targetEpoch,uint targetConnection,ulong incoming)
    {
        if(!ThreadManager.IsMainThread()||target?.IsRemote()!=true||!ReferenceEquals(target,GameManager.Instance?.World))return false;
        if(world!=null&&(!ReferenceEquals(world,target)||directory!=GameIO.GetSaveGameDir()||epoch!=targetEpoch||connection!=targetConnection||incoming<generation))return false;
        return true;
    }
}
