#requires -Version 7.0
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperBounds=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/GameBridge/RebirthGameBridgeVehicleBounds.cs'))
$camperFixture=@'
public static class VehicleBoundsFixture{
 static void Check(bool b,string label){if(!b)throw new System.Exception(label);}
 public static void Run(){RebirthGameBridgeVehicleBounds bounds;
 Check(RebirthGameBridgeVehicleBounds.TryParse(new[]{"10"},out bounds)&&bounds==null,"legacy standalone invocation");
 Check(!RebirthGameBridgeVehicleBounds.TryParse(new string[0],out bounds),"empty command refused");
 Check(RebirthGameBridgeVehicleBounds.TryParse(new[]{"10","-20","20","100","160"},out bounds),"valid pad");
 Check(bounds.Contains(-20,100)&&bounds.Contains(20,160)&&bounds.Contains(0,130),"interior and boundary");
 Check(!bounds.Contains(-20.01f,130)&&!bounds.Contains(20.01f,130)&&!bounds.Contains(0,99.99f)&&!bounds.Contains(0,160.01f),"outside every edge");
 Check(!bounds.Contains(float.NaN,130)&&!bounds.Contains(0,float.PositiveInfinity),"nonfinite position refused");
 foreach(var invalid in new[]{new[]{"10","NaN","20","100","160"},new[]{"10","0","Infinity","100","160"},new[]{"10","20","20","100","160"},new[]{"10","21","20","100","160"},new[]{"10","0","81","100","160"},new[]{"10","0","20","100","181"},new[]{"10","0","20"}})
 Check(!RebirthGameBridgeVehicleBounds.TryParse(invalid,out bounds),"malformed or oversized pad refused");
 var prior=System.Globalization.CultureInfo.CurrentCulture;try{System.Globalization.CultureInfo.CurrentCulture=new System.Globalization.CultureInfo("fr-FR");Check(RebirthGameBridgeVehicleBounds.TryParse(new[]{"10","-1.5","20.5","100","160"},out bounds)&&bounds.Contains(-1.5f,130),"invariant coordinates");}finally{System.Globalization.CultureInfo.CurrentCulture=prior;}
 System.Console.WriteLine("PASS actual vehicle bounds parsing and containment. Native input-release integration still requires gameplay verification.");}}
'@
Add-Type -TypeDefinition ($camperBounds+"`n"+$camperFixture)
[VehicleBoundsFixture]::Run()
