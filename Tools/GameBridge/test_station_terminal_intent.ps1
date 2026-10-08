#requires -Version 7.0
$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Survivor/Persistence/RebirthStationTerminalIntent.cs'))
$fixture=@"
public class RebirthStationGridAdmission{public bool IsPublicationAttempted=true;public string JobId="job",CreationId="creation";public XElement Write(){return new XElement("admission",new XAttribute("job",JobId),new XAttribute("creation",CreationId),new XAttribute("attempted",IsPublicationAttempted));}}
public static class TerminalIntentFixture{static int checks;static void Check(bool value){checks++;if(!value)throw new Exception("intent assertion "+checks);}public static string Run(){var a=new RebirthStationGridAdmission();RebirthStationTerminalIntent completion,cancellation,parsed;
Check(RebirthStationTerminalIntent.TryCreate(a,true,42,out completion)&&completion.IsCompletion);
Check(RebirthStationTerminalIntent.TryCreate(a,false,42,out cancellation)&&!cancellation.IsCompletion);
Check(!RebirthStationTerminalIntent.TryCreate(a,true,0,out parsed)&&parsed==null);
var node=completion.Write();Check(RebirthStationTerminalIntent.TryRead(node,a,out parsed)&&parsed.IsCompletion);node.SetAttributeValue("outcome","invalid");Check(!RebirthStationTerminalIntent.TryRead(node,a,out parsed));Check(completion.Write().Attribute("outcome").Value=="completion");
node=completion.Write();node.SetAttributeValue("phase","settled");Check(!RebirthStationTerminalIntent.TryRead(node,a,out parsed));node=completion.Write();node.Add(new XAttribute("extra",1));Check(!RebirthStationTerminalIntent.TryRead(node,a,out parsed));
a.CreationId="foreign";Check(!RebirthStationTerminalIntent.TryRead(completion.Write(),a,out parsed));a.CreationId="creation";a.IsPublicationAttempted=false;Check(!RebirthStationTerminalIntent.TryRead(completion.Write(),a,out parsed));a.IsPublicationAttempted=true;
var admissions=new Dictionary<string,RebirthStationGridAdmission>{{a.JobId,a}};var intents=new Dictionary<string,RebirthStationTerminalIntent>{{a.JobId,completion}};Dictionary<string,RebirthStationTerminalIntent> loaded;
Check(RebirthStationTerminalIntent.ReadAll(new XElement("progression"),admissions,out loaded)&&loaded.Count==0);
var section=RebirthStationTerminalIntent.WriteAll(intents,admissions);Check(RebirthStationTerminalIntent.ReadAll(new XElement("progression",section),admissions,out loaded)&&loaded.Count==1&&loaded[a.JobId].IsCompletion);
section.Add(cancellation.Write());Check(!RebirthStationTerminalIntent.ReadAll(new XElement("progression",section),admissions,out loaded));
Check(!RebirthStationTerminalIntent.ReadAll(new XElement("progression",RebirthStationTerminalIntent.WriteAll(intents,admissions),RebirthStationTerminalIntent.WriteAll(intents,admissions)),admissions,out loaded));
Check(!RebirthStationTerminalIntent.ReadAll(new XElement("progression",RebirthStationTerminalIntent.WriteAll(intents,admissions)),new Dictionary<string,RebirthStationGridAdmission>(),out loaded));
var clone=completion.Clone();var image=clone.Write();image.SetAttributeValue("actor",99);Check(clone.Write().Attribute("actor").Value=="42"&&completion.Write().Attribute("actor").Value=="42");
return "PASS "+checks+" actual terminal intent codec checks with admission double; no native storage or settlement";}}
"@
Add-Type -TypeDefinition ($source+$fixture)
[TerminalIntentFixture]::Run()