using System;
using System.Collections.Generic;
using System.Globalization;

#nullable disable

/// <summary>
/// PE-05 read-only universal search over the canonical Progression Graph.
/// Search never grants progression and never owns progression rules.
/// </summary>
public sealed class RebirthProgressionExplorerSearchResult
{
    public readonly string NodeId;
    public readonly RebirthProgressionGraphNodeType NodeType;
    public readonly string DisplayName;
    public readonly string Category;
    public readonly string Description;
    public readonly int Score;

    public RebirthProgressionExplorerSearchResult(string nodeId,RebirthProgressionGraphNodeType nodeType,string displayName,string category,string description,int score)
    {
        NodeId=nodeId??string.Empty;NodeType=nodeType;DisplayName=displayName??string.Empty;Category=category??string.Empty;Description=description??string.Empty;Score=score;
    }
}

public static class RebirthProgressionExplorerSearchService
{
    private sealed class SearchEntry
    {
        public RebirthProgressionGraphNode Node;public string Display,Id,Name,Category,Description,Type;
    }
    private static string cachedGraphHash=string.Empty,cachedLocaleMarker=string.Empty;
    private static SearchEntry[] cachedEntries=new SearchEntry[0];

    private static SearchEntry[] GetEntries()
    {
        string graphHash=RebirthProgressionGraphRegistry.SemanticHash??string.Empty;
        string localeMarker=Localization.Get("xuiRebirthProgressionExplorer")??string.Empty;
        if(cachedEntries.Length>0&&string.Equals(graphHash,cachedGraphHash,StringComparison.Ordinal)&&string.Equals(localeMarker,cachedLocaleMarker,StringComparison.Ordinal))return cachedEntries;
        RebirthProgressionGraphNode[] nodes=RebirthProgressionGraphRegistry.GetNodesSnapshot();List<SearchEntry> entries=new List<SearchEntry>(nodes.Length);
        for(int i=0;i<nodes.Length;i++)
        {
            RebirthProgressionGraphNode node=nodes[i];if(node==null||!IsExplorerNodeType(node.Type))continue;
            string display=ResolveDisplayName(node);string description=ResolveDescription(node);
            entries.Add(new SearchEntry{Node=node,Display=display,Id=Normalize(node.Id),Name=Normalize(display),Category=Normalize(node.Category),Description=Normalize(description),Type=Normalize(node.Type.ToString())});
        }
        cachedGraphHash=graphHash;cachedLocaleMarker=localeMarker;cachedEntries=entries.ToArray();return cachedEntries;
    }

    public static RebirthProgressionExplorerSearchResult[] Search(string query,int limit)
    {
        if(limit<1)limit=1;if(limit>32)limit=32;
        string normalized=Normalize(query);if(normalized.Length==0)return new RebirthProgressionExplorerSearchResult[0];
        string[] tokens=normalized.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
        SearchEntry[] entries=GetEntries();
        List<RebirthProgressionExplorerSearchResult> results=new List<RebirthProgressionExplorerSearchResult>();
        for(int i=0;i<entries.Length;i++)
        {
            SearchEntry entry=entries[i];RebirthProgressionGraphNode node=entry.Node;
            int score=Score(normalized,tokens,entry.Id,entry.Name,entry.Category,entry.Description,entry.Type);if(score<=0)continue;
            results.Add(new RebirthProgressionExplorerSearchResult(node.Id,node.Type,entry.Display,node.Category,ResolveDescription(node),score));
        }
        results.Sort(Compare);
        if(results.Count>limit)results.RemoveRange(limit,results.Count-limit);
        return results.ToArray();
    }

    private static bool IsExplorerNodeType(RebirthProgressionGraphNodeType type)
    {
        return type==RebirthProgressionGraphNodeType.Background||
               type==RebirthProgressionGraphNodeType.Skill||
               type==RebirthProgressionGraphNodeType.Knowledge||
               type==RebirthProgressionGraphNodeType.Action||
               type==RebirthProgressionGraphNodeType.Recipe||
               type==RebirthProgressionGraphNodeType.Discipline;
    }

