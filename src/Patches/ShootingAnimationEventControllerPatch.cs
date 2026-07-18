using HarmonyLib;
using ZumbiBots.Classes;

namespace ZumbiBots.Patches;

[HarmonyPatch(typeof(ShootingAnimationEventController))]
internal static class ShootingAnimationEventControllerPatch
{
    internal static bool[] SavedEventRanFlags;

    [HarmonyPrefix]
    [HarmonyPatch(nameof(ShootingAnimationEventController.ProcessEventsFor))]
    private static bool FixShellEjection(PlayerAnimationEvents animationEvents)
    {
        if (animationEvents.targetSkin?.CurrentPlayer != null &&
            Helpers.IsBot(animationEvents.targetSkin.CurrentPlayer))
        {
            return false;
        }

        return true;
    }
}
