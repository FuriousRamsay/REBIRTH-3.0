using System;
using System.Security.Cryptography;
using System.Text;

// A UI fingerprint is only a source selection constraint. Custody always captures
// the authoritative native ItemValue; serialized client item data is never trusted.
public static class RebirthMusicSourceProof
{
    public const int Length=64;
    public static bool TryCreate(ItemValue value,out string proof)
    {
        proof=string.Empty;
        if(value==null||value.IsEmpty())return false;
        try
        {
            byte[] native=Convert.FromBase64String(RebirthMusicLibraryService.Encode(value));
            if(native.Length==0)return false;
            using(var hash=SHA256.Create())
            {
                var bytes=hash.ComputeHash(native);
                var text=new StringBuilder(Length);
                foreach(byte part in bytes)text.Append(part.ToString("x2",System.Globalization.CultureInfo.InvariantCulture));
                proof=text.ToString();return true;
            }
        }
        catch(Exception){return false;}
    }
    public static bool IsValid(string proof)
    {
        if(proof==null||proof.Length!=Length)return false;
        foreach(char c in proof)if(!(c>='0'&&c<='9')&&!(c>='a'&&c<='f'))return false;
        return true;
    }
    public static bool Matches(ItemValue value,string expected)
    {
        string actual;
        return IsValid(expected)&&TryCreate(value,out actual)&&string.Equals(actual,expected,StringComparison.Ordinal);
    }
}