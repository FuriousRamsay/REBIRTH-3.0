using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
class PooledBinaryWriter:BinaryWriter{public PooledBinaryWriter(Stream stream):base(stream,Encoding.UTF8,true){}}
// ACTUAL_FRAGMENT_TYPES
static class ActualPlacement
{
// ACTUAL_FRAGMENT_WRITE
// ACTUAL_RELEASE_GATE
// ACTUAL_PURPOSE_OBJECTS
}
static class Program
{
    static void Need(bool value,string message){if(!value)throw new Exception(message);}
    static void Main()
    {
        // Framing payload, not a fabricated native entity or settlement receipt.
        var input=new byte[4*1024*1024];for(int n=0;n<input.Length;n++)input[n]=(byte)(n*31+n/257);
        var body=new RebirthRemoteResourceFragments.Body(input);var operation=Guid.NewGuid();int maximum=0;byte[] completed=null;
        for(int index=body.Count-1;index>=0;index--)
        {
            using(var stream=new MemoryStream())using(var writer=new PooledBinaryWriter(stream))
            {writer.Write(-1);writer.Write((ushort)1);ActualPlacement.WriteDeferredFragmentPayload(writer,7,operation,body.Hash,body.Length,body.Count,index,body.Part(index));writer.Flush();maximum=Math.Max(maximum,(int)stream.Length);Need(stream.Length<13*1024,"actual fragment writer exceeds13KiB");Need(stream.Length<32256,"native pre-full stage overflow");}
            Need(RebirthRemoteResourceFragments.TryAdd("fixture-own-scope",operation,body.Hash,body.Length,body.Count,index,body.Part(index),1,out completed),"reordered fragment rejected");
            if(index>0)Need(RebirthRemoteResourceFragments.TryAdd("fixture-own-scope",operation,body.Hash,body.Length,body.Count,index,body.Part(index),1,out var duplicate)&&duplicate==null,"duplicate changed assembly");
        }
        Need(completed!=null&&completed.SequenceEqual(input),"complete body not exact");
        var queue=new RebirthRemoteResourceRepairSendQueue();bool ready=false,current=true;int sends=0;Need(queue.Enqueue("one",body,()=>current,()=>ready,index=>sends++),"queue rejected");queue.Pump(4);Need(sends==0&&queue.Count==1,"pressure lost retained job");ready=true;queue.Pump(4);Need(sends==4,"per-frame send bound");current=false;queue.Pump(4);Need(queue.Count==0,"obsolete transport retained");
        Need(!ActualPlacement.CanReleaseBodyCustody(false,false),"release before any proof");Need(!ActualPlacement.CanReleaseBodyCustody(true,false),"body-only release");Need(!ActualPlacement.CanReleaseBodyCustody(false,true),"source-only release");Need(ActualPlacement.CanReleaseBodyCustody(true,true),"both proofs do not release");
        var packet=new byte[]{1,2,3};var identicalPacket=new byte[]{1,2,3};var creation=new byte[]{4,5};var identicalCreation=new byte[]{4,5};Need(ActualPlacement.SamePreparedBodyObjects(packet,packet,creation,creation),"exact purpose objects refused");Need(!ActualPlacement.SamePreparedBodyObjects(packet,identicalPacket,creation,creation),"identical-content packet inherited authority");Need(!ActualPlacement.SamePreparedBodyObjects(packet,packet,creation,identicalCreation),"substituted creation inherited authority");Need(!ActualPlacement.SamePreparedBodyObjects(null,null,creation,creation),"empty purpose inherited authority");
        Console.WriteLine("PASS actual fragment serializer max="+maximum+"; reordered exact framing, pressure and both-proof release predicate. Native integration NOT RUN.");
    }
}