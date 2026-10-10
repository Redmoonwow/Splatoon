using Dalamud.Game.ClientState.Conditions;
using SharpDX.Direct3D11;
using static Splatoon.RenderEngines.ImGuiLegacy.ImGuiLegacyDisplayObjects;

namespace Splatoon.RenderEngines.ImGuiLegacy;

internal class ImGuiLegacyScene : IDisposable
{
    internal readonly ImGuiLegacyRenderer ImGuiLegacyRenderer;

    internal double CamAngleX;
    internal float CamAngleY;
    internal float CamZoom = 1.5f;
    internal int CurrentLineSegments;

    public ImGuiLegacyScene(ImGuiLegacyRenderer imGuiLegacyRenderer)
    {
        ImGuiLegacyRenderer = imGuiLegacyRenderer;
        Svc.PluginInterface.UiBuilder.Draw += Draw;
        Svc.Framework.Update += Update;
    }

    private void Update(object _)
    {
        if(Svc.ClientState.LocalPlayer != null)
        {
            CamAngleX = Camera.GetAngleX() + Math.PI;
            if(CamAngleX > Math.PI) CamAngleX -= 2 * Math.PI;
            CamAngleY = Camera.GetAngleY();
            CamZoom = Math.Min(Camera.GetZoom(), 20);
            /*Range conversion https://stackoverflow.com/questions/5731863/mapping-a-numeric-range-onto-another
            slope = (output_end - output_start) / (input_end - input_start)
            output = output_start + slope * (input - input_start) */
            CurrentLineSegments = (int)((3f + (-0.108108f * (CamZoom - 1.5f))) * P.Config.lineSegments);
        }
    }

