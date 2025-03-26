using Dalamud.Game.ClientState.Conditions;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;

namespace SplatoonScriptsOfficial.Duties.Dawntrail.The_Futures_Rewritten.AutoBuffs.NIN;
internal unsafe class Auto_Fire_buffs :SplatoonScript
{
    #region types
    /********************************************************************/
    /* types                                                            */
    /********************************************************************/

    private enum Phase
    {
        None = 0,
        P1,
        P2,
        P2_5,
        P3,
        P4,
        P5,
    }

    private class QueueData
    {
        public uint actionId = 0u;
        public uint Delay = 0u;
        public bool usedAction = false;
        public uint ActionCount = 0u;
        public long now = (long)0u;

        public QueueData(uint actionId, uint Delay)
        {
            this.actionId = actionId;
            this.Delay = Delay;
        }
    }
    #endregion

    #region class
    /********************************************************************/
    /* class                                                            */
    /********************************************************************/

    public class Config :IEzConfig { }
    #endregion

    #region const
    /********************************************************************/
    /* const                                                            */
    /********************************************************************/

    private const uint kTryActionCount = 100u;
    #endregion

    #region public properties
    /********************************************************************/
    /* public properties                                                */
    /********************************************************************/

    public override HashSet<uint>? ValidTerritories => [1238];
    public override Metadata? Metadata => new(1, "redmoon");
    #endregion

    #region private properties
    /********************************************************************/
    /* private properties                                               */
    /********************************************************************/

    private ActionManager* actionManager = ActionManager.Instance();
    private Queue<QueueData> actionQueue = new Queue<QueueData>();
    private QueueData? workingQueueData = null;
    private Phase phase = Phase.None;
    long lastUpdateTick = (long)0u;
    #endregion

    #region public methods
    /********************************************************************/
    /* public methods                                                   */
    /********************************************************************/

    public override void OnStartingCast(uint source, uint castId)
    {
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
    }

    public override void OnMessage(string Message)
    {
        if (Message.Contains("あくまで静寂を乱そうというのか……。"))
        {
            phase = Phase.P2;
        }
        if (Message.Contains("このままじゃ、リーンの魂がもたない！"))
        {
            phase = Phase.P2_5;
        }
        if (Message.Contains("私は、アシエン・アログリフ…… 戒律王の巫女……！"))
        {
            phase = Phase.P2_5;
        }
    }

    public override void OnUpdate()
    {
        // Update Freq 500ms
        if (lastUpdateTick != 0 && Environment.TickCount64 - lastUpdateTick < 500) return;
        lastUpdateTick = Environment.TickCount64;

        UpdateFireBuffEffects();
    }

    public override void OnDirectorUpdate(DirectorUpdateCategory category)
    {
        if (category == (DirectorUpdateCategory)0x40002476)
        {
            phase = Phase.P1;
        }
    }

    public override void OnReset()
    {
        phase = Phase.None;
        workingQueueData = null;
        actionQueue.Clear();
    }

    public override void OnSettingsDraw()
    {
        ImGui.Text($"Phase: {phase}");
    }
    #endregion

    #region private methods
    /********************************************************************/
    /* private methods                                                  */
    /********************************************************************/

    private void EnqueueFireBuff(uint actionId, uint Delay)
    {
        actionQueue.Enqueue(new QueueData(actionId, Delay));
    }

    private void UpdateFireBuffEffects()
    {
        if (workingQueueData == null || workingQueueData.usedAction)
        {
            if (actionQueue.Count <= 0) return;
            FireBuffEffects(actionQueue.Dequeue());
        }
        else
        {
            FireBuffEffects();
        }
    }

    private void FireBuffEffects(QueueData? queueData = null)
    {
        if (queueData != null)
        {
            if (queueData.actionId == 0u) return;
            workingQueueData = actionQueue.Dequeue();
            workingQueueData.now = Environment.TickCount64;
        }

        if (workingQueueData == null) return;

        if (Environment.TickCount64 - workingQueueData.now < workingQueueData.Delay) return;

        if (GetRemainingTime(workingQueueData.actionId, out float remainingTime))
        {
            if (remainingTime >= 10f) return;
        }

        if (workingQueueData.usedAction) return;

        if (actionManager->AnimationLock == 0 &&
            !actionManager->IsRecastTimerActive(ActionType.Action, workingQueueData.actionId) &&
            !workingQueueData.usedAction)
        {
            if (Svc.Condition[ConditionFlag.DutyRecorderPlayback])
            {
                DuoLog.Information($"Used {workingQueueData.actionId}");
            }

            actionManager->UseAction(ActionType.Action, workingQueueData.actionId);

            if (actionManager->IsRecastTimerActive(ActionType.Action, workingQueueData.actionId))
            {
                workingQueueData.usedAction = true;
            }
            else
            {
                workingQueueData.ActionCount++;
            }

            if (workingQueueData.ActionCount > kTryActionCount)
            {
                DuoLog.Error($"Failed to use action {workingQueueData.actionId}");
                workingQueueData.usedAction = true;
                workingQueueData.ActionCount = 0;
            }
        }
    }

    bool GetRemainingTime(uint actionId, out float remainingTime)
    {
        for (int i = 0; i < 80; i++)
        {
            if (actionManager->Cooldowns[i].ActionId == actionId)
            {
                remainingTime = actionManager->Cooldowns[i].Total - actionManager->Cooldowns[i].Elapsed;
                return true;
            }
        }
        remainingTime = 0f;
        return false;
    }
    #endregion
}
