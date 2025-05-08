using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;

namespace SplatoonScriptsOfficial.Tests;
internal unsafe class ActionTest :SplatoonScript
{
    public override HashSet<uint> ValidTerritories => new();
    private ActionManager* _actionManager = ActionManager.Instance();
    private TaskManager _taskManager = new();
    private ActionType _usedActionType = ActionType.Action;
    private uint _setActionId = 0u;
    private long _timeoutTick = 0u;

    public override void OnSettingsDraw()
    {
        ImGui.Text($"AL: {_actionManager->AnimationLock}");
        ImGui.Text($"ActionQueue: {_actionManager->ActionQueued} {_actionManager->QueuedActionId} {_actionManager->QueuedActionType}");
        ImGui.Text("Fire Buffs");
        fixed (uint* setActionIdPtr = &_setActionId)
        {
            if (ImGui.BeginCombo("##ActionType", "ActionType"))
            {
                int i = 0;
                foreach (var actionType in Enum.GetValues(typeof(ActionType)))
                {
                    ImGui.PushID($"{actionType}{i}");
                    if (ImGui.Selectable(actionType.ToString(), _usedActionType == (ActionType)actionType))
                    {
                        _usedActionType = (ActionType)actionType;
                    }
                    ImGui.PopID();
                    i++;
                }
                ImGui.EndCombo();
            }
            ImGui.InputScalar("ActionId", ImGuiDataType.U32, (IntPtr)setActionIdPtr);
        }
        ImGui.Text($"ActionType: {_usedActionType}");
        var actionName = GetActionName(_usedActionType, _setActionId);
        ImGui.Text($"Action: {actionName}");
        if (ImGui.Button("SetAction"))
        {
            if (_setActionId == 0 || _usedActionType == ActionType.None)
            {
                DuoLog.Error("SetAction: ActionId or ActionType is not set");
                return;
            }

            _taskManager.Enqueue(() =>
                {
                    long timeoutTick = Environment.TickCount64 + 3000;
                    var copyid = _setActionId;
                    var copytype = _usedActionType;
                    this.UseAction(copytype, copyid, timeoutTick);
                });
        }

        if (ImGuiEx.CollapsingHeader("ActionManager"))
        {
            ImGui.Text($"8:   AnimationLock: {_actionManager->AnimationLock}");
            ImGui.Text($"12:  CompanionActionCooldown: {_actionManager->CompanionActionCooldown}");
            ImGui.Text($"16:  BuddyActionCooldown: {_actionManager->BuddyActionCooldown}");
            ImGui.Text($"20:  PetActionCooldown: {_actionManager->PetActionCooldown}");
            ImGui.Text($"24:  NumPetActionsOnCooldown: {_actionManager->NumPetActionsOnCooldown}");
            ImGui.Text($"36:  CastSpellId: {_actionManager->CastSpellId}");
            ImGui.Text($"40:  CastActionType: {_actionManager->CastActionType}");
            ImGui.Text($"44:  CastActionId: {_actionManager->CastActionId}");
            ImGui.Text($"48:  CastTimeElapsed: {_actionManager->CastTimeElapsed}");
            ImGui.Text($"52:  CastTimeTotal: {_actionManager->CastTimeTotal}");
            ImGui.Text($"56:  CastTargetId: {_actionManager->CastTargetId}");
            ImGui.Text($"64:  CastTargetPosition: {_actionManager->CastTargetPosition}");
            ImGui.Text($"80:  CastRotation: {_actionManager->CastRotation}");
            ImGui.Text($"96:  Combo: {_actionManager->Combo}");
            ImGui.Text($"104: ActionQueued: {_actionManager->ActionQueued}");
            ImGui.Text($"108: QueuedActionType: {_actionManager->QueuedActionType}");
            ImGui.Text($"112: QueuedActionId: {_actionManager->QueuedActionId}");
            ImGui.Text($"120: QueuedTargetId: {_actionManager->QueuedTargetId}");
            ImGui.Text($"128: QueuedExtraParam: {_actionManager->QueuedExtraParam}");
            ImGui.Text($"132: QueueType: {_actionManager->QueueType}");
            ImGui.Text($"136: QueuedComboRouteId: {_actionManager->QueuedComboRouteId}");
            ImGui.Text($"144: AreaTargetingActionId: {_actionManager->AreaTargetingActionId}");
            ImGui.Text($"148: AreaTargetingActionType: {_actionManager->AreaTargetingActionType}");
            ImGui.Text($"152: AreaTargetingSpellId: {_actionManager->AreaTargetingSpellId}");
            ImGui.Text($"160: AreaTargetingExecuteAtObject: {_actionManager->AreaTargetingExecuteAtObject}");
            ImGui.Text($"176: AreaTargetingVfx1: {new IntPtr(_actionManager->AreaTargetingVfx1):X}");
            ImGui.Text($"184: AreaTargetingVfx2: {new IntPtr(_actionManager->AreaTargetingVfx2):X}");
            ImGui.Text($"192: AreaTargetingExecuteAtCursor: {_actionManager->AreaTargetingExecuteAtCursor}");
            ImGui.Text($"240: BallistaActive: {_actionManager->BallistaActive}");
            ImGui.Text($"241: BallistaRowId: {_actionManager->BallistaRowId}");
            ImGui.Text($"256: BallistaOrigin: {_actionManager->BallistaOrigin}");
            ImGui.Text($"272: BallistaRefAngle: {_actionManager->BallistaRefAngle}");
            ImGui.Text($"276: BallistaRadius: {_actionManager->BallistaRadius}");
            ImGui.Text($"280: BallistaEntityId: {_actionManager->BallistaEntityId}");
            ImGui.Text($"288: LastUsedActionSequence: {_actionManager->LastUsedActionSequence}");
            ImGui.Text($"290: LastHandledActionSequence: {_actionManager->LastHandledActionSequence}");
            ImGui.Text($"2024: DistanceToTargetHitbox: {_actionManager->DistanceToTargetHitbox}");

            for (int i = 0; i < 80; i++)
            {
                if (_actionManager->Cooldowns[i].ActionId == 0) continue;
                ImGuiEx.EzTable(new[]
                {
                    new ImGuiEx.EzTableEntry("ActionId", () => ImGui.Text($"{_actionManager->Cooldowns[i].ActionId}")),
                    new ImGuiEx.EzTableEntry("Total", () => ImGui.Text($"{_actionManager->Cooldowns[i].Total}")),
                    new ImGuiEx.EzTableEntry("Elapsed", () => ImGui.Text($"{_actionManager->Cooldowns[i].Elapsed}")),
                    new ImGuiEx.EzTableEntry("IsActive", () => ImGui.Text($"{_actionManager->Cooldowns[i].IsActive}"))
                });
            }
        }
    }

