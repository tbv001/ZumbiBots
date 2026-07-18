using HarmonyLib;
using ZumbiBots.Classes;

namespace ZumbiBots.Patches;

[HarmonyPatch(typeof(Shot))]
internal static class ShotPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("HitDamageTaker")]
    private static void SuppressBotHitmarker(Shot __instance, out bool __state)
    {
        var traverse = Traverse.Create(__instance);
        __state = traverse.Field<bool>("isFriendly").Value;

        var curDamage = traverse.Field<Damage>("curDamage").Value;
        if (curDamage?.zombieDamage.sourcePlayer != null)
        {
            if (Helpers.IsBot(curDamage.zombieDamage.sourcePlayer))
            {
                traverse.Field("isFriendly").SetValue(false);
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch("HitDamageTaker")]
    private static void RestoreBotHitmarker(Shot __instance, bool __state)
    {
        Traverse.Create(__instance).Field("isFriendly").SetValue(__state);
    }
}
