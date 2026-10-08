#requires -Version 7.0
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$codec=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/NPC/WorldIntegration/RebirthNpcSpawnReplayCodec.cs'))
$fixture=@"
public static class ReplayCodecFixture{
 static int checks;static void Check(bool condition,string reason){if(!condition)throw new System.Exception(reason);checks++;}
 static bool Read(string xml){var doc=new System.Xml.XmlDocument();doc.LoadXml(xml);return RebirthNpcSpawnReplayCodec.TryRead(doc.DocumentElement,out var keys);}
 public static string Run(){
  Check(Read("<root/>"),"legacy rejected");
  Check(Read("<root><replays version='1'><replay key='spawn:a'/><replay key='promote:b'/></replays></root>"),"valid rejected");
  foreach(var bad in new[]{"<replays version='2'/>","<replays version='1'/><replays version='1'/>","<replays version='1'><replay key='spawn:a'/><replay key='spawn:a'/></replays>","<replays version='1'><replay key='spawn:'/></replays>","<replays version='1'><replay key='other:a'/></replays>","<replays version='1'><replay key='spawn:a' extra='x'/></replays>","<replays version='1'>bad</replays>","<replays version='1'><other/></replays>"})Check(!Read("<root>"+bad+"</root>"),"malformed accepted");
  Check(!RebirthNpcSpawnReplayCodec.ValidKey("spawn:"+new string('a',507)),"oversize accepted");
  Check(!RebirthNpcSpawnReplayCodec.ValidKey("promote:a\n"),"control accepted");
  return "PASS "+checks+" actual spawn replay codec checks; no native spawn/reload exercised";
 }
}
"@
Add-Type -TypeDefinition ($codec+$fixture)
[ReplayCodecFixture]::Run()