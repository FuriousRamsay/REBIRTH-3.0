using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Read-only cold custody index. It grants no permission and applies no effect.
// Unknown/partial saved originals prevent inventory writers before reattachment.
internal static class RebirthNpcTradeColdFence
{
    private static World boundWorld;
    private static string boundDirectory;
    private static bool scanned,unknown;
    private static Dictionary<Guid,RebirthNpcStableId> pending=new Dictionary<Guid,RebirthNpcStableId>();
    internal static bool CanBeginFreshGeneration(World world)=>ThreadManager.IsMainThread()&&!unknown&&pending.Count==0&&
        (boundWorld==null||ReferenceEquals(boundWorld,world));
    internal static void AppendQuarantineIds(HashSet<RebirthNpcStableId> ids)
    {
        if(!ThreadManager.IsMainThread()||unknown)throw new InvalidOperationException("Cold trade custody unknown");
        foreach(var npc in pending.Values)RebirthNpcSaveQuarantineIds.Add(ids,npc);
    }
    internal static RebirthNpcSaveQuarantineDomain PrepareQuarantine(RebirthNpcSaveQuarantineScope scope)
    {
        if(!ThreadManager.IsMainThread()||scope==null||boundWorld!=null&&!ReferenceEquals(boundWorld,scope.World)||
            boundWorld==null&&(scanned||unknown||pending.Count!=0)||pending.Count>4)throw new InvalidOperationException("Cold trade original binding unavailable");
        if(boundWorld!=null&&(!Guid.TryParse(boundWorld.worldState?.Guid,out var world)||
            boundDirectory!=Path.Combine(Path.GetFullPath(scope.Directory),"RebirthData","NpcTradeOriginals",world.ToString("N"))))throw new InvalidOperationException("Cold trade original path unavailable");
        var records=pending;var originalWorld=boundWorld;string directory=boundDirectory;bool wasScanned=scanned,wasUnknown=unknown;
        var empty=new Dictionary<Guid,RebirthNpcStableId>();var doc=Image();string image=doc.OuterXml;
        return new RebirthNpcSaveQuarantineDomain("trade-cold-host",doc,
            ()=>ReferenceEquals(pending,records)&&ReferenceEquals(boundWorld,originalWorld)&&boundDirectory==directory&&scanned==wasScanned&&unknown==wasUnknown&&Image().OuterXml==image,
            ()=>{pending=empty;boundWorld=null;boundDirectory=null;scanned=unknown=false;},
            ()=>{pending=records;boundWorld=originalWorld;boundDirectory=directory;scanned=wasScanned;unknown=wasUnknown;});
    }
    private static XmlDocument Image()
    {
        var doc=new XmlDocument{XmlResolver=null};var root=doc.CreateElement("tradeColdHostCustody");doc.AppendChild(root);
        root.SetAttribute("directory",boundDirectory??string.Empty);root.SetAttribute("scanned",scanned?"1":"0");root.SetAttribute("unknown",unknown?"1":"0");
        root.SetAttribute("authority","index-only-no-payment");var keys=new List<Guid>(pending.Keys);keys.Sort();
        foreach(var key in keys){var row=doc.CreateElement("pending");root.AppendChild(row);row.SetAttribute("operation",key.ToString("N"));row.SetAttribute("npc",pending[key].ToString());}
        return doc;
    }
    internal static bool Blocks(RebirthNpcStableId npc)
    {
        if(!ThreadManager.IsMainThread())return true;
        var world=GameManager.Instance?.World;
        if(world?.IsRemote()!=false)return true;
        try
        {
            string selected=GameIO.GetSaveGameDir();
            if(string.IsNullOrWhiteSpace(selected)||!Path.IsPathRooted(selected)||!Guid.TryParse(world.worldState?.Guid,out var worldId))return true;
            string directory=Path.Combine(Path.GetFullPath(selected),"RebirthData","NpcTradeOriginals",worldId.ToString("N"));
            if(scanned&&(!ReferenceEquals(boundWorld,world)||boundDirectory!=directory))return true;
            if(!scanned)
            {
                boundWorld=world;boundDirectory=directory;scanned=true;unknown=true;
                Scan(directory,world.worldState.Guid);
                unknown=false;
            }
            if(unknown)return true;
            foreach(var value in pending.Values)if(value==npc)return true;
            return false;
        }
        catch{unknown=true;return true;}
    }
    internal static void ObserveClosed(RebirthNpcTradeClosedHistory history)
    {if(history!=null)RemoveClosed(history.Operation);}
    internal static void ObserveClosed(RebirthNpcTradeService.HostClosed closed)
    {if(closed?.CanRetireFromQuote==true)RemoveClosed(closed.Prepared.Original.Operation);}
    private static void RemoveClosed(Guid operation)
    {
        // Called only after an independently qualified own closed chain. Scope
        // must still match the index; this cannot create or clear another index.
        if(ThreadManager.IsMainThread()&&scanned&&!unknown&&ReferenceEquals(boundWorld,GameManager.Instance?.World)&&
            Guid.TryParse(boundWorld.worldState?.Guid,out var world)&&boundDirectory==Path.Combine(Path.GetFullPath(GameIO.GetSaveGameDir()),"RebirthData","NpcTradeOriginals",world.ToString("N")))pending.Remove(operation);
    }
    private static void Scan(string directory,string world)
    {
        Safe(directory);if(!Directory.Exists(directory))return;
        int files=0,accounts=0;long total=0;
        foreach(string account in Directory.EnumerateDirectories(directory))
        {
            if(++accounts>8192)throw new InvalidDataException("Trade account index bound");
            Safe(account);string name=Path.GetFileName(account);
            if(name.Length!=64)throw new InvalidDataException("Trade account index unknown");
            foreach(char c in name)if(!(c>='0'&&c<='9'||c>='a'&&c<='f'))throw new InvalidDataException();
            foreach(string path in Directory.EnumerateFiles(account,"*.xml"))
            {
                if(++files>8192)throw new InvalidDataException("Trade original index bound");
                string image=Read(path,64*1024*1024,ref total);var root=Parse(image);
                var original=RebirthNpcTradeOriginal.Read(root.Element("original")?.Value);
                string before=root.Element("savedNpcBefore")?.Value;
                if(original.World!=world||Path.GetFileName(path)!=original.Operation.ToString("N")+".xml"||
                    name!=RebirthRemoteResourceDurableJournal.Digest(original.Account)||
                    image!=new XElement("npcTradePrepared",new XAttribute("version",1),new XAttribute("originalDigest",original.Digest),
                        new XElement("original",original.Image),new XElement("savedNpcBefore",before)).ToString(SaveOptions.DisableFormatting))throw new InvalidDataException("Trade original index mismatch");
                // History qualification reads its complete canonical chain;
                // mere .closed presence never frees the cold inventory fence.
                bool closed=false;
                if(File.Exists(path+".closed"))
                {
                    // Include repeated original reads and every closure leg in
                    // the one scan budget before invoking full qualification.
                    long additional=2L*Encoding.UTF8.GetByteCount(image);
                    foreach(string suffix in new[]{".grant",".terminal",".closed"})
                    {
                        string leg=path+suffix;Safe(leg);
                        if(!File.Exists(leg))continue;
                        var info=new FileInfo(leg);
                        if((info.Attributes&(FileAttributes.ReparsePoint|FileAttributes.Directory))!=0||info.Length<1||info.Length>(suffix==".terminal"?64*1024*1024:4096))throw new InvalidDataException("Trade history bound");
                        additional+=info.Length;
                    }
                    if(additional>256L*1024*1024-total)throw new InvalidDataException("Trade history scan byte bound");
                    total+=additional;closed=RebirthNpcTradeClosedHistory.IsClosedPreparation(path,original,image);
                }
                if(!closed)
                {
                    if(pending.Count>=4||pending.ContainsKey(original.Operation))throw new InvalidDataException("Trade pending custody bound");
                    pending.Add(original.Operation,original.CopyQuote().NpcId);
                }
            }
        }
        Safe(directory);
    }
    private static XElement Parse(string image)
    {
        using var text=new StringReader(image);
        using var reader=XmlReader.Create(text,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=64*1024*1024});
        return XElement.Load(reader);
    }
    private static string Read(string path,int maximum,ref long total)
    {
        Safe(path);if((File.GetAttributes(path)&(FileAttributes.Directory|FileAttributes.ReparsePoint))!=0)throw new InvalidDataException();
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        long length=stream.Length;if(length<1||length>maximum||length>256L*1024*1024-total)throw new InvalidDataException("Trade index byte bound");total+=length;
        using var reader=new StreamReader(stream,new UTF8Encoding(false,true),false,8192,true);string image=reader.ReadToEnd();
        if(stream.Length!=length||Encoding.UTF8.GetByteCount(image)!=length)throw new InvalidDataException();return image;
    }
    private static void Safe(string path)
    {
        for(var info=new DirectoryInfo(path);info!=null;info=info.Parent)
            if(info.Exists&&(info.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Trade cold index redirected");
    }
}
