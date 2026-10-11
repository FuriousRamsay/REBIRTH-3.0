using System;using System.Collections.Generic;using System.Diagnostics;
public readonly struct RebirthNpcStableId : IEquatable<RebirthNpcStableId>
{
    public readonly ulong High;
    public readonly ulong Low;

    public RebirthNpcStableId(ulong high, ulong low)
    {
        High = high;
        Low = low;
    }

    public bool IsEmpty => High == 0UL && Low == 0UL;

    public static bool TryParse(string value, out RebirthNpcStableId stableId)
    {
        stableId = default(RebirthNpcStableId);
        if (string.IsNullOrWhiteSpace(value)) return false;
        string normalized = value.Trim();
        if (normalized.Length != 32) return false;
        ulong high, low;
        if (!ulong.TryParse(normalized.Substring(0, 16), System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out high) ||
            !ulong.TryParse(normalized.Substring(16, 16), System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out low)) return false;
        stableId = new RebirthNpcStableId(high, low);
        return !stableId.IsEmpty;
    }

    public static RebirthNpcStableId NewId()
    {
        byte[] bytes = Guid.NewGuid().ToByteArray();
        return new RebirthNpcStableId(BitConverter.ToUInt64(bytes, 0), BitConverter.ToUInt64(bytes, 8));
    }

    public bool Equals(RebirthNpcStableId other) => High == other.High && Low == other.Low;
    public override bool Equals(object obj) => obj is RebirthNpcStableId other && Equals(other);
    public override int GetHashCode() => unchecked((High.GetHashCode() * 397) ^ Low.GetHashCode());
    public override string ToString() => High.ToString("x16") + Low.ToString("x16");
    public static bool operator ==(RebirthNpcStableId left, RebirthNpcStableId right) => left.Equals(right);
    public static bool operator !=(RebirthNpcStableId left, RebirthNpcStableId right) => !left.Equals(right);
}
public class RebirthNpcRuntimeState { public RebirthNpcStableId StableId; public RebirthNpcRuntimeState CloneForProjection()=>new RebirthNpcRuntimeState{StableId=StableId}; }
static class Before { static readonly object Sync=new object(); public static readonly Dictionary<int,RebirthNpcRuntimeState> ByEntityId=new Dictionary<int,RebirthNpcRuntimeState>(); public static RebirthNpcRuntimeState[] GetSnapshot()
    {
        lock (Sync)
        {
            RebirthNpcRuntimeState[] result = new RebirthNpcRuntimeState[ByEntityId.Count];
            int index = 0;
            foreach (RebirthNpcRuntimeState state in ByEntityId.Values) result[index++] = state.CloneForProjection();
            Array.Sort(result, (left, right) => string.Compare(
                left.StableId.ToString(), right.StableId.ToString(), StringComparison.Ordinal));
            return result;
        }
    } }
static class After { static readonly object Sync=new object(); public static readonly Dictionary<int,RebirthNpcRuntimeState> ByEntityId=new Dictionary<int,RebirthNpcRuntimeState>(); public static RebirthNpcRuntimeState[] GetSnapshot()
    {
        lock (Sync)
        {
            RebirthNpcRuntimeState[] result = new RebirthNpcRuntimeState[ByEntityId.Count];
            int index = 0;
            foreach (RebirthNpcRuntimeState state in ByEntityId.Values) result[index++] = state.CloneForProjection();
            // Stable IDs format as fixed-width High/Low hexadecimal, so unsigned
            // numeric ordering is identical without strings per sort comparison.
            Array.Sort(result, (left, right) =>
            {
                int high = left.StableId.High.CompareTo(right.StableId.High);
                return high != 0 ? high : left.StableId.Low.CompareTo(right.StableId.Low);
            });
            return result;
        }
    } }
class Program {
static Random random=new Random(741); static ulong U(){var b=new byte[8];random.NextBytes(b);return BitConverter.ToUInt64(b,0);}
static void Set(int n){Before.ByEntityId.Clear();After.ByEntityId.Clear();for(int i=0;i<n;i++){var x=new RebirthNpcRuntimeState{StableId=new RebirthNpcStableId(U(),U())};Before.ByEntityId[i]=x;After.ByEntityId[i]=x;}}
static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
static void Main(){
ulong[] edges={0,1,15,16,255,256,0x7fffffffffffffff,0x8000000000000000,ulong.MaxValue};
var ids=new List<RebirthNpcStableId>();foreach(var h in edges)foreach(var l in edges)ids.Add(new RebirthNpcStableId(h,l));
foreach(var a in ids)foreach(var b in ids){int old=string.Compare(a.ToString(),b.ToString(),StringComparison.Ordinal);int cmp=a.High.CompareTo(b.High);if(cmp==0)cmp=a.Low.CompareTo(b.Low);if(Math.Sign(old)!=Math.Sign(cmp))throw new Exception("Boundary ordering mismatch");}
Check(true,"6561 boundary pairs match ordinal hexadecimal order");
for(int run=0;run<1000;run++){Set(random.Next(0,101));var a=Before.GetSnapshot();var b=After.GetSnapshot();for(int i=0;i<a.Length;i++)if(!a[i].StableId.Equals(b[i].StableId))throw new Exception("Snapshot order mismatch");}
Check(true,"1000 randomized extracted snapshot comparisons match");
Set(1);var one=After.GetSnapshot();var two=After.GetSnapshot();Check(!ReferenceEquals(one,two)&&!ReferenceEquals(one[0],two[0])&&!ReferenceEquals(one[0],After.ByEntityId[0]),"Independent projection copies retained (state clone doubled)");
foreach(int n in new[]{64,512,2048}){Set(n);Measure("before",n,Before.GetSnapshot);Measure("after",n,After.GetSnapshot);}
}
static void Measure(string label,int n,Func<RebirthNpcRuntimeState[]> action){for(int i=0;i<5;i++)action();long start=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();for(int i=0;i<30;i++)action();watch.Stop();long bytes=GC.GetAllocatedBytesForCurrentThread()-start;Console.WriteLine(label+" count="+n+" bytes/snapshot="+(bytes/30.0).ToString("F1")+" us/snapshot="+(watch.Elapsed.TotalMilliseconds*1000/30).ToString("F1"));}
}