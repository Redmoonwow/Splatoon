using ECommons.Automation;
using ECommons.Automation.UIInput;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using ECommons.UIHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SplatoonScriptsOfficial.Generic;
internal unsafe partial class B_Miner_Cosmo :SplatoonScript
{
    public enum State
    {
        CloseMission = -2,
        AbandonMission = -1,
        Inactive = 0,
        Active,
        OpenMission,
        CheckWKSMission,
        SelectMission,
        SelectYesNo,
        MoveTargetPoint,
        InteractTargetPoint,
    }

    private struct MissinData
    {
        public int Index;
        public uint JobID;
        public uint MissionID;
        public uint Rank;
        public uint Unknown;
    }

    public override HashSet<uint> ValidTerritories => [1237];
    [MemberNotNull(nameof(_instance))]
    public static B_Miner_Cosmo Instance
    {
        get
        {
            if (_instance == null)
                throw new InvalidOperationException("B_Miner_Cosmo is not initialized.");
            return _instance;
        }
    }

    private static B_Miner_Cosmo? _instance = null;
    private State _lastState = State.Inactive;
    private State _state = State.Inactive;
    private State _nextState = State.Inactive;
    private ActionManager* _actionManager = ActionManager.Instance();
    private ExpectAwaiter _expectAwaiter = new ExpectAwaiter(
        onExpectedReachedFunc: &OnExpectReached,
        onTimeoutReachedFunc: &OnTimeoutReached
        );

    public B_Miner_Cosmo()
    {
        if (_instance != null) return;
        _instance = this;
        this.OnReset();
    }

    public override void OnSettingsDraw()
    {
        if (ImGui.Button("Find Mission"))
        {
            _state = State.Active;
        }
        if (ImGui.Button("Reset"))
        {
            this.OnReset();
        }

        ImGui.Text($"State: {_state}");

        var potCDElapsed = _actionManager->GetRecastTimeElapsed(ActionType.Item, 12669);
        var potCDTotal = _actionManager->GetRecastTime(ActionType.Item, 12669);

        ImGuiEx.Text($"PotData: {potCDTotal - potCDElapsed}");

        var missionDataList = ParseWKSMissionAtkValues();
        List<ImGuiEx.EzTableEntry> tableEntries = new();
        foreach (var data in missionDataList)
        {
            tableEntries.Add(new ImGuiEx.EzTableEntry("Index", () => ImGuiEx.Text(data.Index.ToString())));
            tableEntries.Add(new ImGuiEx.EzTableEntry("Job ID", () => ImGuiEx.Text(data.JobID.ToString())));
            tableEntries.Add(new ImGuiEx.EzTableEntry("Mission ID", () => ImGuiEx.Text(data.MissionID.ToString())));
            tableEntries.Add(new ImGuiEx.EzTableEntry("Rank", () => ImGuiEx.Text(data.Rank.ToString())));
            tableEntries.Add(new ImGuiEx.EzTableEntry("Unknown", () => ImGuiEx.Text(data.Unknown.ToString())));
        }
        ImGuiEx.EzTable("Mission Data", tableEntries);
    }

    public override void OnUpdate()
    {
        var _ = _expectAwaiter.Update();
        if (_expectAwaiter.IsBusy) return;

        switch (_state)
        {
            case State.CloseMission:
                CloseWKSMission();
                break;
            case State.AbandonMission:
                AbandonWKSMission();
                break;
            case State.Inactive:
                break;
            case State.Active:
                ActiveState();
                break;
            case State.OpenMission:
                OpenWKSMission();
                break;
            case State.CheckWKSMission:
                CheckWKSMission();
                break;
            case State.SelectMission:
                SelectMission();
                break;
            case State.SelectYesNo:
                YesNo();
                break;
            default:
                break;
        }
    }

    public override void OnReset()
    {
        _state = State.Inactive;
        _expectAwaiter.Abort();
    }

