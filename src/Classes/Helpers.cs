using UnityEngine;
using ZumbiBots.Components;

namespace ZumbiBots.Classes;

public static class Helpers
{
    public static bool IsBot(PlayerMain playerMain)
    {
        return playerMain.lobbyPlayer != null && BotManager.BotLobbyIDs.Contains(playerMain.lobbyPlayer.lobbyID);
    }

    public static float DistToSqr(Vector3 pos1, Vector3 pos2)
    {
        return (pos1 - pos2).sqrMagnitude;
    }

    public static float DistToSqr_2D(Vector3 pos1, Vector3 pos2)
    {
        var dx = pos1.x - pos2.x;
        var dz = pos1.z - pos2.z;
        return dx * dx + dz * dz;
    }

    public static bool IsDistTo(Vector3 pos1, Vector3 pos2, float dist)
    {
        var distToComp = dist * dist;
        return DistToSqr(pos1, pos2) <= distToComp;
    }

    public static bool IsDistTo_2D(Vector3 pos1, Vector3 pos2, float dist)
    {
        var distToComp = dist * dist;
        return DistToSqr_2D(pos1, pos2) <= distToComp;
    }
}