    private void Draw()
    {
        if(ImGuiLegacyRenderer.DisplayObjects.Count == 0) return;
        try
        {
            if(!Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent]
                && !Svc.Condition[ConditionFlag.WatchingCutscene78])
            {
                if(P.Config.segments > 1000 || P.Config.segments < 4)
                {
                    P.Config.segments = 100;
                    P.Log("Your smoothness setting was unsafe. It was reset to 100.");
                }
                if(P.Config.lineSegments > 50 || P.Config.lineSegments < 4)
                {
                    P.Config.lineSegments = 20;
                    P.Log("Your line segment setting was unsafe. It was reset to 20.");
                }
                try
                {
                    void DrawShapes()
                    {
                        foreach(var element in ImGuiLegacyRenderer.DisplayObjects)
                        {
                            if(element is DisplayObjectCircle elementCircle)
                            {
                                DrawRingWorld(elementCircle);
                            }
                            else if(element is DisplayObjectDot elementDot)
                            {
                                DrawPoint(elementDot);
                            }
                            else if(element is DisplayObjectLine elementLine)
                            {
                                DrawLineWorld(elementLine);
                            }
                            else if(element is DisplayObjectRect elementRect)
                            {
                                DrawRectWorld(elementRect);
                            }
                            else if(element is DisplayObjectDonut elementDonut)
                            {
                                DrawDonutWorld(elementDonut);
                            }
                            else if(element is DisplayObjectCone elementCone)
                            {
                                DrawConeWorld(elementCone);
                            }
                        }
                    }

                    void DrawTexts()
                    {
                        foreach(var element in ImGuiLegacyRenderer.DisplayObjects)
                        {
                            if(element is DisplayObjectText elementText)
                            {
                                DrawTextWorld(elementText);
                            }
                        }
                    }

                    ImGuiHelpers.ForceNextWindowMainViewport();
                    ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                    ImGuiHelpers.SetNextWindowPosRelativeMainViewport(Vector2.Zero);
                    ImGui.SetNextWindowSize(ImGuiHelpers.MainViewport.Size);
                    ImGui.Begin("Splatoon Legacy Renderer Scene", ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing);
                    if(P.Config.SplatoonLowerZ)
                    {
                        CImGui.igBringWindowToDisplayBack(CImGui.igGetCurrentWindow());
                    }
                    if(P.Config.RenderableZones.Count == 0 || !P.Config.RenderableZonesValid)
                    {
                        DrawShapes();
                        DrawTexts();
                    }
                    else
                    {
                        // Texts go on top of the shapes of every zone
                        foreach(var e in P.Config.RenderableZones)
                        {
                            //var trans = e.Trans != 1.0f;
                            //if (trans) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, e.Trans);
                            ImGui.PushClipRect(new Vector2(e.Rect.X, e.Rect.Y), new Vector2(e.Rect.Right, e.Rect.Bottom), false);
                            DrawShapes();
                            ImGui.PopClipRect();
                            //if(trans)ImGui.PopStyleVar();
                        }
                        foreach(var e in P.Config.RenderableZones)
                        {
                            ImGui.PushClipRect(new Vector2(e.Rect.X, e.Rect.Y), new Vector2(e.Rect.Right, e.Rect.Bottom), false);
                            DrawTexts();
                            ImGui.PopClipRect();
                        }
                    }
                    ImGui.End();
                    ImGui.PopStyleVar();
                }
                catch(Exception e)
                {
                    P.Log("Splatoon exception: please report it to developer", true);
                    P.Log(e.ToStringFull(), true);
                }
            }
        }
        catch(Exception e)
        {
            P.Log("Caught exception: " + e.ToStringFull(), true);
        }
    }

    internal Vector3 TranslateToScreen(double x, double y, double z)
    {
        Utils.WorldToScreen(
            new Vector3((float)x, (float)y, (float)z),
            out var temp
        );
        return new Vector3(temp.X, temp.Y, (float)z);
    }

    private static readonly double[] DonutSin = Enumerable.Range(0, 49).Select(k => Math.Sin(Math.PI / 24.0 * k)).ToArray();
    private static readonly double[] DonutCos = Enumerable.Range(0, 49).Select(k => Math.Cos(Math.PI / 24.0 * k)).ToArray();

    private void DrawDonutWorld(DisplayObjectDonut elementDonut)
    {
        Vector3 v1, v2, v3, v4;
        var outerradiuschonk = elementDonut.radius + elementDonut.donut;
        v1 = TranslateToScreen(
            elementDonut.x + (elementDonut.radius * Math.Sin(Math.PI / 23.0 * 0)),
            elementDonut.z,
            elementDonut.y + (elementDonut.radius * Math.Cos(Math.PI / 24.0 * 0))
        );
        v4 = TranslateToScreen(
            elementDonut.x + (outerradiuschonk * Math.Sin(Math.PI / 24.0 * 0)),
            elementDonut.z,
            elementDonut.y + (outerradiuschonk * Math.Cos(Math.PI / 24.0 * 0))
        );
        var drawList = ImGui.GetWindowDrawList();
        var fillColor = Utils.TransformAlpha(elementDonut.color, elementDonut.intensity);
        for(var i = 0; i <= 47; i++)
        {
            v2 = TranslateToScreen(
                elementDonut.x + (elementDonut.radius * DonutSin[i + 1]),
                elementDonut.z,
                elementDonut.y + (elementDonut.radius * DonutCos[i + 1])
            );
            v3 = TranslateToScreen(
                elementDonut.x + (outerradiuschonk * DonutSin[i + 1]),
                elementDonut.z,
                elementDonut.y + (outerradiuschonk * DonutCos[i + 1])
            );
            drawList.PathLineTo(new Vector2(v1.X, v1.Y));
            drawList.PathLineTo(new Vector2(v2.X, v2.Y));
            drawList.PathLineTo(new Vector2(v3.X, v3.Y));
            drawList.PathLineTo(new Vector2(v4.X, v4.Y));
            drawList.PathLineTo(new Vector2(v1.X, v1.Y));
            drawList.PathFillConvex(fillColor);
            v1 = v2;
            v4 = v3;
        }
    }

    private void DrawLineWorld(DisplayObjectLine e)
    {
        var result = GetAdjustedLine(new Vector3(e.ax, e.ay, e.az), new Vector3(e.bx, e.by, e.bz));
        if(result.posA == null) return;
        ImGui.GetWindowDrawList().PathLineTo(new Vector2(result.posA.Value.X, result.posA.Value.Y));
        ImGui.GetWindowDrawList().PathLineTo(new Vector2(result.posB.Value.X, result.posB.Value.Y));
        ImGui.GetWindowDrawList().PathStroke(e.color, ImDrawFlags.None, e.thickness);
    }

    private (Vector2? posA, Vector2? posB) GetAdjustedLine(Vector3 pointA, Vector3 pointB)
    {
        var resultA = Utils.WorldToScreen(new Vector3(pointA.X, pointA.Z, pointA.Y), out var posA);
        if(!resultA && !P.DisableLineFix)
        {
            var posA2 = GetLineClosestToVisiblePoint(pointA,
            (pointB - pointA) / CurrentLineSegments, 0, CurrentLineSegments);
            if(posA2 == null)
            {
                return (null, null);
            }
            else
            {
                posA = posA2.Value;
            }
        }
        var resultB = Utils.WorldToScreen(new Vector3(pointB.X, pointB.Z, pointB.Y), out var posB);
        if(!resultB && !P.DisableLineFix)
        {
            var posB2 = GetLineClosestToVisiblePoint(pointB,
            (pointA - pointB) / CurrentLineSegments, 0, CurrentLineSegments);
            if(posB2 == null)
            {
                return (null, null);
            }
            else
            {
                posB = posB2.Value;
            }
        }

        return (posA, posB);
    }

    private void DrawRectWorld(DisplayObjectRect e) //oof
    {
        var result1 = GetAdjustedLine(new Vector3(e.l1.ax, e.l1.ay, e.l1.az), new Vector3(e.l1.bx, e.l1.by, e.l1.bz));
        if(result1.posA == null) goto Alternative;
        var result2 = GetAdjustedLine(new Vector3(e.l2.ax, e.l2.ay, e.l2.az), new Vector3(e.l2.bx, e.l2.by, e.l2.bz));
        if(result2.posA == null) goto Alternative;
        goto Build;
    Alternative:
        result1 = GetAdjustedLine(new Vector3(e.l1.ax, e.l1.ay, e.l1.az), new Vector3(e.l2.ax, e.l2.ay, e.l2.az));
        if(result1.posA == null) goto Quit;
        result2 = GetAdjustedLine(new Vector3(e.l1.bx, e.l1.by, e.l1.bz), new Vector3(e.l2.bx, e.l2.by, e.l2.bz));
        if(result2.posA == null) goto Quit;
    Build:
        ImGui.GetWindowDrawList().AddQuadFilled(
            new Vector2(result1.posA.Value.X, result1.posA.Value.Y),
            new Vector2(result1.posB.Value.X, result1.posB.Value.Y),
            new Vector2(result2.posB.Value.X, result2.posB.Value.Y),
            new Vector2(result2.posA.Value.X, result2.posA.Value.Y), e.l1.color
            );
    Quit:
        return;
    }

    private Vector2? GetLineClosestToVisiblePoint(Vector3 currentPos, Vector3 targetPos, float eps)
    {
        if(!Utils.WorldToScreen(targetPos, out var res)) return null;

        while(true)
        {
            var mid = (currentPos + targetPos) / 2;
            if(Utils.WorldToScreen(mid, out var pos))
            {
                if((res - pos).Length() < eps) return res;
                targetPos = mid;
                res = pos;
            }
            else currentPos = mid;
        }
    }

    private Vector2? GetLineClosestToVisiblePoint(Vector3 currentPos, Vector3 delta, int curSegment, int numSegments)
    {
        if(curSegment > numSegments) return null;
        var nextPos = currentPos + delta;
        if(Utils.WorldToScreen(new Vector3(nextPos.X, nextPos.Z, nextPos.Y), out var pos))
        {
            var preciseVector = GetLineClosestToVisiblePoint(currentPos, (nextPos - currentPos) / P.Config.lineSegments, 0, P.Config.lineSegments);
            return preciseVector.HasValue ? preciseVector.Value : pos;
        }
        else
        {
            return GetLineClosestToVisiblePoint(nextPos, delta, ++curSegment, numSegments);
        }
    }

    public void DrawTextWorld(DisplayObjectText e)
    {
        if(Utils.WorldToScreen(
                        new Vector3(e.x, e.z, e.y),
                        out var pos))
        {
            DrawText(e, pos);
        }
    }

    public void DrawText(DisplayObjectText e, Vector2 pos)
    {
        CommonRenderUtils.DrawOverlayText(pos, e.text, e.bgcolor, e.fgcolor, e.fscale);
    }

    // Sin/cos of every circle segment and the point buffer, rebuilt only when the segment count changes.
    private int RingSegments = -1;
    private float[] RingSin = [];
    private float[] RingCos = [];
    private Vector2?[] RingPoints = [];

    private void UpdateRingTables()
    {
        var segments = P.Config.segments;
        if(segments == RingSegments) return;
        var seg = segments / 2;
        RingSin = new float[segments];
        RingCos = new float[segments];
        RingPoints = new Vector2?[segments];
        for(var i = 0; i < segments; i++)
        {
            RingSin[i] = (float)Math.Sin(Math.PI / seg * i);
            RingCos[i] = (float)Math.Cos(Math.PI / seg * i);
        }
        RingSegments = segments;
    }

    public void DrawRingWorld(DisplayObjectCircle e)
    {
        UpdateRingTables();
        Utils.WorldToScreen(new Vector3(
            e.x + (e.radius * (float)Math.Sin(CamAngleX)),
            e.z,
            e.y + (e.radius * (float)Math.Cos(CamAngleX))
            ), out var refpos);
        var visible = false;
        var elements = RingPoints;
        for(var i = 0; i < elements.Length; i++)
        {
            visible = Utils.WorldToScreen(
                new Vector3(e.x + (e.radius * RingSin[i]),
                e.z,
                e.y + (e.radius * RingCos[i])
                ),
                out var pos)
                || visible;
            elements[i] = pos.Y > refpos.Y || P.Config.NoCircleFix ? new Vector2(pos.X, pos.Y) : null;
        }
        if(visible)
        {
            var drawList = ImGui.GetWindowDrawList();
            foreach(var pos in elements)
            {
                if(pos == null) continue;
                drawList.PathLineTo(pos.Value);
            }

            if(e.filled)
            {
                drawList.PathFillConvex(e.color);
            }
            else
            {
                drawList.PathStroke(e.color, ImDrawFlags.Closed, e.thickness);
            }
        }
    }

    public void DrawConeWorld(DisplayObjectCone e)
    {
        var drawList = ImGui.GetWindowDrawList();
        (e.y, e.z) = (e.z, e.y);

        Utils.WorldToScreen(new Vector3(e.x, e.y, e.z), out var v);
        drawList.PathLineTo(v);

        Utils.WorldToScreen(new Vector3(e.x + (e.radius * MathF.Cos(e.startRad)), e.y, e.z + (e.radius * MathF.Sin(e.startRad))), out v);
        drawList.PathLineTo(v);

        for(var i = e.startRad; i < e.endRad; i += MathF.PI / 2)
        {
            var theta = MathF.Min(e.endRad - i, MathF.PI / 2);
            var h = 1.3f * (1 - MathF.Cos(theta / 2)) / MathF.Sin(theta / 2);

            var arcMid1 = new Vector3(
                e.x + (e.radius * (MathF.Cos(i) - (h * MathF.Sin(i)))),
                e.y,
                e.z + (e.radius * (MathF.Sin(i) + (h * MathF.Cos(i)))));
            var arcMid2 = new Vector3(
                e.x + (e.radius * (MathF.Cos(i + theta) + (h * MathF.Sin(i + theta)))),
                e.y,
                e.z + (e.radius * (MathF.Sin(i + theta) - (h * MathF.Cos(i + theta)))));
            var endPoint = new Vector3(
                e.x + (e.radius * MathF.Cos(i + theta)), e.y, e.z + (e.radius * MathF.Sin(i + theta)));

            Utils.WorldToScreen(arcMid1, out var v1);
            Utils.WorldToScreen(arcMid2, out var v2);
            Utils.WorldToScreen(endPoint, out v);
            drawList.PathBezierCubicCurveTo(v1, v2, v);
        }

        if(e.filled)
        {
            drawList.PathFillConvex(e.color);
        }
        else
        {
            drawList.PathStroke(e.color, ImDrawFlags.Closed);
        }
    }

    public void DrawPoint(DisplayObjectDot e)
    {
        if(Utils.WorldToScreen(new Vector3(e.x, e.z, e.y), out var pos))
            ImGui.GetWindowDrawList().AddCircleFilled(
            new Vector2(pos.X, pos.Y),
            e.thickness,
            ImGui.GetColorU32(e.color),
            100);
    }

    public void Dispose()
    {
        Svc.PluginInterface.UiBuilder.Draw -= Draw;
        Svc.Framework.Update -= Update;
    }
}
