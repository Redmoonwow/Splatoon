// Ignore Spelling: Metadata

using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.ImGuiMethods;
using ECommons.PartyFunctions;
using ImGuiNET;
using Splatoon;
using Splatoon.Memory;
using Splatoon.SplatoonScripting;
using Splatoon.SplatoonScripting.Priority;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SplatoonScriptsOfficial.Duties.Dawntrail;

internal class M7S_Blow_Seeds :SplatoonScript
{
    public override HashSet<uint>? ValidTerritories { get; } = [1261];
    public override Metadata? Metadata => new(3, "Redmoon");

    private static readonly Dictionary<string, Vector3> MarkerPositions = new()
    {
        { "A", new Vector3(100f, -200f, -10f) },
        { "1", new Vector3(110f, -200f, -5f) },
        { "B", new Vector3(115f, -200f, 5f) },
        { "2", new Vector3(110f, -200f, 15f) },
        { "C", new Vector3(100f, -200f, 20f) },
        { "3", new Vector3(90f, -200f, 15f) },
        { "D", new Vector3(85f, -200f, 5f) },
        { "4", new Vector3(90f, -200f, -5f) },
    };

    private static readonly string[] InitialMapping = { "4", "1", "3", "2" };
    private static readonly string[] InitialDpsMapping = { "3", "2", "4", "1" };
    private static readonly Dictionary<string, string[]> MarkerMap = new()
    {
        ["A"] = new[] { "2", "3", "1", "4" },
        ["B"] = new[] { "3", "4", "2", "1" },
        ["C"] = new[] { "4", "1", "3", "2" },
        ["D"] = new[] { "1", "2", "4", "3" },
    };

    private const int PartyMemberCount = 4;
    private const string BaitElementName = "Bait";

    private int blowSeedsCount = 0;
    private bool gimmickActive = false;
    private Vector3 enemyPos = Vector3.Zero;

    private bool HasAoe
    {
        get
        {
            if (MyPlayer.TryGetVfx(out var vfx) && vfx != null)
            {
                return vfx.Any(x => x.Key.Contains("vfx/lockon/eff/loc06sp_05ak1.avfx") && x.Value.AgeF <= 5.0f);
            }
            return false;
        }
    }

    private IPlayerCharacter MyPlayer
    {
        get
        {
            var config = Controller.GetConfig<Config>();
            if (Svc.Condition[ConditionFlag.DutyRecorderPlayback] && config.DebugPlayerJob != Job.ADV)
            {
                foreach (var x in FakeParty.Get())
                {
                    if (x.GetJob() == config.DebugPlayerJob)
                        return x;
                }
            }
            return Player.Object;
        }
    }

    private List<UniversalPartyMember>? PtList
    {
        get
        {
            var config = Controller.GetConfig<Config>();
            var isTH = MyPlayer.GetRole() is CombatRole.Tank or CombatRole.Healer;
            if (isTH)
            {
                return config.CommonPriority.GetPlayers(x => x.IGameObject is IPlayerCharacter pc && pc.GetRole() != CombatRole.DPS);
            }
            else
            {
                return config.CommonPriority.GetPlayers(x => x.IGameObject is IPlayerCharacter pc && pc.GetRole() == CombatRole.DPS);
            }
        }
    }

    private int MyPriority
    {
        get
        {
            if (PtList == null) return 0;
            for (int i = 0; i < PtList.Count; i++)
            {
                if (PtList[i].IGameObject == MyPlayer) return i + 1; // 1-indexed
            }
            return 0; // Not found
        }
    }

    public override void OnSetup()
    {
        Controller.RegisterElement(BaitElementName, new Element(0)
        {
            Name = BaitElementName,
            tether = true,
            thicc = 15f,
            radius = 1f,
            color = 0xC800FF00u,
            Filled = false,
        });
    }

    public override void OnStartingCast(uint source, uint castId)
    {
        if (castId == 43274) gimmickActive = true;
        if (!gimmickActive) return;
        if (castId == 42408 && source.TryGetObject(out var obj) && obj is IBattleChara enemy)
            enemyPos = enemy.Position;
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if (set.Action == null) return;
        if (set.Action.Value.RowId == 42392)
        {
            blowSeedsCount++;
            if (blowSeedsCount == 8)
            {
                this.OnReset();
            }
        }
    }

    public override void OnUpdate()
    {
        if (!gimmickActive) return;
        if (HasAoe) CheckShouldShowElements();
        else Controller.GetRegisteredElements().Each(x => x.Value.Enabled = false);
    }

    public class Config :IEzConfig
    {
        public PriorityData CommonPriority = new();
        public Job DebugPlayerJob = Job.ADV;
    }