    private static int Score(string query,string[] tokens,string id,string name,string category,string description,string type)
    {
        int score=0;
        if(name==query)score+=1400;else if(id==query)score+=1350;
        if(name.StartsWith(query,StringComparison.Ordinal))score+=900;else if(id.StartsWith(query,StringComparison.Ordinal))score+=850;
        if(name.IndexOf(query,StringComparison.Ordinal)>=0)score+=650;
        if(id.IndexOf(query,StringComparison.Ordinal)>=0)score+=550;
        if(category.IndexOf(query,StringComparison.Ordinal)>=0)score+=260;
        if(type.IndexOf(query,StringComparison.Ordinal)>=0)score+=220;
        if(description.IndexOf(query,StringComparison.Ordinal)>=0)score+=160;

        int matched=0;
        for(int i=0;i<tokens.Length;i++)
        {
            string token=tokens[i];if(token.Length==0)continue;
            bool found=false;
            if(name.IndexOf(token,StringComparison.Ordinal)>=0){score+=180;found=true;}
            else if(id.IndexOf(token,StringComparison.Ordinal)>=0){score+=150;found=true;}
            else if(category.IndexOf(token,StringComparison.Ordinal)>=0){score+=90;found=true;}
            else if(type.IndexOf(token,StringComparison.Ordinal)>=0){score+=80;found=true;}
            else if(description.IndexOf(token,StringComparison.Ordinal)>=0){score+=55;found=true;}
            if(found)matched++;
        }
        if(tokens.Length>1)
        {
            if(matched==tokens.Length)score+=300;else if(matched==0)return 0;else score-=150*(tokens.Length-matched);
        }
        return score;
    }

    private static int Compare(RebirthProgressionExplorerSearchResult a,RebirthProgressionExplorerSearchResult b)
    {
        int c=b.Score.CompareTo(a.Score);if(c!=0)return c;c=string.Compare(a.DisplayName,b.DisplayName,StringComparison.OrdinalIgnoreCase);if(c!=0)return c;return string.Compare(a.NodeId,b.NodeId,StringComparison.OrdinalIgnoreCase);
    }

    public static string ResolveDisplayName(RebirthProgressionGraphNode node)
    {
        if(node==null)return string.Empty;
        if(!string.IsNullOrEmpty(node.NameKey)){string value=Localization.Get(node.NameKey);if(!string.IsNullOrEmpty(value)&&value!=node.NameKey)return value;}
        if(!string.IsNullOrEmpty(node.DisplayName))
        {
            string display=node.DisplayName;
            const string trainingPrefix="Training: ";
            if(node.Type==RebirthProgressionGraphNodeType.Action && display.StartsWith(trainingPrefix,StringComparison.OrdinalIgnoreCase))
            {
                string referencedId=display.Substring(trainingPrefix.Length).Trim();
                RebirthProgressionGraphNode referenced;
                if(RebirthProgressionGraphRegistry.TryGetNode(referencedId,out referenced)&&referenced!=null)return trainingPrefix+ResolveDisplayName(referenced);
            }

            // Recipe graph nodes often store the native recipe/item localization key as DisplayName
            // (for example electricfencepost / tripwirepost). In 7DTD those names are also
            // localization keys, so always resolve them before exposing the raw identifier.
            string localizedDisplay=Localization.Get(display);
            if(!string.IsNullOrEmpty(localizedDisplay)&&localizedDisplay!=display)return localizedDisplay;
            return HumanizeIdentifier(display);
        }

        string fallback=node.Id??string.Empty;
        int dot=fallback.LastIndexOf('.');
        if(dot>=0&&dot+1<fallback.Length)fallback=fallback.Substring(dot+1);
        string localizedFallback=Localization.Get(fallback);
        if(!string.IsNullOrEmpty(localizedFallback)&&localizedFallback!=fallback)return localizedFallback;
        return HumanizeIdentifier(fallback);
    }

    private static string HumanizeIdentifier(string value)
    {
        if(string.IsNullOrWhiteSpace(value))return string.Empty;
        string v=value.Trim().Replace('_',' ').Replace('-',' ').Replace('.',' ');
        System.Text.StringBuilder b=new System.Text.StringBuilder(v.Length+8);
        for(int i=0;i<v.Length;i++)
        {
            char c=v[i];
            if(i>0&&char.IsUpper(c)&&char.IsLetterOrDigit(v[i-1])&&!char.IsUpper(v[i-1]))b.Append(' ');
            b.Append(c);
        }
        string[] parts=b.ToString().Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
        for(int i=0;i<parts.Length;i++)
            if(parts[i].Length>0)parts[i]=char.ToUpperInvariant(parts[i][0])+parts[i].Substring(1);
        return string.Join(" ",parts);
    }

    public static string ResolveDescription(RebirthProgressionGraphNode node)
    {
        if(node==null)return string.Empty;string value=node.Description??string.Empty;
        if(value.Length>0 && (value.StartsWith("xui",StringComparison.OrdinalIgnoreCase)||value.StartsWith("lbl",StringComparison.OrdinalIgnoreCase))){string localized=Localization.Get(value);if(!string.IsNullOrEmpty(localized)&&localized!=value)return localized;}
        return value;
    }

    private static string Normalize(string value)
    {
        if(string.IsNullOrEmpty(value))return string.Empty;char[] chars=value.Trim().ToLowerInvariant().ToCharArray();
        for(int i=0;i<chars.Length;i++)if(!char.IsLetterOrDigit(chars[i]))chars[i]=' ';
        return string.Join(" ",new string(chars).Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries));
    }
}
