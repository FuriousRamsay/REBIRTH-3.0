// Actual canonical history description reader. Authentication, native path
// binding and source provenance are not simulated as native evidence.
static class TradeUnusedHistoryFixture
{
 // ACTUAL_UNUSED_DESCRIPTION
 internal static void Run(RebirthNpcTradeOriginal original)
 {
  string Body(string reason)=>new XElement("npcTradeUnused",new XAttribute("version",1),new XAttribute("reason",reason),new XAttribute("originalDigest",original.Digest),new XElement("original",original.Image)).ToString(SaveOptions.DisableFormatting);
  void Refuse(string image){try{Describe(image,out _);}catch(InvalidDataException){return;}catch(XmlException){return;}throw new Exception("Invalid unused history accepted");}
  foreach(string reason in new[]{"expired","stockChanged"})if(Describe(Body(reason),out var read).Image!=original.Image||read!=reason)throw new Exception("Unused history lost immutable original");
  Refuse(Body("missingGrant"));Refuse(Body("expired").Replace(original.Digest,new string('f',64)));
  Refuse(Body("expired").Replace("version=\"1\"","version=\"01\""));
  var changed=XElement.Parse(Body("expired"));changed.SetAttributeValue("extra","1");Refuse(changed.ToString(SaveOptions.DisableFormatting));
  changed=XElement.Parse(Body("expired"));changed.Add(new XElement("original",original.Image));Refuse(changed.ToString(SaveOptions.DisableFormatting));
  Refuse("<!DOCTYPE npcTradeUnused [<!ENTITY x 'injected'>]>"+Body("expired"));
  Refuse("partial");
 }
}