    private static void OnExpectReached()
    {
        var instance = B_Miner_Cosmo.Instance;
        instance.ChangeState(instance._nextState);
        instance._nextState = B_Miner_Cosmo.State.Inactive;
    }

    private static void OnTimeoutReached()
    {
        var instance = B_Miner_Cosmo.Instance;
        instance.OnReset();
    }

    private void ChangeState(State state)
    {
        _lastState = _state;
        _state = state;
    }

    private List<MissinData> ParseWKSMissionAtkValues()
    {
        var missionData = new List<MissinData>();
        var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("WKSHud");
        var mission = (AtkUnitBase*)Svc.GameGui.GetAddonByName("WKSMission");
        if (addon == null || !addon->IsVisible) return missionData;
        if (mission == null || !mission->IsVisible) return missionData;

        bool isSkip = false;
        int index = 1;
        for (var i = 36; i < mission->AtkValuesCount; i++)
        {
            if (!isSkip)
            {
                var data1 = mission->AtkValues[i];
                var data2 = mission->AtkValues[i + 1];
                var data3 = mission->AtkValues[i + 2];
                var data4 = mission->AtkValues[i + 3];
                missionData.Add(new MissinData
                {
                    Index = index,
                    JobID = data1.UInt,
                    MissionID = data2.UInt,
                    Rank = data3.UInt,
                    Unknown = data4.UInt
                });
            }
            index++;
            i += 4;

            // Check Skip
            var data5 = mission->AtkValues[i];
            if (data5.Type == FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Undefined)
            {
                break;
            }
            else if (data5.UInt == 0)
            {
                isSkip = false;
            }
            else if (data5.UInt == 4)
            {
                isSkip = true;
            }
            else
            {
                break;
            }
        }

        return missionData;
    }

    private AtkResNode* SearchResNode(AtkUnitBase* unit, uint nodeId)
    {
        AtkResNode* returnNode = null;
        for (int i = 0; i < unit->UldManager.NodeListCount; i++)
        {
            var node = unit->UldManager.NodeList[i];
            if (node->NodeId == nodeId)
            {
                returnNode = node;
                break;
            }
        }

        return returnNode;
    }

    private AtkResNode* SearchResNode(AtkComponentBase* comp, uint nodeId)
    {
        AtkResNode* returnNode = null;
        for (int i = 0; i < comp->UldManager.NodeListCount; i++)
        {
            var node = comp->UldManager.NodeList[i];
            if (node->NodeId == nodeId)
            {
                returnNode = node;
                break;
            }
        }

        return returnNode;
    }
}

// State Handler
internal unsafe partial class B_Miner_Cosmo
{
    private void CloseWKSMission()
    {
        var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("WKSHud");
        var mission = (AtkUnitBase*)Svc.GameGui.GetAddonByName("WKSMission");
        if (addon == null || !addon->IsVisible) return;
        if (mission == null || !mission->IsVisible) return;

        var btnRootNode = SearchResNode(addon, 6u);
        if (btnRootNode == null) return;
        var btnComponent = (AtkComponentButton*)btnRootNode->GetComponent();
        if (btnComponent == null) return;
        btnComponent->ClickAddonButton(addon);
    }

    private void AbandonWKSMission()
    {
        var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("WKSHud");
        var mission = (AtkUnitBase*)Svc.GameGui.GetAddonByName("WKSMission");
        if (addon == null || !addon->IsVisible) return;
        if (mission == null || !mission->IsVisible)
        {
            _state = State.OpenMission;
        }

        var MissionData = ParseWKSMissionAtkValues();
        var data = MissionData.FirstOrDefault(x => x.Index == 8);
        Callback.Fire(mission, true, 13, data.MissionID);

        _nextState = State.SelectYesNo;
        _expectAwaiter.Enqueue(&WaitOpenSelectYesNo, 1000);
    }

