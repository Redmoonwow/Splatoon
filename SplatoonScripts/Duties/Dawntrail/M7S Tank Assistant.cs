using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Automation.NeoTaskManager;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.DalamudServices.Legacy;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SplatoonScriptsOfficial.Duties.Dawntrail;
internal unsafe partial class M7S_Tank_Assistant :SplatoonScript
{
    private const uint kSinisterSeed = 42349u;
    private const uint kCrossingCross = 43277u;
    private const uint kBloomingAbominationNameId = 0x35BBu;
    private readonly Dictionary<string, Vector3> Phase1Pos = new Dictionary<string, Vector3>()
    {
        { "N", new Vector3(100f, 0f, 80f) },
        { "E", new Vector3(120f, 0f, 100f) },
        { "S", new Vector3(100f, 0f, 120f) },
        { "W", new Vector3(80f, 0f, 100f) },
    };
    private readonly Dictionary<string, Vector3> Phase2Pos = new Dictionary<string, Vector3>()
    {
        { "N", new Vector3(100f, -200f, -15f) },
        { "E", new Vector3(120f, -200f, 5f) },
        { "S", new Vector3(100f, -200f, 25f) },
        { "W", new Vector3(80f, -200f, 5f) },
    };

    public override HashSet<uint>? ValidTerritories { get; } = [1261];
#pragma warning disable VSSpell001 // Spell Check
    public override Metadata? Metadata => new(1, "Redmoon");
#pragma warning restore VSSpell001 // Spell Check

    private bool _gimmickActive = false;
    private bool _mobSpawned = false;
    private bool _isTargetDone = false;
    private bool _isInterjectDone = false;
    private int _scatterSeedCounts = 0;
    private List<(string, uint)> _mobIdByPosList = new List<(string, uint)>();
    private List<IBattleChara> _bloomingAbominationEnemyList => GetBloomingAbominationList();
    private TaskManager _taskManager = new TaskManager(new TaskManagerConfiguration
    {
        AbortOnTimeout = true,
        AbortOnError = true,
        ShowDebug = true,
        ShowError = true,
        TimeLimitMS = 30000,
        TimeoutSilently = false,
        ExecuteDefaultConfigurationEvents = true,
    });
    private ActionManager* _actionManager => ActionManager.Instance();

    public override void OnStartingCast(uint source, uint castId)
    {
        if (castId == kSinisterSeed && _gimmickActive == false)
        {
            _scatterSeedCounts++;
            _gimmickActive = true;
            if (_taskManager.StepMode == false) _taskManager.StepMode = true;
        }

        if (!_gimmickActive) return;
    }

    public override void OnUpdate()
    {
        if (!_gimmickActive) return;
        _taskManager.Step();

        if (_mobSpawned && _bloomingAbominationEnemyList.Count() == 0)
        {
            WarmReset();
        }

        if (_bloomingAbominationEnemyList.Count() >= 4 && !_mobSpawned)
        {
            _mobSpawned = true;
        }

        if (!_mobSpawned) return;

        if (_config.UseTargetEnforcer)
        {
            UseTargetEnforcer();
        }

        if (_config.UseAutoInterject && !_isInterjectDone)
        {
            UseAutoInterject();
        }
    }

