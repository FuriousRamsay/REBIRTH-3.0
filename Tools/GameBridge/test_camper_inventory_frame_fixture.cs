// Test doubles deliberately make native Read lose texture: Restore must use the
// canonical codec for populated values rather than reading them directly.
public static class ThreadManager { public static bool Main=true; public static bool IsMainThread()=>Main; }
public sealed class ItemValue
{
    public int Type; public long Texture;
    public System.Collections.Generic.Dictionary<string,object> Metadata;
    public void Write(System.IO.BinaryWriter writer){writer.Write(Type);writer.Write(Texture);}
}
public sealed class ItemStack
{
    public ItemValue itemValue;public int count;
    public ItemStack(ItemValue value,int number){itemValue=value;count=number;}
}
public static class MemoryPools
{
    public static readonly Pool poolBinaryWriter=new Pool();
    public sealed class Pool{public Writer AllocSync(bool ignored)=>new Writer();}
    public sealed class Writer:System.IO.BinaryWriter
    {
        public System.Text.Encoding Encoding=System.Text.Encoding.UTF8;
        public Writer():base(new System.IO.MemoryStream()){}
        public void SetBaseStream(System.IO.Stream stream){OutStream=stream;}
    }
}
public static class RebirthNativeItemCodec
{
    public static bool TryDecode(string encoded,out ItemValue value)
    {
        value=null;var bytes=System.Convert.FromBase64String(encoded);
        if(bytes.Length!=12||System.BitConverter.ToInt32(bytes,0)<=0)return false;
        value=new ItemValue{Type=System.BitConverter.ToInt32(bytes,0),Texture=System.BitConverter.ToInt64(bytes,4)};return true;
    }
}
public static class CamperInventoryFrameChecks
{
    static void Check(bool ok,string message){if(!ok)throw new System.Exception(message);}
    static void Refuses(byte[] frame)
    {
        try{RebirthCamperInventorySnapshot.Restore(frame);}
        catch(System.IO.InvalidDataException){return;}
        throw new System.Exception("invalid frame accepted");
    }
    public static void Run()
    {
        var original=new[]{new ItemStack(new ItemValue(),0),new ItemStack(new ItemValue{Type=7,Texture=9123456789L},100000)};
        var frame=RebirthCamperInventorySnapshot.Capture(original);
        var decoded=RebirthCamperInventorySnapshot.Restore(frame);
        Check(decoded.Length==2&&decoded[0].count==0&&decoded[1].count==100000&&decoded[1].itemValue.Texture==9123456789L,"count/texture/empty lost");
        var trailing=new byte[frame.Length+1];System.Array.Copy(frame,trailing,frame.Length);Refuses(trailing);
        var malformed=(byte[])frame.Clone();System.Array.Copy(System.BitConverter.GetBytes(-1),0,malformed,8,4);Refuses(malformed);
        malformed=(byte[])frame.Clone();malformed[20]=1;Refuses(malformed); // malformed empty texture cannot hide behind codec refusal
        ThreadManager.Main=false;
        try{RebirthCamperInventorySnapshot.Restore(frame);throw new System.Exception("off-thread read accepted");}
        catch(System.InvalidOperationException){}finally{ThreadManager.Main=true;}
        System.Console.WriteLine("PASS actual snapshot frame: Int32 counts, canonical populated dispatch, exact empty, trailing/count corruption, thread fence; native codec NOT covered");
    }
}