    private void ActiveState()
    {
        var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("WKSHud");
        var mission = (AtkUnitBase*)Svc.GameGui.GetAddonByName("WKSMission");
        if (addon == null || !addon->IsVisible) return;
        if (mission == null || !mission->IsVisible) _state = State.OpenMission;
        else _state = State.CheckWKSMission;
    }

    private void OpenWKSMission()
    {
        var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("WKSHud");
        if (addon == null || !addon->IsVisible) return;

        var btnRootNode = SearchResNode(addon, 6u);
        if (btnRootNode == null) return;
        var btnComponent = (AtkComponentButton*)btnRootNode->GetComponent();
        if (btnComponent == null) return;
        btnComponent->ClickAddonButton(addon);

        if (_lastState == State.Active) _nextState = State.CheckWKSMission;
        if (_lastState == State.AbandonMission) _nextState = State.AbandonMission;
        _expectAwaiter.Enqueue(&WaitOpenedWKSMission, 1000);
    }

    private void CheckWKSMission()
    {
        var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("WKSHud");
        var mission = (AtkUnitBase*)Svc.GameGui.GetAddonByName("WKSMission");
        if (addon == null || !addon->IsVisible) return;
        if (mission == null || !mission->IsVisible)
        {
            OpenWKSMission();
            return;
        }

        var MissionData = ParseWKSMissionAtkValues();

        bool isFound = false;
        foreach (var data in MissionData)
        {
            if (data.MissionID == 381)
            {
                isFound = true;
                break;
            }
        }

        if (isFound)
        {
            _state = State.Inactive;
            return;
            _state = State.SelectMission;
        }
        else
        {
            _state = State.AbandonMission;
        }
    }

    private void OpenWKSMissionInfo()
    {
        var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("WKSHud");
        if (addon == null || !addon->IsVisible) return;

        var btnRootNode = SearchResNode(addon, 6u);
        if (btnRootNode == null) return;
        var btnComponent = (AtkComponentButton*)btnRootNode->GetComponent();
        if (btnComponent == null) return;
        btnComponent->ClickAddonButton(addon);
    }


    private void YesNo()
    {
        var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("WKSHud");
        var yesno = AddonFinder.YesNo.ToArray().FirstOrDefault();
        if (yesno == null) return;

        yesno.Yes();

        if (_lastState == State.AbandonMission)
        {
            _nextState = State.AbandonWKSMissionAfter;
            _expectAwaiter.Enqueue(&WaitOpenedWKSMission, 1000);
        }
        else
        {
            _nextState = State.Inactive;
            _expectAwaiter.Enqueue(&WaitOpenedWKSMission, 1000);
        }

    }

    private void AbandonWKSMissionAfter()
    {
        var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("WKSHud");
        var missionInfo = (AtkUnitBase*)Svc.GameGui.GetAddonByName("WKSMissionInfomation");
        if (addon == null || !addon->IsVisible) return;
        if (missionInfo == null || !missionInfo->IsVisible)
        {
            _taskManager.Enqueue(OpenWKSMissionInfo);
            _taskManager.EnqueueDelay(_delay);
            _taskManager.Enqueue(AbandonWKSMissionAfter);
            return;
        }

        var btnRootNode = SearchResNode(missionInfo, 30u);
        if (btnRootNode == null) return;
        var btnComponent = (AtkComponentButton*)btnRootNode->GetComponent();
        if (btnComponent == null) return;
        btnComponent->ClickAddonButton(missionInfo);

        _taskManager.EnqueueDelay(_delay);
        _taskManager.Enqueue(YesNo);
        _taskManager.EnqueueDelay(_delay);
        _taskManager.Enqueue(OpenWKSMission);
        _taskManager.EnqueueDelay(_delay);
        _taskManager.Enqueue(CheckWKSMission);
    }

    private static bool WaitOpenedWKSMission()
    {
        var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("WKSHud");
        if (addon == null || !addon->IsVisible) return false;
        var mission = (AtkUnitBase*)Svc.GameGui.GetAddonByName("WKSMission");
        if (mission == null || !mission->IsVisible) return false;
        return true;
    }

