using System;
using System.Collections.Generic;

static class Program
{
    static int checks;
    static void Check(string expected, CommandSenderInfo sender, params string[] args)
    {
        new ConsoleCmdRebirthPlayerId().Execute(new List<string>(args), sender);
        if (!SingletonMonoBehaviour<SdtdConsole>.Instance.Last.Contains(expected))
            throw new Exception("Unexpected output: " + SingletonMonoBehaviour<SdtdConsole>.Instance.Last);
        checks++;
    }
    static void Main()
    {
        var local = new CommandSenderInfo { IsLocalGame = true };
        GameManager.Instance = new GameManager();
        Check("EOS_local", local, "playerid"); // SP has no ClientInfo.
        GameManager.Instance.Persistent = null;
        Check("EOS_fallback", local, "PLAYERID");
        GameManager.Instance.Fallback = null;
        Check("not available yet", local, "playerid");
        var remote = new CommandSenderInfo { RemoteClientInfo = new ClientInfo { InternalId = new Id("EOS_remote") } };
        Check("EOS_remote", remote, "playerid");
        remote.RemoteClientInfo.InternalId = null;
        Check("not available yet", remote, "playerid"); // Must not leak host fallback.
        remote.RemoteClientInfo.InternalId = new Id("Steam_platform");
        Check("Steam_platform", remote, "playerid");
        Check("Join a game", new CommandSenderInfo(), "playerid");
        GameManager.IsDedicatedServer = true;
        Check("Join a game", local, "playerid");
        GameManager.IsDedicatedServer = false;
        GameManager.Instance.World = null;
        Check("Join a game", local, "playerid");
        Check("rebirth playerid", local);
        Check("rebirth playerid", local, "playerid", "someone-else");
        Console.WriteLine(checks + " actual-command identity routing checks PASS (controlled native adapters; gameplay not run).");
    }
}
public class Id { public string CombinedString; public Id(string value) { CombinedString = value; } }
public class ClientInfo { public Id InternalId; }
public struct CommandSenderInfo { public bool IsLocalGame; public ClientInfo RemoteClientInfo; }
public class Persistent { public Id PrimaryId = new Id("EOS_local"); }
public class World { public object GetPrimaryPlayer() => new object(); }
public class GameManager
{
    public static GameManager Instance;
    public static bool IsDedicatedServer;
    public World World = new World();
    public Persistent Persistent = new Persistent();
    public Id Fallback = new Id("EOS_fallback");
    public Persistent GetPersistentLocalPlayer() => Persistent;
    public Id getPersistentPlayerID(ClientInfo info) => Fallback;
}
public class SdtdConsole { public string Last; public void Output(string value) { Last = value; } }
public class SingletonMonoBehaviour<T> where T : new() { public static T Instance = new T(); }
public abstract class ConsoleCmdAbstract
{
    public virtual bool IsExecuteOnClient => false;
    public virtual int DefaultPermissionLevel => 0;
    public abstract string[] getCommands();
    public abstract string getDescription();
    public abstract string getHelp();
    public abstract void Execute(List<string> args, CommandSenderInfo sender);
}
namespace UnityEngine.Scripting { public class PreserveAttribute : Attribute { } }
