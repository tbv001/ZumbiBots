using HarmonyLib;
using ZumbiBots.Classes;
using UnityEngine;

namespace ZumbiBots.Components;

public class BotBrain : MonoBehaviour
{
    public PlayerMain BotPlayerMain;
    public BotPathfinding Pathfinding;

    // Bot states
    private Vector3? _targetMovePos;
    private Vector3? _targetLookPos;
    private Vector3? _backupPos;
    public Vector3? InactiveBossPos;
    public Zombie CurrentTarget;
    public Zombie BossTarget;
    public Zombie WaveTarget;
    private bool _moveNoMatterWhat;
    private bool _lootIsSack;
    private bool _alwaysUseGun;

    // Timers
    private float _deadTimer;
    private float _doorInteractCd;
    private float _backupTimer;
    private float _statDebugTimer;
    private float _needsTimer;

    // Stuck handling
    private Vector3 _lastStuckPos;
    private float _stuckTimer;
    private float _doorStuckTimer;
    private float _doorStuckCd;

    // Macro stuck handling
    private Vector3 _macroStuckCheckPos;
    private float _macroStuckTimer;
    private float _macroDoorTime;
    private Vector3? _macroDoorPos;

    // Random strafing
    private Vector3 _currentStrafeDir;
    private bool _shouldStrafe;
    private float _strafeTime;
    private float _strafeDirTimer;

    // Horde handling
    public Vector3 ClosestHordePos;
    public Vector3 ClosestZombieInHordePos;
    public int ClosestHordeCount;

    // Revive handling
    public InteractableObject TargetRevive;

    // Water avoidance
    private float _waterTimer;
    private bool _isInWater;

    // Retreat
    private float _retreatTimer;
    private bool _shouldRetreat;

    // Pyre handling
    private PyreInteractable _targetPyre;
    private float _pyreUpdateTimer;

    // Throwables handling
    private bool _shouldThrow;
    private float _throwableCooldown;
    private float _throwTime;

    // Looting
    public InteractableObject ClosestLoot;

    // Random pos
    private Vector3? _randomPos;
    private float _randomPosTimer;

    // Vault spot
    private float _vaultSpotTimer;
    private Vector3? _vaultSpotPos;

    // Bot input
    private bool _shouldShoot;
    private bool _shouldRun;
    private bool _shouldJump;
    private bool _shouldRoll;
    private bool _shouldReload;
    private bool _shouldInteract;

    // Bot needs
    private bool _needEat;
    private bool _needDrink;
    private bool _needHeal;
    private bool _needStaminaRegen;
    private bool _hasEverything;
    private bool _hasMelee;
    public bool HasGun;
    public bool HasFood;
    public bool HasDrink;
    public bool HasHeal;

    private void Start()
    {
        BotPlayerMain = GetComponent<PlayerMain>();
        BotPlayerMain?.movement?.body?.isKinematic = false;

        if (BotPlayerMain != null)
        {
            Pathfinding = new BotPathfinding(BotPlayerMain);
            Traverse.Create(BotPlayerMain).Method("FillHealthAndStamina").GetValue();

            Logging.DebugLog(
                $"'{BotPlayerMain.lobbyPlayer?.playerName}' has spawned: " +
                $"healthFast: {BotPlayerMain.healthFast}, " +
                $"healthSlow: {BotPlayerMain.healthSlow}, " +
                $"staminaFast: {BotPlayerMain.staminaFast}, " +
                $"staminaSlow: {BotPlayerMain.staminaSlow}, " +
                $"staminaBaseRegenFactor: {BotPlayerMain.staminaBaseRegenFactor}, " +
                $"staminaBaseDrain: {BotPlayerMain.staminaBaseDrain}");

            _targetMovePos = BotPlayerMain.transform.position;
            _targetLookPos = BotPlayerMain.transform.position + BotPlayerMain.transform.forward;
            _lastStuckPos = BotPlayerMain.transform.position;
            _macroStuckCheckPos = BotPlayerMain.transform.position;
        }
    }

    private void ClearBotStates()
    {
        _targetLookPos = null;
        _targetMovePos = null;
        _shouldShoot = false;
        _shouldRun = true;
        _shouldJump = false;
        _shouldRoll = false;
        _shouldReload = false;
        _shouldInteract = false;
        _shouldStrafe = false;
        _moveNoMatterWhat = false;
        _lootIsSack = false;
        _shouldRetreat = false;
        _alwaysUseGun = false;
    }

