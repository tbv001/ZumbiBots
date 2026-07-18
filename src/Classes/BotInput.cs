using UnityEngine;

namespace ZumbiBots.Classes;

public static class BotInput
{
    private const float _aimSpeed = 10f;

    public static void ClearInput(PlayerMain playerMain)
    {
        if (playerMain.input == null)
            return;

        foreach (var key in playerMain.input.keys)
        {
            key.getKey = false;
            key.getKeyDown = false;
        }
    }

    public static void LookAtVec(PlayerMain playerMain, Vector3 pos)
    {
        if (playerMain.input == null)
            return;

        var transform = playerMain.transform;
        var dir = (pos - transform.position);
        var distance2D = new Vector2(dir.x, dir.z).magnitude;
        var pitchAngle = Mathf.Atan2(dir.y, distance2D) * Mathf.Rad2Deg;
        playerMain.cam?.angle = Mathf.Lerp(playerMain.cam.angle, Mathf.Clamp(pitchAngle, -80f, 80f),
            Time.deltaTime * _aimSpeed);
        playerMain.SpineControl?.camAng = Mathf.Lerp(playerMain.SpineControl.camAng, Mathf.Clamp(pitchAngle, -80f, 80f),
            Time.deltaTime * _aimSpeed);

        dir.y = 0;
        if (dir.sqrMagnitude > 0.01f)
        {
            var targetRot = Quaternion.LookRotation(dir);
            transform.rotation =
                Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * _aimSpeed);
        }

        playerMain.input.mouseX = 0f;
        playerMain.input.mouseY = 0f;
    }

    public static void MoveToVec(PlayerMain playerMain, Vector3 pos)
    {
        if (playerMain.input == null)
            return;

        var localPos = playerMain.transform.InverseTransformPoint(pos);
        if (localPos.sqrMagnitude <= 0.04f)
            return;

        var localDir = localPos.normalized;
        const float threshold = 0.05f;

        switch (localDir.z)
        {
            case > threshold:
                playerMain.input.keys[(int)PlayerInputKey.KeyID.MoveForward].getKey = true;
                break;
            case < -threshold:
                playerMain.input.keys[(int)PlayerInputKey.KeyID.MoveBack].getKey = true;
                break;
        }

        switch (localDir.x)
        {
            case > threshold:
                playerMain.input.keys[(int)PlayerInputKey.KeyID.MoveRight].getKey = true;
                break;
            case < -threshold:
                playerMain.input.keys[(int)PlayerInputKey.KeyID.MoveLeft].getKey = true;
                break;
        }
    }

    public static void AddKey(PlayerMain playerMain, PlayerInputKey.KeyID key)
    {
        if (playerMain.input == null)
            return;

        playerMain.input.keys[(int)key].getKey = true;
        playerMain.input.keys[(int)key].getKeyDown = true;
    }
}
