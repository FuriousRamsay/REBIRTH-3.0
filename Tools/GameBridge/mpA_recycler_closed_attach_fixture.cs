// AUTHORED_NOT_RUN. Extracts the actual private-source attachment boundary.
using System;using System.Text;using System.Xml.Linq;
class RemoteHostOriginal
{internal object Peer,Connection,Actor,Station;internal byte[] Image=Encoding.UTF8.GetBytes("<placedRecyclerOriginal/>");}
class Subject
{
 internal object Peer=new object(),Connection=new object(),Recipient=new object(),Station=new object();
 internal RemoteHostOriginal Attached;internal bool Matches=true,Qualified=true;
 internal bool MatchesOwnerOriginal(XElement original)=>Matches;
 internal bool Current()=>Qualified;
 // PRODUCTION_ATTACH
 static RemoteHostOriginal Original(Subject s)=>new RemoteHostOriginal{Peer=s.Peer,Connection=s.Connection,Actor=s.Recipient,Station=s.Station};
 static void Check(bool value,string label){if(!value)throw new Exception(label);}
 static void Main()
 {
  var source=new Subject();var original=Original(source);
  Check(source.Attach(original)&&ReferenceEquals(source.Attached,original),"positive exact private original attaches");
  var another=Original(source);Check(!source.Attach(another)&&ReferenceEquals(source.Attached,original),"existing original never replaced");
  source=new Subject{Qualified=false};original=Original(source);
  Check(!source.Attach(original)&&source.Attached==null,"failed final current qualification rolls back own pointer");
  source.Qualified=true;Check(source.Attach(original),"positive retry can use same retained original after rolled-back qualification");
  source=new Subject();original=Original(source);original.Actor=new object();
  Check(!source.Attach(original)&&source.Attached==null,"foreign actual actor cannot attach");
  source=new Subject{Matches=false};original=Original(source);
  Check(!source.Attach(original)&&source.Attached==null,"different immutable original cannot attach");
  Console.WriteLine("PASS closed authority attachment: 6 actual-method scenarios");
 }
}
