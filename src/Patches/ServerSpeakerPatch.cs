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
        foreach (var player in LobbyController.instance.players)
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

    [HarmonyPrefix]
    [HarmonyPatch(nameof(ServerSpeaker.TreatLobbyLoadoutRequest))]
    private static bool FixBotLoadoutSync(ServerSpeaker __instance, int lobbyID)
    {
        if (lobbyID == 0)
        {
            if (LoadoutSelector.instance != null)
            {
                LoadoutSelector.instance.SyncLobbyLoadout();
            }

            return false;
        }

        var player = LobbyController.instance?.GetPlayerByLobbyID(lobbyID);
        if (player == null)
        {
            return false;
        }

        if (player.connection != null)
        {
            NetMatchMessage.LobbyLoadoutRequest(__instance.sendBuffer, lobbyID);
            __instance.SendBuffer(ServerController.PacketReliability.Unreliable, player.connection.ConnectionID);
        }
        else
        {
            InventoryItem.ID[] items = null;
            var playerIndex = LobbyController.instance?.GetPlayerIndex(lobbyID) ?? -1;
            var lobbyMenu = LobbyController.instance?.lobbyMenu;
            if (lobbyMenu?.slots != null && playerIndex >= 0 && playerIndex < lobbyMenu.slots.Length)
            {
                var slot = lobbyMenu.slots[playerIndex];
                if (slot != null)
                {
                    var equippedItems = Traverse.Create(slot).Field<LobbyEquippedItems>("equippedItems").Value;
                    items = equippedItems?.Items;
                }
            }

            if (items == null || items.Length < 4)
            {
                items =
                [
                    InventoryItem.ID.None,
                    InventoryItem.ID.None,
                    InventoryItem.ID.None,
                    InventoryItem.ID.None
                ];
            }

            __instance.BroadcastLobbyLoadout(-1, lobbyID, player.loadoutLevel, items, player.perks ?? []);
        }

        return false;
    }
}