    public void UseAction(ActionType actionType, uint actionId, long timeoutTick)
    {
        var seq = _actionManager->LastUsedActionSequence;
        if (_actionManager->AnimationLock == 0 &&
            !_actionManager->IsRecastTimerActive(actionType, actionId))
        {
            _actionManager->UseAction(actionType, actionId);
        }

        if (actionType is not ActionType.Action) return;

        if (seq == _actionManager->LastUsedActionSequence)
        {
            // Timeout
            if (timeoutTick <= Environment.TickCount64)
            {
                DuoLog.Error($"Failed to use action {actionId} of type {actionType}");
                return;
            }
            // Retry
            else
            {
                _taskManager.Enqueue(() =>
                {
                    long copytimeoutTick = timeoutTick;
                    var copyid = actionId;
                    var copytype = actionType;
                    this.UseAction(copytype, copyid, copytimeoutTick);
                });
            }
        }
    }

    private string GetActionName(ActionType actionType, uint actionId)
    {
        return actionType switch
        {
            ActionType.Action => GetName<Lumina.Excel.Sheets.Action>(actionId, row => row.Name.ToString()),
            ActionType.Item => GetName<Item>(actionId, row => row.Name.ToString()),
            ActionType.GeneralAction => GetName<GeneralAction>(actionId, row => row.Name.ToString()),
            ActionType.MainCommand => GetName<MainCommand>(actionId, row => row.Name.ToString()),
            _ => "",
        };
    }

    private string GetName<T>(uint id, Func<T, string> nameSelector) where T : struct, IExcelRow<T>
    {
        var row = Svc.Data.GetExcelSheet<T>()?.GetRowOrDefault(id);
        return row.HasValue ? nameSelector(row.Value) : "";
    }
}
