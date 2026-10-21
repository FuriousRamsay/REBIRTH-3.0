using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.Scripting;

#nullable disable

public static class RebirthNpcInteractionPayloadCodec
{
    public static List<string> SplitEscaped(string value, char delimiter)
    {
        List<string> result=new List<string>(); if(value==null){result.Add(string.Empty);return result;}
        int start=0; bool escaped=false;
        for(int i=0;i<value.Length;i++)
        {
            char c=value[i];
            if(escaped){escaped=false;continue;}
            if(c=='\\'){escaped=true;continue;}
            if(c==delimiter){result.Add(value.Substring(start,i-start));start=i+1;}
        }
        result.Add(value.Substring(start)); return result;
    }
    public static int IndexOfUnescaped(string value,char delimiter)
    {
        bool escaped=false; for(int i=0;i<(value??string.Empty).Length;i++)
        {char c=value[i];if(escaped){escaped=false;continue;}if(c=='\\'){escaped=true;continue;}if(c==delimiter)return i;}return -1;
    }
    public static string UnescapeBackslash(string value)
    {
        if(string.IsNullOrEmpty(value))return string.Empty;System.Text.StringBuilder b=new System.Text.StringBuilder(value.Length);bool escaped=false;
        for(int i=0;i<value.Length;i++){char c=value[i];if(escaped){b.Append(c);escaped=false;}else if(c=='\\')escaped=true;else b.Append(c);}if(escaped)b.Append('\\');return b.ToString();
    }
    public static Dictionary<string,string> ParseBackslashMap(string payload)
    {
        Dictionary<string,string> result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        List<string> parts=SplitEscaped(payload,';'); if(parts.Count>32)return result;
        for(int i=0;i<parts.Count;i++){int at=IndexOfUnescaped(parts[i],'=');if(at<=0)continue;string key=UnescapeBackslash(parts[i].Substring(0,at));string value=UnescapeBackslash(parts[i].Substring(at+1));if(key.Length>0)result[key]=value;}return result;
    }
}

