using ECommons.ImGuiMethods;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;

namespace SplatoonScriptsOfficial.Tests;
internal unsafe class ActionTest :SplatoonScript
{
    public override HashSet<uint> ValidTerritories => new();
    private ActionManager* actionManager = ActionManager.Instance();
    private uint usedActionId = 0u;
    private uint setActionId = 0u;
    private bool usedAction = true;
    private const uint kTryActionCount = 10u;
    private uint ActionCount = 0u;
    public override void OnSettingsDraw()
    {
        ImGui.Text($"AL: {actionManager->AnimationLock}");
        ImGui.Text($"ActionQueue: {actionManager->ActionQueued} {actionManager->QueuedActionId} {actionManager->QueuedActionType}");
        ImGui.Text("Fire Buffs");
        fixed (uint* setActionIdPtr = &setActionId)
        {
            ImGui.InputScalar("ActionId", ImGuiDataType.U32, (IntPtr)setActionIdPtr);
        }
        if (ImGui.Button("SetAction"))
        {
            usedAction = false;
            usedActionId = setActionId;
            ActionCount = 0;
        }

        if (ImGuiEx.CollapsingHeader("Cooldowns"))
        {
            for (int i = 0; i < 80; i++)
            {
                ImGui.Text($"ActionStatus {i}: {actionManager->Cooldowns[i].ActionId} {actionManager->Cooldowns[i].Total}");
                ImGui.Text($"ActionStatus {i}: {actionManager->Cooldowns[i].IsActive} {actionManager->Cooldowns[i].Elapsed}");
            }
        }

        if (usedActionId == 0) return;
        float remainingTime = 0f;
        for (int i = 0; i < 80; i++)
        {
            if (actionManager->Cooldowns[i].ActionId == usedActionId)
            {
                remainingTime = actionManager->Cooldowns[i].Total - actionManager->Cooldowns[i].Elapsed;
                break;
            }
        }
        ImGui.Text($"ReminingTime: {remainingTime}");
        ImGui.Text($"IsRecastTimerActive: {actionManager->IsRecastTimerActive(ActionType.Action, usedActionId)}");
        if (actionManager->AnimationLock == 0 &&
            !actionManager->IsRecastTimerActive(ActionType.Action, usedActionId) &&
            usedAction == false)
        {
            actionManager->UseAction(ActionType.Action, usedActionId);

            if (actionManager->IsRecastTimerActive(ActionType.Action, usedActionId))
            {
                usedAction = true;
            }
            else
            {
                ActionCount++;
            }

            if (ActionCount > kTryActionCount)
            {
                DuoLog.Error($"Failed to use action {usedActionId}");
                usedAction = true;
                usedActionId = 0;
            }
        }
    }
}
