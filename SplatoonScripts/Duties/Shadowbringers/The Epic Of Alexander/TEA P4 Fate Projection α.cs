using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using ECommons.Throttlers;
using ImGuiNET;
using Splatoon;
using Splatoon.SplatoonScripting;
using System.Collections.Generic;
using System.Linq;
using Vector3 = System.Numerics.Vector3;

namespace SplatoonScriptsOfficial.Duties.Shadowbringers.The_Epic_Of_Alexander;

public class TEA_P4_Fate_Projection_α : SplatoonScript
{
    private readonly List<uint> _futurePlayers = [];

    private FutureActionType[] _futureActionTypes =
    [
        FutureActionType.None,
        FutureActionType.None,
        FutureActionType.None
    ];

    private bool _isOpenSafeSpot;

    private bool _isStartFateProjectionCasting;
    private uint? _myFuturePlayer;

    private IBattleChara? SafeAlexander => Svc.Objects.OfType<IBattleNpc>()
        .FirstOrDefault(x => x is { NameId: 0x2352, IsCasting: true, CastActionId: 18858 });

    public override HashSet<uint>? ValidTerritories => [887];
    public override Metadata? Metadata => new(3, "Garume");


    private string GetFutureActionText(FutureActionType type)
    {
        return type switch
        {
            FutureActionType.FirstMotion => Loc(en: "Move in the first half!", jp: "最初は動け！"),
            FutureActionType.FirstStillness => Loc(en: "Don't move in the first half.", jp: "最初は動くな"),
            FutureActionType.SecondMotion => Loc(en: "Move in the second half!", jp: "最後は動け！"),
            FutureActionType.SecondStillness => Loc(en: "Don't move in the second half.", jp: "最後は動くな！"),
            FutureActionType.Defamation => Loc(en: "Defamation: Go up", jp: "名誉罰: 上へ"),
            FutureActionType.SharedSentence => Loc(en: "Shared Sentence: Go to the bottom left", jp: "集団罰: 左下へ"),
            FutureActionType.Aggravated => Loc(en: "Aggravated Sentence: Go to the bottom right", jp: "加重罰: 右下へ"),
            FutureActionType.Nothing => Loc(en: "Nothing: Go to the bottom left", jp: "無職: 左下へ"),
            FutureActionType.UnKnown => Loc(en: "Unknown: Go to the bottom left?", jp: "UnKnown: 左下へ？"),
            _ => Loc(en: "None: Go to the bottom left?", jp: "None: 左下へ？")
        };
    }


    public override void OnStartingCast(uint source, uint castId)
    {
        if(castId == 18555)
        {
            _isStartFateProjectionCasting = true;
            Controller.Schedule(() =>
            {
                if(Controller.TryGetElementByName("FirstText", out var firstTextElement))
                    firstTextElement.overlayTextColor = EColor.Red.ToUint();
            }, 34 * 1000);
            Controller.Schedule(() =>
            {
                if(Controller.TryGetElementByName("FirstText", out var firstTextElement))
                    firstTextElement.overlayTextColor = EColor.White.ToUint();

                if(Controller.TryGetElementByName("ThirdText", out var thirdTextElement))
                    thirdTextElement.overlayTextColor = EColor.Red.ToUint();
            }, 39 * 1000);
            Controller.Schedule(() =>
            {
                if(Controller.TryGetElementByName("ThirdText", out var thirdTextElement))
                    thirdTextElement.overlayTextColor = EColor.White.ToUint();
                _isStartFateProjectionCasting = false;
            }, 44 * 1000);
        }
    }

    public override void OnReset()
    {
        _isStartFateProjectionCasting = false;
        _futureActionTypes =
        [
            FutureActionType.None,
            FutureActionType.None,
            FutureActionType.None
        ];
        _futurePlayers.Clear();
        _myFuturePlayer = null;
        _isOpenSafeSpot = false;
        EzThrottler.Reset("FateProjectionAlphaActionEffectDelay");
    }

    public override void OnSetup()
    {
        var firstTextElement = new Element(0)
        {
            overlayText = "",
            overlayVOffset = 8f,
            overlayFScale = 5f,
            Filled = false,
            radius = 0f
        };
        firstTextElement.SetOffPosition(new Vector3(100f, 0, 100f));
        Controller.RegisterElement("FirstText", firstTextElement, true);

        var secondTextElement = new Element(0)
        {
            overlayText = "",
            overlayVOffset = 5f,
            overlayFScale = 5f,
            Filled = false,
            radius = 0f
        };
        secondTextElement.SetOffPosition(new Vector3(100f, 0, 100f));
        Controller.RegisterElement("SecondText", secondTextElement, true);

        var thirdTextElement = new Element(0)
        {
            overlayText = "",
            overlayVOffset = 2f,
            overlayFScale = 5f,
            Filled = false,
            radius = 0f
        };
        thirdTextElement.SetOffPosition(new Vector3(100f, 0, 100f));
        Controller.RegisterElement("ThirdText", thirdTextElement, true);

        Controller.RegisterElementFromCode("NorthBait",
            "{\"Name\":\"北側サークル\",\"type\":1,\"offY\":7.0,\"radius\":3.0,\"color\":3372169472,\"fillIntensity\":0.0,\"thicc\":5.0,\"refActorNPCNameID\":9042,\"refActorRequireCast\":true,\"refActorCastId\":[18858],\"refActorUseCastTime\":true,\"refActorCastTimeMax\":30.0,\"refActorUseOvercast\":true,\"refActorComparisonType\":6,\"includeRotation\":true,\"onlyUnTargetable\":true,\"onlyVisible\":true,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0}");
        Controller.RegisterElementFromCode("SouthEastBait",
            "{\"Name\":\"南側サークル1\",\"type\":1,\"offX\":-2.5,\"offY\":41.5,\"radius\":1.0,\"color\":3372169472,\"fillIntensity\":0.0,\"thicc\":5.0,\"refActorNPCNameID\":9042,\"refActorRequireCast\":true,\"refActorCastId\":[18858],\"refActorUseCastTime\":true,\"refActorCastTimeMax\":30.0,\"refActorUseOvercast\":true,\"refActorComparisonType\":6,\"includeRotation\":true,\"onlyUnTargetable\":true,\"onlyVisible\":true,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0}");
        Controller.RegisterElementFromCode("SouthWestBait",
            "{\"Name\":\"南側サークル2\",\"type\":1,\"offX\":2.5,\"offY\":41.5,\"radius\":1.0,\"color\":3372169472,\"fillIntensity\":0.0,\"thicc\":5.0,\"refActorNPCNameID\":9042,\"refActorRequireCast\":true,\"refActorCastId\":[18858],\"refActorUseCastTime\":true,\"refActorCastTimeMax\":30.0,\"refActorUseOvercast\":true,\"refActorComparisonType\":6,\"includeRotation\":true,\"onlyUnTargetable\":true,\"onlyVisible\":true,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0}");
    }

