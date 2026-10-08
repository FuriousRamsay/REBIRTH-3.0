using System;
using System.Collections.Generic;
using System.Text;

#nullable disable

public static class RebirthLiteratureDuplicateVectorHarness
{
    public static string Run()
    {
        StringBuilder b=new StringBuilder();bool ok=true;
        RebirthLiteratureDefinition[] values=RebirthProgressionRuntimeConfig.GetLiteratureSnapshot();
        HashSet<string> markers=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> itemIds=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int theory=0,discovery=0;
        Check(b,ref ok,"catalogue non-empty",values!=null&&values.Length>0);
        if(values!=null)for(int i=0;i<values.Length;i++)
        {
            RebirthLiteratureDefinition d=values[i];
            if(d==null){Check(b,ref ok,"non-null definition",false);continue;}
            Check(b,ref ok,"unique item "+d.ItemId,!string.IsNullOrEmpty(d.ItemId)&&itemIds.Add(d.ItemId));
            if(string.Equals(d.Kind,"theory",StringComparison.OrdinalIgnoreCase))
            {
                theory++;Check(b,ref ok,"theory skill "+d.ItemId,!string.IsNullOrEmpty(d.SkillId));
                Check(b,ref ok,"theory amount "+d.ItemId,d.Amount>0f&&!float.IsNaN(d.Amount)&&!float.IsInfinity(d.Amount));
                Check(b,ref ok,"internal marker "+d.ItemId,!string.IsNullOrEmpty(d.MarkerId)&&RebirthLiteratureService.IsInternalReadMarker(d.MarkerId));
                Check(b,ref ok,"unique marker "+d.ItemId,!string.IsNullOrEmpty(d.MarkerId)&&markers.Add(d.MarkerId));
            }
            else if(string.Equals(d.Kind,"discovery",StringComparison.OrdinalIgnoreCase))
            {
                discovery++;Check(b,ref ok,"discovery target "+d.ItemId,!string.IsNullOrEmpty(d.KnowledgeId));
            }
            else Check(b,ref ok,"supported kind "+d.ItemId,false);
        }
        Check(b,ref ok,"theory partition present",theory>0);
        Check(b,ref ok,"discovery partition present",discovery>0);
        Check(b,ref ok,"theory markers one-to-one",markers.Count==theory);
        b.AppendLine("observed catalogue total="+(values==null?0:values.Length)+" theory="+theory+" discovery="+discovery+" uniqueTheoryMarkers="+markers.Count+"; cardinalities are observations, not versionless acceptance constants");
        b.Insert(0,ok?"PASS semantic-contract\n":"FAIL semantic-contract\n");return b.ToString().TrimEnd();
    }

    private static void Check(StringBuilder b,ref bool ok,string name,bool pass)
    {
        if(!pass)ok=false;
        b.Append(pass?"PASS ":"FAIL ").AppendLine(name);
    }
}
