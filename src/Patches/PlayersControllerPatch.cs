using HarmonyLib;
using ZumbiBots.Components;

namespace ZumbiBots.Patches;

[HarmonyPatch(typeof(PlayersController))]
internal static class PlayersControllerPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(PlayersController.InstantiatePlayer))]
    private static void AddBrainsToBots(PlayerMain __result, LobbyPlayer lobbyPlayer)
    {
        if (lobbyPlayer != null && BotManager.BotLobbyIDs.Contains(lobbyPlayer.lobbyID))
        {
            __result.SetLobbyReference(lobbyPlayer);
            __result.gameObject.AddComponent<BotBrain>();
        }
    }
}