    public override void OnSettingsDraw()
    {
        var config = Controller.GetConfig<Config>();
        config.CommonPriority.Draw();
        if (ImGuiEx.CollapsingHeader("Debug"))
        {
            if (ImGui.BeginCombo("Debug Player Job", config.DebugPlayerJob == Job.ADV ? "" : config.DebugPlayerJob.ToString()))
            {
                if (ImGui.Selectable("", config.DebugPlayerJob == Job.ADV))
                {
                    config.DebugPlayerJob = Job.ADV;
                }

                foreach (var x in FakeParty.Get())
                {
                    if (ImGui.Selectable(x.GetJob().ToString(), config.DebugPlayerJob == x.GetJob()))
                    {
                        config.DebugPlayerJob = x.GetJob();
                    }
                }
                ImGui.EndCombo();
            }

            ImGuiEx.Text($"Blow Seeds Count: {blowSeedsCount}");
            ImGuiEx.Text($"Gimmick Active: {gimmickActive}");
            ImGuiEx.Text($"My Job: {MyPlayer.GetJob()}  (Role: {MyPlayer.GetRole()})");
            ImGuiEx.Text($"My Priority: {MyPriority}");
            ImGuiEx.Text($"Enemy Pos: {enemyPos}");

            if (PtList != null)
            {
                ImGuiEx.Text("Party List:");
                for (int i = 0; i < PtList.Count; i++)
                {
                    var m = PtList[i];
                    if (m.IGameObject is IPlayerCharacter pc)
                    {
                        ImGuiEx.Text($" {i + 1}: {pc.Name.ToString()} ({pc.GetJob()}/{pc.GetRole()})");
                    }
                    else
                    {
                        ImGuiEx.Text($" {i + 1}: {m.IGameObject.GetType().Name}");
                    }
                }
            }

            if (blowSeedsCount == 4)
            {
                var dists = new Dictionary<string, float>
                {
                    ["A"] = Vector3.Distance(enemyPos, MarkerPositions["A"]),
                    ["B"] = Vector3.Distance(enemyPos, MarkerPositions["B"]),
                    ["C"] = Vector3.Distance(enemyPos, MarkerPositions["C"]),
                    ["D"] = Vector3.Distance(enemyPos, MarkerPositions["D"]),
                };
                var nearest = dists.OrderBy(x => x.Value).First().Key;
                var markerName = MarkerMap[nearest][MyPriority - 1];
                var pos = MarkerPositions[markerName];

                ImGui.Separator();
                ImGuiEx.Text($"Marker Decision (blowSeedsCount==4):");
                foreach (var (k, v) in dists)
                {
                    ImGuiEx.Text($"  Dist to {k}: {v:F2}");
                }
                ImGuiEx.Text($"  Nearest: {nearest}");
                ImGuiEx.Text($"  MarkerName: {markerName}");
                ImGuiEx.Text($"  MarkerPos: {pos}");
            }

            if (Controller.TryGetElementByName(BaitElementName, out var baitElem))
            {
                ImGui.Separator();
                ImGuiEx.Text($"Bait Element: Enabled={baitElem.Enabled}  Name={baitElem.Name}");
            }
        }
    }


    public override void OnReset()
    {
        gimmickActive = false;
        blowSeedsCount = 0;
        Controller.GetRegisteredElements().Each(x => x.Value.Enabled = false);
    }

    private void CheckShouldShowElements()
    {
        Controller.GetRegisteredElements().Each(x => x.Value.Enabled = false);
        if (!gimmickActive) return;
        if (PtList == null || PtList.Count != PartyMemberCount || MyPriority is < 1 or > PartyMemberCount) return;
        if (!Controller.TryGetElementByName(BaitElementName, out var element)) return;

        if (blowSeedsCount == 0)
        {
            var pos = MarkerPositions[MyPlayer.GetRole() == CombatRole.DPS ? InitialDpsMapping[MyPriority - 1] : InitialMapping[MyPriority - 1]];
            element.SetRefPosition(pos);
            element.Enabled = true;
            return;
        }

        if (blowSeedsCount == 4)
        {
            var dists = new Dictionary<string, float>
            {
                ["A"] = Vector3.Distance(enemyPos, MarkerPositions["A"]),
                ["B"] = Vector3.Distance(enemyPos, MarkerPositions["B"]),
                ["C"] = Vector3.Distance(enemyPos, MarkerPositions["C"]),
                ["D"] = Vector3.Distance(enemyPos, MarkerPositions["D"]),
            };
            var nearest = dists.OrderBy(x => x.Value).First().Key;
            var markerName = MarkerMap[nearest][MyPriority - 1];
            var pos = MarkerPositions[markerName];
            element.SetRefPosition(pos);
            element.Enabled = true;
        }
    }
}
