using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

public static partial class RemoteResourceTransactions
{
    internal static bool AllowActionSourceEscape(object endpoint,string boundary)
    {
        if(endpoint==null)return true;ActionPayment[] held;
        lock(actionOwnerGate)held=actionPayments.Where(value=>!value.Terminal&&!value.Retired&&
            (ReferenceEquals(value.ServiceTarget,endpoint)||value.Legs.Any(leg=>ReferenceEquals(leg.Endpoint,endpoint)))).ToArray();
        if(held.Length==0)return true;
        foreach(var payment in held)
        {
            lock(actionOwnerGate)
            {
                string source=ReferenceEquals(payment.ServiceTarget,endpoint)?"service-target":"source-"+payment.Legs.FindIndex(leg=>ReferenceEquals(leg.Endpoint,endpoint));
                string key=source+"|"+boundary;
                if(payment.RefusedBoundaries.Count<32*4+4&&!payment.RefusedBoundaries.ContainsKey(key))payment.RefusedBoundaries.Add(key,false);
            }
            try{FlushActionRefusals(payment);}catch{/* Native voluntary escape remains refused if metadata cannot be saved. */}
        }
        return false;
    }
    internal static bool AllowActionBlockEscape(WorldBase world,Vector3i position,string boundary)
    {
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world))return true;
        return AllowActionSourceEscape(world.GetTileEntity(position),boundary);
    }
    private static bool FlushActionRefusals(ActionPayment payment)
    {
        if(!ThreadManager.IsMainThread()||payment.Retired||payment.Original==null||!ReferenceEquals(GameManager.Instance?.World,payment.World)||
            Path.GetFullPath(GameIO.GetSaveGameDir())!=payment.Save)return false;
        KeyValuePair<string,bool>[] pending;lock(actionOwnerGate)pending=payment.RefusedBoundaries.Where(pair=>!pair.Value).ToArray();
        if(pending.Length==0)return true;
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out var scratch))return false;
        using(scratch)foreach(var refusal in pending)
        {
            var image=new XElement("actionVoluntaryBoundaryRefused",new XAttribute("version",1),new XAttribute("operation",payment.Operation.ToString("N")),
                new XElement("boundary",refusal.Key),new XElement("original",payment.Original));
            byte[] bytes=new UTF8Encoding(false,true).GetBytes(image.ToString(SaveOptions.DisableFormatting));if(bytes.Length>24*1024*1024)throw new InvalidDataException();
            // Hash is a filename locator only; every comparison retains the
            // full boundary and original native material, never hash authority.
            string name;using(var sha=SHA256.Create())name=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(refusal.Key))).Replace("-","");
            string root=Path.Combine(payment.Directory,"NativeRefusals");ActionPath(root);Directory.CreateDirectory(root);string path=Path.Combine(root,name+".xml");
            if(!File.Exists(path))using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);output.Flush(true);}
            if(!ActionBytes(path,bytes.Length).SequenceEqual(bytes))return false;
            lock(actionOwnerGate)payment.RefusedBoundaries[refusal.Key]=true;
        }
        return true;
    }
}