    private static bool WaitOpenSelectYesNo()
    {
        var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("SelectString");
        if (addon == null || !addon->IsVisible) return false;
        return true;
    }
}

internal unsafe partial class B_Miner_Cosmo
{
    private class ExpectAwaiter
    {
        public enum ReturnCode
        {
            Error = 0,
            Success,
            Timeout,
            Aborted,
            Pending,
        }

        private struct QueueData
        {
            public delegate*<bool> ExpectNextFunc;
            public long TimeoutTime;
            public QueueData(delegate*<bool> expectNextFunc, long timeoutTime)
            {
                ExpectNextFunc = expectNextFunc;
                TimeoutTime = timeoutTime;
            }

            public bool IsEmpty => ExpectNextFunc == null;
            public bool IsBusy => !IsEmpty;

            public bool IsTimeout()
            {
                if (TimeoutTime == 0) return false;
                return TimeoutTime < Environment.TickCount64;
            }

            public bool ShouldNextState()
            {
                if (ExpectNextFunc == null) return false;
                return ExpectNextFunc();
            }

            public void Clear()
            {
                ExpectNextFunc = null;
                TimeoutTime = 0;
            }

            public long GetRemainingTime()
            {
                if (TimeoutTime == 0) return 0;
                return TimeoutTime - Environment.TickCount64;
            }
        }

        private QueueData _queueData = new QueueData(null, 0);
        private ReturnCode _lastReturnCode = ReturnCode.Error;
        private long _commonTimeoutMilliSeconds = 300;
        private delegate*<bool> _lastSetFunc = null;
        private delegate*<void> _onExpectedReachedFunc = null;
        private delegate*<void> _onTimeoutReachedFunc = null;

        public bool IsBusy => _queueData.IsBusy;
        public bool IsEmpty => _queueData.IsEmpty;
        public ReturnCode CheckLast() => _lastReturnCode;
        public long GetRemainingTime() => _queueData.GetRemainingTime();

        public ExpectAwaiter(
            long commonTimeoutMilliSeconds = 60000,
            delegate*<void> onExpectedReachedFunc = null,
            delegate*<void> onTimeoutReachedFunc = null)
        {
            _commonTimeoutMilliSeconds = commonTimeoutMilliSeconds;
            _onExpectedReachedFunc = onExpectedReachedFunc;
            _onTimeoutReachedFunc = onTimeoutReachedFunc;
        }

        public void Enqueue(delegate*<bool> expectNextFunc, int timeoutMilliSeconds = int.MaxValue)
        {
            if (_queueData.IsBusy)
            {
                DuoLog.Error($"TaskManager is busy!");
                return;
            }

            long timeoutTime = Environment.TickCount64 + _commonTimeoutMilliSeconds;
            if (timeoutMilliSeconds != int.MaxValue) timeoutTime = Environment.TickCount64 + timeoutMilliSeconds;

            _queueData = new QueueData(expectNextFunc, timeoutTime);
            _lastSetFunc = expectNextFunc;
        }

        public ReturnCode Update()
        {
            if (_queueData.IsEmpty)
            {
                return _lastReturnCode = ReturnCode.Error;
            }
            if (_queueData.IsTimeout())
            {
                _queueData.Clear();
                if (_onTimeoutReachedFunc != null) _onTimeoutReachedFunc();
                return _lastReturnCode = ReturnCode.Timeout;
            }
            if (_queueData.ShouldNextState())
            {
                _queueData.Clear();
                if (_onExpectedReachedFunc != null) _onExpectedReachedFunc();
                return _lastReturnCode = ReturnCode.Success;
            }
            return _lastReturnCode = ReturnCode.Pending; ;
        }

        public void Abort()
        {
            if (_queueData.IsBusy)
            {
                _queueData.Clear();
                _lastReturnCode = ReturnCode.Aborted;
            }
        }
    }
}
