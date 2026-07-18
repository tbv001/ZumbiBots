using HarmonyLib;

namespace ZumbiBots.Patches;

[HarmonyPatch(typeof(PlayerPositionSynchronizer))]
internal static class PlayerPositionSynchronizerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(PlayerPositionSynchronizer.SendAnimation))]
    private static bool SendAnimation_Prefix(PlayerPositionSynchronizer __instance)
    {
        if (__instance.playerMain == null || __instance.playerMain.lobbyPlayer == null)
        {
            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(PlayerPositionSynchronizer.SendUpdate))]
    private static bool SendUpdate_Prefix(PlayerPositionSynchronizer __instance)
    {
        if (__instance.playerMain == null || __instance.playerMain.lobbyPlayer == null)
        {
            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(PlayerPositionSynchronizer.SendRollVector))]
    private static bool SendRollVector_Prefix(PlayerPositionSynchronizer __instance)
    {
        if (__instance.playerMain == null || __instance.playerMain.lobbyPlayer == null)
        {
            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(PlayerPositionSynchronizer.SendEquipment))]
    private static bool SendEquipment_Prefix(PlayerPositionSynchronizer __instance)
    {
        if (__instance.playerMain == null || __instance.playerMain.lobbyPlayer == null)
        {
            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(PlayerPositionSynchronizer.SyncShotOnline))]
    private static bool SyncShotOnline_Prefix(PlayerPositionSynchronizer __instance)
    {
        if (__instance.playerMain == null || __instance.playerMain.lobbyPlayer == null)
        {
            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(PlayerPositionSynchronizer.SyncHealth))]
    private static bool SyncHealth_Prefix(PlayerPositionSynchronizer __instance)
    {
        if (__instance.playerMain == null || __instance.playerMain.lobbyPlayer == null)
        {
            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(PlayerPositionSynchronizer.SyncPingPosition))]
    private static bool SyncPingPosition_Prefix(PlayerPositionSynchronizer __instance)
    {
        if (__instance.playerMain == null || __instance.playerMain.lobbyPlayer == null)
        {
            return false;
        }

        return true;
    }
}
