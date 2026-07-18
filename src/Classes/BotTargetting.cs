using System;
using UnityEngine;

namespace ZumbiBots.Classes;

public static class BotTargetting
{
    public static float TargetRange = 30f;

    public static bool IsABossActive()
    {
        if (ZombieLoader.Instance == null)
            return false;

        foreach (var zombie in ZombieLoader.Instance.zombies)
        {
            if (zombie.IsBoss && zombie.health.isAlive && !zombie.sleeping && !zombie.IsAiIdle)
                return true;
        }

        return false;
    }

    public static bool GetClosestBoss(PlayerMain playerMain, out Zombie closestBoss, bool inactive = false)
    {
        closestBoss = null;
        if (ZombieLoader.Instance == null)
            return false;

        var minSqrDist = float.MaxValue;
        var playerPos = playerMain.transform.position;

        foreach (var zombie in ZombieLoader.Instance.zombies)
        {
            if (!zombie.IsBoss || !zombie.health.isAlive)
                continue;

            if ((zombie.sleeping || zombie.IsAiIdle) && !inactive)
                continue;

            var sqrDist = Helpers.DistToSqr(zombie.obj.transform.position, playerPos);
            if (!(sqrDist < minSqrDist))
                continue;

            minSqrDist = sqrDist;
            closestBoss = zombie;
        }

        return closestBoss != null;
    }

    public static bool GetClosestInactiveBossForTier(PlayerMain playerMain, ZombieType bossType, out Vector3? position)
    {
        position = null;
        if (ZombieLoader.Instance == null)
            return false;

        var minSqrDist = float.MaxValue;
        var playerPos = playerMain.transform.position;

        foreach (var zombie in ZombieLoader.Instance.zombies)
        {
            if (!zombie.IsBoss || !zombie.health.isAlive || zombie.identity.type != bossType)
                continue;

            if (!zombie.sleeping && !zombie.IsAiIdle)
                continue;

            var sqrDist = Helpers.DistToSqr(zombie.obj.transform.position, playerPos);
            if (!(sqrDist < minSqrDist))
                continue;

            minSqrDist = sqrDist;
            position = zombie.obj.transform.position;

            if (bossType == ZombieType.BossReaper)
                position += zombie.obj.transform.forward * 10;
        }

        foreach (var unloadedZombie in ZombieLoader.Instance.unloadedZombies)
        {
            if (!unloadedZombie.identity.IsBoss || unloadedZombie.identity.type != bossType ||
                unloadedZombie.isWaveZombie)
                continue;

            var sqrDist = Helpers.DistToSqr(unloadedZombie.transform.position, playerPos);
            if (!(sqrDist < minSqrDist))
                continue;

            minSqrDist = sqrDist;
            position = unloadedZombie.transform.position;
        }

        foreach (var propZombie in ZombieLoader.Instance.zombieProps)
        {
            if (!propZombie.identity.IsBoss || propZombie.identity.type != bossType || propZombie.isWaveZombie)
                continue;

            var sqrDist = Helpers.DistToSqr(propZombie.Transform.position, playerPos);
            if (!(sqrDist < minSqrDist))
                continue;

            minSqrDist = sqrDist;
            position = propZombie.Transform.position;
        }

        return position != null;
    }

    public static bool GetClosestWaveZombie(PlayerMain playerMain, out Zombie closestWaveZombie)
    {
        closestWaveZombie = null;
        if (ZombieLoader.Instance == null)
            return false;

        var minSqrDist = float.MaxValue;
        var playerPos = playerMain.transform.position;

        foreach (var zombie in ZombieLoader.Instance.zombies)
        {
            if (!zombie.isWaveZombie || zombie.IsBoss || !zombie.health.isAlive || zombie.sleeping || zombie.IsAiIdle)
                continue;

            var sqrDist = Helpers.DistToSqr(zombie.obj.transform.position, playerPos);
            if (!(sqrDist < minSqrDist))
                continue;

            minSqrDist = sqrDist;
            closestWaveZombie = zombie;
        }

        return closestWaveZombie != null;
    }

    public static bool GetClosestAny(PlayerMain playerMain, out Zombie closestAny)
    {
        closestAny = null;
        if (ZombieLoader.Instance == null)
            return false;

        var minSqrDist = float.MaxValue;
        var playerPos = playerMain.transform.position;

        foreach (var zombie in ZombieLoader.Instance.zombies)
        {
            if (!zombie.health.isAlive)
                continue;

            if ((zombie.sleeping || zombie.IsAiIdle) &&
                !Helpers.IsDistTo(playerPos, zombie.obj.transform.position, 10f))
                continue;

            if (!IsZombieVisible(playerMain, zombie))
                continue;

            var sqrDist = Helpers.DistToSqr(zombie.obj.transform.position, playerPos);
            if (!(sqrDist < minSqrDist))
                continue;

            minSqrDist = sqrDist;
            closestAny = zombie;
        }

        return closestAny != null;
    }

    public static bool IsZombieVisible(PlayerMain playerMain, Zombie zombie)
    {
        if (zombie == null || zombie.obj == null)
            return false;

        var hitboxes = zombie.obj.GetComponentsInChildren<ZombieDamageCollider>();
        if (hitboxes == null || hitboxes.Length == 0)
            return false;

        var botHeadPos = BotVision.GetBotHeadPosition(playerMain);
        var playerPos = playerMain.transform.position;

        foreach (var hitbox in hitboxes)
        {
            var targetPos = hitbox.transform.position - (Vector3.up * 0.5f);
            var direction = (targetPos - botHeadPos).normalized;

            if (!Helpers.IsDistTo(zombie.obj.transform.position, playerPos, TargetRange))
                continue;

            if (!Physics.Raycast(botHeadPos, direction, out var hit, TargetRange, BotVision.TargetMask))
                continue;

            if (hit.collider.GetComponentInParent<ZombieObject>() == zombie.obj)
            {
                return true;
            }
        }

        return false;
    }

    public static Vector3 GetBestHitbox(PlayerMain playerMain, Zombie zombie)
    {
        if (zombie == null || zombie.obj == null)
            return Vector3.zero;

        var hitboxes = zombie.obj.GetComponentsInChildren<ZombieDamageCollider>();
        if (hitboxes == null || hitboxes.Length == 0)
            return zombie.obj.transform.position;

        Array.Sort(hitboxes, (a, b) => b.dmgMultiplier.CompareTo(a.dmgMultiplier));

        var botHeadPos = BotVision.GetBotHeadPosition(playerMain);
        foreach (var hitbox in hitboxes)
        {
            var targetPos = hitbox.transform.position - (Vector3.up * 0.5f);
            var direction = (targetPos - botHeadPos).normalized;

            if (!Physics.Raycast(botHeadPos, direction, out var hit, TargetRange, BotVision.TargetMask))
                continue;

            if (hit.collider.GetComponentInParent<ZombieObject>() == zombie.obj)
            {
                return targetPos;
            }
        }

        return zombie.obj.zombieEyeRef != null ? zombie.obj.zombieEyeRef.position : zombie.obj.transform.position;
    }
}
