using System.ComponentModel;
using System.Text.RegularExpressions;

namespace Splatoon;

[Serializable]
public class Trigger
{
    [NonSerialized] public static string[] Types = { "Show at time in combat", "Hide at time in combat", "Show at log message", "Hide at log message" };
    [NonSerialized] internal string GUID = Guid.NewGuid().ToString();
    /// <summary>
    /// 0: Show at time |
    /// 1: Hide at time |
    /// 2: Show at text |
    /// 3: Hide at text
    /// </summary>
    [DefaultValue(false)] public bool UserDisabled = false;
    [DefaultValue(0)] public int Type = 0;
    [DefaultValue(0f)] public float TimeBegin = 0;
    [DefaultValue(0f)] public float Duration = 0;
    [DefaultValue("")] public string Match = "";
    public InternationalString MatchIntl = new();
    [DefaultValue(0f)] public float MatchDelay = 0;
    [DefaultValue(true)] public bool ResetOnCombatExit = true;
    [DefaultValue(true)] public bool ResetOnTChange = true;
    [DefaultValue(false)] public bool FireOnce = false;
    /// <summary>
    /// 0: not fired |
    /// 1: fired but not ended |
    /// 2: fired and ended
    /// </summary>
    [NonSerialized] public int FiredState = 0;
    [NonSerialized] public List<long> EnableAt = [];
    [NonSerialized] public List<long> DisableAt = [];
    [NonSerialized] internal bool Disabled = false;
    [DefaultValue(false)] public bool IsRegex = false;
    [NonSerialized] private Regex CachedRegex;

    /// <summary>
    /// Regex for <paramref name="pattern"/>. Kept per trigger because the static Regex cache only holds 15 patterns.
    /// </summary>
    internal Regex GetRegex(string pattern)
    {
        if(CachedRegex == null || CachedRegex.ToString() != pattern)
        {
            CachedRegex = new Regex(pattern);
        }
        return CachedRegex;
    }

    public bool ShouldSerializeMatchIntl()
    {
        return !MatchIntl.IsEmpty();
    }
}
