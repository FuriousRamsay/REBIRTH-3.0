class Label {public string Text;}
class QuestJournal {}
class EntityPlayerLocal {public QuestJournal QuestJournal=new();}
class PlayerUI {public EntityPlayerLocal entityPlayer=new();}
class Xui {public PlayerUI playerUI=new();}
static class Localization {public static string Format="Accepted Jobs: {0} / {1}";public static string Get(string key)=>Format;}
static class RebirthTraderJobPolicy {public static int Daily=4,Tier=3,Max=3,Reserved=2,Open=1;public static int GetDailyQuestLimit()=>Daily;public static int GetAcceptedJobLimit(int t,bool m)=>Tier;public static bool IsMultiplayerClient()=>false;public static int GetEffectiveAcceptedJobLimit(int t)=>Max;public static int CountDailyReservedTraderJobs(QuestJournal j)=>Reserved;public static int CountOpenPersonalTraderJobs(QuestJournal j)=>Open;}
class Old{public Label jobStatusLabel=new();public Xui xui=new();public int Tier=1;private int GetCurrentJobTier()=>Tier;public void Run()=>UpdateJobAcceptanceStatus();    private void UpdateJobAcceptanceStatus()
    {
        if (jobStatusLabel == null || xui == null || xui.playerUI == null)
            return;

        EntityPlayerLocal player = xui.playerUI.entityPlayer;
        QuestJournal journal = player != null ? player.QuestJournal : null;
        int tier = GetCurrentJobTier();

        if (journal == null || tier < 1)
        {
            jobStatusLabel.Text = string.Empty;
            return;
        }

        int dailyLimit = RebirthTraderJobPolicy.GetDailyQuestLimit();
        int tierLimit = RebirthTraderJobPolicy.GetAcceptedJobLimit(
            tier,
            RebirthTraderJobPolicy.IsMultiplayerClient());

        int effectiveMax =
            RebirthTraderJobPolicy.GetEffectiveAcceptedJobLimit(tier);

        // The numerator follows whichever cap is actually the lower one.
        // If the daily cap is the limiting cap, completed/reserved jobs today matter.
        // If the tier concurrent cap is lower, currently open accepted jobs matter.
        int accepted;
        if (dailyLimit != -1 && dailyLimit <= tierLimit)
            accepted = RebirthTraderJobPolicy.CountDailyReservedTraderJobs(journal);
        else
            accepted = RebirthTraderJobPolicy.CountOpenPersonalTraderJobs(journal);

        string format = Localization.Get("xuiRebirthAcceptedJobsStatus");
        if (string.IsNullOrEmpty(format) || format == "xuiRebirthAcceptedJobsStatus")
            format = "Accepted Jobs: {0} / {1}";

        jobStatusLabel.Text = string.Format(format, accepted, effectiveMax);
    }

}
class New{public Label jobStatusLabel=new();public Xui xui=new();public int Tier=1;private int GetCurrentJobTier()=>Tier;public void Run()=>UpdateJobAcceptanceStatus();private string statusFormat,statusText;private int statusAccepted,statusMaximum;private System.Globalization.CultureInfo statusCulture;    private void UpdateJobAcceptanceStatus()
    {
        if (jobStatusLabel == null || xui == null || xui.playerUI == null)
            return;

        EntityPlayerLocal player = xui.playerUI.entityPlayer;
        QuestJournal journal = player != null ? player.QuestJournal : null;
        int tier = GetCurrentJobTier();

        if (journal == null || tier < 1)
        {
            jobStatusLabel.Text = string.Empty;
            return;
        }

        int dailyLimit = RebirthTraderJobPolicy.GetDailyQuestLimit();
        int tierLimit = RebirthTraderJobPolicy.GetAcceptedJobLimit(
            tier,
            RebirthTraderJobPolicy.IsMultiplayerClient());

        int effectiveMax =
            RebirthTraderJobPolicy.GetEffectiveAcceptedJobLimit(tier);

        // The numerator follows whichever cap is actually the lower one.
        // If the daily cap is the limiting cap, completed/reserved jobs today matter.
        // If the tier concurrent cap is lower, currently open accepted jobs matter.
        int accepted;
        if (dailyLimit != -1 && dailyLimit <= tierLimit)
            accepted = RebirthTraderJobPolicy.CountDailyReservedTraderJobs(journal);
        else
            accepted = RebirthTraderJobPolicy.CountOpenPersonalTraderJobs(journal);

        string format = Localization.Get("xuiRebirthAcceptedJobsStatus");
        if (string.IsNullOrEmpty(format) || format == "xuiRebirthAcceptedJobsStatus")
            format = "Accepted Jobs: {0} / {1}";

        // Keep all live policy/count reads; only reuse unchanged presentation text.
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        if (!culture.IsReadOnly || !ReferenceEquals(statusCulture, culture) || statusText == null || statusFormat != format || statusAccepted != accepted || statusMaximum != effectiveMax)
        {
            statusCulture = culture;
            statusFormat = format;
            statusAccepted = accepted;
            statusMaximum = effectiveMax;
            statusText = string.Format(format, accepted, effectiveMax);
        }
        if (jobStatusLabel.Text != statusText) jobStatusLabel.Text = statusText;
    }

}
class Program {static void Main(){var a=new Old();var b=new New();var random=new Random(73);
for(int i=0;i<1000;i++){RebirthTraderJobPolicy.Daily=random.Next(-1,8);RebirthTraderJobPolicy.Tier=random.Next(1,8);RebirthTraderJobPolicy.Max=random.Next(1,8);RebirthTraderJobPolicy.Open=random.Next(8);RebirthTraderJobPolicy.Reserved=random.Next(8);Localization.Format=i%5==0?"{0:N2} of {1:N2}":i%7==0?null:"Accepted Jobs: {0} / {1}";System.Globalization.CultureInfo.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo(i%2==0?"en-US":"fr-FR");a.Tier=b.Tier=i%11==0?0:1;a.jobStatusLabel.Text=b.jobStatusLabel.Text="native overwrite";a.Run();b.Run();if(a.jobStatusLabel.Text!=b.jobStatusLabel.Text)throw new Exception("Mismatch");a.Run();b.Run();if(a.jobStatusLabel.Text!=b.jobStatusLabel.Text)throw new Exception("Stable mismatch");}
var mutable=(System.Globalization.CultureInfo)System.Globalization.CultureInfo.InvariantCulture.Clone();System.Globalization.CultureInfo.CurrentCulture=mutable;Localization.Format="{0:N2}/{1:N2}";a.Tier=b.Tier=1;foreach(var sep in new[]{"!","~"}){mutable.NumberFormat.NumberDecimalSeparator=sep;a.Run();b.Run();if(a.jobStatusLabel.Text!=b.jobStatusLabel.Text)throw new Exception("Mutable culture");}
Console.WriteLine("PASS 1000 changing/stable policy, localization, invalid-tier and native-overwrite comparisons plus mutable culture. Actual extracted methods; policy/native collaborators doubled.");}}
