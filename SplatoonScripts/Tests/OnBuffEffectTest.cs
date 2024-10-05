using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using Splatoon.SplatoonScripting;
using System.Collections.Generic;

namespace SplatoonScriptsOfficial.Tests;
internal class OnBuffEffectTest :SplatoonScript
{
    public override HashSet<uint>? ValidTerritories => null;

    public override void OnGainBuffEffect(uint sourceId, Status Status)
    {
        var gameObject = sourceId.GetObject();
        PluginLog.Information($"OnGainBuffEffect: [{gameObject.Name}({sourceId})] {Status.StatusId} : {Status.Param}");
    }

    public override void OnRemoveBuffEffect(uint sourceId, Status Status)
    {
        var gameObject = sourceId.GetObject();
        PluginLog.Information($"OnRemoveBuffEffect: [{gameObject.Name}({sourceId})] {Status.StatusId}  :  {Status.Param}");
    }
}
