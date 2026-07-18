using HarmonyLib;
using ZumbiBots.Classes;

namespace ZumbiBots.Patches;

[HarmonyPatch]
internal static class ThrowableControllerPatch
{
    private static int? _overrideSourceLobbyId;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ThrowableController), "ExplodeThrowable")]
    private static void ExplodeThrowablePrefix(ThrowableInstance tInst)
    {
        if (tInst.throwingPlayer != null && Helpers.IsBot(tInst.throwingPlayer))
        {
            _overrideSourceLobbyId = tInst.throwingPlayer.lobbyPlayer.lobbyID;
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