    public override void OnUpdate()
    {
        if(!_isStartFateProjectionCasting)
        {
            Controller.GetRegisteredElements().Each(x => x.Value.Enabled = false);
            return;
        }

        ApplyTextFromFutureAction("FirstText", _futureActionTypes[0]);
        ApplyTextFromFutureAction("SecondText", _futureActionTypes[1]);
        ApplyTextFromFutureAction("ThirdText", _futureActionTypes[2]);

        var safeAlexander = SafeAlexander;
        if(safeAlexander == null || _isOpenSafeSpot) return;
        _isOpenSafeSpot = true;
        switch(_futureActionTypes[1])
        {
            case FutureActionType.Defamation:
                ApplyBaitStyle("NorthBait");
                break;
            case FutureActionType.Aggravated:
                ApplyBaitStyle("SouthEastBait");
                break;
            case FutureActionType.SharedSentence:
            case FutureActionType.Nothing:
            case FutureActionType.None:
            case FutureActionType.FirstMotion:
            case FutureActionType.FirstStillness:
            case FutureActionType.SecondMotion:
            case FutureActionType.SecondStillness:
            case FutureActionType.UnKnown:
            default:
                ApplyBaitStyle("SouthWestBait");
                break;
        }
    }

    public override void OnSettingsDraw()
    {
        if(ImGuiEx.CollapsingHeader("Debug"))
        {
            ImGui.Text($"_futureActionTypes[0]: {_futureActionTypes[0]}");
            ImGui.Text($"_futureActionTypes[1]: {_futureActionTypes[1]}");
            ImGui.Text($"_futureActionTypes[2]: {_futureActionTypes[2]}");
        }
    }

    private void ApplyTextFromFutureAction(string elementName, FutureActionType type)
    {
        if(type == FutureActionType.None) return;
        if(!Controller.TryGetElementByName(elementName, out var element)) return;
        var text = GetFutureActionText(type);
        element.overlayText = text;
        element.Enabled = true;
    }

    private void ApplyBaitStyle(string elementName)
    {
        if(!Controller.TryGetElementByName(elementName, out var element)) return;
        element.Enabled = true;
        element.tether = true;
        element.color = GradientColor.Get(0xFFFF00FF.ToVector4(), 0xFFFFFF00.ToVector4()).ToUint();
        element.thicc = 10f;
    }

    public override void OnTetherCreate(uint source, uint target, uint data2, uint data3, uint data5)
    {
        if(!_isStartFateProjectionCasting) return;
        _futurePlayers.Add(target);
        if(source == Player.Object.EntityId)
        {
            _myFuturePlayer = target;
            Controller.Schedule(() =>
            {
                var reversed = _futurePlayers.ToArray().Reverse().ToArray();
                for(var i = 0; i < reversed.Length; i++)
                    if(_myFuturePlayer == reversed[i])
                        switch(i)
                        {
                            case 0:
                                _futureActionTypes[1] = FutureActionType.SharedSentence;
                                break;
                            case 1:
                                _futureActionTypes[1] = FutureActionType.Defamation;
                                break;
                            case 2:
                            case 3:
                            case 4:
                                _futureActionTypes[1] = FutureActionType.Aggravated;
                                break;
                            case 5:
                            case 6:
                            case 7:
                                _futureActionTypes[1] = FutureActionType.Nothing;
                                break;
                        }
            }, 1000);
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if(!_isStartFateProjectionCasting) return;
        if(set is { Action: not null, Source: not null, Target: not null })
        {
            PluginLog.Warning("ActionId: " + set.Action.Value.RowId);

            var futureAction = set.Action.Value.RowId switch
            {
                19213 => FutureActionType.FirstMotion,
                19214 => FutureActionType.FirstStillness,
                18585 => FutureActionType.SecondMotion,
                18586 => FutureActionType.SecondStillness,
                18597 => FutureActionType.SecondStillness,
                _ => FutureActionType.None
            };
            if(futureAction == FutureActionType.None) return;
            if(!EzThrottler.Throttle("FateProjectionAlphaActionEffectDelay", 1500)) return;
            if(_futureActionTypes[0] == FutureActionType.None) _futureActionTypes[0] = futureAction;
            else _futureActionTypes[2] = futureAction;
        }
    }

    private enum FutureActionType : byte
    {
        None,
        FirstMotion,
        FirstStillness,
        SecondMotion,
        SecondStillness,
        Defamation,
        SharedSentence,
        Aggravated,
        Nothing,
        UnKnown
    }
}
