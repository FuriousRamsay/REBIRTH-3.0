from pathlib import Path
p=Path('Scripts/UI/XUiC_RebirthPurgePoiPopup.cs');s=p.read_text();a=s.index('            string name=');b=s.index('            if(picture!=null)',a)
s=s[:a]+'''            var discoveryNames=Names(record.Discoverer);
            var names=Names(record.Contributors);
            bool discoveryKnown=discoveryNames.Length>0||record.DiscoveredAt>0;
            Set(discoverer,string.Join("\\n",discoveryNames.Select(n=>"[EBD276]"+n+"[-]")));
            Set(contributors,string.Join("\\n",names.Length==0?new[]{L("xuiRebirthPurgeHistoryUnknown")}:names.Select(n=>"[EBD276]"+n+"[-]").ToArray()));
            Set(Label("poiPopupAreasValue"),record.AreasLeft<0?"—":record.AreasLeft.ToString(CultureInfo.InvariantCulture));
            Set(Label("poiPopupEntitiesValue"),record.EstimatedEntities<0?"—":(record.Approximate?"~":"")+record.EstimatedEntities.ToString(CultureInfo.InvariantCulture));
            Set(discoveredTime,TimeText(record.DiscoveredAt));Set(purgedTime,TimeText(record.PurgedAt));
            int discoveryHeight=discoveryKnown?Math.Max(30,30*discoveryNames.Length):0;
            int purgeHeight=purged?Math.Max(30,30*names.Length):0;
            int height=246+discoveryHeight+purgeHeight;
            ViewComponent.Size=new Vector2i(620,height);
            if(background!=null)background.Size=new Vector2i(620,height);
            if(border!=null)border.Size=new Vector2i(620,height);
            LayoutAttribution("Discoverer",discoverer,discoveryHeight,-242,discoveryKnown);
            LayoutAttribution("Contributors",contributors,purgeHeight,-242-discoveryHeight,purged);
            SetDateRow("Discovered",record.DiscoveredAt,-257,discoveryKnown);
            SetDateRow("Purged",record.PurgedAt,-257-discoveryHeight,purged);
''' + s[b:]
pos=s.index('    private void SetDateRow(')
s=s[:pos]+'''    private static string[] Names(string text)=> (text??"").Split(new[]{'\\n'},StringSplitOptions.RemoveEmptyEntries)
        .Select(RebirthPurgeDiscoveryNotification.CleanDisplayName).Where(n=>n.Length>0).Distinct(StringComparer.Ordinal).ToArray();
    private void LayoutAttribution(string kind,XUiV_Label names,int height,int top,bool visible)
    {
        if(names!=null){names.Position=new Vector2i(164,top-height/2);names.Size=new Vector2i(272,height);names.IsVisible=visible;}
        var caption=Label("poiPopup"+kind+"Caption");
        if(caption!=null){caption.Position=new Vector2i(54,top-15);caption.IsVisible=visible;}
        var icon=Sprite(kind=="Discoverer"?"poiPopupDiscovererIcon":"poiPopupContributorsIcon");
        if(icon!=null){icon.Position=new Vector2i(20,top-3);icon.IsVisible=visible;}
    }
''' + s[pos:];p.write_text(s)
