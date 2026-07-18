using Pathfinding;
using UnityEngine;

namespace ZumbiBots.Classes;

public class BotPathfinding(PlayerMain playerMain)
{
    private const float _pathSmoothing = 5f;
    private uint _targetId;
    private Transform _targetTransform;
    private Vector3 _smoothedDirection;
    private bool _hasTarget;
    public PathingOrder CurrentOrder { get; private set; }

    private Transform ProxyTarget
    {
        get
        {
            if (field != null)
                return field;

            var proxyTarget = new GameObject("BotPathfinding_ProxyTarget") { hideFlags = HideFlags.HideInHierarchy };
            proxyTarget.transform.SetParent(playerMain.transform, true);
            proxyTarget.transform.position = playerMain.transform.position;
            field = proxyTarget.transform;

            return field;
        }
    }

    private bool TryGetDirectTargetPosition(out Vector3 targetPos)
    {
        targetPos = _targetTransform.position;

        var botPos = playerMain.transform.position;
        var toTarget = targetPos - botPos;
        if (toTarget.sqrMagnitude < 0.01f)
            return false;

        var direction = toTarget.normalized;
        var distance = toTarget.magnitude;
        return !Physics.Raycast(botPos, direction, distance, BotVision.GeneralMask);
    }

    public Vector3 GetClosestNode()
    {
        var nodes = PathfindingData.Nodes;
        var closestIndex = 0;
        var closestSqrDist = float.MaxValue;

        for (var i = 0; i < nodes.Length; i++)
        {
            var sqrDist = (nodes[i].center - playerMain.transform.position).sqrMagnitude;
            if (!(sqrDist < closestSqrDist))
                continue;

            closestSqrDist = sqrDist;
            closestIndex = i;
        }

        return nodes[closestIndex].center;
    }

    public Vector3 GetRandomNode()
    {
        var nodes = PathfindingData.Nodes;
        return nodes[Random.Range(0, nodes.Length)].center;
    }

    public void SetTarget(Vector3 point)
    {
        ProxyTarget.position = point;
        _targetTransform = ProxyTarget;
        _hasTarget = true;
        _targetId = TargetsManager.Instance.EnsureTarget(_targetTransform);
    }

    public void Update()
    {
        if (!_hasTarget || playerMain == null || playerMain.entityLocation == null)
        {
            CurrentOrder = PathingOrder.StayOrder;
            _smoothedDirection = Vector3.zero;
            return;
        }

        if (_targetTransform != null)
        {
            _targetId = TargetsManager.Instance.EnsureTarget(_targetTransform);
        }

        if (!TargetsManager.Instance.TargetIDIsValid(_targetId))
        {
            _hasTarget = false;
            CurrentOrder = PathingOrder.StayOrder;
            _smoothedDirection = Vector3.zero;
            return;
        }

        TargetsManager.Instance.ResetUnusedTime(_targetId);
        CurrentOrder = PathsManager.Instance.GetPathingOrder(playerMain.entityLocation, false, _targetId);

        if (CurrentOrder is { Climb: false, Jump: false } && TryGetDirectTargetPosition(out var directTargetPos))
        {
            var directDir = directTargetPos - playerMain.transform.position;
            directDir.y = 0f;
            if (directDir.sqrMagnitude > 0.01f)
            {
                CurrentOrder = PathingOrder.AdvanceOrder(directDir.normalized);
            }
        }

        switch (CurrentOrder.Advance)
        {
            case true when CurrentOrder.Direction != Vector3.zero:
            {
                if (_smoothedDirection == Vector3.zero)
                {
                    _smoothedDirection = CurrentOrder.Direction;
                }
                else
                {
                    _smoothedDirection = Vector3.Slerp(_smoothedDirection, CurrentOrder.Direction,
                        Time.deltaTime * _pathSmoothing);
                }

                break;
            }
            case false:
                _smoothedDirection = Vector3.Lerp(_smoothedDirection, Vector3.zero,
                    Time.deltaTime * _pathSmoothing);
                break;
        }
    }

    public Vector3 GetNextMovePos()
    {
        var direction = _smoothedDirection != Vector3.zero ? _smoothedDirection : CurrentOrder.Direction;

        if (direction == Vector3.zero)
        {
            if (_hasTarget)
            {
                return _targetTransform.position;
            }

            return playerMain.transform.position;
        }

        return playerMain.transform.position + direction;
    }

    public bool ShouldJump()
    {
        return CurrentOrder.Jump || CurrentOrder.Climb;
    }
}
