using Newtonsoft.Json;
using Splatoon.Structures;
using Snapshot = (int version, uint frame, uint territoryId, long generatedAtTickMs, System.Collections.Generic.List<(string id, string source, string Namespace, string layout, string element, string kind, string renderEngine, uint color, System.Numerics.Vector3 center, System.Numerics.Vector3 start, System.Numerics.Vector3 end, float? radius, float? innerRadius, float? outerRadius, float? lineRadius, float? facingRad, float? halfAngleRad, float? angleMinRad, float? angleMaxRad)> items);

namespace Splatoon.Modules;

internal static class SplatoonIPC
{
    internal static void Init()
    {
        Svc.PluginInterface.GetIpcProvider<bool>("Splatoon.Loaded").SendMessage();
        Svc.PluginInterface.GetIpcProvider<bool>("Splatoon.IsLoaded").RegisterFunc(() => { return true; });
        Svc.PluginInterface.GetIpcProvider<Snapshot>("Splatoon.GetActiveDrawGeometryV1").RegisterFunc(GetActiveDrawGeometry);
        Svc.PluginInterface.GetIpcProvider<string, string, long[], string>("Splatoon.AddDynamicElementsJson").RegisterFunc(AddDynamicElementsJson);
        Svc.PluginInterface.GetIpcProvider<string, object>("Splatoon.RemoveDynamicElements").RegisterAction(RemoveDynamicElements);
        Svc.PluginInterface.GetIpcProvider<List<(string name, string group, bool enabled)>>("Splatoon.GetLayouts").RegisterFunc(GetLayouts);
        Svc.PluginInterface.GetIpcProvider<string, bool, object>("Splatoon.SetLayoutState").RegisterAction(SetLayoutState);
    }

    internal static void Dispose()
    {
        Svc.PluginInterface.GetIpcProvider<string, bool, object>("Splatoon.SetLayoutState").UnregisterAction();
        Svc.PluginInterface.GetIpcProvider<List<(string name, string group, bool enabled)>>("Splatoon.GetLayouts").UnregisterFunc();
        Svc.PluginInterface.GetIpcProvider<string, object>("Splatoon.RemoveDynamicElements").UnregisterAction();
        Svc.PluginInterface.GetIpcProvider<string, string, long[], string>("Splatoon.AddDynamicElementsJson").UnregisterFunc();
        Svc.PluginInterface.GetIpcProvider<Snapshot>("Splatoon.GetActiveDrawGeometryV1").UnregisterFunc();
        Svc.PluginInterface.GetIpcProvider<bool>("Splatoon.IsLoaded").UnregisterFunc();
        Svc.PluginInterface.GetIpcProvider<bool>("Splatoon.Unloaded").SendMessage();
    }

    private static Snapshot GetActiveDrawGeometry()
    {
        return ActiveDrawGeometrySnapshot.Build(S.RenderManager.GetUnifiedDisplayObjects());
    }

    /// <summary>
    /// Adds dynamic elements from JSON, the same formats as the web API accepts:
    /// a single element object, an array of element objects, or a layout prefixed with "~".
    /// <paramref name="destroyConditions"/>: positive values are milliseconds from now, otherwise <see cref="DestroyCondition"/> values.
    /// Null or empty means the elements stay until removed.
    /// Returns null on success or an error message.
    /// </summary>
    private static string AddDynamicElementsJson(string name, string json, long[] destroyConditions)
    {
        try
        {
            var layouts = new List<Layout>();
            var elements = new List<Element>();
            var trimmed = json?.Trim() ?? "";
            if(trimmed.StartsWith('~'))
            {
                var layout = JsonConvert.DeserializeObject<Layout>(trimmed[1..]);
                layout.Enabled = true;
                foreach(var e in layout.ElementsL)
                {
                    e.Enabled = true;
                }
                layouts.Add(layout);
            }
            else if(trimmed.StartsWith('['))
            {
                elements.AddRange(JsonConvert.DeserializeObject<Element[]>(trimmed));
            }
            else
            {
                elements.Add(JsonConvert.DeserializeObject<Element>(trimmed));
            }
            foreach(var e in elements)
            {
                e.Enabled = true;
            }
            var now = Environment.TickCount64;
            var destroyTime = (destroyConditions ?? []).Select(x => x > 0 ? now + x : x).ToArray();
            var dynElem = new DynamicElement()
            {
                Name = name ?? "",
                Elements = elements.ToArray(),
                Layouts = layouts.ToArray(),
                DestroyTime = destroyTime.Length > 0 ? destroyTime : [(long)DestroyCondition.NEVER],
            };
            Svc.Framework.RunOnFrameworkThread(() => P.dynamicElements.Add(dynElem));
            return null;
        }
        catch(Exception e)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// Removes dynamic elements by name. "*" removes all of them.
    /// </summary>
    private static void RemoveDynamicElements(string name)
    {
        Svc.Framework.RunOnFrameworkThread(() =>
        {
            if(name == "*")
            {
                P.dynamicElements.Clear();
            }
            else
            {
                P.RemoveDynamicElements(name);
            }
        });
    }

    private static List<(string name, string group, bool enabled)> GetLayouts()
    {
        return P.Config.LayoutsL.Select(x => (x.Name, x.Group, x.Enabled)).ToList();
    }

    /// <summary>
    /// Enables or disables a layout ("Layout") or an element of a layout ("Layout~Element"), the same as the web API.
    /// Layouts with "Disable disabling" set are not changed.
    /// </summary>
    private static void SetLayoutState(string name, bool enabled)
    {
        Svc.Framework.RunOnFrameworkThread(() => P.CommandManager.SwitchState(name, enabled, web: true));
    }
}
