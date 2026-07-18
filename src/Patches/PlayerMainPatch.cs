using HarmonyLib;
using ZumbiBots.Classes;
using UnityEngine;

namespace ZumbiBots.Patches;

[HarmonyPatch(typeof(PlayerMain))]
internal static class PlayerMainPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("ProcessHealth")]
    private static bool SkipDeadTimerForBots(PlayerMain __instance)
    {
        if (!Helpers.IsBot(__instance)) return true;

        if (__instance.healthState == PlayerMain.HealthState.Dead)
        {
            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch("TakeDamage")]
    private static bool BotTakeDamage(PlayerMain __instance, Damage damage)
    {
        if (!Helpers.IsBot(__instance)) return true;

        if (__instance.healthState != PlayerMain.HealthState.Alive)
            return false;

        if (!damage.playerDamage.dodgeOverride)
        {
            if (__instance.movement.IsRolling())
                return false;

            if (__instance.CanUseStamina())
            {
                BotInput.AddKey(__instance, PlayerInputKey.KeyID.Roll);
                return false;
            }
        }

        var traverse = Traverse.Create(__instance);
        var dmgAmount = traverse.Method("ModifiedDamageAmount", damage).GetValue<float>();
        var permanency = traverse.Method("ModifiedPermanency", damage.playerDamage.permanency).GetValue<float>();

        __instance.healthFast -= dmgAmount;
        __instance.healthSlow -= dmgAmount * permanency;

        if (__instance.healthSlow < __instance.MaxHealth * 0.25f)
            __instance.healthSlow = __instance.MaxHealth * 0.25f;

        FXPropsController.instance.SpawnProp(
            __instance.statusEffects.HasToxic ? FXProp.ID.ToxicBlood : FXProp.ID.Blood1, damage.hitPoint,
            Quaternion.identity, null, true);
        AudioController.instance.PlayImpact(__instance.playerAudio.gun.transform.position,
            AudioController.ImpactFXID.CleanPunch, true);

        if (__instance.healthFast < 0f)
        {
            __instance.healthFast = 0f;
            __instance.SetHealthState(PlayerMain.HealthState.Dying, true);
            __instance.movement?.EnterDyingState();
            return false;
        }

        if (Randomizer.Unity.HitChance(damage.playerDamage.staggerChance))
            __instance.movement?.TryStagger();

        return false;
    }
}