    private void UseTargetEnforcer()
    {
        // Mapping of positions to directions
        if (_mobIdByPosList.Count == 0)
        {
            if (_scatterSeedCounts == 1)
            {
                foreach (var mob in _bloomingAbominationEnemyList)
                {
                    if (mob == null) continue;
                    var pos = mob.Position;
                    var closestPos = Phase1Pos.OrderBy(x => Vector3.Distance(pos, x.Value)).First();
                    _mobIdByPosList.Add((closestPos.Key, mob.EntityId));
                }
            }
            else if (_scatterSeedCounts == 2)
            {
                foreach (var mob in _bloomingAbominationEnemyList)
                {
                    if (mob == null) continue;
                    var pos = mob.Position;
                    var closestPos = Phase2Pos.OrderBy(x => Vector3.Distance(pos, x.Value)).First();
                    _mobIdByPosList.Add((closestPos.Key, mob.EntityId));
                }
            }
        }
        if (_mobIdByPosList.Count == 0) return;

        // Wait Target able
        if (_bloomingAbominationEnemyList.All(x => x != null && x.IsTargetable == false)) return;

        // Targeting in order N, E, S, W
        bool isProvokeDone = false;
        foreach (var mob in _mobIdByPosList)
        {
            if (mob.Item1 == "N" && !_config.TargetNorth) continue;
            if (mob.Item1 == "E" && !_config.TargetEast) continue;
            if (mob.Item1 == "S" && !_config.TargetSouth) continue;
            if (mob.Item1 == "W" && !_config.TargetWest) continue;

            if (mob.Item2.TryGetObject(out var mobObj))
            {
                var otherTank = Svc.Objects.OfType<IPlayerCharacter>()
                    .FirstOrDefault(x => (x.GetRole() == CombatRole.Tank) && x.EntityId != BasePlayer.EntityId);
                if (otherTank == null) return;

                if ((mobObj.TargetObjectId == BasePlayer.EntityId) ||
                    (mobObj.TargetObjectId == otherTank.EntityId)) continue;

                if (Svc.Targets.Target == null) return;
                if (Svc.Targets.Target.EntityId == mob.Item2) return;
                if (!_actionManager->IsRecastTimerActive(ActionType.Action, 7533u) && !isProvokeDone)
                {
                    if (EzThrottler.Throttle("Provoke", 3000))
                    {
                        _taskManager.Enqueue(() => UseProvoke(mob.Item2));
                    }
                    isProvokeDone = true;
                    continue;
                }
                if (Svc.Condition[ConditionFlag.DutyRecorderPlayback])
                {
                    if (EzThrottler.Throttle("SetTarget", 1000))
                    {
                        DuoLog.Information($"SetTarget {mobObj.EntityId.ToString("X")}");
                    }
                }
                else
                {
                    Svc.Targets.SetTarget(mobObj);
                }

                break;
            }

        }
    }

    public override void OnReset()
    {
        _gimmickActive = false;
        _mobSpawned = false;
        _mobIdByPosList.Clear();
        _scatterSeedCounts = 0;
        _taskManager.Abort();
    }

    public void WarmReset()
    {
        _gimmickActive = false;
        _mobSpawned = false;
        _mobIdByPosList.Clear();
        _taskManager.Abort();
    }

    private void UseAutoInterject()
    {
        if (_bloomingAbominationEnemyList.Count() == 0) return;
        if (_bloomingAbominationEnemyList.All(x => x == null || !x.IsCasting)) return;
        if (_bloomingAbominationEnemyList.Where(x => x.CastActionId == kCrossingCross).Count() == 0) return;
        switch (_config.UseAutoInterjectType)
        {
            case Config.InterjectType.UseTooCloseMob:
                UseAutoInterjectTooCloseMob();
                break;
            case Config.InterjectType.FindAboveEnemyList:
                UseAutoInterjectFindAboveEnemyList();
                break;
            case Config.InterjectType.FindBelowEnemyList:
                UseAutoInterjectFindBelowEnemyList();
                break;
            case Config.InterjectType.WaitAtleast1Mob:
                UseAutoInterjectWaitAtleast1Mob();
                break;
            default:
                break;
        }
    }

    private void UseAutoInterjectTooCloseMob()
    {
        if (_bloomingAbominationEnemyList.Any(x => x == null)) return;
        foreach (var mob in _bloomingAbominationEnemyList
            .Where(x => x != null)
            .OrderBy(x => Vector3.Distance(Player.Position, x!.Position)))
        {
            if (mob == null) continue;

            if (mob.IsCasting && mob.IsTargetable && (mob.CastActionId == kCrossingCross))
            {
                _taskManager.Enqueue(() => UseInterject(mob.EntityId));
                return;
            }
        }
    }

    private void UseAutoInterjectFindAboveEnemyList()
    {
        if (_bloomingAbominationEnemyList.Any(x => x == null)) return;
        foreach (var mob in _bloomingAbominationEnemyList.Where(x => x != null))
        {
            if (mob == null) continue;
            if (mob.IsCasting && mob.IsTargetable && (mob.CastActionId == kCrossingCross))
            {
                _taskManager.Enqueue(() => UseInterject(mob.EntityId));
                return;
            }
        }
    }

    private void UseAutoInterjectFindBelowEnemyList()
    {
        if (_bloomingAbominationEnemyList.Any(x => x == null)) return;
        foreach (var mob in _bloomingAbominationEnemyList.Where(x => x != null).Reverse())
        {
            if (mob == null) continue;

            if (mob.IsCasting && mob.IsTargetable && (mob.CastActionId == kCrossingCross))
            {
                _taskManager.Enqueue(() => UseInterject(mob.EntityId));
                return;
            }
        }
    }