    private void Update()
    {
        if (BotPlayerMain == null || BotPlayerMain.lobbyPlayer == null)
            return;

        // Update bot
        UpdateBotControlled();
        BotInput.ClearInput(BotPlayerMain);
        ClearBotStates();

        // Respawn if dead after a while
        if (BotPlayerMain.healthState == PlayerMain.HealthState.Dead)
        {
            _deadTimer += Time.deltaTime;
            if (_deadTimer >= BotPlayerMain.RespawnTime)
            {
                _deadTimer = 0f;
                BotGeneral.BotRespawn(BotPlayerMain);
            }

            return;
        }

        _deadTimer = 0f;

        // Debug
        if (BotMenu.EnableDebug)
        {
            _statDebugTimer += Time.deltaTime;
            if (_statDebugTimer >= 1f)
            {
                _statDebugTimer = 0f;

                var statusStr = "";
                if (BotPlayerMain.statusEffects != null && BotPlayerMain.statusEffects.statusEffect != null)
                {
                    foreach (var effect in BotPlayerMain.statusEffects.statusEffect)
                    {
                        if (effect == null)
                            continue;

                        statusStr += $"{effect.id}: {effect.curValue:F1} (T{effect.tier:F1}), ";
                    }
                }

                Logging.DebugLog(
                    $"'{BotPlayerMain.lobbyPlayer?.playerName}' stats: " +
                    $"health: {BotPlayerMain.healthFast:F1}/{BotPlayerMain.healthSlow:F1}, " +
                    $"stamina: {BotPlayerMain.staminaFast:F1}/{BotPlayerMain.staminaSlow:F1}, " +
                    $"effects: {statusStr}");
            }
        }

        if (BotMenu.DisableThinking)
            return;

        // Stuck handling
        if (Helpers.IsDistTo_2D(BotPlayerMain.transform.position, _lastStuckPos, 0.055f))
        {
            _stuckTimer += Time.deltaTime;
            if (_stuckTimer >= 1f)
            {
                _stuckTimer = 0f;

                if (BotInteraction.GetClosestInteractableDoor(BotPlayerMain, out var doorStuck, true) &&
                    _doorStuckCd <= 0f)
                {
                    BotInteraction.ForceInteract(BotPlayerMain, doorStuck);
                    _doorInteractCd = 1f;
                    _doorStuckCd = 3f;
                    _doorStuckTimer = 1f;
                }
                else
                {
                    _shouldJump = true;
                    _shouldRun = false;
                }
            }
        }
        else
        {
            _stuckTimer = 0f;
        }

        if (_doorStuckCd > 0f)
        {
            _doorStuckCd -= Time.deltaTime;
        }

        // Macro stuck handling
        _macroStuckTimer += Time.deltaTime;
        if (_macroStuckTimer >= 3f)
        {
            _macroStuckTimer = 0f;
            if (Helpers.IsDistTo_2D(BotPlayerMain.transform.position, _macroStuckCheckPos, 3f))
            {
                if (HasGun && BotInteraction.GetClosestDestroyableDoor(BotPlayerMain, out var doorHitPos))
                {
                    _macroDoorTime = 2f;
                    _macroDoorPos = doorHitPos;
                }

                _shouldJump = true;
                _macroStuckCheckPos = BotPlayerMain.transform.position;
            }
            else
            {
                _macroStuckCheckPos = BotPlayerMain.transform.position;
            }
        }

        _lastStuckPos = BotPlayerMain.transform.position;

        // Get to the closest alive player if down
        if (BotPlayerMain.healthState == PlayerMain.HealthState.Dying)
        {
            if (BotGeneral.GetClosestPlayer(BotPlayerMain, out var closestPlayer))
            {
                _targetMovePos = closestPlayer.transform.position;
                _moveNoMatterWhat = true;
            }

            UpdateLate();
            return;
        }

        // Needs
        _needsTimer += Time.deltaTime;
        if (_needsTimer > 1f)
        {
            _needsTimer = 0f;
            if (BotPlayerMain.statusEffects?.statusEffect != null)
            {
                foreach (var effect in BotPlayerMain.statusEffects.statusEffect)
                {
                    if (effect == null)
                        continue;

                    switch (effect.id)
                    {
                        case StatusEffect.ID.Drink:
                        {
                            var effectPercentage = effect.curValue / effect.maxValue;
                            _needDrink = effectPercentage < 0.5f;
                            break;
                        }
                        case StatusEffect.ID.Food:
                        {
                            var effectPercentage = effect.curValue / effect.maxValue;
                            _needEat = effectPercentage < 0.5f;
                            break;
                        }
                    }
                }
            }

            var maxHealthPercentage = BotPlayerMain.healthSlow / BotPlayerMain.MaxHealth;
            var healthPercentage = BotPlayerMain.healthFast / BotPlayerMain.MaxHealth;
            _needHeal = healthPercentage < 0.3f || maxHealthPercentage < 0.6f;

            BotInventory.CheckNeeds(BotPlayerMain, out HasGun, out _hasMelee, out HasFood, out HasDrink,
                out HasHeal);
            BotInventory.ManageInventory(BotPlayerMain);
            _hasEverything = HasGun && HasFood && HasDrink && HasHeal;
        }

        _shouldRetreat = HasHeal && _needHeal && (CurrentTarget != null || ClosestHordeCount > 0);

        // Retreat
        if (ClosestHordeCount >= 5 && Helpers.IsDistTo(BotPlayerMain.transform.position, ClosestHordePos, 5f))
        {
            _retreatTimer = 1f;
        }
        else if (_retreatTimer > 0f)
        {
            _retreatTimer -= Time.deltaTime;
        }

        if (_retreatTimer > 0f)
        {
            var awayDir = (BotPlayerMain.transform.position - ClosestZombieInHordePos).normalized;
            _targetMovePos = BotPlayerMain.transform.position + awayDir * 100f;
            _shouldRetreat = true;
        }

        // Targetting
        if (CurrentTarget != null && CurrentTarget.health.isAlive && !_shouldRetreat)
        {
            var isHoldingMelee = BotInventory.IsHoldingMelee(BotPlayerMain) || BotPlayerMain.arms?.EquippedItem == null;
            var isMelee = (isHoldingMelee || !HasGun) && ClosestHordeCount <= 2 && !CurrentTarget.IsBoss;
            if ((isMelee && !CurrentTarget.IsBoss) || HasGun)
            {
                var bestTargetHitbox = BotTargetting.GetBestHitbox(BotPlayerMain, CurrentTarget);
                _targetLookPos = bestTargetHitbox;

                if (isMelee)
                {
                    _shouldStrafe = false;
                    _targetMovePos = CurrentTarget.obj.transform.position;

                    var equippedMelee = BotPlayerMain.arms?.EquippedMelee;
                    var baseReach = equippedMelee != null ? equippedMelee.reach : 0.8f;
                    var inMeleeState = BotPlayerMain.movement?.GetEffectiveState() == PlayerMovement.State.Melee;
                    var effectiveReach = inMeleeState ? baseReach + 2.0f : baseReach + 1.0f;

                    _shouldShoot = Helpers.IsDistTo(BotPlayerMain.transform.position,
                        CurrentTarget.obj.transform.position, effectiveReach);
                    _shouldRun = !Helpers.IsDistTo(BotPlayerMain.transform.position,
                        CurrentTarget.obj.transform.position, effectiveReach + 1.0f);
                }
                else
                {
                    _shouldRun = false;
                    _shouldShoot = true;
                    _shouldStrafe = true;
                }

                var backupDistance = isMelee ? 1f : 10f;
                if (Helpers.IsDistTo(BotPlayerMain.transform.position, ClosestZombieInHordePos, backupDistance))
                {
                    var awayDir = (BotPlayerMain.transform.position - ClosestZombieInHordePos).normalized;
                    _backupPos = BotPlayerMain.transform.position + awayDir * 100f;
                    _backupTimer = 1f;
                }
            }
        }
        else
        {
            CurrentTarget = null;
        }

        // Reload
        if (BotInventory.IsHoldingGun(BotPlayerMain))
        {
            var curAmmo = BotInventory.GetCurAmmoCount(BotPlayerMain);
            var maxAmmo = BotInventory.GetMaxAmmoCount(BotPlayerMain);
            if ((CurrentTarget == null && curAmmo < maxAmmo) || curAmmo == 0)
            {
                _shouldReload = true;
                _shouldRun = false;
                _shouldShoot = false;
            }
        }

        // Revive handling
        if (TargetRevive != null)
        {
            _targetMovePos = TargetRevive.transform.position;
            _shouldRun = true;
            _moveNoMatterWhat = _backupTimer <= 0f;
            if (Helpers.IsDistTo(BotPlayerMain.transform.position, TargetRevive.transform.position, 2f))
            {
                _targetLookPos = TargetRevive.transform.position;
                _shouldRun = false;
                _shouldShoot = false;
                _shouldInteract = true;
            }
        }

        // Pyre/Brazier lighting
        if (!_shouldRetreat && CurrentTarget == null && TargetRevive == null && !BotGameManager.HelicopterArrived &&
            WorkbenchInteractions.instance.BurningPyreCount < 12 &&
            !WavesController.instance.HaveToKillBoss)
        {
            var hasPyreFuel = BotInventory.HasPyreFuel(BotPlayerMain);
            if (!hasPyreFuel)
            {
                _pyreUpdateTimer = 0f;
                _targetPyre = null;
            }
            else
            {
                _pyreUpdateTimer += Time.deltaTime;
            }

            if (_pyreUpdateTimer >= 10f)
            {
                _pyreUpdateTimer = 0f;
                _targetPyre = null;
                if (hasPyreFuel && BotInteraction.GetClosestUnlitPyre(BotPlayerMain, out var pyre))
                {
                    _targetPyre = pyre;
                }
            }

            if (_targetPyre != null && !_targetPyre.IsLit && hasPyreFuel)
            {
                _targetMovePos = _targetPyre.InteractionPoint;
                if (Helpers.IsDistTo(BotPlayerMain.transform.position, _targetPyre.InteractionPoint, 1.5f))
                {
                    _targetLookPos = _targetPyre.InteractionPoint;
                    _shouldShoot = false;
                    _shouldRun = false;
                    _targetPyre.LightUp();
                    BotInventory.ConsumePyreFuel(BotPlayerMain);
                    _targetPyre = null;
                    _pyreUpdateTimer = 0f;
                }
            }
        }
        else
        {
            _targetPyre = null;
        }

        // Looting
        if (ClosestLoot != null)
        {
            if (!_shouldRetreat && CurrentTarget == null && TargetRevive == null)
            {
                _lootIsSack = ClosestLoot is DroppedLoot { IsSack: true };
                _targetMovePos = _lootIsSack || _targetPyre == null || _targetPyre.IsLit
                    ? ClosestLoot.transform.position + Vector3.up
                    : _targetMovePos;
            }

            if (Helpers.IsDistTo(BotPlayerMain.transform.position, ClosestLoot.transform.position, 2f))
            {
                _targetLookPos = ClosestLoot.transform.position;
                _shouldShoot = false;
                _shouldInteract = true;
                _shouldRun = false;
            }
        }

        // Engage closest inactive boss corresponding to current wave
        if (!_shouldRetreat && _hasEverything && TargetRevive == null && InactiveBossPos.HasValue &&
            !BotGameManager.IsBossActive && !WavesController.instance.HaveToKillZombies &&
            WavesController.instance.HaveToKillBoss && !BotGameManager.HelicopterArrived)
        {
            _targetMovePos = InactiveBossPos.Value;
            _alwaysUseGun = true;

            var botHeadPos = BotVision.GetBotHeadPosition(BotPlayerMain);
            if (Helpers.IsDistTo(BotPlayerMain.transform.position, InactiveBossPos.Value, BotTargetting.TargetRange) &&
                BotVision.IsPosVisible(botHeadPos, InactiveBossPos.Value))
            {
                _targetLookPos = InactiveBossPos.Value;
                _shouldShoot = true;
                _shouldRun = false;
            }
        }

        // Engage active closest boss
        if (HasGun && !_shouldRetreat && TargetRevive == null && BotGameManager.IsBossActive &&
            !BotGameManager.HelicopterArrived)
        {
            _alwaysUseGun = true;
            if (BossTarget != null && !BotTargetting.IsZombieVisible(BotPlayerMain, BossTarget))
            {
                _targetMovePos = BossTarget.obj.transform.position;
            }
        }
        else
        {
            BossTarget = null;
        }

        // Engage current active wave zombies
        if (HasGun && !_shouldRetreat && TargetRevive == null && WavesController.instance.HaveToKillZombies &&
            !BotGameManager.HelicopterArrived)
        {
            _alwaysUseGun = true;
            if (WaveTarget != null && !BotTargetting.IsZombieVisible(BotPlayerMain, WaveTarget))
            {
                _targetMovePos = WaveTarget.obj.transform.position;
            }
        }
        else
        {
            WaveTarget = null;
        }

        // Throw nades at horde
        if (_throwableCooldown > 0f)
        {
            _throwableCooldown -= Time.deltaTime;
            _throwTime = 0f;
        }

        var distToHorde = Helpers.DistToSqr(BotPlayerMain.transform.position, ClosestHordePos);
        var hasThrowable = BotInventory.BotSlots.TryGetValue(BotPlayerMain, out var slots) && slots.ThrowableIdx >= 0 &&
                           BotInventory.IsEquipSlotAvailable(BotPlayerMain, EquipmentIndex.Misc(slots.ThrowableIdx));
        if (_throwableCooldown <= 0f && !_shouldRetreat && ClosestHordeCount >= 10 && hasThrowable &&
            distToHorde is >= 100f and <= 900f)
        {
            _shouldThrow = true;
            _shouldShoot = false;
            _targetLookPos = ClosestHordePos;
            _shouldRun = false;
        }

        // Door handling
        if (BotInteraction.GetClosestInteractableDoor(BotPlayerMain, out var closestDoor) &&
            _doorInteractCd <= 0 &&
            closestDoor.DoorState is DoorState.Closed or DoorState.Locked)
        {
            _targetLookPos = closestDoor.InteractionPoint;
            _shouldInteract = true;
            _shouldRun = false;
        }

        if (_doorInteractCd > 0)
        {
            _doorInteractCd -= Time.deltaTime;
        }

        if (_macroDoorTime > 0f && _macroDoorPos.HasValue)
        {
            _macroDoorTime -= Time.deltaTime;
            _targetLookPos = _macroDoorPos.Value;
            _shouldShoot = true;
            _shouldRun = false;
        }
        else
        {
            _macroDoorPos = null;
        }

        // GET TO THE CHOPPA! 🗣️🔥
        if (BotGameManager.HelicopterArrived && HasGun && TargetRevive == null)
        {
            var heliLanding = HelicopterLanding.Instance;
            _alwaysUseGun = true;
            var laptop = heliLanding.Helicopter?.Laptop;
            if (laptop != null)
            {
                var stateValue = Traverse.Create(heliLanding).Field<HelicopterState>("State").Value;
                var rescueStarted = stateValue > HelicopterState.RescueAvailable;
                var timer = heliLanding.GetTimeToDisplayOnHud();
                var shouldEnterHeli = rescueStarted && (!heliLanding.DuringWaveState || timer is <= 15f);
                var botNearHeli = BotGeneral.NearHeli(BotPlayerMain, heliLanding.Helicopter.transform.position);
                var outsideHeliPos = heliLanding.Helicopter.Ramp.transform.position -
                                     heliLanding.Helicopter.transform.forward * 10f;
                var insideHeliPos = heliLanding.Helicopter.Ramp.transform.position +
                                    heliLanding.Helicopter.transform.forward * 4f;

                if (shouldEnterHeli)
                {
                    _targetMovePos = insideHeliPos;
                    _shouldRun = true;
                    _moveNoMatterWhat = true;
                    _shouldRetreat = true;

                    var isReallyNearHeli = Helpers.IsDistTo(BotPlayerMain.transform.position,
                        heliLanding.Helicopter.transform.position, 7f);
                    if (isReallyNearHeli)
                    {
                        _shouldRun = false;
                        _shouldRetreat = false;
                    }
                }
                else if (rescueStarted)
                {
                    if (botNearHeli)
                    {
                        _shouldRun = false;
                    }

                    if (!_shouldRetreat && TargetRevive == null)
                    {
                        _targetMovePos = outsideHeliPos;
                    }
                }
                else
                {
                    var allPlayersNearHeli = BotGeneral.AllPlayersNearHeli(heliLanding.Helicopter.transform.position);
                    if (!_shouldRetreat && TargetRevive == null)
                    {
                        _targetMovePos = allPlayersNearHeli ? laptop.InteractionPoint : outsideHeliPos;
                        _moveNoMatterWhat = true;
                    }

                    if (Helpers.IsDistTo(BotPlayerMain.transform.position, laptop.InteractionPoint, 1.5f)
                        && allPlayersNearHeli)
                    {
                        _targetLookPos = laptop.InteractionPoint;
                        _shouldInteract = true;
                        _shouldRun = false;
                    }

                    if (botNearHeli)
                    {
                        _shouldRun = false;
                    }
                }
            }
        }

        // Retreat
        if (_shouldRetreat)
        {
            _shouldShoot = false;
            _shouldStrafe = false;
            _shouldRun = !_needHeal;
            _moveNoMatterWhat = true;

            if (!_targetMovePos.HasValue)
            {
                var botPos = BotPlayerMain.transform.position;
                var awayDir = (botPos - ClosestZombieInHordePos).normalized;
                _targetMovePos = botPos + awayDir * 100f;
            }
        }

        // Water avoidance
        if (AuxiliarMapObjects.instance != null)
        {
            var botPos = BotPlayerMain.transform.position;
            var waterY = AuxiliarMapObjects.instance.WaterY + 0.8f;
            var botY = botPos.y;
            if (botY > waterY)
            {
                if (_waterTimer <= 0f)
                {
                    _isInWater = false;
                }
                else
                {
                    _waterTimer -= Time.deltaTime;
                }
            }
            else if (!_isInWater)
            {
                _isInWater = true;
                _waterTimer = 2f;
            }

            if (_isInWater && !_lootIsSack && TargetRevive == null)
            {
                _targetMovePos = Pathfinding.GetClosestNode();
                _moveNoMatterWhat = true;
            }
        }

        // Get to random node position if there is nothing to do (this is a bug)
        if (!_targetMovePos.HasValue)
        {
            if (_randomPosTimer <= 0f)
            {
                _randomPosTimer = 10f;
                _randomPos = Pathfinding.GetRandomNode();
            }
            else
            {
                _randomPosTimer -= Time.deltaTime;
            }

            _randomPos ??= Pathfinding.GetRandomNode();
            _targetMovePos = _randomPos;
        }
        else
        {
            _randomPosTimer = 0f;
        }

        // We don't want to use Unity's LateUpdate because of early return paths in this update loop
        UpdateLate();
    }

