
using ECommons.MathHelpers;
using Splatoon.RenderEngines;
using Splatoon.Serializables;

namespace Splatoon.Structures;

public abstract class DisplayObject
{
    public RenderEngineKind RenderEngineKind;
    public DisplayObjectOrigin Origin = DisplayObjectOrigin.Current;
}

