using HarmonyLib;
using ZumbiBots.Classes;

namespace ZumbiBots.Patches;

[HarmonyPatch(typeof(PlayerMeleeState))]
internal static class PlayerMeleeStatePatch
{
    [HarmonyPrefix]
    [HarmonyPatch("TriggerHitEffects")]
    private static bool SuppressBotMeleeHitmarker(PlayerMeleeState __instance, PlayerMain playerMain)
    {
        if (playerMain == null || !Helpers.IsBot(playerMain)) return true;

        var traverse = Traverse.Create(__instance);
        var attack = traverse.Field<PlayerMeleeAttack>("attack").Value;
        var hitStopTrigger = traverse.Field<bool>("hitStopTrigger").Value;

        var num = attack != null ? attack.HitStopStrength : 0f;
        if (!hitStopTrigger)
            num *= 0.25f;

        traverse.Field("hitStopTrigger").SetValue(false);
        playerMain.TriggerHitStop(num);

        return false;
    }
}
