// Ignore Spelling: Metadata

using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Logging;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;

namespace SplatoonScriptsOfficial.Generic;
internal unsafe class AR_Stack_Remover :SplatoonScript
{
    public override HashSet<uint>? ValidTerritories => null;
    public override Metadata? Metadata => new(1, "redmoon");

    long _timeoutTick = 0;

    public override void OnUpdate()
    {
        // Freq 1s
        if (EzThrottler.Throttle("ARStackRemover", 1000)) return;

        if (_timeoutTick == 0)
        {
            var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("RetainerTaskSupply");
            if (addon->IsVisible)
            {
                _timeoutTick = Environment.TickCount64 + 300000; // 5 minutes
            }
        }
        else
        {
            var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("RetainerTaskSupply");
            if (!addon->IsVisible)
            {
                _timeoutTick = 0;
                return;
            }

            if (Environment.TickCount64 > _timeoutTick && addon->IsVisible)
            {
                Callback.Fire(addon, true, -1);
                _timeoutTick = 0;
                DuoLog.Error($"AR Stack Remover: Retainer Task Supply is still visible after 5 minute, removing it");
            }
        }
    }

    public override void OnSettingsDraw()
    {
        if (_timeoutTick == 0)
            ImGui.Text("AR Stack Remover: Retainer Task Supply is not visible");
        else
            ImGui.Text($"AR Stack Remover: Retainer Task Supply is visible, will remove it in {(_timeoutTick - Environment.TickCount64) / 1000} seconds");
    }
}