    private void UpdateLate()
    {
        var backingUp = false;
        if (_backupTimer > 0f && _backupPos.HasValue && !_moveNoMatterWhat)
        {
            _backupTimer -= Time.deltaTime;
            _targetMovePos = _backupPos.Value;
            backingUp = true;
        }
        else
        {
            _backupPos = null;
        }

        if (_targetMovePos.HasValue)
        {
            Pathfinding.SetTarget(_targetMovePos.Value);
        }

        Pathfinding.Update();

        var resultingMoveVec = Pathfinding.GetNextMovePos();
        _strafeDirTimer -= Time.deltaTime;

        if (_strafeDirTimer <= 0f)
        {
            _strafeDirTimer = 1f;
            _currentStrafeDir = BotMovement.GetRandomStrafeDirection(BotPlayerMain);
        }

        if (_doorStuckTimer > 0f)
        {
            _strafeTime = _doorStuckTimer;
            _doorStuckTimer -= Time.deltaTime;
        }

        if (!backingUp && (_strafeTime > 0f || (_shouldStrafe && !_moveNoMatterWhat)))
        {
            if (_strafeTime > 0f)
                _strafeTime -= Time.deltaTime;

            resultingMoveVec = _currentStrafeDir;
        }

        var moveDir = resultingMoveVec - BotPlayerMain.transform.position;
        _vaultSpotTimer += Time.deltaTime;
        if (_vaultSpotTimer > 0.1f)
        {
            _vaultSpotTimer = 0f;
            _vaultSpotPos =
                BotInteraction.TryGetClosestVaultSpot(BotPlayerMain.transform.position, moveDir, out var vaultPos)
                    ? vaultPos
                    : null;
        }

        if (_vaultSpotPos.HasValue)
        {
            _targetLookPos = _vaultSpotPos.Value;
            _shouldJump = true;
        }

        BotInput.LookAtVec(BotPlayerMain, _targetLookPos ?? resultingMoveVec);
        BotInput.MoveToVec(BotPlayerMain, resultingMoveVec);
        UpdateBotEquip();
        UpdateBotInput();
    }

