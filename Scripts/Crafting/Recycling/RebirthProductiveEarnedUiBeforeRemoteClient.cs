using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiCoordinator
{
    private sealed class RemoteBefore:IDisposable
    {internal Original Source;internal byte[] Image;internal string Digest;internal Guid Nonce;internal bool Sent,Saved;internal double Next;internal IDisposable Retention;
        public void Dispose(){Image=null;Retention?.Dispose();Retention=null;}}
    private static readonly Dictionary<Original,RemoteBefore> remoteBefores=new Dictionary<Original,RemoteBefore>();
    private static bool EnsureRemoteBefore(Original original)
    {
        if(!original.Current()||!original.Ui.CurrentBefore())return false;
        if(!remoteBefores.TryGetValue(original,out var source))
        {
            var b=original.Binding;var flush=original.BeforeFlush;
            if(flush?.IsObserved!=true)return false;
            using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            {
                if(decoder==null)return false;var frame=original.Ui.ReadFrame();
                if(!RebirthProductiveEarnedNativeBody.SameExceptElapsed(flush.After,flush.AfterOffset,frame.BeforeNative,frame.BeforeElapsedOffset))return false;
                var node=new XElement("uiNativeBefore",new XAttribute("version",1),new XAttribute("operation",b.Operation.ToString("N")),new XAttribute("birth",b.Birth.ToString("N")),
                    new XAttribute("uio",original.Digest),new XAttribute("index",b.Index),new XAttribute("offset",frame.BeforeElapsedOffset),
                    new XElement("purpose",b.OwnerPurpose),new XElement("uiOriginal",Convert.ToBase64String(flush.Image)),
                    new XElement("after",Convert.ToBase64String(frame.BeforeNative)));
                byte[] image=Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting));
                if(image.Length>RebirthProductiveEarnedRowProof.MaximumArtifactBytes||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(8L*image.Length+65536),out var retention))return false;
                source=new RemoteBefore{Source=original,Image=image,Digest=Hash(image),Retention=retention};
                if(!WriteExact(original.Path+".nativeBefore",image)){source.Dispose();return false;}remoteBefores.Add(original,source);
            }
        }
        if(source.Saved)return true;
        var binding=original.Binding;
        if(RebirthProductiveEarnedWire.TryReadReply(binding.Operation,out var reply)&&reply.Digest==source.Digest&&reply.Birth==binding.Birth&&
            reply.Owner==binding.Owner&&reply.Creation==binding.Creation&&reply.Deaths==binding.Deaths&&reply.World==binding.WorldKey&&reply.Position.Equals(binding.Position))
        {
            if(reply.State==RebirthProductiveEarnedWire.Status.UiBeforeSaved&&source.Sent&&reply.Nonce==source.Nonce)
            {source.Saved=Stage(original,"UiNativeBeforeSaved");return source.Saved;}
            if(reply.State==RebirthProductiveEarnedWire.Status.UiBeforeHeader&&reply.Nonce!=Guid.Empty)
            {
                if(source.Nonce!=Guid.Empty&&source.Nonce!=reply.Nonce)return false;source.Nonce=reply.Nonce;
                if(!source.Sent)
                {
                    if(!Stage(original,"UiNativeBeforeUploadAttempted"))return false;
                    source.Sent=true;UploadUi(original); // one current native send; no timeout resend.
                }
            }
        }
        double now=Monotonic();if(now<source.Next)return false;source.Next=now+0.5;
        using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
            if(decoder!=null)RebirthProductiveEarnedWire.Send(binding.Actor,binding.Station,source.Sent?RebirthProductiveEarnedWire.Kind.UiBeforeQuery:RebirthProductiveEarnedWire.Kind.UiBeforeFlush,
                binding.Operation,source.Nonce,source.Sent?null:source.Image,source.Digest);
        return false;
    }
    internal static bool CanSendRemoteBefore(EntityPlayerLocal actor,TileEntityWorkstation station,Guid operation,string digest)
        =>remoteBefores.Values.Any(value=>ReferenceEquals(value.Source.Binding.Actor,actor)&&ReferenceEquals(value.Source.Binding.Station,station)&&
            value.Source.Binding.Operation==operation&&value.Digest==digest&&value.Source.Current()&&value.Source.Ui.CurrentBefore());
    private static void ReleaseRemoteBefore(Original original)
    {if(remoteBefores.TryGetValue(original,out var value)){remoteBefores.Remove(original);value.Dispose();}}
    private static void ReleaseRemoteBefores(){foreach(var value in remoteBefores.Values)value.Dispose();remoteBefores.Clear();}
}
