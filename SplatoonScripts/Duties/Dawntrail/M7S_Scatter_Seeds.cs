// Ignore Spelling: Metadata

using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.ImGuiMethods;
using ImGuiNET;
using Splatoon;
using Splatoon.Memory;
using Splatoon.SplatoonScripting;
using Splatoon.SplatoonScripting.Priority;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Numerics;
using ConditionFlag = Dalamud.Game.ClientState.Conditions.ConditionFlag;

namespace SplatoonScriptsOfficial.Duties.Dawntrail;
internal unsafe class M7S_Scatter_Seeds :SplatoonScript
{
    private static readonly Vector3[] GuidePositions = new Vector3[]
    {
        new Vector3(89.555f, -200f, -2.335f), // NE
        new Vector3(97.555f, -200f, -2.335f), // SE
        new Vector3(105.555f, -200f, -2.335f), // SW
        new Vector3(105.555f, -200f, 6.335f)   // NW
    };

    private static readonly Vector2 SeedPos_NE = new Vector2(83.5f, 83.5f);
    private static readonly Vector2 SeedPos_NW = new Vector2(116.5f, 83.5f);

    public override HashSet<uint>? ValidTerritories { get; } = new HashSet<uint> { 1261 };
    public override Metadata? Metadata => new Metadata(1, "Redmoon");

    private bool isGimmickActive = false;
    private bool isSeedAvoidNE = false;
    private bool isSeedAvoidNW = false;
    private int scatterSeedCount = 0;
    private int hitCount = 0;
    private bool IsDps => MyPlayer.GetRole() == CombatRole.DPS;
    private bool HasAoe
    {
        get
        {
            if (MyPlayer.TryGetVfx(out var Vfx) && Vfx != null && Vfx.Count > 0)
            {
                foreach (var v in Vfx)
                {
                    if (v.Key.Contains("vfx/lockon/eff/target_ae_s7k1.avfx") && v.Value.AgeF <= 7.0f)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

    private IPlayerCharacter MyPlayer
    {
        get
        {
            var c = Controller.GetConfig<Config>();
            if (Svc.Condition[ConditionFlag.DutyRecorderPlayback] && c.DebugPlayerJob != Job.ADV)
            {
                foreach (var x in FakeParty.Get())
                {
                    if (x.GetJob() == c.DebugPlayerJob)
                    {
                        return x;
                    }
                }
            }
            return Player.Object;
        }
    }

    private Vector3 GetPos()

    public override void OnSetup()
    {
        Controller.RegisterElement("Bait", CreateElement("Bait", 15f, 1f, false, 0xC800FF00u));
    }

    public override void OnStartingCast(uint source, uint castId)
    {
        switch (castId)
        {
            case 42349:
                scatterSeedCount++;
                isGimmickActive = true;
                break;

            case 42347:
                if (source.TryGetObject(out var obj))
                {
                    var pos = new Vector2(obj.Position.X, obj.Position.Z);
                    if (pos == SeedPos_NE) isSeedAvoidNE = true;
                    if (pos == SeedPos_NW) isSeedAvoidNW = true;
                }
                break;

            case 42353:
                hitCount++;
                if (hitCount >= 16) ResetAll();
                break;
        }
    }

    public override void OnUpdate()
    {
        if (scatterSeedCount != 2 || !isGimmickActive) return;
        UpdateGuideVisibility();
        // Handle bait element position and visibility
        if (Controller.TryGetElementByName("Bait", out Element? bait))
        {
            bait.Enabled = true;
            bait.SetRefPosition(GuidePositions[0]);
        }
        // Handle guides based on hit count
        if (this.TryGetElements(out Element?[] guides) && guides.Length > 0)
        {
            for (int i = 0; i < guides.Length; i++)
            {
                if (guides[i] != null)
                {
                    guides[i].Enabled = hitCount >= (i + 1) * 4;
                }
            }
        }
    }

    public class Config :IEzConfig
    {
        public PriorityData CommonPriority = new();
        public Job DebugPlayerJob = Job.ADV;
    }

    // Draw settings/debug info in ImGui
    public override void OnSettingsDraw()
    {
        var c = Controller.GetConfig<Config>();

        c.CommonPriority.Draw();
        if (ImGuiEx.CollapsingHeader("Debug"))
        {
            if (ImGui.BeginCombo("Debug Player Job", c.DebugPlayerJob.ToString()))
            {
                foreach (var x in FakeParty.Get())
                {
                    if (ImGui.Selectable(x.GetJob().ToString(), c.DebugPlayerJob == x.GetJob()))
                    {
                        c.DebugPlayerJob = x.GetJob();
                    }
                }
                ImGui.EndCombo();
            }
            ImGuiEx.Text($"Seed Count: {scatterSeedCount}");
            ImGuiEx.Text($"Hit Count: {hitCount}");
            ImGuiEx.Text($"Job: {playerJob}");
            ImGuiEx.Text($"Seed Pos NE: {isSeedAvoidNE}");
            ImGuiEx.Text($"Seed Pos NW: {isSeedAvoidNW}");
            ImGuiEx.Text($"Gimmick Active: {isGimmickActive}");
        }
    }

    public override void OnReset()
    {
        scatterSeedCount = 0;
        ResetAll();
    }

    // Reset all internal states and element visibility
    private void ResetAll()
    {
        isGimmickActive = false;
        isSeedAvoidNE = false;
        isSeedAvoidNW = false;
        playerJob = 0;
        hitCount = 0;
        Controller.GetRegisteredElements().Each(x => x.Value.Enabled = false);
    }

    // Update element visibility based on the current logic/state
    private void UpdateGuideVisibility()
    {
        Controller.GetRegisteredElements().Each(x => x.Value.Enabled = false);

        if (scatterSeedCount == 0) return;

        if (!this.TryGetElements(out Element? bait, out Element?[] guides) || bait == null) return;

        if (scatterSeedCount == 2)
        {
            // When targetRole is None and the job is one of DRK, PLD, MNK, VPR
            if (this.IsJobOf(playerJob, Job.DRK, Job.PLD, Job.MNK, Job.VPR) && targetRole == Role.None)
            {
                bait.SetRefPosition(GuidePositions[0]);
                bait.Enabled = true;
            }
            // If player is Tank role
            if (targetRole == Role.TD)
            {
                bait.SetRefPosition(this.GetTankPosition(playerJob));
                if (this.IsJobOf(playerJob, Job.DRK, Job.PLD, Job.MNK, Job.VPR))
                    bait.Enabled = true;
            }
            // If player is Healer role
            if (targetRole == Role.HD && this.IsJobOf(playerJob, Job.DRK, Job.PLD, Job.MNK, Job.VPR))
            {
                if (hitCount == 0 && guides[0] != null) guides[0].Enabled = true;
                if (hitCount == 4 && guides[1] != null) guides[1].Enabled = true;
                if (hitCount == 8 && guides[2] != null) guides[2].Enabled = true;
                if (hitCount == 12 && guides[3] != null) guides[3].Enabled = true;
            }
        }
    }

    // Get the correct position for tank role depending on job
    private Vector3 GetTankPosition([Range(1, 4)] int job)
    {
        switch (job)
        {
            case 1: return new Vector3(100f, -200f, 15f);
            case 2: return new Vector3(100f, -200f, -5f);
            case 3: return new Vector3(90f, -200f, 5f);
            case 4: return new Vector3(110f, -200f, 5f);
            default: return GuidePositions[0];
        }
    }
}
