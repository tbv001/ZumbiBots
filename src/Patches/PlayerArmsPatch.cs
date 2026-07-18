using HarmonyLib;
using ZumbiBots.Classes;
using UnityEngine;

namespace ZumbiBots.Patches;

[HarmonyPatch(typeof(PlayerArms))]
internal static class PlayerArmsPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("GetBulletOrigin")]
    private static bool BotGetBulletOrigin(PlayerArms __instance, ref Vector3 __result)
    {
        if (__instance.playerMain == null || !Helpers.IsBot(__instance.playerMain)) return true;

        __result = BotVision.GetBotHeadPosition(__instance.playerMain);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("GetCameraBasedShotPath")]
    private static bool BotGetCameraBasedShotPath(PlayerArms __instance, ref ShotPath __result)
    {
        if (__instance.playerMain == null || !Helpers.IsBot(__instance.playerMain)) return true;

        var headPos = BotVision.GetBotHeadPosition(__instance.playerMain);
        var aimDir = BotVision.GetBotAimingDirection(__instance.playerMain);
        var tracerOrigin = __instance.EquippedGun != null ? __instance.EquippedGun.barrel.position : headPos;

        __result = new ShotPath(headPos, tracerOrigin, headPos, aimDir);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("GetBaselineShotPath")]
    private static bool BotGetBaselineShotPath(PlayerArms __instance, ref ShotPath __result)
    {
        if (__instance.playerMain == null || !Helpers.IsBot(__instance.playerMain)) return true;

        var headPos = BotVision.GetBotHeadPosition(__instance.playerMain);
        var aimDir = BotVision.GetBotAimingDirection(__instance.playerMain);

        __result = new ShotPath(headPos, headPos, headPos, aimDir);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("GetConvergingOrigin", typeof(Vector3))]
    private static bool BotGetConvergingOrigin(PlayerArms __instance, ref Vector3 tracerOrigin, ref Vector3 __result)
    {
        if (__instance.playerMain == null || !Helpers.IsBot(__instance.playerMain)) return true;

        var headPos = BotVision.GetBotHeadPosition(__instance.playerMain);
        var aimDir = BotVision.GetBotAimingDirection(__instance.playerMain);
        var origin = tracerOrigin;
        __result = headPos + aimDir.normalized * origin.magnitude;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("GetConvergingOrigin", [])]
    private static bool BotGetConvergingOriginNoArgs(PlayerArms __instance, ref Vector3 __result)
    {
        if (__instance.playerMain == null || !Helpers.IsBot(__instance.playerMain)) return true;

        __result = BotVision.GetBotHeadPosition(__instance.playerMain);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("ShotConvergingDirection", MethodType.Getter)]
    private static bool BotGetShotConvergingDirection(PlayerArms __instance, ref Vector3 __result)
    {
        if (__instance.playerMain == null || !Helpers.IsBot(__instance.playerMain)) return true;

        __result = BotVision.GetBotAimingDirection(__instance.playerMain);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("CanReload")]
    private static bool BotCanReload(PlayerArms __instance, ref bool __result)
    {
        if (__instance.playerMain == null || !Helpers.IsBot(__instance.playerMain)) return true;
        if (__instance.EquippedGun == null)
            return true;

        var equipment = __instance.playerMain.inventory.GetEquipment(__instance.selectedWeapon);
        var databaseGun = equipment?.GetDataBaseItem() as DatabaseGun;
        if (databaseGun == null)
            return true;

        __result = equipment.ammo < databaseGun.maxAmmo;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("ReloadGun")]
    private static bool BotReloadGun(PlayerArms __instance, int reloadAmount)
    {
        if (__instance.playerMain == null || !Helpers.IsBot(__instance.playerMain)) return true;

        var equipment = __instance.playerMain.inventory.GetEquipment(__instance.selectedWeapon);
        var databaseGun = equipment?.GetDataBaseItem() as DatabaseGun;
        if (databaseGun == null)
            return true;

        equipment.ammo = databaseGun.maxAmmo;
        if (__instance.EquippedGun != null)
        {
            __instance.EquippedGun.ResetCooldown();
        }

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch("UpdateArms")]
    private static void SaveBotAnimEventFlags(PlayerArms __instance)
    {
        if (__instance.playerMain == null || !Helpers.IsBot(__instance.playerMain))
            return;

        var controller = ShootingAnimationEventController.instance;
        if (controller == null)
            return;

        var flags = Traverse.Create(controller).Field("eventRanFlags").GetValue<bool[]>();
        ShootingAnimationEventControllerPatch.SavedEventRanFlags = flags;
    }

    [HarmonyPostfix]
    [HarmonyPatch("UpdateArms")]
    private static void RestoreBotAnimEventFlags(PlayerArms __instance)
    {
        if (__instance.playerMain == null || !Helpers.IsBot(__instance.playerMain))
            return;

        var controller = ShootingAnimationEventController.instance;
        if (controller == null)
            return;

        Traverse.Create(controller).Field("eventRanFlags")
            .SetValue(ShootingAnimationEventControllerPatch.SavedEventRanFlags);
        ShootingAnimationEventControllerPatch.SavedEventRanFlags = null;
    }
}
