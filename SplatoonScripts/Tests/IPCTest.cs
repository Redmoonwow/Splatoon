using ECommons.EzIpcManager;
using ECommons.Logging;
using ImGuiNET;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;

namespace SplatoonScriptsOfficial.Tests;
internal class IPCTest :SplatoonScript
{
    public override HashSet<uint>? ValidTerritories => null;

    public override void OnSettingsDraw()
    {
        ImGui.Text($"IsBusy: {AutoRetainer.IsBusy()}");
        if (DailyRoutines.IsModuleEnabled == null)
        {
            ImGui.Text("IsModuleEnabled func not found");
            return;
        }
        if (DailyRoutines.Version == null)
        {
            ImGui.Text("Version func not found");
            return;
        }
        if (DailyRoutines.LoadModule == null)
        {
            ImGui.Text("LoadModule func not found");
            return;
        }
        if (DailyRoutines.UnloadModule == null)
        {
            ImGui.Text("UnloadModule func not found");
            return;
        }

        ImGui.Text($"Version: {DailyRoutines.Version()}");

        var ret = DailyRoutines.IsModuleEnabled("DisableGroundActionAutoFace");
        if (ret == null)
        {
            ImGui.Text("Module not found");
        }
        else if (ret == false)
        {
            ImGui.Text("Module not enabled");
        }
        else
        {
            ImGui.Text("Module enabled");
        }

        if (ImGui.Button("Enable"))
        {
            ret = DailyRoutines.LoadModule("DisableGroundActionAutoFace", false);
        }

        if (ImGui.Button("Disable"))
        {
            DailyRoutines.UnloadModule("DisableGroundActionAutoFace", false, false);
        }
    }

    public override void OnReset()
    {
        AutoRetainer.Dispose();
        DailyRoutines.Dispose();
    }
}

internal static class AutoRetainer
{
    private static EzIPCDisposalToken[] _disposalTokens = EzIPC.Init(typeof(AutoRetainer), "AutoRetainer.PluginState", SafeWrapper.IPCException);

    [EzIPC] internal static readonly Func<bool> IsBusy;
    [EzIPC] internal static readonly Func<Dictionary<ulong, HashSet<string>>> GetEnabledRetainers;
    [EzIPC] internal static readonly Func<bool> AreAnyRetainersAvailableForCurrentChara;
    [EzIPC] internal static readonly Action AbortAllTasks;
    [EzIPC] internal static readonly Action DisableAllFunctions;
    [EzIPC] internal static readonly Action EnableMultiMode;
    [EzIPC] internal static readonly Func<int> GetInventoryFreeSlotCount;
    [EzIPC] internal static readonly Action EnqueueHET;

    internal static void Dispose()
    {
        if (_disposalTokens == null) return;
        try
        {
            foreach (var token in _disposalTokens)
            {
                token.Dispose();
            }
        }
        catch (Exception e)
        {
            PluginLog.Error($"[EzIPC Disposer] Error while disposing EzIPC");
            PluginLog.Error(e.ToString());
        }
    }
}

internal static class DailyRoutines
{
    private static EzIPCDisposalToken[] _disposalTokens = EzIPC.Init(typeof(DailyRoutines), "DailyRoutines", SafeWrapper.IPCException);

    [EzIPC] public static readonly Func<string, bool?> IsModuleEnabled;
    [EzIPC] public static readonly Func<Version> Version;
    [EzIPC] public static Func<string, bool, bool> LoadModule;
    [EzIPC] public static Func<string, bool, bool, bool> UnloadModule;

    internal static void Dispose()
    {
        if (_disposalTokens == null) return;
        try
        {
            foreach (var token in _disposalTokens)
            {
                token.Dispose();
            }
        }
        catch (Exception e)
        {
            PluginLog.Error($"[EzIPC Disposer] Error while disposing EzIPC");
            PluginLog.Error(e.ToString());
        }
    }
}

