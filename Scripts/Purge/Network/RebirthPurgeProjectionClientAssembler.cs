using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;

// Detached sequencing and byte custody. The caller's context token is not authentication.
// Native connection authority and completed manifest/ledger validation are separate prerequisites.
internal sealed class RebirthPurgeProjectionClientAssembler
{
    internal sealed class StagedBytes
    {
        private readonly byte[][] blobs;
        public readonly Guid World,Snapshot;
        public readonly long Epoch,Generation,Sequence,Revision;
        internal StagedBytes(RebirthPurgeProjectionFragmentCodec.Frame begin,byte[][] bytes)
        { World=begin.World;Snapshot=begin.Snapshot;Epoch=begin.Epoch;Generation=begin.Generation;Sequence=begin.Sequence;Revision=begin.Revision;blobs=bytes; }
        public int Count { get { return blobs.Length; } }
        public byte[] GetBlob(int index) { return (byte[])blobs[index].Clone(); }
    }
    private sealed class Blob
    {
        public readonly byte[] Bytes,Hash;
        public readonly bool[] Present;
        public int Received;
        public Blob(int length,byte[] hash)
        { Bytes=new byte[length];Hash=hash;Present=new bool[(length+RebirthPurgeProjectionFragmentCodec.PayloadLimit-1)/RebirthPurgeProjectionFragmentCodec.PayloadLimit]; }
    }
    private readonly object originalContext;
    private readonly Guid world;
    private readonly long epoch,generation;
    private RebirthPurgeProjectionFragmentCodec.Frame begin;
    private Blob[] blobs;
    private int allocated,fragments;
    private long newestSequence;
    private byte[] completedManifestHash,highestManifestHash;
    private long completedRevision=-1,highestRevision=-1;
    public RebirthPurgeProjectionClientAssembler(object sequencingContext,Guid world,long epoch,long generation)
    {
        if(sequencingContext==null || world==Guid.Empty || epoch<0 || generation<0) throw new ArgumentException("Invalid detached context.");
        originalContext=sequencingContext;this.world=world;this.epoch=epoch;this.generation=generation;
    }
    public void Discard() { begin=null;blobs=null;allocated=0;fragments=0; }
    private static bool Equal(byte[] a,byte[] b)
    { if(a==null || b==null || a.Length!=b.Length) return false;for(int i=0;i<a.Length;i++) if(a[i]!=b[i]) return false;return true; }
    private static byte[] Digest(byte[] bytes) { using(var sha=SHA256.Create()) return sha.ComputeHash(bytes); }
    private bool Same(RebirthPurgeProjectionFragmentCodec.Frame f)
    { return begin!=null && f.Snapshot==begin.Snapshot && f.Sequence==begin.Sequence && f.Revision==begin.Revision && f.Blobs==begin.Blobs && f.Aggregate==begin.Aggregate; }
    private bool Reject() { Discard();return false; }
    public bool TryAccept(object sequencingContext,RebirthPurgeProjectionFragmentCodec.Frame f,out StagedBytes staged)
    {
        staged=null;
        if(!ReferenceEquals(originalContext,sequencingContext) || f==null || f.World!=world || f.Epoch!=epoch || f.Generation!=generation) return Reject();
        if(f.Sequence<newestSequence || f.Revision<highestRevision) return false;
        if((f.Type==RebirthPurgeProjectionFragmentCodec.Kind.Begin || f.Type==RebirthPurgeProjectionFragmentCodec.Kind.Invalidate) &&
            f.Revision==highestRevision && !Equal(f.Hash,highestManifestHash)) return false;
        if(f.Type==RebirthPurgeProjectionFragmentCodec.Kind.Begin)
        {
            if(f.Revision<completedRevision || f.Sequence==newestSequence && !Same(f)) return false;
            if(Same(f)) return Equal(f.Hash,begin.Hash) || Reject();
            if(f.Revision==completedRevision && !Equal(f.Hash,completedManifestHash)) return false;
            Discard();newestSequence=f.Sequence;highestRevision=f.Revision;highestManifestHash=f.Hash;begin=f;blobs=new Blob[f.Blobs];return true;
        }
        if(f.Type==RebirthPurgeProjectionFragmentCodec.Kind.Invalidate)
        {
            if(f.Sequence<newestSequence || f.Revision<completedRevision) return false;
            if(f.Revision==completedRevision && completedManifestHash!=null && !Equal(f.Hash,completedManifestHash)) return false;
            newestSequence=f.Sequence;highestRevision=f.Revision;highestManifestHash=f.Hash;Discard();completedManifestHash=f.Hash;completedRevision=Math.Max(completedRevision,f.Revision);return true;
        }
        if(!Same(f)) return false;
        if(f.Type==RebirthPurgeProjectionFragmentCodec.Kind.Fragment)
        {
            var b=blobs[f.Blob];byte[] hash=f.Hash;
            if(b==null)
            {
                if(f.BlobLength>begin.Aggregate-allocated) return Reject();
                b=new Blob(f.BlobLength,hash);allocated+=f.BlobLength;blobs[f.Blob]=b;
            }
            if(b.Bytes.Length!=f.BlobLength || !Equal(b.Hash,hash)) return Reject();
            int part=f.Offset/RebirthPurgeProjectionFragmentCodec.PayloadLimit;byte[] bytes=f.Payload;
            if(b.Present[part])
            { for(int i=0;i<bytes.Length;i++) if(b.Bytes[f.Offset+i]!=bytes[i]) return Reject();return true; }
            if(++fragments>RebirthPurgeProjectionFragmentCodec.AggregateLimit/RebirthPurgeProjectionFragmentCodec.PayloadLimit+RebirthPurgeProjectionFragmentCodec.BlobCountLimit) return Reject();
            Buffer.BlockCopy(bytes,0,b.Bytes,f.Offset,bytes.Length);b.Present[part]=true;b.Received+=bytes.Length;return true;
        }
        if(f.Type!=RebirthPurgeProjectionFragmentCodec.Kind.Fence || !Equal(f.Hash,begin.Hash) || allocated!=begin.Aggregate) return Reject();
        foreach(var b in blobs) if(b==null || b.Received!=b.Bytes.Length || !Equal(Digest(b.Bytes),b.Hash)) return Reject();
        if(!Equal(blobs[0].Hash,begin.Hash)) return Reject();
        var result=new byte[blobs.Length][];for(int i=0;i<result.Length;i++) result[i]=blobs[i].Bytes;
        staged=new StagedBytes(begin,result);completedRevision=begin.Revision;completedManifestHash=begin.Hash;Discard();return true;
    }
}


