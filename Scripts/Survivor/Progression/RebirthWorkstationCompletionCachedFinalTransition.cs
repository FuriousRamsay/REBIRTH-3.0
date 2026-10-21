using System;
using System.Xml.Linq;

internal static partial class RebirthWorkstationCompletionPipeline
{
    internal sealed class LoadedCachedFinalTransition:IDisposable
    {
        private readonly CachedPhysicalHostAction action;
        private readonly RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.SavedCachedFinalOwner owner;
        internal readonly byte[] Before;internal readonly int Offset;
        private IDisposable memory,retention;
        private LoadedCachedFinalTransition(CachedPhysicalHostAction action,RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.SavedCachedFinalOwner owner,IDisposable memory,IDisposable retention)
        {this.action=action;this.owner=owner;Before=(byte[])action.Issue.Cache.Native.Clone();Offset=action.Issue.Cache.Offset;this.memory=memory;this.retention=retention;}
        internal bool Current()=>memory!=null&&retention!=null&&ReferenceEquals(action.LoadedFinal,this)&&!action.PhysicalAccepted&&!action.PhysicalFinalObserved&&
            action.Current()&&owner.Current()&&action.Issue.Cache.SavedCurrent()&&action.Issue.Cache.NativeCurrent()&&
            RebirthProductiveEarnedNativeBody.SameExceptElapsed(action.Issue.Cache.Native,action.Issue.Cache.Offset,Before,Offset);
        internal bool Matches(XElement active,XElement delivery)=>Current()&&owner.Matches(action,active,delivery);
        internal bool Validate(XElement active,XElement delivery,byte[] proposed,int offset)=>Matches(active,delivery)&&
            ValidateCachedPhysicalDelivery(action,active,delivery,Before,Offset,proposed,offset,LoadedCachedFinalValidation);
        internal static bool TryInstall(CachedPhysicalHostAction action)
        {
            if(action?.LoadedFinal!=null)return action.LoadedFinal.Current();
            IDisposable memory=null,retention=null;LoadedCachedFinalTransition result=null;
            try
            {
                if(action?.Current()!=true||action.PhysicalAccepted||action.PhysicalFinalObserved||!action.Issue.Cache.SavedCurrent()||!action.Issue.Cache.NativeCurrent())return false;
                var owner=RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.SavedCachedFinalOwner.Capture(action);
                if(owner==null||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16*(owner.MeasureImage()+action.Issue.Cache.Native.LongLength)+65536),out memory)||
                    (retention=action.Issue.Cache.RetainResource())==null)return false;
                result=new LoadedCachedFinalTransition(action,owner,memory,retention);memory=retention=null;action.LoadedFinal=result;
                if(!result.Current()){action.LoadedFinal=null;return false;}result=null;return true;
            }
            catch{if(result!=null&&ReferenceEquals(action?.LoadedFinal,result))action.LoadedFinal=null;return false;}
            finally{result?.Dispose();retention?.Dispose();memory?.Dispose();}
        }
        public void Dispose(){retention?.Dispose();retention=null;memory?.Dispose();memory=null;}
    }
    internal sealed partial class CachedPhysicalHostAction
    {
        internal LoadedCachedFinalTransition LoadedFinal;
    }
}
