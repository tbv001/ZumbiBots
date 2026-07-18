using HarmonyLib;
using ZumbiBots.Classes;

namespace ZumbiBots.Patches;

[HarmonyPatch(typeof(PlayerMovement))]
internal static class PlayerMovementPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(PlayerMovement.GetGround))]
    private static void BotsFallDamageFix(PlayerMovement __instance, ref bool fallDamage)
    {
        if (MultiplayerController.instance.IsServer() && Helpers.IsBot(__instance.playerMain))
        {
            fallDamage = true;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch("Revive", typeof(PlayerMain))]
    private static void ReviveFixSinglePlayer(PlayerMovement __instance, object[] __args)
    {
        if (MultiplayerController.instance.IsClient() || MultiplayerController.instance.IsOnlineServer())
            return;

        var target = __args[0] as PlayerMain;
        target?.Revive();
    }
}
