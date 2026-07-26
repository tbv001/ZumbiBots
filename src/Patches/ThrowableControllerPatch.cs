using HarmonyLib;
using ZumbiBots.Classes;

namespace ZumbiBots.Patches;

[HarmonyPatch]
internal static class ThrowableControllerPatch
{
    private static int? _overrideSourceLobbyId;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ThrowableController), "ExplodeThrowable")]
    private static void ExplodeThrowablePrefix(ThrowableInstance throwableInstance)
    {
        if (throwableInstance.throwingPlayer != null && Helpers.IsBot(throwableInstance.throwingPlayer))
        {
            _overrideSourceLobbyId = throwableInstance.throwingPlayer.lobbyPlayer.lobbyID;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ThrowableController), "ExplodeThrowable")]
    private static void ExplodeThrowablePostfix()
    {
        _overrideSourceLobbyId = null;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ExplosionController), "ProcessExplosion")]
    private static void ProcessExplosionPrefix(ref int sourceLobbyID)
    {
        if (_overrideSourceLobbyId.HasValue)
        {
            sourceLobbyID = _overrideSourceLobbyId.Value;
        }
    }
}