    private void UseAutoInterjectWaitAtleast1Mob()
    {
        if (_bloomingAbominationEnemyList.Any(x => x == null)) return;
        if (_bloomingAbominationEnemyList.Where(x => x!.CastActionId == kCrossingCross).Count() > 1) return;
        foreach (var mob in _bloomingAbominationEnemyList.Where(x => x != null))
        {
            if (mob == null) continue;

            if (mob.IsCasting && mob.IsTargetable && (mob.CastActionId == kCrossingCross))
            {
                _taskManager.Enqueue(() => UseInterject(mob.EntityId));
                return;
            }
        }
    }

    private List<IBattleChara> GetBloomingAbominationList()
    {
        var list = new List<IBattleChara>();
        var array = AtkStage.Instance()->AtkArrayDataHolder->NumberArrays[21];
        var characters =
            Svc.Objects.OfType<IBattleChara>()
            .Where(x => x.NameId == kBloomingAbominationNameId)
            .ToArray();
        for (int i = 0; i < 8; i++)
        {
            var id = *(uint*)&array->IntArray[8 + (i * 6)];
            if (id != 0xE0000000)
            {
                var c = characters.FirstOrDefault(x => x.EntityId == id);
                if (c == null) continue;
                list.Add(c);
            }
        }
        return list;
    }

    private void UseProvoke(uint targetId)
    {
        if (_actionManager->IsRecastTimerActive(ActionType.Action, 7533u)) return;
        if (targetId == 0) return;
        if (targetId.TryGetObject(out var mobObj))
        {
            if (Player.DistanceTo(mobObj) > mobObj.HitboxRadius)
            {
                _taskManager.EnqueueDelay(100);
                _taskManager.Enqueue(() => UseProvoke(targetId));
            }
            if (_actionManager->AnimationLock != 0 ||
                _actionManager->IsRecastTimerActive(ActionType.Action, 7533u)) return;
            if (Svc.Condition[ConditionFlag.DutyRecorderPlayback])
            {
                DuoLog.Information($"Provoke {targetId.ToString("X")}");
            }
            else
            {
                _actionManager->UseAction(ActionType.Action, 7533u, targetId);
                if (!_gimmickActive) return;
                if (!_actionManager->IsRecastTimerActive(ActionType.Action, 7533u))
                {
                    _taskManager.EnqueueDelay(100);
                    _taskManager.Enqueue(() => UseProvoke(targetId));
                }
            }
        }
    }

    private void UseInterject(uint targetId)
    {
        if (_actionManager->IsRecastTimerActive(ActionType.Action, 7538u))
        {
            _isInterjectDone = true;
            return;
        }
        if (targetId == 0) return;

        if (targetId.TryGetObject(out var mobObj))
        {
            if (Player.DistanceTo(mobObj) > mobObj.HitboxRadius)
            {
                _taskManager.EnqueueDelay(100);
                _taskManager.Enqueue(() => UseInterject(targetId));
            }
            if (_actionManager->AnimationLock != 0 ||
                _actionManager->IsRecastTimerActive(ActionType.Action, 7538u)) return;
            if (Svc.Condition[ConditionFlag.DutyRecorderPlayback])
            {
                DuoLog.Information($"Interject {targetId.ToString("X")}");
                _isInterjectDone = true;
            }
            else
            {
                _actionManager->UseAction(ActionType.Action, 7538u, targetId);
                if (!_gimmickActive) return;
                if (!_actionManager->IsRecastTimerActive(ActionType.Action, 7538u))
                {
                    _taskManager.EnqueueDelay(100);
                    _taskManager.Enqueue(() => UseInterject(targetId));
                }
                else
                {
                    _isInterjectDone = true;
                }
            }
        }
    }
}

// GUI
internal unsafe partial class M7S_Tank_Assistant
{
    private Config _config => Controller.GetConfig<Config>();
    private string _basePlayerOverride = "";
    private IPlayerCharacter BasePlayer
    {
        get
        {
            if (_basePlayerOverride == "")
                return Player.Object;
            return Svc.Objects.OfType<IPlayerCharacter>()
                .FirstOrDefault(x => x.Name.ToString().EqualsIgnoreCase(_basePlayerOverride)) ?? Player.Object;
        }
    }

    public class Config :IEzConfig
    {
        public bool UseTargetEnforcer = false;
        public bool TargetNorth = false;
        public bool TargetEast = false;
        public bool TargetSouth = false;
        public bool TargetWest = false;
        public bool UseAutoInterject = false;
        public enum InterjectType
        {
            UseTooCloseMob = 0,
            FindAboveEnemyList = 1,
            FindBelowEnemyList = 2,
            WaitAtleast1Mob = 3,
        }
        public InterjectType UseAutoInterjectType = InterjectType.UseTooCloseMob;
    }

