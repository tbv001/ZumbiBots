using HarmonyLib;
using ZumbiBots.Classes;

namespace ZumbiBots.Patches;

[HarmonyPatch(typeof(ServerSpeaker))]
internal static class ServerSpeakerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(ServerSpeaker.SyncPlayerRevive))]
    private static bool BotsReviveFix(int playerlobbyID)
    {
        var targetLobbyPlayer = LobbyController.instance.GetPlayerByLobbyID(playerlobbyID);
        if (targetLobbyPlayer == null) return true;

        if (targetLobbyPlayer.playerObj != null && Helpers.IsBot(targetLobbyPlayer.playerObj))
        {
            targetLobbyPlayer.playerObj.Revive();
            return false;
        }

        if (targetLobbyPlayer.type != LobbyPlayer.Type.Host && targetLobbyPlayer.connection == null)
        {
            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch("BroadcastBufferIngame", typeof(Buffer), typeof(ServerController.PacketReliability), typeof(int),
        typeof(int))]
    private static bool SkipBotBroadcasts(Buffer targetBuffer, ServerController.PacketReliability reliability,
        int ignoreLobbyID, int ignoreConnectionID)
    {
        foreach (LobbyPlayer player in LobbyController.instance.players)
        {
            if (player.type == LobbyPlayer.Type.Client && player.IsInGame && player.connection != null &&
                (ignoreLobbyID < 0 || player.lobbyID != ignoreLobbyID) &&
                (ignoreConnectionID < 0 || player.connection.ConnectionID != ignoreConnectionID))
            {
                ServerController.instance.GetSpeaker.SendBuffer(reliability, player.connection.ConnectionID,
                    targetBuffer);
            }
        }

        return false;
    }
}
