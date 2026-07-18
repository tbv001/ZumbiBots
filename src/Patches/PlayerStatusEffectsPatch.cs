using HarmonyLib;
using ZumbiBots.Classes;
using UnityEngine;

namespace ZumbiBots.Patches;

[HarmonyPatch(typeof(PlayerStatusEffects))]
internal static class PlayerStatusEffectsPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("MyUpdate")]
    private static bool BotUpdateStatusEffects(PlayerStatusEffects __instance)
    {
        if (__instance.targetPlayer == null)
        {
            __instance.targetPlayer = __instance.GetComponent<PlayerMain>();
        }

        if (!Helpers.IsBot(__instance.targetPlayer)) return true;

        if (__instance.targetPlayer.healthState == PlayerMain.HealthState.Alive)
        {
            var traverse = Traverse.Create(__instance);
            traverse.Method("ProcessMeds").GetValue();
            traverse.Method("ProcessFood").GetValue();
            traverse.Method("ProcessToxic").GetValue();
            traverse.Method("ProcessDrink").GetValue();
        }
        else
        {
            foreach (var statusEffect in __instance.statusEffect)
            {
                statusEffect.curValue = 0f;
                statusEffect.tier = 0f;
            }
        }

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("ProcessFood")]
    private static bool BotProcessFood(PlayerStatusEffects __instance)
    {
        if (!Helpers.IsBot(__instance.targetPlayer)) return true;

        var statusEffect = __instance.statusEffect[0];
        if (__instance.targetPlayer.healthFast >= __instance.targetPlayer.healthSlow)
        {
            statusEffect.curValue -= __instance.foodDecayRate * Time.deltaTime;
        }
        else if (statusEffect.curValue > 0f)
        {
            Traverse.Create(__instance).Method("ProcessHealingThroughFood", statusEffect).GetValue();
        }

        statusEffect.curValue = Mathf.Clamp(statusEffect.curValue, 0f, statusEffect.maxValue);

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("ProcessDrink")]
    private static bool BotProcessDrink(PlayerStatusEffects __instance)
    {
        if (!Helpers.IsBot(__instance.targetPlayer)) return true;

        var statusEffect = __instance.statusEffect[3];
        if (__instance.targetPlayer.staminaSlow < __instance.targetPlayer.maxStamina && statusEffect.curValue > 0f)
        {
            Traverse.Create(__instance).Method("ProcessStaminaRecoveryWithDrink", statusEffect).GetValue();
        }

        statusEffect.curValue = Mathf.Clamp(statusEffect.curValue, 0f, statusEffect.maxValue);

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("AddEffect")]
    private static bool BotAddEffect(PlayerStatusEffects __instance, StatusEffect.ID statusID, ref float amount,
        float tier)
    {
        if (!Helpers.IsBot(__instance.targetPlayer)) return true;

        var statusEffect = __instance.statusEffect[(int)statusID];
        if (statusID == StatusEffect.ID.Toxic)
        {
            statusEffect.curValue += amount;
            if (statusEffect.curValue > statusEffect.maxValue)
            {
                statusEffect.tier = 0f;
                statusEffect.curValue = statusEffect.maxValue;
            }
        }
        else
        {
            if (tier < statusEffect.tier)
            {
                amount = Mathf.Min(amount, statusEffect.maxValue - statusEffect.curValue);
            }
            else
            {
                statusEffect.curValue = Mathf.Min(statusEffect.curValue, statusEffect.maxValue - amount);
            }

            var num = statusEffect.curValue + amount;
            var tier2 = statusEffect.curValue / num * statusEffect.tier + amount / num * tier;
            statusEffect.tier = tier2;
            statusEffect.curValue = Mathf.Clamp(statusEffect.curValue + amount, 0f, statusEffect.maxValue);
        }

        return false;
    }
}
