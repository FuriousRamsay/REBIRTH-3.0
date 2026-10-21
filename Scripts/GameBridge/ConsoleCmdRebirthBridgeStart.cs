using System.Collections.Generic;

// Enables the existing test bridge after a normal Steam launch, only in the disposable test save.
public sealed class ConsoleCmdRebirthBridgeStart : ConsoleCmdAbstract
{
    public override string[] getCommands() => new[] { "rbbridge" };
    public override string getDescription() => "Start the local Game Bridge in CodexTest.";
    public override string getHelp() => "rbbridge (local CodexTest save only)";

    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        var world = GameManager.Instance?.World;
        var player = world?.GetPrimaryPlayer() as EntityPlayerLocal;
        if (args.Count != 0 || sender.RemoteClientInfo != null || player == null
            || world.IsRemote() || GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest")
        {
            SdtdConsole.Instance.Output("*** ERROR rbbridge works only in the local CodexTest save.");
            return;
        }

        RebirthGameBridge.TryStart(null, RebirthGameBridge.DefaultPort);
        SdtdConsole.Instance.Output(RebirthGameBridge.IsRunning
            ? "Game Bridge is ready for CodexTest."
            : "Game Bridge could not start; check the log.");
    }
}