    private void UpdateBotInput()
    {
        // Bot shooting & item use
        var isHoldingMelee = BotInventory.IsHoldingMelee(BotPlayerMain) || BotPlayerMain.arms?.EquippedItem == null;
        var inMeleeState = BotPlayerMain.movement?.GetEffectiveState() == PlayerMovement.State.Melee;
        if (_shouldShoot && (BotInventory.IsHoldingGun(BotPlayerMain) || isHoldingMelee || inMeleeState))
        {
            if (isHoldingMelee || inMeleeState)
            {
                var fovThreshold = inMeleeState ? 120f : 60f;
                if (_targetLookPos.HasValue &&
                    BotVision.IsPosWithinFov(BotPlayerMain, _targetLookPos.Value, fovThreshold))
                {
                    var whichAttack = Random.Range(0f, 100f);
                    BotInput.AddKey(BotPlayerMain,
                        whichAttack <= 50f ? PlayerInputKey.KeyID.Shoot : PlayerInputKey.KeyID.Aim);
                }
            }
            else
            {
                BotInput.AddKey(BotPlayerMain, PlayerInputKey.KeyID.Aim);

                if (_targetLookPos.HasValue && BotVision.IsPosWithinFov(BotPlayerMain, _targetLookPos.Value, 15f))
                {
                    BotInput.AddKey(BotPlayerMain, PlayerInputKey.KeyID.Shoot);
                }
            }
        }
        else
        {
            if (BotPlayerMain.arms != null)
            {
                var selectedEq = BotPlayerMain.inventory?.GetEquipment(BotPlayerMain.arms.selectedItem);
                var selectedSubType = selectedEq?.GetDataBaseItem()?.GetSubType();
                if ((_needEat || _needDrink) && selectedSubType == DatabaseItem.SubType.Food ||
                    _needHeal && selectedSubType == DatabaseItem.SubType.Healing)
                {
                    _shouldRun = false;
                    BotInput.AddKey(BotPlayerMain, PlayerInputKey.KeyID.Shoot);
                }

                if (_shouldThrow && selectedSubType == DatabaseItem.SubType.Throwable &&
                    _targetLookPos.HasValue && BotVision.IsPosWithinFov(BotPlayerMain, _targetLookPos.Value, 5f))
                {
                    BotInput.AddKey(BotPlayerMain, PlayerInputKey.KeyID.Shoot);

                    _throwTime += Time.deltaTime;
                    if (_throwTime >= 0.5f)
                    {
                        _shouldThrow = false;
                        _throwableCooldown = Random.Range(10f, 30f);
                    }
                }
            }
        }

        // Bot running
        if (_needStaminaRegen && !_shouldRetreat)
        {
            _shouldRun = false;
            if (BotPlayerMain.staminaFast >= BotPlayerMain.staminaSlow * 0.9f)
            {
                _needStaminaRegen = false;
            }
        }
        else if (BotPlayerMain.staminaFast <= 0f)
        {
            _needStaminaRegen = true;
        }

        // Bot running
        if (_shouldRun)
        {
            BotInput.AddKey(BotPlayerMain, PlayerInputKey.KeyID.Run);
        }

        // Bot jumping
        if (_shouldJump || Pathfinding.ShouldJump())
        {
            BotInput.AddKey(BotPlayerMain, PlayerInputKey.KeyID.Jump);
        }

        // Bot rolling
        if (_shouldRoll)
        {
            BotInput.AddKey(BotPlayerMain, PlayerInputKey.KeyID.Roll);
        }

        // Bot reloading
        if (_shouldReload)
        {
            BotInput.AddKey(BotPlayerMain, PlayerInputKey.KeyID.Reload);
        }

        // Bot interaction
        if (_shouldInteract && _targetLookPos.HasValue &&
            BotVision.IsPosWithinFov(BotPlayerMain, _targetLookPos.Value, 30f))
        {
            BotInput.AddKey(BotPlayerMain, PlayerInputKey.KeyID.Interact);
        }
    }

