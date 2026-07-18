using UnityEngine;

namespace ZumbiBots.Classes;

public static class BotMovement
{
    public static Vector3 GetRandomStrafeDirection(PlayerMain playerMain)
    {
        var playerMainPos = playerMain.transform.position;
        var forward = playerMain.transform.forward;
        var right = playerMain.transform.right;

        return playerMainPos + forward + (Random.value > 0.5f ? right : -right) * 10f;
    }
}
