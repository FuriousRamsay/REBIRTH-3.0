using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Security.Cryptography;
using System.Text.Json;
var output="_Documentation/PerformanceAudit_20261010";
var rows=new List<object>();var roots=new List<object>();var errors=new List<object>();
var callbacks=new HashSet<string>{"Update","LateUpdate","FixedUpdate","OnGUI","Tick","Pump","OnUpdateLive","OnUpdateEntity","OnGameUpdate"};
var risks=new HashSet<string>{"ToArray","ToList","OrderBy","OrderByDescending","FindObjectsOfType","FindObjectOfType","GetComponentsInChildren","GetComponentInParent","GetMethods","GetFields","GetProperties","WriteAllText","WriteAllBytes","Save","SaveAtomic","Flush","GetChildById","GetChildByType"};
foreach(var file in Directory.EnumerateFiles("Scripts","*.cs",SearchOption.AllDirectories).Order())
{
 var text=File.ReadAllText(file);var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
 // Both configurations are indexed. Inactive source is intentionally not treated as active runtime code.
 foreach(var debug in new[]{false,true})
 {
  var tree=CSharpSyntaxTree.ParseText(text,new CSharpParseOptions(preprocessorSymbols:debug?new[]{"DEBUG","REBIRTH_UI_DIAGNOSTICS"}:Array.Empty<string>()));
  foreach(var e in tree.GetDiagnostics().Where(x=>x.Severity==DiagnosticSeverity.Error))errors.Add(new{file,debug,error=e.ToString()});
  var root=tree.GetRoot();
  foreach(var method in root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
  {
   var name=method is MethodDeclarationSyntax m?m.Identifier.Text:method is ConstructorDeclarationSyntax c?c.Identifier.Text:"operator";
   var type=method.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text;
   var calls=method.DescendantNodes().OfType<InvocationExpressionSyntax>().Select(x=>x.Expression.ToString()).Distinct().Order().ToArray();
   var flagged=calls.Where(x=>risks.Contains(x.Split('.').Last().Split('<')[0])).ToArray();
   var attributes=string.Join(" ",method.AttributeLists.Select(x=>x.ToString()));
   var row=new{file,hash,debug,type,name,line=tree.GetLineSpan(method.Span).StartLinePosition.Line+1,callback=callbacks.Contains(name),attributes,calls,risks=flagged};
   rows.Add(row);
   if(row.callback || attributes.Contains("Harmony") || calls.Any(x=>x.Contains("GameUpdate") || x.EndsWith(".RegisterHandler")))roots.Add(row);
  }
 }
}
File.WriteAllText(output+"/METHOD_INDEX.json",JsonSerializer.Serialize(rows));
File.WriteAllText(output+"/ENTRYPOINT_INDEX.json",JsonSerializer.Serialize(roots,new JsonSerializerOptions{WriteIndented=true}));
File.WriteAllText(output+"/SOURCE_PARSE_ERRORS.json",JsonSerializer.Serialize(errors,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Parsed methods/configurations: {rows.Count}; candidate entrypoints/configurations: {roots.Count}; parser errors: {errors.Count}. Syntax index is not a semantic call graph or runtime coverage proof.");
