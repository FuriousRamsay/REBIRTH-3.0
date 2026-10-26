using System;
using System.Collections.Generic;
using System.Globalization;
using Platform;
using UnityEngine.Scripting;

namespace RebirthVehicleInspector.Commands
{
    [Preserve]
    public sealed class ConsoleCmdVehicleInspector : ConsoleCmdAbstract
    {
        public override bool IsExecuteOnClient => true;
        public override DeviceFlag AllowedDeviceTypesClient =>
            DeviceFlag.StandaloneWindows | DeviceFlag.StandaloneLinux | DeviceFlag.StandaloneOSX;
        public override string[] getCommands() { return new[]{"vinspect","rebirthvinspect"}; }
        public override string getDescription() { return "Read-only local vehicle hierarchy, XML and occupant-pose inspector (REBIRTH)."; }
        public override string getHelp() { return "Usage: "+(ModEntry.Service!=null?ModEntry.Service.Help:"vinspect help"); }
        public override void Execute(List<string> args,CommandSenderInfo sender)
        {
            if(GameManager.IsDedicatedServer)
            {
                Output("This tool captures client-rendered geometry. Run on a local client with EAC off, not a headless dedicated server.");
                return;
            }
            VehicleInspectorService s=ModEntry.Service;
            if(s==null) {Output("Collector not ready. Enter a local world first (wait for GameStartDone).");return;}
            try
            {
                string cmd=args.Count==0?"help":args[0].ToLowerInvariant();
                switch(cmd)
                {
                    case "help": Output(s.Help);break;
                    case "status":Output(s.Status());break;
                    case "list":Output(s.ListVehicles());break;
                    case "select":
                        if(args.Count>=2 && args[1].Equals("nearest",StringComparison.OrdinalIgnoreCase))
                        {
                            float radius=args.Count>=3?Parse(args[2],2f,150f):15f;
                            Output(s.SelectNearest(radius));
                        }
                        else if(args.Count==2 && int.TryParse(args[1],NumberStyles.Integer,CultureInfo.InvariantCulture,out int id))
                            Output(s.SelectId(id));
                        else Output("Usage: vinspect select nearest [radius] OR vinspect select <entityId>");
                        break;
                    case "bind": Output(args.Count==2?s.BindXml(args[1]):"Usage: vinspect bind <exactVehicleNameFromVehicles.xml>");break;
                    case "snap": Output(s.Snapshot(args.Count>1?args[1]:"snapshot"));break;
                    case "record":
                        if(args.Count<2) {Output("Usage: vinspect record <seconds 1-120> [hz 1-10] [scenarioLabel]");break;}
                        Output(s.StartRecord(Parse(args[1],1f,120f),args.Count>=3?Parse(args[2],1f,10f):5f,
                            args.Count>=4?args[3]:"motion"));break;
                    case "stop":Output(s.StopRecord());break;
                    case "diagnose":Output(s.Diagnose());break;
                    case "seatdiag":Output(s.SeatDiagnostics());break;
                    default:Output("Unknown subcommand. "+s.Help);break;
                }
            }
            catch(Exception ex)
            {
                Output("Inspection error: "+ex.Message);
                Log.Out("[VehicleInspector] "+ex);
            }
        }
        private static float Parse(string text,float min,float max)
        {
            if(!float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out float v) ||
               float.IsNaN(v) || float.IsInfinity(v) || v<min || v>max)
                throw new ArgumentException("Expected number in ["+min+", "+max+"] using '.' as decimal point.");
            return v;
        }
        private static void Output(string message) {SdtdConsole.Instance.Output("[VehicleInspector] "+message);}
    }
}
