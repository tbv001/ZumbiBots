using System.Collections.Generic;
using HarmonyLib;

namespace ZumbiBots.Patches;

[HarmonyPatch(typeof(LoadoutUnlocker))]
internal static class LoadoutUnlockerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(LoadoutUnlocker.DistributeLoadoutUnlock))]
    private static bool SkipUnlockingLoadoutForBots(Zombie zombie, List<int> killerLobbyIDs)
    {
        var bossTier = BossfightController.instance.GetBossTier(zombie.identity.type);
        foreach (var killerLobbyId in killerLobbyIDs)
        {
            var playerByLobbyId = LobbyController.instance?.GetPlayerByLobbyID(killerLobbyId);
            if (playerByLobbyId == null)
                continue;

            if (playerByLobbyId.type == LobbyPlayer.Type.Host)
            {
                LoadoutUnlocker.instance?.TryUnlockRandomLoadout(bossTier);
                TutorialController.instance?.OnKilledBoss(zombie.identity.type);
            }
            else
            {
                if (playerByLobbyId.connection == null || ServerController.instance?.GetSpeaker == null)
                    continue;

                ServerController.instance.GetSpeaker.SyncUnlockLoadout(bossTier,
                    playerByLobbyId.connection.ConnectionID);
                ServerController.instance.GetSpeaker.SyncKillBoss(playerByLobbyId.connection.ConnectionID,
                    zombie.identity.type);
            }
        }

        return false;
    }
}
