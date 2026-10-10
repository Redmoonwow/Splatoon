using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Collections;
using ECommons.CSExtensions;
using ECommons.ExcelServices;
using ECommons.GameFunctions;
using ECommons.MathHelpers;
using ECommons.ObjectLifeTracker;
using FFXIVClientStructs;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Splatoon.Serializables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TerraFX.Interop.Windows;
using static System.Net.Mime.MediaTypeNames;

namespace Splatoon.RenderEngines;
/// <summary>
/// This class contains render utils that are commonly used across all engines
/// </summary>
public static unsafe class CommonRenderUtils
{
    internal static string ProcessPlaceholders(this string s, IGameObject go, Element element)
    {
        // Called for every overlay text on every frame. Most texts contain no placeholders,
        // so only build a replacement value when its placeholder is actually present.
        if(s.IndexOfAny(PlaceholderStartChars) < 0) return s;
        var ret = s;
        if(ret.Contains("$ELEMENT")) ret = ret.Replace("$ELEMENT", $"{element.Name}");
        if(go != null)
        {
            if(ret.Contains("$OBJECTID")) ret = ret.Replace("$OBJECTID", $"{go.EntityId.Format()}");
            if(ret.Contains("$DATAID")) ret = ret.Replace("$DATAID", $"{go.DataId.Format()}");
            if(ret.Contains("$GIMMICKID")) ret = ret.Replace("$GIMMICKID", $"{go.Struct()->GimmickId.Format()}");
            if(ret.Contains("$ESTATE")) ret = ret.Replace("$ESTATE", $"{go.Struct()->EventState.ToInt().Format()}");
            if(ret.Contains("$EVENTID")) ret = ret.Replace("$EVENTID", $"{go.Struct()->EventId.Id.ToInt().Format()}");
            if(ret.Contains("$HITBOXR")) ret = ret.Replace("$HITBOXR", $"{go.HitboxRadius:F1}");
            if(ret.Contains("$KIND")) ret = ret.Replace("$KIND", $"{go.ObjectKind}");
            if(ret.Contains("$VFLAGS")) ret = ret.Replace("$VFLAGS", $"{go.Struct()->RenderFlags}");
            if(ret.Contains("$NPCID")) ret = ret.Replace("$NPCID", $"{go.Struct()->GetNameId().Format()}");
            if(ret.Contains("$LIFE")) ret = ret.Replace("$LIFE", $"{go.GetLifeTimeSeconds():F1}");
            if(ret.Contains("$DISTANCE")) ret = ret.Replace("$DISTANCE", $"{Vector3.Distance(BasePlayer?.Position ?? Vector3.Zero, go.Position):F1}");
            if(ret.Contains("\\n")) ret = ret.Replace("\\n", "\n");
            if(ret.Contains("$MSTATUS")) ret = ret.Replace("$MSTATUS", $"{(*(int*)(go.Address + 0x104)).Format()}");
            if(!ret.Contains('$')) return ret;
            if(go is IEventObj eobj)
            {
                if(ret.Contains("$ANIMATIONID")) ret = ret.Replace("$ANIMATIONID", $"{eobj.AnimationId.Format()}");
            }
            if(go.IsBattleChara(out var chr))
            {
                if(ret.Contains("$MODELID")) ret = ret.Replace("$MODELID", $"{chr.ModelId.Format()}");
                if(ret.Contains("$NAMEID")) ret = ret.Replace("$NAMEID", $"{chr.NameId.Format()}");
                if(ret.Contains("$STLP")) ret = ret.Replace("$STLP", $"{chr.StatusLoop.Format()}");
                if(ret.Contains("$TETHER")) ret = ret.Replace("$TETHER", $"{chr.Struct()->Vfx.Tethers.ToArray().Where(x => x.Id != 0).Select(x => $"{x.Id}").Print(",")}");
                if(ret.Contains("$TRANSFORM")) ret = ret.Replace("$TRANSFORM", $"{((int)chr.GetTransformationID()).Format()}");
                if(ret.Contains("$STREM:"))
                {
                    try
                    {
                        var match = Regex.Match(ret, @"\$STREM:(\d+):(.*?)\$");
                        if(match.Success && int.TryParse(match.Groups[1].Value, out var statusId) && chr.StatusList.TryGetFirst(s => s.StatusId == statusId, out var status))
                        {
                            ret = ret.Replace(match.Groups[0].Value, $"{status.RemainingTime.ToString(match.Groups[2].Value)}");
                        }
                    }
                    catch(Exception e)
                    {
                        e.Log();
                    }
                }
                if(ret.Contains("$CAST:"))
                {
                    try
                    {
                        var match = Regex.Match(ret, @"\$CAST:(.*?)\$");
                        if(match.Success)
                        {
                            if(chr.IsCasting())
                            {
                                ret = ret.Replace(match.Groups[0].Value, $"{(chr.CastInfo.TotalCastTime - chr.CastInfo.CurrentCastTime).ToString(match.Groups[1].Value)}")
                                    .Replace("$CASTNAME", ExcelActionHelper.GetActionName(chr.CastInfo.ActionId));
                            }
                            else
                            {
                                ret = ret.Replace(match.Groups[0].Value, "").Replace("$CASTNAME", ExcelActionHelper.GetActionName(chr.CastInfo.ActionId));
                            }
                        }
                        else
                        {
                            castFallback();
                        }
                    }
                    catch(Exception e)
                    {
                        e.Log();
                        castFallback();
                    }
                }
                else
                {
                    castFallback();
                }
                void castFallback()
                {
                    if(!ret.Contains("$CAST")) return;
                    ret = ret.Replace("$CAST", chr.Struct()->GetCastInfo() != null ? $"[{chr.CastInfo.ActionId.Format()}] {chr.CastInfo.CurrentCastTime}/{chr.CastInfo.TotalCastTime}" : "");
                }
            }
            if(ret.Contains("$NAME")) ret = ret.Replace("$NAME", go.Name.ToString());
        }
        return ret;
    }

