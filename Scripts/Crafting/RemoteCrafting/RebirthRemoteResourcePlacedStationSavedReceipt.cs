using System;
using System.IO;
using System.Text;

// Readonly exact installed EntityBuffs v3 / BuffValue v3 framing. Unlike native
// BuffValue.Read this validates/skips buff names and fixed fields without
// creating BuffValue objects or resolving BuffManager catalogue entries.
// A parsed scalar is not authority without the original SAME owner-file proof.
internal static class RebirthRemoteResourcePlacedStationSavedReceipt
{
    internal const int MaximumBytes=1024*1024; // Existing receipt-reader support, not native Int32 blob maximum.
    private sealed class Cursor
    {
        internal readonly byte[] Bytes;internal int Position;private static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        internal Cursor(byte[] bytes){Bytes=bytes;}
        internal void Need(int count){if(count<0||count>Bytes.Length-Position)throw new EndOfStreamException();}
        internal byte Byte(){Need(1);return Bytes[Position++];}
        internal int U16(){Need(2);int value=Bytes[Position]|Bytes[Position+1]<<8;Position+=2;return value;}
        internal void Skip(int count){Need(count);Position+=count;}
        internal bool TextEquals(byte[] wanted)
        {
            uint size=0;int groups=0;byte part;
            do{part=Byte();if(groups==4&&(part&248)!=0)throw new InvalidDataException();size|=(uint)(part&127)<<(groups*7);groups++;if(groups>5)throw new InvalidDataException();}while((part&128)!=0);
            if(groups>1&&(part&127)==0||size>Bytes.Length-Position)throw new InvalidDataException();int count=(int)size;
            Utf8.GetCharCount(Bytes,Position,count);bool same=wanted!=null&&wanted.Length==count;
            if(same)for(int index=0;index<count;index++)if(Bytes[Position+index]!=wanted[index]){same=false;break;}
            Position+=count;return same;
        }
        internal float Float(){Need(4);float value=BitConverter.ToSingle(Bytes,Position);Position+=4;return value;}
        internal string Text()
        {
            int start=Position;TextEquals(null);int end=Position;Position=start;
            uint length=0;int groups=0;byte part;do{part=Byte();length|=(uint)(part&127)<<(groups++*7);}while((part&128)!=0);
            string result=Utf8.GetString(Bytes,Position,(int)length);Position=end;return result;
        }
    }
    internal static bool TryRead(PlayerDataFile saved,string receipt,out float value,out bool present)
    {
        value=0;present=false;
        return saved?.buffData!=null&&saved.buffData.Length<=MaximumBytes&&TryRead(saved.buffData.ToArray(),receipt,out value,out present);
    }
    internal static bool TryRead(byte[] bytes,string receipt,out float value,out bool present)
    {
        value=0;present=false;
        try
        {
            if(bytes==null||bytes.Length<5||bytes.Length>MaximumBytes||receipt==null||receipt.Length>512||EntityBuffs.Version!=3)return false;
            var key=new UTF8Encoding(false,true).GetBytes(receipt);var reader=new Cursor(bytes);if(reader.Byte()!=3)return false;
            int count=reader.U16();if(count>(bytes.Length-reader.Position)/25)return false;
            for(int index=0;index<count;index++){reader.TextEquals(null);reader.Skip(24);}
            count=reader.U16();if(count>(bytes.Length-reader.Position)/5)return false;bool found=false;float observed=0;
            for(int index=0;index<count;index++)
            {
                bool match=reader.TextEquals(key);float scalar=reader.Float();if(!match)continue;
                if(found||float.IsNaN(scalar)||float.IsInfinity(scalar))return false;observed=scalar;found=true;
            }
            if(reader.Position!=bytes.Length)return false;value=observed;present=found;return true;
        }
        catch{return false;}
    }
    internal static bool TryCancellationBeforeKeys(PlayerDataFile saved,out Guid[] operations)=>TryCancellationKeys(saved,1f,out operations);
    internal static bool TryActionPaymentKeys(byte[] bytes,out Guid[] operations)
    {
        operations=null;
        try
        {
            if(bytes==null||bytes.Length<5||bytes.Length>MaximumBytes||EntityBuffs.Version!=3)return false;
            var reader=new Cursor(bytes);if(reader.Byte()!=3)return false;int count=reader.U16();if(count>(bytes.Length-reader.Position)/25)return false;
            for(int index=0;index<count;index++){reader.TextEquals(null);reader.Skip(24);}
            count=reader.U16();if(count>(bytes.Length-reader.Position)/5)return false;
            var keys=new System.Collections.Generic.HashSet<Guid>();const string prefix="rbActionPayment_";
            for(int index=0;index<count;index++)
            {
                string key=reader.Text();float value=reader.Float();if(!key.StartsWith(prefix,StringComparison.Ordinal))continue;
                string suffix=key.Substring(prefix.Length);
                if(!Guid.TryParseExact(suffix,"N",out var operation)||operation==Guid.Empty||suffix!=operation.ToString("N")||!keys.Add(operation)||
                    value!=1f&&value!=2f&&value!=3f&&value!=4f&&value!=5f&&value!=6f&&value!=8f||keys.Count>8)return false;
            }
            if(reader.Position!=bytes.Length)return false;operations=new Guid[keys.Count];keys.CopyTo(operations);return true;
        }
        catch{return false;}
    }
    internal static bool TryPlayerConsumptionKeys(byte[] bytes,out Guid[] operations)
    {
        operations=null;
        try
        {
            if(bytes==null||bytes.Length<5||bytes.Length>MaximumBytes||EntityBuffs.Version!=3)return false;
            var reader=new Cursor(bytes);if(reader.Byte()!=3)return false;int count=reader.U16();if(count>(bytes.Length-reader.Position)/25)return false;
            for(int index=0;index<count;index++){reader.TextEquals(null);reader.Skip(24);}
            count=reader.U16();if(count>(bytes.Length-reader.Position)/5)return false;
            var keys=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);var found=new System.Collections.Generic.HashSet<Guid>();
            string[] prefixes={"rbPlayerConsumeBefore_","rbPlayerConsumeSource_","rbPlayerConsumeSecondary_","rbPlayerConsumeUse_"};
            for(int index=0;index<count;index++)
            {
                string key=reader.Text();float value=reader.Float();int kind=-1;
                for(int candidate=0;candidate<prefixes.Length;candidate++)if(key.StartsWith(prefixes[candidate],StringComparison.Ordinal)){kind=candidate;break;}
                if(kind<0)continue;
                if(!keys.Add(key)||float.IsNaN(value)||float.IsInfinity(value))return false;
                string suffix=key.Substring(prefixes[kind].Length);
                if(!Guid.TryParseExact(suffix,"N",out var operation)||operation==Guid.Empty||suffix!=operation.ToString("N"))return false;
                bool allowed=kind==1?value==0f||value==1f||value==3f||(float)(int)value==value&&(value>=101f&&value<=289f||value<=-1f&&value>=-189f):
                    kind==0?value==0f||value==1f:value==0f||value==1f||value==2f;
                if(!allowed)return false;if(value!=0f){found.Add(operation);if(found.Count>32)return false;}
            }
            if(reader.Position!=bytes.Length)return false;operations=new Guid[found.Count];found.CopyTo(operations);return true;
        }
        catch{return false;}
    }
    internal static bool TryRecipeOutputKeys(byte[] bytes,out Guid[] operations)
    {
        operations=null;
        try
        {
            if(bytes==null||bytes.Length<5||bytes.Length>MaximumBytes||EntityBuffs.Version!=3)return false;
            var reader=new Cursor(bytes);if(reader.Byte()!=3)return false;int count=reader.U16();if(count>(bytes.Length-reader.Position)/25)return false;
            for(int index=0;index<count;index++){reader.TextEquals(null);reader.Skip(24);}
            count=reader.U16();if(count>(bytes.Length-reader.Position)/5)return false;
            const string prefix="rbRecipeOutput_";var keys=new System.Collections.Generic.HashSet<Guid>();var active=new System.Collections.Generic.List<Guid>();
            for(int index=0;index<count;index++)
            {
                string key=reader.Text();float value=reader.Float();if(!key.StartsWith(prefix,StringComparison.Ordinal))continue;
                string suffix=key.Substring(prefix.Length);
                if(!Guid.TryParseExact(suffix,"N",out var operation)||operation==Guid.Empty||suffix!=operation.ToString("N")||!keys.Add(operation)||
                    float.IsNaN(value)||float.IsInfinity(value)||value<0f||value>65f||value!=(int)value)return false;
                if(value>0f){active.Add(operation);if(active.Count>65)return false;}
            }
            if(reader.Position!=bytes.Length)return false;operations=active.ToArray();return true;
        }
        catch{return false;}
    }
    internal static bool TryNpcConsumptionKeys(byte[] bytes,out Guid[] operations)
    {
        operations=null;
        try
        {
            if(bytes==null||bytes.Length<5||bytes.Length>MaximumBytes||EntityBuffs.Version!=3)return false;
            var reader=new Cursor(bytes);if(reader.Byte()!=3)return false;int count=reader.U16();if(count>(bytes.Length-reader.Position)/25)return false;
            for(int index=0;index<count;index++){reader.TextEquals(null);reader.Skip(24);}
            count=reader.U16();if(count>(bytes.Length-reader.Position)/5)return false;
            var keys=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);var found=new System.Collections.Generic.HashSet<Guid>();
            for(int index=0;index<count;index++)
            {
                string key=reader.Text();float value=reader.Float();if(!key.StartsWith("rbNpcConsume_",StringComparison.Ordinal))continue;
                if(!keys.Add(key)||float.IsNaN(value)||float.IsInfinity(value))return false;
                string suffix=key.Substring(13);if(suffix.EndsWith("_secondary",StringComparison.Ordinal))suffix=suffix.Substring(0,suffix.Length-10);
                else if(suffix.EndsWith("_before",StringComparison.Ordinal))suffix=suffix.Substring(0,suffix.Length-7);
                if(!Guid.TryParseExact(suffix,"N",out var operation)||operation==Guid.Empty||suffix!=operation.ToString("N")||value!=0f&&value!=1f&&value!=2f)return false;
                if(value!=0f){found.Add(operation);if(found.Count>32)return false;}
            }
            if(reader.Position!=bytes.Length)return false;operations=new Guid[found.Count];found.CopyTo(operations);return true;
        }
        catch{return false;}
    }
    internal static bool TryCancellationTerminalKeys(PlayerDataFile saved,out Guid[] operations)
        =>TryCancellationKeys(saved,3f,out operations);
    internal static bool TryCancellationSavedAfterKeys(PlayerDataFile saved,out Guid[] operations)
        =>TryCancellationKeys(saved,2f,out operations);
    private static bool TryCancellationKeys(PlayerDataFile saved,float selected,out Guid[] operations)
    {
        operations=null;try
        {
            if(saved?.buffData==null||saved.buffData.Length<5||saved.buffData.Length>MaximumBytes||EntityBuffs.Version!=3)return false;
            var bytes=saved.buffData.ToArray();var reader=new Cursor(bytes);if(reader.Byte()!=3)return false;
            int count=reader.U16();if(count>(bytes.Length-reader.Position)/25)return false;
            for(int i=0;i<count;i++){reader.TextEquals(null);reader.Skip(24);}
            count=reader.U16();if(count>(bytes.Length-reader.Position)/5)return false;
            var names=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);var result=new System.Collections.Generic.List<Guid>();
            const string prefix="RebirthPlacedCancelV1_";
            for(int i=0;i<count;i++)
            {
                string name=reader.Text();float value=reader.Float();
                if(!name.StartsWith(prefix,StringComparison.Ordinal))continue;
                if(!names.Add(name)||!Guid.TryParseExact(name.Substring(prefix.Length),"N",out var operation)||operation==Guid.Empty||float.IsNaN(value)||float.IsInfinity(value))return false;
                if(value==selected){if(result.Count>=32)return false;result.Add(operation);}
            }
            if(reader.Position!=bytes.Length)return false;operations=result.ToArray();return true;
        }
        catch{return false;}
    }
}
