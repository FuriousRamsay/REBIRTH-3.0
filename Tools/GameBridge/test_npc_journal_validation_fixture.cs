using System;
using System.Xml;
using System.Globalization;
using System.Collections.Generic;
// ENUM
class Test {
 // SOURCE
 static bool Check(string body){var d=new XmlDocument();d.LoadXml("<rebirthNpcExternalTransactions format='1'>"+body+"</rebirthNpcExternalTransactions>");return ValidateReplayDocument(d);}
 static void Main(){
 string entry="<transaction id='"+Guid.NewGuid()+"' result='0' revision='1' fingerprint='owner|transfer'/>";
 if(!Check(entry)||!Check(""))throw new Exception("valid rejected");
 foreach(string bad in new[]{entry+entry,entry.Replace("result='0'","result='256'"),entry.Replace("result='0'","result='13'"),entry.Replace("revision='1'","revision='-1'"),entry.Replace("owner|transfer"," "),entry.Replace("transaction ","unknown ")})
 if(Check(bad))throw new Exception("invalid accepted: "+bad);
 for(int i=0;i<=12;i++)if(!Check(entry.Replace("result='0'","result='"+i+"'")))throw new Exception("defined enum rejected");
 if(ValidateReplayDocument(null))throw new Exception("null accepted");
 Console.WriteLine("PASS: valid/empty documents, all defined results, duplicate IDs, overflow/undefined results, negative revisions, missing fingerprint and unknown entries");
 }
}
