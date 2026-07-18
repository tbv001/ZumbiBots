using HarmonyLib;

namespace ZumbiBots.Patches;

[HarmonyPatch(typeof(LobbyController))]
internal static class LobbyControllerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("AddDisconnectedPlayer")]
    private static bool SkipBotConnection(LobbyPlayer lobbyPlayer)
    {
        return lobbyPlayer.connection != null;
    }
}
