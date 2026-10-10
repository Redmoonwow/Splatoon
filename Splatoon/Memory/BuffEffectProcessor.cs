using ECommons.GameHelpers;
using ECommons.GameHelpers.LegacyPlayer;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Splatoon.SplatoonScripting;
using System.Runtime.CompilerServices;

namespace Splatoon.Memory;

internal unsafe class BuffEffectProcessor : IDisposable
{
    #region privateDefine
    private const int MAX_STATUS_NUM = 60;
    // There are 629 object slots in total, but only 299 objects up to EventObject are needed.
    private const int MAX_OBJECT_NUM = 299;
    private const uint INVALID_OBJECTID = 0xE0000000;

    // Statuses seen on the previous frame: one entity ID and MAX_STATUS_NUM statuses per object slot.
    private readonly uint[] ObjectIds = new uint[MAX_OBJECT_NUM];
    private readonly Status[] Statuses = new Status[MAX_OBJECT_NUM * MAX_STATUS_NUM];
    private bool IsRunning = true;
    #endregion

    #region public
    public void Dispose()
    {
        IsRunning = false;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void ActorEffectUpdate()
    {
        if(!IsRunning) return;
        try
        {
            Update();
        }
        catch(Exception ex)
        {
            DuoLog.Error($"[Splatoon]: {ex.Message}");
            IsRunning = false;
        }
    }
    #endregion

    private void Update()
    {
        var objects = GameObjectManager.Instance()->Objects.IndexSorted;

        for(var i = 0; i < MAX_OBJECT_NUM; ++i)
        {
            var gameObject = objects[i].Value;
            if(gameObject == null) continue;
            if(!gameObject->IsCharacter()) continue;
            if(gameObject->EntityId == INVALID_OBJECTID) continue;
            var character = (Character*)gameObject;
            var sm = character->GetStatusManager();
            if(sm == null) continue;
            var current = sm->Status[..Math.Min((int)sm->NumValidStatuses, MAX_STATUS_NUM)];
            var previous = Statuses.AsSpan(i * MAX_STATUS_NUM, MAX_STATUS_NUM);

            // New object: take over its current statuses without raising events
            if(ObjectIds[i] != character->EntityId)
            {
                ObjectIds[i] = character->EntityId;
                previous.Clear();
                current.CopyTo(previous);
                continue;
            }

            // Existing object
            // Check status change
            for(var j = 0; j < current.Length; ++j)
            {
                ref var prev = ref previous[j];
                var cur = current[j];
                if(prev.StatusId != cur.StatusId)
                {
                    if(prev.StatusId != 0)
                    {
                        // Remove
                        RemoveStatusLog(prev, character);
                        ScriptingProcessor.OnRemoveBuffEffect(character->EntityId, prev);
                    }
                    if(cur.StatusId != 0)
                    {
                        // Gain
                        AddStatusLog(cur, character);
                        ScriptingProcessor.OnGainBuffEffect(character->EntityId, cur);
                    }
                }
                else if(cur.StatusId != 0 && prev.Param != cur.Param)
                {
                    // Update
                    UpdateStatusLog(cur, character);
                    ScriptingProcessor.OnUpdateBuffEffect(character->EntityId, cur);
                }
                // Update status
                prev = cur;
            }
        }
    }

    #region private
    private void AddStatusLog(in Status data, Character* gameObjectCharactor) => StatusLog("buff+", data, gameObjectCharactor);
    private void RemoveStatusLog(in Status data, Character* gameObjectCharactor) => StatusLog("buff-", data, gameObjectCharactor);
    private void UpdateStatusLog(in Status data, Character* gameObjectCharactor) => StatusLog("buff*", data, gameObjectCharactor);

    // Updated LogChanges method
    private void StatusLog(string prefix, in Status data, Character* gameObjectCharactor)
    {
        var text = "";
        var PositionString = P.Config.LogPosition == true ? $"({gameObjectCharactor->Position})" : "";
        var ElementTrigger = "";
        if(gameObjectCharactor->ObjectKind == ObjectKind.Pc)
        {
            ElementTrigger = $"[{prefix}]PC:{data.StatusId}:{gameObjectCharactor->ClassJob}";
        }
        else
        {
            ElementTrigger = $"[{prefix}]{gameObjectCharactor->NameId}:{data.StatusId}";
        }

        if(gameObjectCharactor->EntityId == BasePlayer?.EntityId)
        {

            text = $"You gains the effect of {data.StatusId} Param: {data.Param} ([{prefix}]You:{data.StatusId}:{BasePlayer.GetJob()})";
            P.ChatMessageQueue.Enqueue(text);
        }

        text = $"{gameObjectCharactor->NameString} ({PositionString}) gains the effect of {data.StatusId} Param: {data.Param} ({ElementTrigger})";

        P.ChatMessageQueue.Enqueue(text);
    }
    #endregion
}
