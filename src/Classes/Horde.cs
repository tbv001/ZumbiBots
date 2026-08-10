using System.Collections.Generic;
using UnityEngine;

namespace ZumbiBots.Classes;

public static class Horde
{
    private const float LinkDistance = 5f;
    private const float SqrLinkDistance = LinkDistance * LinkDistance;
    private static List<List<Zombie>> _hordes;

    public static void ComputeHordes()
    {
        _hordes = [];
        if (ZombieLoader.Instance == null)
            return;

        var visited = new HashSet<Zombie>();
        foreach (var zombie in ZombieLoader.Instance.zombies)
        {
            if (zombie == null || !zombie.health.isAlive || visited.Contains(zombie))
                continue;

            var cluster = new List<Zombie>();
            var stack = new Stack<Zombie>();
            stack.Push(zombie);
            visited.Add(zombie);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                cluster.Add(current);
                var currentPos = current.obj.transform.position;

                foreach (var other in ZombieLoader.Instance.zombies)
                {
                    if (other == null || !other.health.isAlive || visited.Contains(other))
                        continue;

                    if (Helpers.DistToSqr(currentPos, other.obj.transform.position) <= SqrLinkDistance)
                    {
                        visited.Add(other);
                        stack.Push(other);
                    }
                }
            }

            if (cluster.Count > 0)
                _hordes.Add(cluster);
        }
    }

    public static Vector3 GetClosestZombieInClosestHorde(PlayerMain player, List<Zombie> horde)
    {
        if (_hordes == null || _hordes.Count == 0 || player == null)
            return Vector3.zero;

        var playerPos = player.transform.position;
        if (horde.Count == 0)
            return Vector3.zero;

        var closest = horde[0];
        var minSqrDist = Helpers.DistToSqr(playerPos, closest.obj.transform.position);
        for (var i = 1; i < horde.Count; i++)
        {
            var sqrDist = Helpers.DistToSqr(playerPos, horde[i].obj.transform.position);
            if (sqrDist < minSqrDist)
            {
                minSqrDist = sqrDist;
                closest = horde[i];
            }
        }

        return closest.obj.transform.position;
    }

    public static int GetClosestHordeCount(PlayerMain player, List<Zombie> horde)
    {
        if (_hordes == null || _hordes.Count == 0 || player == null)
            return 0;

        return horde.Count;
    }

    public static List<Zombie> FindClosestHorde(Vector3 playerPos, out Vector3 closestCenter)
    {
        if (_hordes == null || _hordes.Count == 0)
        {
            closestCenter = Vector3.zero;
            return [];
        }

        var closestHorde = _hordes[0];
        closestCenter = ComputeCenter(closestHorde);
        var minSqrDist = float.MaxValue;
        foreach (var horde in _hordes)
        {
            var center = ComputeCenter(horde);
            var sqrDist = (center - playerPos).sqrMagnitude;
            if (sqrDist < minSqrDist)
            {
                minSqrDist = sqrDist;
                closestHorde = horde;
                closestCenter = center;
            }
        }

        return closestHorde;
    }

    private static Vector3 ComputeCenter(List<Zombie> horde)
    {
        var center = Vector3.zero;
        foreach (var zombie in horde)
            center += zombie.obj.transform.position;

        return center / horde.Count;
    }
}
