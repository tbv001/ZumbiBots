using HarmonyLib;
using ZumbiBots.Components;

namespace ZumbiBots.Patches;

[HarmonyPatch(typeof(ServerController))]
internal static class ServerControllerPatch
{
    [HarmonyPostfix]
    [HarmonyPatch("OnSuccessfullyStartedServer")]
    private static void MakeBotsAvailable()
    {
        BotManager.BotIsAvailable = true;
    }

    [HarmonyPrefix]
    [HarmonyPatch("Shutdown")]
    private static void MakeBotsUnavailable()
    {
        BotManager.BotIsAvailable = false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(ServerController.OnPlayerEntryMidgame))]
    private static bool SkipMidgameEntryForBots(int lobbyID)
    {
        var player = LobbyController.instance?.GetPlayerByLobbyID(lobbyID);
        return player == null || player.connection != null;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(ServerController.PlayerEntrySync))]
    private static bool SkipEntrySyncForBots(LobbyPlayer player)
    {
        if (player.connection == null)
        {
            return false;
        }

        return true;
    }
}
