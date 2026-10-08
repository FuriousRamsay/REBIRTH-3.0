using System;
using System.Collections.Generic;
using System.Linq;

// Original callback accumulator. A lookup alone never enters this receipt set.
// The native adapter must supply original Find/Create/Add and enclosing copy outcomes.
internal sealed class RebirthPoiNativeCopyReceiptScope
{
    internal sealed class Creation
    {
        internal readonly RebirthPoiNativeAuthoredManifest.Slot Slot;
        internal readonly object[] Arguments;
        internal object Candidate;
        internal int AddedId=-1;
        internal bool Added,Finished;
        internal Creation(RebirthPoiNativeAuthoredManifest.Slot slot,object[] args){Slot=slot;Arguments=(object[])args.Clone();}
    }
    internal readonly RebirthPoiNativeAuthoredManifest Manifest;
    internal readonly Guid Transaction;
    internal readonly object CopyOwner;
    private readonly Func<bool> current;
    private readonly Dictionary<string,Tuple<int,object>> provisional=new Dictionary<string,Tuple<int,object>>(StringComparer.Ordinal);
    private readonly Dictionary<string,RebirthPoiAuthoredRuntimeBinding> published;
    private Creation active;
    private bool failed,finished;
    internal RebirthPoiNativeCopyReceiptScope(RebirthPoiNativeAuthoredManifest manifest,Guid transaction,object copyOwner,Func<bool> originalCurrent,Dictionary<string,RebirthPoiAuthoredRuntimeBinding> originalReceipts)
    {
        if(manifest==null||transaction==Guid.Empty||copyOwner==null||originalCurrent==null||originalReceipts==null)throw new ArgumentException("Original copy scope required.");
        Manifest=manifest;Transaction=transaction;CopyOwner=copyOwner;current=originalCurrent;published=originalReceipts;
    }
    internal bool IsCurrent
    {get{try{return !failed&&!finished&&Manifest.IsOriginalAuthoredCurrent&&current();}catch{return false;}}}
    internal bool FindCompleted(object list,World world,Vector3i minimum,Vector3i maximum,int result,bool originalRan)
    {
        if(!IsCurrent||!originalRan||!ReferenceEquals(world,Manifest.World)){Fail();return false;}
        var slot=Manifest.Slots.SingleOrDefault(s=>ReferenceEquals(s.AuthoredList,list)&&s.WorldMinimum.Equals(minimum)&&s.WorldMaximum.Equals(maximum));
        if(slot==null){Fail();return false;}
        // Native absence is an observation only; it cannot qualify reuse.
        if(result<0)return !slot.Expected.OriginalNativeId.HasValue;
        int actual;object runtime;
        if(!slot.TryReadRuntime(out actual,out runtime)||actual!=result){Fail();return false;}
        // Newly registered slots may be found again in later chunks only after
        // their earlier original Create/Add/copy receipt exists in this transaction.
        RebirthPoiAuthoredRuntimeBinding earlier;
        if(!slot.Expected.OriginalNativeId.HasValue&&(!published.TryGetValue(slot.Expected.Key,out earlier)||earlier.NativeId!=actual)){Fail();return false;}
        return Stage(slot,actual,runtime);
    }
    internal Creation BeginCreation(object list,object[] args)
    {
        if(!IsCurrent||active!=null||args==null||args.Length!=8){Fail();return null;}
        var slot=Manifest.Slots.SingleOrDefault(s=>ReferenceEquals(s.AuthoredList,list)&&args[0] is int&&s.Expected.Index==(int)args[0]&&ReferenceEquals(s.Definition,args[1]));
        if(slot==null||slot.Expected.OriginalNativeId.HasValue||!ReferenceEquals(args[5],Manifest.World)||!(args[2] is Vector3i)||!((Vector3i)args[2]).Equals(Manifest.Prefab.boundingBoxPosition)||!(args[6] is Vector3i)||!(args[7] is Vector3i)||!slot.WorldMinimum.Equals((Vector3i)args[6])||!slot.WorldMaximum.Equals((Vector3i)args[7])){Fail();return null;}
        active=new Creation(slot,args);return active;
    }
    internal bool AddCompleted(Creation creation,World world,object candidate,int result,bool originalRan)
    {
        if(!IsCurrent||creation==null||!ReferenceEquals(active,creation)||creation.Added||!originalRan||!ReferenceEquals(world,Manifest.World)||candidate==null||result<0){Fail();return false;}
        int actual;object runtime;
        if(!creation.Slot.TryReadRuntime(out actual,out runtime)||actual!=result||!ReferenceEquals(runtime,candidate)){Fail();return false;}
        creation.Candidate=candidate;creation.AddedId=result;creation.Added=true;return true;
    }
    internal bool CreationCompleted(Creation creation,object list,object[] args,int result,bool originalRan)
    {
        if(!IsCurrent||creation==null||!ReferenceEquals(active,creation)||!originalRan||!creation.Added||creation.Finished||!ReferenceEquals(list,creation.Slot.AuthoredList)||args==null||args.Length!=8||result!=creation.AddedId){Fail();return false;}
        for(int i=0;i<8;i++)if(!Equals(args[i],creation.Arguments[i])){Fail();return false;}
        creation.Finished=true;active=null;return Stage(creation.Slot,result,creation.Candidate);
    }
    private bool Stage(RebirthPoiNativeAuthoredManifest.Slot slot,int id,object runtime)
    {
        Tuple<int,object> prior;
        if(provisional.TryGetValue(slot.Expected.Key,out prior))
        {if(prior.Item1!=id||!ReferenceEquals(prior.Item2,runtime)){Fail();return false;}return true;}
        provisional.Add(slot.Expected.Key,Tuple.Create(id,runtime));return true;
    }
    internal bool CopyCompleted(object owner,bool originalRan,out IReadOnlyList<RebirthPoiAuthoredRuntimeBinding> receipts)
    {
        receipts=null;
        if(!IsCurrent||active!=null||!ReferenceEquals(owner,CopyOwner)||!originalRan){Fail();return false;}
        var next=new List<RebirthPoiAuthoredRuntimeBinding>();
        foreach(var item in provisional)
        {
            var slot=Manifest.Slots.Single(s=>s.Expected.Key==item.Key);int actual;object runtime;
            if(!slot.TryReadRuntime(out actual,out runtime)||actual!=item.Value.Item1||!ReferenceEquals(runtime,item.Value.Item2)){Fail();return false;}
            RebirthPoiAuthoredRuntimeBinding receipt;
            if(published.TryGetValue(item.Key,out receipt))
            {if(receipt.NativeId!=actual||receipt.Descriptor!=slot.Expected.Descriptor){Fail();return false;}}
            else receipt=new RebirthPoiAuthoredRuntimeBinding(item.Key,slot.Expected.Descriptor,actual,Guid.NewGuid());
            next.Add(receipt);
        }
        // Publish only after validating every provisional outcome. Failed copy
        // never manufactures a partial positive registration receipt.
        foreach(var receipt in next)published[receipt.ExpectationKey]=receipt;
        finished=true;receipts=Array.AsReadOnly(next.ToArray());return true;
    }
    internal bool Admits(RebirthPoiNativeAuthoredManifest.Slot slot,int id,object runtime)
    {
        if(failed||slot==null||runtime==null)return false;
        Tuple<int,object> proof;return provisional.TryGetValue(slot.Expected.Key,out proof)&&proof.Item1==id&&ReferenceEquals(proof.Item2,runtime);
    }
    internal void Fail(){failed=true;active=null;provisional.Clear();}
}