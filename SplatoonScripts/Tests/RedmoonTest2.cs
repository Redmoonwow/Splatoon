using ECommons.GameFunctions;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Graphics.Vfx;
using ImGuiNET;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;


namespace SplatoonScriptsOfficial.Tests;
internal unsafe class RedmoonTest2 :SplatoonScript
{
    public override HashSet<uint>? ValidTerritories => null;

    public override void OnSettingsDraw()
    {
        var pc = Player.Object;
        var pcObj = pc.Struct();

        ImGui.Text($"VfxData Address: 0x{(ulong)(IntPtr)pcObj->Vfx.VfxData:X}");
        ImGui.Text($"VfxData2 Address: 0x{(ulong)(IntPtr)pcObj->Vfx.VfxData2:X}");
        ImGui.Text($"Omen Address: 0x{(ulong)(IntPtr)pcObj->Vfx.Omen:X}");

        if (pcObj->Vfx.VfxData == null)
        {
            ImGui.Text("VfxData is null");
        }
    }

    static byte[] StructToBytes(VfxData data)
    {
        int size = Marshal.SizeOf<VfxData>();
        byte[] bytes = new byte[size];
        IntPtr ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(data, ptr, false);
            Marshal.Copy(ptr, bytes, 0, size);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
        return bytes;
    }
}
