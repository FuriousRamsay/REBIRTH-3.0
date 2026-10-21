using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using System.Xml;

internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    // Actual accepted owner upload + actual native Save/readback only. No native
    // setter, inventory reconstruction or missing-file recovery lives here.
    internal static class OwnerPds
    {
        private const int Maximum=16*1024*1024;
        internal sealed class Window
        {
            internal RecipientScope Scope;internal IDisposable Budget;
            internal SavedOwnerFile Before,After;internal bool Closed;internal int Workers;
        }
        internal sealed class SavedOwnerFile
        {
            internal Window window;
            internal RecipientScope Scope;internal bool After,Accepted,Published;
            internal byte[] Native;internal string Path,Root,Name,Frame;
            internal SavedOwnerFile(object issuer){if(!ReferenceEquals(issuer,tokenIssuer))throw new InvalidOperationException();}
            internal bool Current()=>ThreadManager.IsMainThread()&&window!=null&&!window.Closed&&Scope.Current()&&OwnerPds.Frame(After?Scope.ExpectedAfter:Scope.ExpectedBefore)==Frame&&Accepted&&Published&&Native!=null;
            internal string NativeDigest {get{using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Native));}}
        }
        private sealed class Attempt {internal Window Window;internal SavedOwnerFile Token;internal PlayerDataFile Data;}
        private sealed class Writing {internal Window Window;internal SavedOwnerFile Token;internal byte[] Image;internal string Path;}
        private sealed class AttemptGroup {internal Attempt[] Entries;internal PlayerDataFile Data;}
        private sealed class WritingGroup {internal Writing[] Entries;}
        private static readonly object gate=new object(),tokenIssuer=new object();
        private static readonly Dictionary<RecipientScope,Window> windows=new Dictionary<RecipientScope,Window>();
        private static readonly ConditionalWeakTable<PlayerDataFile,AttemptGroup> accepted=new ConditionalWeakTable<PlayerDataFile,AttemptGroup>();
        private sealed class FrameHashStream:Stream
        {
            private readonly HashAlgorithm hash;private long size;
            internal FrameHashStream(HashAlgorithm value){hash=value;}
            public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
            public override long Length=>size;public override long Position {get=>size;set=>throw new NotSupportedException();}
            public override void Flush(){}
            public override void Write(byte[] buffer,int offset,int count)
            {
                if(count<0||size>Maximum-count)throw new InvalidDataException("Deferred owner frame bound");
                hash.TransformBlock(buffer,offset,count,null,0);size+=count;
            }
            public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
            public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
            public override void SetLength(long value)=>throw new NotSupportedException();
        }
        private static string Frame(XElement image)
        {
            if(image==null)return null;
            try
            {
                using(var hash=SHA256.Create())using(var stream=new FrameHashStream(hash))
                {
                    using(var writer=XmlWriter.Create(stream,new XmlWriterSettings{Encoding=new UTF8Encoding(false),OmitXmlDeclaration=true,
                        Indent=false,NewLineHandling=NewLineHandling.Entitize,CloseOutput=false}))image.WriteTo(writer);
                    hash.TransformFinalBlock(Array.Empty<byte>(),0,0);return Convert.ToBase64String(hash.Hash);
                }
            }
            catch{return null;}
        }
        internal static bool Arm(RecipientScope scope,bool after)
        {
            if(!ThreadManager.IsMainThread()||scope?.Current()!=true)return false;
            lock(gate)
            {
                Sweep();
                if(!windows.TryGetValue(scope,out var window))
                {
                    if(windows.Count>=4)return false;var budget=scope.RetainOwnerPdsBudget();if(budget==null)return false;
                    window=new Window{Scope=scope,Budget=budget};windows.Add(scope,window);
                }
                string frame=Frame(after?scope.ExpectedAfter:scope.ExpectedBefore);if(frame==null)return false;
                var prior=after?window.After:window.Before;
                if(prior!=null)return prior.Frame==frame&&!window.Closed;
                var token=new SavedOwnerFile(tokenIssuer){window=window,Scope=scope,After=after,Frame=frame};
                if(after)window.After=token;else window.Before=token;return true;
            }
        }
        internal static bool TryReadBefore(RecipientScope scope,out SavedOwnerFile token)=>Read(scope,false,out token);
        internal static bool TryReadAfter(RecipientScope scope,out SavedOwnerFile token)=>Read(scope,true,out token);
        private static bool Read(RecipientScope scope,bool after,out SavedOwnerFile token)
        {
            token=null;if(!ThreadManager.IsMainThread())return false;
            lock(gate){if(!windows.TryGetValue(scope,out var window))return false;var value=after?window.After:window.Before;
                if(value?.Current()!=true)return false;token=value;return true;}
        }
        internal static void Release(RecipientScope scope)
        {lock(gate){if(!windows.TryGetValue(scope,out var window))return;windows.Remove(scope);Close(window);}}
        private static void Close(Window window)
        {
            window.Closed=true;
            if(window.Workers!=0)return;
            foreach(var token in new[]{window.Before,window.After})if(token!=null)
            {token.Native=null;token.Scope=null;token.Frame=token.Path=token.Root=token.Name=null;}
            window.Before=window.After=null;window.Scope=null;
            window.Budget?.Dispose();window.Budget=null;
        }
        private static void Sweep()
        {
            foreach(var pair in windows.Where(pair=>!pair.Key.Current()).ToArray()){windows.Remove(pair.Key);Close(pair.Value);}
        }
        // Existing OwnerFile accepted-upload prefix must first prove its exact
        // NetPackagePlayerData sender/file scope before composing this callback.
        internal static void ObserveAcceptedPrefix(ClientInfo peer,PlayerDataFile data,bool run,out object state)
        {
            state=null;if(!run||!ThreadManager.IsMainThread()||peer==null||data==null)return;
            try
            {
                lock(gate)
                {
                    Sweep();var matches=new List<SavedOwnerFile>();
                    foreach(var window in windows.Values)
                    {
                        var scope=window.Scope;
                        if(!scope.Current()||!ReferenceEquals(scope.Peer,peer)||scope.Actor.entityId!=data.id||scope.Deaths!=data.deaths)continue;
                        foreach(var token in new[]{window.Before,window.After})
                            if(token!=null&&!token.Published&&Frame(token.After?scope.ExpectedAfter:scope.ExpectedBefore)==token.Frame&&RebirthProductiveEarnedUiOwnerFile.MatchesDeferredOwnerFrame(data,token.After?scope.ExpectedAfter:scope.ExpectedBefore))matches.Add(token);
                    }
                    if(matches.Count<1||matches.Count>8)return;
                    if(!RebirthStablePlayerIdentity.TryFromClientInfo(peer,out var identity)||matches.Any(token=>identity.StorageKey!=token.Scope.Owner))return;
                    string root=System.IO.Path.GetFullPath(GameIO.GetPlayerDataDir()),name=identity.CanonicalId;
                    string path=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,name+"."+PlayerDataFile.EXT));
                    if(System.IO.Path.GetDirectoryName(path)!=root||!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path))return;
                    var image=RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data);
                    if(image.Length<1||image.Length>Maximum||matches.Any(token=>token.Native!=null&&!token.Native.SequenceEqual(image)))return;
                    var entries=new List<Attempt>();
                    foreach(var token in matches)
                    {
                        token.Native=image;token.Path=path;token.Root=root;token.Name=name;
                        entries.Add(new Attempt{Window=token.window,Token=token,Data=data});
                    }
                    var group=new AttemptGroup{Entries=entries.ToArray(),Data=data};accepted.Remove(data);accepted.Add(data,group);state=group;
                }
            }
            catch{state=null;}
        }
        internal static void ObserveAcceptedPostfix(ClientInfo peer,PlayerDataFile data,bool run,object state)
        {
            if(!run||!(state is AttemptGroup group)||!ThreadManager.IsMainThread()||!ReferenceEquals(group.Data,data)||!ReferenceEquals(peer.latestPlayerData,data))return;
            lock(gate)foreach(var value in group.Entries)
                if(!value.Window.Closed&&value.Token.Scope.Current()&&ReferenceEquals(value.Token.Scope.Peer,peer)&&
                    ReferenceEquals(value.Data,data)&&ReferenceEquals(value.Token.Scope.Actor.QuestJournal,data.questJournal))value.Token.Accepted=true;
        }
        internal static void BeforeSave(PlayerDataFile data,string directory,string name,bool run,out object state)
        {
            state=null;if(!run||data==null)return;
            try
            {
                lock(gate)
                {
                    if(!accepted.TryGetValue(data,out var group)||group.Entries.Length<1||group.Entries.Length>8||
                        group.Entries.Any(value=>value.Window.Closed)||
                        group.Entries.GroupBy(value=>value.Window).Any(values=>values.Key.Workers+values.Count()>2))return;
                    string root=System.IO.Path.GetFullPath(directory),path=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,name+"."+PlayerDataFile.EXT));
                    if(!RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerPathSafe(path))return;
                    var image=RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerImage(data);
                    if(group.Entries.Any(value=>root!=value.Token.Root||name!=value.Token.Name||path!=value.Token.Path||value.Token.Native==null||!image.SequenceEqual(value.Token.Native)))return;
                    var entries=group.Entries.Select(value=>new Writing{Window=value.Window,Token=value.Token,Image=value.Token.Native,Path=path}).ToArray();
                    foreach(var entry in entries)entry.Window.Workers++;
                    state=new WritingGroup{Entries=entries};
                }
            }
            catch{state=null;}
        }
        internal static void AfterSave(bool run,object state)
        {
            if(!(state is WritingGroup group))return;
            try
            {
                var first=group.Entries[0];
                bool saved=run&&RebirthProductiveEarnedUiOwnerFile.DeferredNativeOwnerFileMatches(first.Path,first.Image);
                lock(gate)if(saved)foreach(var value in group.Entries)
                    if(!value.Window.Closed&&ReferenceEquals(value.Token.Native,value.Image))value.Token.Published=true;
            }
            finally{lock(gate)foreach(var value in group.Entries){value.Window.Workers--;if(value.Window.Closed)Close(value.Window);}}
        }
    }
}