    private void UpdateBotEquip()
    {
        if (BotPlayerMain.arms == null)
            return;

        BotInventory.BotSlots.TryGetValue(BotPlayerMain, out var slots);
        var bestIndex = EquipmentIndex.None;
        switch (CurrentTarget)
        {
            case var _ when _shouldThrow && slots.ThrowableIdx >= 0 &&
                            BotInventory.IsEquipSlotAvailable(BotPlayerMain, EquipmentIndex.Misc(slots.ThrowableIdx)):
                bestIndex = EquipmentIndex.Misc(slots.ThrowableIdx);
                break;

            case var _ when _needHeal && slots.HealIdx >= 0 &&
                            BotInventory.IsEquipSlotAvailable(BotPlayerMain, EquipmentIndex.Misc(slots.HealIdx)):
                bestIndex = EquipmentIndex.Misc(slots.HealIdx);
                break;

            case null when _needDrink && slots.DrinkIdx >= 0 &&
                           BotInventory.IsEquipSlotAvailable(BotPlayerMain, EquipmentIndex.Misc(slots.DrinkIdx)):
                bestIndex = EquipmentIndex.Misc(slots.DrinkIdx);
                break;

            case null when _needEat && slots.FoodIdx >= 0 &&
                           BotInventory.IsEquipSlotAvailable(BotPlayerMain, EquipmentIndex.Misc(slots.FoodIdx)):
                bestIndex = EquipmentIndex.Misc(slots.FoodIdx);
                break;

            case var _ when _hasMelee && CurrentTarget is { IsBoss: false }
                                      && ClosestHordeCount <= 2 && !_alwaysUseGun:
            {
                if (BotInventory.IsEquipSlotAvailable(BotPlayerMain, EquipmentIndex.Weapon(3)))
                    bestIndex = EquipmentIndex.Weapon(3);
                else if (BotInventory.IsEquipSlotAvailable(BotPlayerMain, EquipmentIndex.Weapon(0)))
                    bestIndex = EquipmentIndex.Weapon(0);
                else if (BotInventory.IsEquipSlotAvailable(BotPlayerMain, EquipmentIndex.Weapon(1)))
                    bestIndex = EquipmentIndex.Weapon(1);
                else if (BotInventory.IsEquipSlotAvailable(BotPlayerMain, EquipmentIndex.Weapon(2)))
                    bestIndex = EquipmentIndex.Weapon(2);
                break;
            }

            default:
            {
                if (BotInventory.IsEquipSlotAvailable(BotPlayerMain, EquipmentIndex.Weapon(0)))
                    bestIndex = EquipmentIndex.Weapon(0);
                else if (BotInventory.IsEquipSlotAvailable(BotPlayerMain, EquipmentIndex.Weapon(1)))
                    bestIndex = EquipmentIndex.Weapon(1);
                else if (BotInventory.IsEquipSlotAvailable(BotPlayerMain, EquipmentIndex.Weapon(2)))
                    bestIndex = EquipmentIndex.Weapon(2);
                else if (BotInventory.IsEquipSlotAvailable(BotPlayerMain, EquipmentIndex.Weapon(3)))
                    bestIndex = EquipmentIndex.Weapon(3);
                break;
            }
        }

        var selectedItem = BotPlayerMain.arms.selectedItem;
        if (!bestIndex.Exists || (selectedItem.SetType == bestIndex.SetType && selectedItem.Value == bestIndex.Value))
        {
            return;
        }

        PlayerInputKey.KeyID? key = null;
        if (bestIndex.SetType == EquipmentSetType.Weapon)
        {
            key = bestIndex.Value switch
            {
                0 => PlayerInputKey.KeyID.SelectPrimary1,
                1 => PlayerInputKey.KeyID.SelectPrimary2,
                2 => PlayerInputKey.KeyID.SelectPistol,
                3 => PlayerInputKey.KeyID.SelectMelee,
                _ => null
            };
        }

        if (key.HasValue)
        {
            BotInput.AddKey(BotPlayerMain, key.Value);
        }
        else
        {
            BotPlayerMain.arms.targetEquipment = bestIndex;
        }
    }

    private void UpdateBotControlled()
    {
        var traverse = Traverse.Create(BotPlayerMain);
        traverse.Method("ProcessHealth").GetValue();
        traverse.Method("ProcessStamina").GetValue();
        BotPlayerMain.interaction?.MyUpdate();
        BotPlayerMain.statusEffects?.MyUpdate();
        BotPlayerMain.arms?.UpdateArms();
        BotPlayerMain.movement?.GetGround(true);
        BotPlayerMain.movement?.UpdateMovement();
        traverse.Method("UpdatePinging").GetValue();
        BotPlayerMain.posSync?.SendUpdate();
        traverse.Method("ProcessHitStop").GetValue();
    }
}
