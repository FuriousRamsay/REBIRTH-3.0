using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
// PREPARED, NOT RUN. Separate offline process; never load this into a running game.
// Uses actual HTTP parsing/HandleContext/BridgeRequest; no native gameplay pump.
internal static class RebirthBridgeAccountingFixture
{
    internal static void Verify()
    {
        var bridge=typeof(RebirthGameBridge);
        var tokenField=bridge.GetField("token",BindingFlags.Static|BindingFlags.NonPublic);
        var handler=bridge.GetMethod("HandleContext",BindingFlags.Static|BindingFlags.NonPublic);
        var pending=(ConcurrentQueue<BridgeRequest>)bridge.GetField("Pending",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public).GetValue(null);
        if(!pending.IsEmpty)throw new Exception("Isolated request queue required.");
        int baseline=BridgeRequest.OpenCount;object oldToken=tokenField.GetValue(null);
        string token=Guid.NewGuid().ToString("N");tokenField.SetValue(null,token);
        var probe=new TcpListener(IPAddress.Loopback,0);probe.Start();int port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
        using(var listener=new HttpListener())
        {
            listener.Prefixes.Add("http://127.0.0.1:"+port+"/");listener.Start();
            try
            {
                void Exchange(string path,bool complete=false)
                {
                    var context=listener.GetContextAsync();
                    var response=Task.Run(()=>{using(var client=new WebClient()){client.Headers["X-Bridge-Token"]=token;try{return client.DownloadString("http://127.0.0.1:"+port+path);}catch(WebException error){if(error.Response==null)throw;using(var stream=error.Response.GetResponseStream())using(var reader=new System.IO.StreamReader(stream))return reader.ReadToEnd();}}});
                    if(!context.Wait(5000))throw new Exception("Fixture HTTP accept timed out.");
                    var serving=Task.Run(()=>handler.Invoke(null,new object[]{context.Result}));
                    if(complete)
                    {
                        BridgeRequest request=null;var until=DateTime.UtcNow.AddSeconds(5);
                        while(!pending.TryDequeue(out request)&&DateTime.UtcNow<until)Thread.Sleep(5);
                        if(request==null)throw new Exception("Parsed action was not queued.");
                        request.Complete(new JObject{["fixture"]=true});
                        request.Complete(new JObject());
                    }
                    if(!serving.Wait(5000)||!response.Wait(5000))throw new Exception("Fixture response timed out.");
                    if(BridgeRequest.OpenCount!=baseline)throw new Exception("Request accounting leaked: "+path);
                }
                for(int i=0;i<100;i++)Exchange("/ping");
                Exchange("/fixture?timeout=0");Exchange("/fixture?timeout=2147483647");
                Exchange("/fixture?timeout=2",true);
                Exchange("/fixture?timeout=1");
                while(pending.TryDequeue(out var abandoned))
                {if(!abandoned.IsAbandoned)throw new Exception("Timed-out request remained executable.");abandoned.Complete(new JObject());}
                if(BridgeRequest.OpenCount!=baseline)throw new Exception("Late completion changed settled count.");
            }
            finally{listener.Stop();tokenField.SetValue(null,oldToken);}
        }
    }
}