    private static readonly char[] PlaceholderStartChars = ['$', '\\'];

    /// <summary>
    /// Draws an overlay text centered at <paramref name="pos"/>: a rounded background box with the text inside.
    /// Drawn into the current window's draw list. This used to be a child window per text, which is much slower,
    /// and since ImGui never frees windows, texts that change every frame created a new window every frame.
    /// Draw texts after everything else, child windows were always on top of the parent window.
    /// </summary>
    internal static void DrawOverlayText(Vector2 pos, string text, uint bgcolor, uint fgcolor, float fscale)
    {
        var size = ImGui.CalcTextSize(text) * fscale + new Vector2(10f, 10f);
        var min = new Vector2(MathF.Floor(pos.X - (size.X / 2)), MathF.Floor(pos.Y - (size.Y / 2)));
        var drawList = ImGui.GetWindowDrawList();
        // GetColorU32 applies style alpha, like the child window colors did
        drawList.AddRectFilled(min, min + size, ImGui.GetColorU32(ImGui.ColorConvertU32ToFloat4(bgcolor)), 10f);
        drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * fscale, min + new Vector2(5f, 5f), ImGui.GetColorU32(ImGui.ColorConvertU32ToFloat4(fgcolor)), text);
    }

    internal static void HandleEnumeration(Element element, ref List<IGameObject> objectList)
    {
        if(element.Enumeration == EnumerationType.Clockwise || element.Enumeration == EnumerationType.Counter_Clockwise)
        {
            // Locals instead of capturing the element parameter, which would allocate a closure on every call
            var center = element.EnumerationCenter.ToVector2().ToVector3(0);
            var relAngle = MathHelper.GetRelativeAngle(center, element.EnumerationStart.ToVector2().ToVector3(0));
            var orderedList = objectList.OrderBy(x => (MathHelper.GetRelativeAngle(center, x.Position) - relAngle + 360) % 360).ToList();
            if(element.Enumeration == EnumerationType.Counter_Clockwise) orderedList.Reverse();
            List<IGameObject> newObjectList = [];
            foreach(var x in element.EnumerationOrder)
            {
                if(x > 0)
                {
                    if(orderedList.Count > x - 1)
                    {
                        newObjectList.Add(orderedList[x - 1]);
                    }
                }
                if(x < 0)
                {
                    if(orderedList.Count >= -x)
                    {
                        newObjectList.Add(orderedList[^-x]);
                    }
                }
            }
            objectList = newObjectList;
        }
    }

    /// <summary>
    /// Accepts: Z-height vector. Returns: Z-height vector.
    /// </summary>
    /// <param name="tPos"></param>
    /// <param name="e"></param>
    /// <param name="hitboxRadius"></param>
    /// <param name="angle"></param>
    /// <returns></returns>
    internal static (Vector3 pointA, Vector3 pointB) GetRotatedPointsForZeroRadius(Vector3 tPos, Element e, float hitboxRadius, float angle)
    {
        var pointA = Utils.RotatePoint(tPos.X, tPos.Y,
                    -angle + e.AdditionalRotation, new Vector3(
                    tPos.X + -e.refX,
                    tPos.Y + e.refY,
                    tPos.Z + e.refZ) + new Vector3(e.LineAddHitboxLengthXA ? hitboxRadius : 0f, e.LineAddHitboxLengthYA ? hitboxRadius : 0f, e.LineAddHitboxLengthZA ? hitboxRadius : 0f) + new Vector3(e.LineAddPlayerHitboxLengthXA ? BasePlayer.HitboxRadius : 0f, e.LineAddPlayerHitboxLengthYA ? BasePlayer.HitboxRadius : 0f, e.LineAddPlayerHitboxLengthZA ? BasePlayer.HitboxRadius : 0f));
        var pointB = Utils.RotatePoint(tPos.X, tPos.Y,
            -angle + e.AdditionalRotation, new Vector3(
            tPos.X + -e.offX,
            tPos.Y + e.offY,
            tPos.Z + e.offZ) + new Vector3(e.LineAddHitboxLengthX ? hitboxRadius : 0f, e.LineAddHitboxLengthY ? hitboxRadius : 0f, e.LineAddHitboxLengthZ ? hitboxRadius : 0f) + new Vector3(e.LineAddPlayerHitboxLengthX ? BasePlayer.HitboxRadius : 0f, e.LineAddPlayerHitboxLengthY ? BasePlayer.HitboxRadius : 0f, e.LineAddPlayerHitboxLengthZ ? BasePlayer.HitboxRadius : 0f));
        return (pointA, pointB);
    }

    internal static (Vector3 pointA, Vector3 pointB) GetNonRotatedPointsForZeroRadius(Vector3 tPos, Element e, float hitboxRadius, float angle)
    {
        var pointA = new Vector3(
                tPos.X + e.refX,
                tPos.Y + e.refY,
                tPos.Z + e.refZ) + new Vector3(e.LineAddHitboxLengthXA ? hitboxRadius : 0f, e.LineAddHitboxLengthYA ? hitboxRadius : 0f, e.LineAddHitboxLengthZA ? hitboxRadius : 0f) + new Vector3(e.LineAddPlayerHitboxLengthXA ? BasePlayer.HitboxRadius : 0f, e.LineAddPlayerHitboxLengthYA ? BasePlayer.HitboxRadius : 0f, e.LineAddPlayerHitboxLengthZA ? BasePlayer.HitboxRadius : 0f);
        var pointB = new Vector3(
            tPos.X + e.offX,
            tPos.Y + e.offY,
            tPos.Z + e.offZ) + new Vector3(e.LineAddHitboxLengthX ? hitboxRadius : 0f, e.LineAddHitboxLengthY ? hitboxRadius : 0f, e.LineAddHitboxLengthZ ? hitboxRadius : 0f) + new Vector3(e.LineAddPlayerHitboxLengthX ? BasePlayer.HitboxRadius : 0f, e.LineAddPlayerHitboxLengthY ? BasePlayer.HitboxRadius : 0f, e.LineAddPlayerHitboxLengthZ ? BasePlayer.HitboxRadius : 0f);
        return (pointA, pointB);
    }

    internal static bool IsElementObjectMatches(Layout layout, Element element, bool isTargetable, IGameObject gameObject)
    {
        return
            (!element.onlyTargetable || isTargetable)
            && (!element.onlyUnTargetable || !isTargetable)
            && (element.IsDead == null || element.IsDead == gameObject.IsDead)
            && (!element.LimitRotation || (gameObject.Rotation >= element.RotationMax && gameObject.Rotation <= element.RotationMin))
            && (!element.UseHitboxRadius || (gameObject.HitboxRadius >= element.HitboxRadiusMin && gameObject.HitboxRadius <= element.HitboxRadiusMax))
            && (!element.refTargetYou || LayoutUtils.CheckTargetingOption(element, gameObject))
            && (!element.refActorObjectLife || gameObject.GetLifeTimeSeconds().InRange(element.refActorLifetimeMin, element.refActorLifetimeMax))
            && (!element.LimitDistance || IsDistanceMatches(layout, element, gameObject))
            && (element.ObjectKinds.Count == 0 || element.ObjectKinds.Contains(gameObject.ObjectKind))
            && LayoutUtils.CheckCharacterAttributes(element, gameObject);
    }

    internal static bool IsDistanceMatches(Layout layout, Element element, IGameObject go)
    {
        if(element.UseDistanceSourcePlaceholder)
        {
            foreach(var p in element.DistanceSourcePlaceholder)
            {
                var pos = Utils.GetFacePositions(layout, go, p);
                foreach(var x in pos)
                {
                    if(Vector3.Distance(go.Position, x).InRange(element.DistanceMin, element.DistanceMax).Invert(element.LimitDistanceInvert)) return true;
                }
            }
            return false;
        }
        else
        {
            return Vector3.Distance(go.GetPositionXZY(), new(element.DistanceSourceX, element.DistanceSourceY, element.DistanceSourceZ)).InRange(element.DistanceMin, element.DistanceMax).Invert(element.LimitDistanceInvert);
        }
    }

    internal static List<Vector3> GetPointsFromPlaceholderList(Layout layout, IGameObject go, List<string> placeholders)
    {
        var ret = new List<Vector3>();
        foreach(var p in placeholders)
        {
            var pos = Utils.GetFacePositions(layout, go, p);
            foreach(var x in pos)
            {
                ret.Add(x);
            }
        }
        return ret;
    }

    internal readonly record struct RefOffCoords(float refX, float refY, float refZ, float offX, float offY, float offZ);
    internal static OneOrMany<RefOffCoords> ProcessLineAtFixedCoords(Element element, Layout layout)
    {
        if(!element.UsePlaceholderAsRefPosition && !element.UsePlaceholderAsOffPosition)
        {
            return new(new RefOffCoords(element.refX, element.refY, element.refZ, element.offX, element.offY, element.offZ));
        }
        else
        {
            var ret = new List<RefOffCoords>();
            List<Vector3> posA = element.UsePlaceholderAsRefPosition ? CommonRenderUtils.GetPointsFromPlaceholderList(layout, null, element.PlaceholdersRefPosition) : [element.RefPosition];
            List<Vector3> posB = element.UsePlaceholderAsOffPosition ? CommonRenderUtils.GetPointsFromPlaceholderList(layout, null, element.PlaceholdersOffPosition) : [element.OffPosition];
            if(element.PairingMode == PairingMode.One_to_one)
            {
                for(int i = 0; i < Math.Min(posA.Count, posB.Count); i++)
                {
                    var r = posA[i];
                    var o = posB[i];
                    ret.Add(new(r.X, r.Z, r.Y, o.X, o.Z, o.Y));
                }
            }
            else if(element.PairingMode == PairingMode.Every_to_every)
            {
                foreach(var r in posA)
                {
                    foreach(var o in posB)
                    {
                        ret.Add(new(r.X, r.Z, r.Y, o.X, o.Z, o.Y));
                    }
                }
            }
            return new(ret);
        }
    }

    internal readonly record struct RefCoords(float refX, float refY, float refZ);

    internal static OneOrMany<RefCoords> ProcessFixedPositionShape(Element element, Layout layout)
    {
        if(!element.UsePlaceholderAsRefPosition)
        {
            return new(new RefCoords(element.refX, element.refY, element.refZ));
        }
        else
        {
            var ret = new List<RefCoords>();
            List<Vector3> posA = CommonRenderUtils.GetPointsFromPlaceholderList(layout, null, element.PlaceholdersRefPosition);
            foreach(var r in posA)
            {
                ret.Add(new(r.X, r.Z, r.Y));
            }
            return new(ret);
        }
    }
}
