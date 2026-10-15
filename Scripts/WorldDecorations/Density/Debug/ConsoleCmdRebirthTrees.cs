#if DEBUG
using System.Collections.Generic;

#nullable disable

public class ConsoleCmdRebirthTrees : ConsoleCmdAbstract
{
    public override string[] getCommands() { return new[] { "rbtrees" }; }
    public override string getDescription() { return "Reports REBIRTH tree systems and tree/vehicle density binding."; }
    public override string getHelp() { return "rbtrees info\nrbtrees validate\nrbtrees density"; }

    public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
    {
        string cmd = _params != null && _params.Count > 0 ? _params[0].ToLowerInvariant() : "info";
        if (cmd == "validate")
            Log.Out(RebirthAutoReplantTreeRegistry.BuildValidationReport());
        else if (cmd == "density")
            Log.Out(RebirthWorldDecorationDensityRuntimePolicy.BuildDiagnosticReport());
        else if (cmd == "info")
            Log.Out(RebirthAutoReplantTreeRegistry.BuildInfoReport());
        else
            Log.Out(getHelp());
    }
}
#endif
