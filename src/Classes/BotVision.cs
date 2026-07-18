using UnityEngine;

namespace ZumbiBots.Classes;

public static class BotVision
{
    public static readonly int GeneralMask = LayerMask.GetMask("Geometry", "VehicleMain");
    public static readonly int TargetMask = LayerMask.GetMask("Zombie", "ZombieHitBox", "Geometry", "VehicleMain");

    public static Vector3 GetBotAimingDirection(PlayerMain playerMain)
    {
        var forward = playerMain.transform.forward;
        if (playerMain.SpineControl != null)
        {
            forward = Quaternion.AngleAxis(-playerMain.SpineControl.camAng, playerMain.transform.right) * forward;
        }

        return forward;
    }

    public static Vector3 GetBotHeadPosition(PlayerMain playerMain)
    {
        return playerMain == null ? Vector3.zero : playerMain.CenterColumnPosition(0.85f);
    }

    public static bool IsPosVisible(Vector3 pos1, Vector3 pos2)
    {
        var direction = pos2 - pos1;
        var distance = direction.magnitude;
        return !Physics.Raycast(pos1, direction, distance, GeneralMask);
    }

    public static bool IsPosWithinFov(PlayerMain playerMain, Vector3 pos, float fov)
    {
        var headPos = GetBotHeadPosition(playerMain);
        var forward = GetBotAimingDirection(playerMain);
        var offset = pos - headPos;
        var distance = offset.magnitude;
        if (distance < 0.1f)
            return true;

        var directionToPos = offset / distance;
        var angle = Vector3.Angle(forward, directionToPos);
        var leeway = Mathf.Atan(0.5f / distance) * Mathf.Rad2Deg;

        return angle < (fov * 0.5f) + leeway;
    }
}
