using System.Collections.Generic;
using HarmonyLib;

namespace ZumbiBots.Patches;

[HarmonyPatch(typeof(ServerListener))]
internal static class ServerListenerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("BroadcastLobbyLoadout")]
    private static bool FixBotSkinSync(ServerListener __instance, int sourceConnID, int lobbyID,
        int loadoutLevel, InventoryItem.ID[] items, List<PerkID> perks)
    {
        ServerController.instance?.GetSpeaker?.TreatLobbyLoadoutRequest(lobbyID);

        var player = LobbyController.instance?.GetPlayerByLobbyID(lobbyID);
        if (player != null)
        {
            ServerController.instance?.GetSpeaker?.BroadCastSkin(-1, lobbyID, player.skinID, player.skinGender,
                player.skinColorSet);
        }

        return false;
    }
}