    public override void OnSettingsDraw()
    {
        ImGui.Checkbox("Use Target Enforcer", ref _config.UseTargetEnforcer);
        if (_config.UseTargetEnforcer)
        {
            bool prevTargetNorth = _config.TargetNorth;
            bool prevTargetEast = _config.TargetEast;
            bool prevTargetSouth = _config.TargetSouth;
            bool prevTargetWest = _config.TargetWest;
            ImGui.Checkbox("Target North", ref _config.TargetNorth);
            ImGui.Checkbox("Target East", ref _config.TargetEast);
            ImGui.Checkbox("Target South", ref _config.TargetSouth);
            ImGui.Checkbox("Target West", ref _config.TargetWest);
            int trueCount = 0;
            foreach (var targetConfig in new[] { _config.TargetNorth, _config.TargetEast, _config.TargetSouth, _config.TargetWest })
            {
                if (targetConfig) trueCount++;
            }
            if (trueCount > 2)
            {
                // Revert to previous state
                _config.TargetNorth = prevTargetNorth;
                _config.TargetEast = prevTargetEast;
                _config.TargetSouth = prevTargetSouth;
                _config.TargetWest = prevTargetWest;
            }
        }
        ImGui.Checkbox("Use Auto Interject", ref _config.UseAutoInterject);
        if (_config.UseAutoInterject)
        {
            ImGui.Text("Interject Type");
            if (ImGui.BeginCombo("Interject Type", _config.UseAutoInterjectType.ToString()))
            {
                foreach (var type in Enum.GetValues(typeof(Config.InterjectType)))
                {
                    bool isSelected = _config.UseAutoInterjectType == (Config.InterjectType)type;
                    if (ImGui.Selectable(type.ToString(), isSelected))
                    {
                        _config.UseAutoInterjectType = (Config.InterjectType)type;
                    }

                    if (isSelected)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }
                ImGui.EndCombo();
            }
        }

        if (ImGuiEx.CollapsingHeader("Debug"))
        {
            ImGui.Text($"Gimmick Active: {_gimmickActive}");
            ImGui.Text($"Mob Spawned: {_mobSpawned}");
            ImGui.Text($"Scatter Seed Counts: {_scatterSeedCounts}");
            ImGui.Text($"IsTargetDone: {_isTargetDone}");
            ImGui.Text($"IsInterjectDone: {_isInterjectDone}");
            ImGui.Text($"Mob Count: {_bloomingAbominationEnemyList.Count()}");
            if (_bloomingAbominationEnemyList.Count() > 0)
            {
                ImGuiEx.EzTable(
                    _bloomingAbominationEnemyList
                        .Where(x => x != null)
                        .SelectMany(x => new[]
                        {
                            new ImGuiEx.EzTableEntry("Mob Id", () => ImGui.Text($"{x.EntityId}")),
                            new ImGuiEx.EzTableEntry("Mob Name", true, () => ImGui.Text($"{x.NameId}")),
                            new ImGuiEx.EzTableEntry("Mob Position", true, () => ImGui.Text($"{x.Position}")),
                            new ImGuiEx.EzTableEntry("Mob IsCasting", true, () => ImGui.Text($"{x.IsCasting}")),
                            new ImGuiEx.EzTableEntry("Mob IsTargetable", true, () => ImGui.Text($"{x.IsTargetable}")),
                            new ImGuiEx.EzTableEntry("Mob CastActionId", true, () => ImGui.Text($"{x.CastActionId}")),
                            new ImGuiEx.EzTableEntry("Mob TargetObjectId", true, () => ImGui.Text($"{x.TargetObjectId}"))
                        })
                );
            }
            ImGui.Text($"Mob Id By Pos Count: {_mobIdByPosList.Count()}");
            if (_mobIdByPosList.Count() > 0)
            {
                ImGuiEx.EzTable(
                    _mobIdByPosList
                        .SelectMany(x => new[]
                        {
                            new ImGuiEx.EzTableEntry("Mob MarkerPos", () => ImGui.Text($"{x.Item2}")),
                            new ImGuiEx.EzTableEntry("Mob Position", true, () => ImGui.Text($"{x.Item1}"))
                        })
                );
            }

            ImGui.SetNextItemWidth(200);
            ImGui.InputText("Player override", ref _basePlayerOverride, 50);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(200);
            if (ImGui.BeginCombo("Select..", "Select..."))
            {
                foreach (var x in Svc.Objects.OfType<IPlayerCharacter>())
                {
                    if (x.GetRole() != CombatRole.Tank) continue;
                    if (ImGui.Selectable(x.GetNameWithWorld()))
                    {
                        _basePlayerOverride = x.Name.ToString();
                    }
                }
                ImGui.EndCombo();
            }
        }
    }
}
