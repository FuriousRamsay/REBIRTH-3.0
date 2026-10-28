// AUTHORED_NOT_RUN. Actual metadata consumer, with native-join/file doubles.
using System;using System.IO;using System.Linq;using System.Xml.Linq;using System.Collections.Generic;
class INetConnection{}
static class RebirthWorkstationCompletionOutbox{internal static string Root(string purpose)=>"root";}
static class RebirthWorkstationCompletionPublication
{internal static bool Writes=true,Matches=true;internal static XElement Written;internal static bool Write(string path,XElement value){Written=new XElement(value);return Writes;}
 internal static XElement ReadPrimary(string path)=>Matches?Written:new XElement("different");}
class Subject
{
 internal class RecyclerJoining{internal INetConnection Connection=new INetConnection();internal string Owner="owner",WorldKey="world",Creation="creation";internal int Deaths=2;
  internal Guid[] Pending;internal Dictionary<Guid,object> UnqualifiedStations=new Dictionary<Guid,object>();internal HashSet<Guid> Unstarted=new HashSet<Guid>();internal bool Disposed;
  public void Dispose(){Disposed=true;}}
 internal class JoinedUnused:IDisposable{internal Guid Operation,Nonce;internal string OriginalDigest="hash";internal RecyclerJoining Joined;internal XElement Original=new XElement("placedRecyclerOriginal");
  internal bool Valid=true,Disposed;internal bool Current()=>Valid;public void Dispose(){Disposed=true;joinedUnused.Remove(this);}}
 static List<JoinedUnused> joinedUnused=new List<JoinedUnused>();
 // PRODUCTION_REPLY
 static void Check(bool value,string name){if(!value)throw new Exception(name);}
 static JoinedUnused Original(bool sibling=false){var operation=Guid.NewGuid();var value=new JoinedUnused{Operation=operation,Nonce=Guid.NewGuid(),Joined=new RecyclerJoining{Pending=sibling?new[]{operation,Guid.NewGuid()}:new[]{operation}}};joinedUnused.Clear();joinedUnused.Add(value);return value;}
 static XElement Reply(JoinedUnused value)=>new XElement("recyclerRemoteUnused",new XAttribute("nonce",value.Nonce.ToString("N")),new XAttribute("original","hash"),
  new XAttribute("owner","owner"),new XAttribute("world","world"),new XAttribute("creation","creation"),new XAttribute("deaths",2),new XAttribute("savedUnused",true));
 static void Main()
 {
  var value=Original();Check(ObserveJoinedRecyclerUnusedReply(Reply(value),value.Joined.Connection,value.Operation)&&value.Disposed&&value.Joined.Disposed,"positive saved-unused releases exact last join only");
  value=Original(true);Check(ObserveJoinedRecyclerUnusedReply(Reply(value),value.Joined.Connection,value.Operation)&&value.Joined.Pending.Length==1&&!value.Joined.Disposed,"other original retained");
  value=Original();Check(!ObserveJoinedRecyclerUnusedReply(Reply(value),new INetConnection(),value.Operation)&&!value.Disposed,"foreign actual connection ignored");
  value=Original();value.Nonce=Guid.Empty;var beforeReply=Reply(value);beforeReply.SetAttributeValue("nonce",Guid.NewGuid().ToString("N"));
  Check(ObserveJoinedRecyclerUnusedReply(beforeReply,value.Joined.Connection,value.Operation)&&value.Disposed,"actual pre-reply close accepts authenticated positive unused nonce only");
  foreach(string name in new[]{"nonce","original","owner","world","creation","deaths","savedUnused"})
  {value=Original();var reply=Reply(value);reply.SetAttributeValue(name,name=="deaths"?"9":name=="savedUnused"?"false":"wrong");Check(ObserveJoinedRecyclerUnusedReply(reply,value.Joined.Connection,value.Operation)&&!value.Disposed,"mismatch retained: "+name);}
  value=Original();value.Valid=false;ObserveJoinedRecyclerUnusedReply(Reply(value),value.Joined.Connection,value.Operation);Check(!value.Disposed,"unknown native join retained");
  value=Original();RebirthWorkstationCompletionPublication.Writes=false;ObserveJoinedRecyclerUnusedReply(Reply(value),value.Joined.Connection,value.Operation);Check(!value.Disposed,"write failure retained");
  RebirthWorkstationCompletionPublication.Writes=true;RebirthWorkstationCompletionPublication.Matches=false;ObserveJoinedRecyclerUnusedReply(Reply(value),value.Joined.Connection,value.Operation);Check(!value.Disposed,"readback mismatch retained");
  Console.WriteLine("PASS joined-unused actual consumer scenarios");
 }
}
