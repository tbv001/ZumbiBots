using HarmonyLib;
using ZumbiBots.Classes;

namespace ZumbiBots.Patches;

[HarmonyPatch(typeof(Zombie))]
internal static class ZombiePatch
{
    [HarmonyPrefix]
    [HarmonyPatch("OnZombieKilledBy")]
    private static bool SkipBotsKillFeed(PlayerMain sourcePlayer)
    {
        if (sourcePlayer == null)
            return true;

        return !Helpers.IsBot(sourcePlayer);
    }
}
