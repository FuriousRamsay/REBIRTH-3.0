using System;
using System.Globalization;
using System.Xml;

internal static partial class RebirthNpcRecoveryProjectionScope
{
    internal static RebirthNpcSaveQuarantineDomain PrepareClientGenerationQuarantine(World originalWorld,string originalDirectory,uint originalEpoch,uint originalConnection)
    {
        if(!ThreadManager.IsMainThread()||originalWorld?.IsRemote()!=true||(world!=null&&(!ReferenceEquals(world,originalWorld)||!RebirthNpcSaveQuarantineScope.SameDirectory(directory,originalDirectory)||epoch!=originalEpoch||connection!=originalConnection)))throw new InvalidOperationException("Client projection generation scope unresolved");
        var priorWorld=world;string priorDirectory=directory;uint priorEpoch=epoch,priorConnection=connection;ulong priorGeneration=generation;
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("clientGenerationQuarantine");doc.AppendChild(root);root.SetAttribute("directory",directory??string.Empty);root.SetAttribute("epoch",epoch.ToString(CultureInfo.InvariantCulture));root.SetAttribute("connection",connection.ToString(CultureInfo.InvariantCulture));root.SetAttribute("generation",generation.ToString(CultureInfo.InvariantCulture));
        var header=RebirthNpcBaselineClientHeader.PrepareClientHeaderQuarantine(originalWorld,originalDirectory,originalEpoch,originalConnection);
        root.AppendChild(header.CopyEvidence(doc));
        return new RebirthNpcSaveQuarantineDomain("client-recovery-generation",doc,
            ()=>ThreadManager.IsMainThread()&&ReferenceEquals(world,priorWorld)&&directory==priorDirectory&&epoch==priorEpoch&&connection==priorConnection&&generation==priorGeneration&&header.IsCurrent,
            ()=>{header.Detach();world=null;directory=null;epoch=connection=0;generation=0;},
            ()=>{try{header.Rollback();}finally{world=priorWorld;directory=priorDirectory;epoch=priorEpoch;connection=priorConnection;generation=priorGeneration;}});
    }
}
