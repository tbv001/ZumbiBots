using UnityEngine;

namespace ZumbiBots.Classes;

public static class BotGeneral
{
    public static void BotRespawn(PlayerMain playerMain)
    {
        if (playerMain == null)
            return;

        if (!MatchController.instance.RespawningAllowed())
            return;

        var respawnPoint = MatchController.instance.respawn.GetRespawnPoint(playerMain.transform.position);
        playerMain.RespawnAt(respawnPoint);
        Logging.DebugLog($"Bot '{playerMain.lobbyPlayer?.playerName}' respawned at {respawnPoint}");
    }

    public static bool AllPlayersNearHeli(Vector3 heliPos)
    {
        var players = PlayersController.instance.players;
        foreach (var player in players)
        {
            if (!Helpers.IsDistTo(player.transform.position, heliPos, 50f))
                return false;
        }

        return true;
    }

    public static bool GetClosestPlayer(PlayerMain playerMain, out PlayerMain closestPlayer)
    {
        closestPlayer = null;

        var minSqrDist = float.MaxValue;
        var playerPos = playerMain.transform.position;
        var players = PlayersController.instance.players;
        foreach (var player in players)
        {
            if (player == playerMain)
                continue;

            if (player.healthState != PlayerMain.HealthState.Alive)
                continue;

            var sqrDist = (player.transform.position - playerPos).sqrMagnitude;
            if (!(sqrDist < minSqrDist))
                continue;

            minSqrDist = sqrDist;
            closestPlayer = player;
        }

        return closestPlayer != null;
    }

    public static bool NearHeli(PlayerMain playerMain, Vector3 heliPos)
    {
        return Helpers.IsDistTo(playerMain.transform.position, heliPos, 25f);
    }
}
