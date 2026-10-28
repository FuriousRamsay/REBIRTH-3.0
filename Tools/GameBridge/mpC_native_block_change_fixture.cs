using System;
using System.Collections.Generic;
using System.Linq;

// Deferred native-context codec fixture only. An actual captured native DTO is
// required; this fixture grants no original action/payment/placement authority.
internal static class RemoteOriginalNativeBlockChangeChecks
{
    private static void Require(bool value,string label){if(!value)throw new Exception(label);}
    internal static void Run(IReadOnlyList<BlockChangeInfo> actualNativeChanges)
    {
        Require(RebirthOriginalNativeBlockChange.TryEncode(actualNativeChanges,out var original),"actual native change codec");
        Require(RebirthOriginalNativeBlockChange.TryDecode(original,out var decoded)&&decoded.Count==actualNativeChanges.Count,"bounded native read");
        Require(RebirthOriginalNativeBlockChange.TryEncode(decoded,out var encoded)&&encoded==original,"all native change flags/ref/density/texture preserved");
        Require(RebirthOriginalNativeBlockChange.TryEncode(actualNativeChanges,out var unchanged)&&unchanged==original,"detached roundtrip never mutates original native list");
        byte[] bytes=Convert.FromBase64String(original);var trailing=new byte[bytes.Length+1];Array.Copy(bytes,trailing,bytes.Length);
        Require(!RebirthOriginalNativeBlockChange.TryDecode(Convert.ToBase64String(trailing),out _),"trailing bytes refused");
        Require(!RebirthOriginalNativeBlockChange.TryEncode(Array.Empty<BlockChangeInfo>(),out _),"empty DTO not a generated action");
        Require(!RebirthOriginalNativeBlockChange.TryEncode(Enumerable.Repeat(actualNativeChanges[0],RebirthOriginalNativeBlockChange.MaximumChanges+1).ToArray(),out _),"oversized native list refused");
        Require(!RebirthOriginalNativeBlockChange.TryDecode(new string('A',((RebirthOriginalNativeBlockChange.MaximumBytes+2)/3)*4+4),out _),"oversized text rejected before decode");
    }
}
