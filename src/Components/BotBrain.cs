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
    private Zombie _currentTarget;
    private Zombie _bossTarget;
    private Zombie _waveTarget;
    private bool _moveNoMatterWhat;
    private bool _heliIsHere;
    private bool _lootIsSack;
    private bool _alwaysUseGun;

    // Timers
    private float _deadTimer;
    private float _doorInteractCd;
    private float _backupTimer;
    private float _statDebugTimer;

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
    private InteractableObject _closestLoot;

    // Random pos
    private Vector3? _randomPos;
    private float _randomPosTimer;

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
    private bool _hasGun;
    private bool _hasMelee;
    private bool _hasFood;
    private bool _hasDrink;
    private bool _hasHeal;
    private bool _hasEverything;

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
        _needEat = false;
        _needDrink = false;
        _needHeal = false;
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
                if (_hasGun && BotInteraction.GetClosestDestroyableDoor(BotPlayerMain, out var doorHitPos))
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
                        if (effectPercentage > 0.5f)
                            continue;

                        _needDrink = true;
                        break;
                    }
                    case StatusEffect.ID.Food:
                    {
                        var effectPercentage = effect.curValue / effect.maxValue;
                        if (effectPercentage > 0.5f)
                            continue;

                        _needEat = true;
                        break;
                    }
                }
            }
        }

        var maxHealthPercentage = BotPlayerMain.healthSlow / BotPlayerMain.MaxHealth;
        var healthPercentage = BotPlayerMain.healthFast / BotPlayerMain.MaxHealth;
        _needHeal = healthPercentage < 0.3f || maxHealthPercentage < 0.6f;
        BotInventory.CheckNeeds(BotPlayerMain, out _hasGun, out _hasMelee, out _hasFood, out _hasDrink, out _hasHeal);
        BotInventory.ManageInventory(BotPlayerMain, _needEat, _needDrink);
        _hasEverything = _hasGun && _hasFood && _hasDrink && _hasHeal;
        _shouldRetreat = _hasHeal && _needHeal && (_currentTarget != null || ClosestHordeCount > 0);

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
        if (!_shouldRetreat && BotTargetting.GetClosestAny(BotPlayerMain, out _currentTarget)
                            && _currentTarget.health.isAlive)
        {
            var isMelee = BotPlayerMain.arms?.selectedWeapon == 2 && ClosestHordeCount <= 2 && !_currentTarget.IsBoss;

            if ((isMelee && !_currentTarget.IsBoss) || _hasGun)
            {
                var bestTargetHitbox = BotTargetting.GetBestHitbox(BotPlayerMain, _currentTarget);
                _targetLookPos = bestTargetHitbox;

                if (isMelee)
                {
                    _shouldStrafe = false;
                    _targetMovePos = _currentTarget.obj.transform.position;
                    _shouldShoot = Helpers.IsDistTo(BotPlayerMain.transform.position,
                        _currentTarget.obj.transform.position, 2f);
                    _shouldRun = !Helpers.IsDistTo(BotPlayerMain.transform.position,
                        _currentTarget.obj.transform.position, 4f);
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
            _currentTarget = null;
        }

        // Reload
        if (BotInventory.IsHoldingGun(BotPlayerMain))
        {
            var curAmmo = BotInventory.GetCurAmmoCount(BotPlayerMain);
            var maxAmmo = BotInventory.GetMaxAmmoCount(BotPlayerMain);
            if ((_currentTarget == null && curAmmo < maxAmmo) || curAmmo == 0)
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
        if (!_shouldRetreat && _currentTarget == null && TargetRevive == null && !_heliIsHere &&
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
        if (BotInteraction.GetClosestLoot(BotPlayerMain, out _closestLoot, _hasGun, _hasFood, _hasDrink, _hasHeal))
        {
            if (!_shouldRetreat && _currentTarget == null && TargetRevive == null)
            {
                _lootIsSack = _closestLoot is DroppedLoot { IsSack: true };
                _targetMovePos = _lootIsSack || _targetPyre == null || _targetPyre.IsLit
                    ? _closestLoot.transform.position + Vector3.up
                    : _targetMovePos;
            }

            if (Helpers.IsDistTo(BotPlayerMain.transform.position, _closestLoot.transform.position, 2f))
            {
                _targetLookPos = _closestLoot.transform.position;
                _shouldShoot = false;
                _shouldInteract = true;
                _shouldRun = false;
            }
        }

        // Engage closest inactive boss corresponding to current wave
        if (!_shouldRetreat && _hasEverything && TargetRevive == null && !BotTargetting.IsABossActive() &&
            !WavesController.instance.HaveToKillZombies && WavesController.instance.HaveToKillBoss && !_heliIsHere)
        {
            var bossTier = WavesController.instance.CurrentlyEnabledBossTier;
            var bossType = BossfightController.instance.GetZombieTypeForTier(bossTier);
            if (BotTargetting.GetClosestInactiveBossForTier(BotPlayerMain, bossType, out var bossPos))
            {
                _targetMovePos = bossPos;
                _alwaysUseGun = true;

                if (Helpers.IsDistTo(BotPlayerMain.transform.position, (Vector3)bossPos, BotTargetting.TargetRange)
                    && BotTargetting.GetClosestBoss(BotPlayerMain, out var boss, true)
                    && boss.identity.type == bossType
                    && BotTargetting.IsZombieVisible(BotPlayerMain, boss))
                {
                    _targetLookPos = boss.obj.transform.position;
                    _shouldShoot = true;
                    _shouldRun = false;
                }
            }
        }

        // Engage active closest boss
        if (_hasGun && !_shouldRetreat && TargetRevive == null && BotTargetting.IsABossActive() && !_heliIsHere)
        {
            BotTargetting.GetClosestBoss(BotPlayerMain, out _bossTarget);
            _alwaysUseGun = true;
            if (_bossTarget != null && !BotTargetting.IsZombieVisible(BotPlayerMain, _bossTarget))
            {
                _targetMovePos = _bossTarget.obj.transform.position;
            }
        }
        else
        {
            _bossTarget = null;
        }

        // Engage current active wave zombies
        if (_hasGun && !_shouldRetreat && TargetRevive == null && WavesController.instance.HaveToKillZombies &&
            !_heliIsHere)
        {
            BotTargetting.GetClosestWaveZombie(BotPlayerMain, out _waveTarget);
            _alwaysUseGun = true;
            if (_waveTarget != null && !BotTargetting.IsZombieVisible(BotPlayerMain, _waveTarget))
            {
                _targetMovePos = _waveTarget.obj.transform.position;
            }
        }
        else
        {
            _waveTarget = null;
        }

        // Throw nades at horde
        if (_throwableCooldown > 0f)
        {
            _throwableCooldown -= Time.deltaTime;
            _throwTime = 0f;
        }

        var distToHorde = Helpers.DistToSqr(BotPlayerMain.transform.position, ClosestHordePos);
        if (_throwableCooldown <= 0f && !_shouldRetreat && ClosestHordeCount >= 10 &&
            BotInventory.IsEquipSlotAvailable(BotPlayerMain, 3) &&
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
        var heliLanding = HelicopterLanding.Instance;
        if (heliLanding != null && heliLanding.HelicopterSpawned && !heliLanding.HelicopterLeaving &&
            heliLanding.HelicopterIsLanded && _hasGun && TargetRevive == null)
        {
            _heliIsHere = true;
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
        else
        {
            _heliIsHere = false;
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
        if (BotInteraction.TryGetClosestVaultSpot(BotPlayerMain.transform.position, moveDir, out var vaultPos))
        {
            _targetLookPos = vaultPos;
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
        var isHoldingMelee = BotInventory.IsHoldingMelee(BotPlayerMain);
        if (_shouldShoot && (BotInventory.IsHoldingGun(BotPlayerMain) || isHoldingMelee))
        {
            if (!isHoldingMelee)
                BotInput.AddKey(BotPlayerMain, PlayerInputKey.KeyID.Aim);

            if (_targetLookPos.HasValue && BotVision.IsPosWithinFov(BotPlayerMain, _targetLookPos.Value, 15f))
            {
                BotInput.AddKey(BotPlayerMain, PlayerInputKey.KeyID.Shoot);
            }
        }
        else
        {
            if ((_needEat || _needDrink) && BotInventory.HasMatchingConsumable(BotPlayerMain, _needEat, _needDrink) &&
                BotPlayerMain.arms.selectedWeapon == 4 ||
                _needHeal && BotPlayerMain.arms.selectedWeapon == 6)
            {
                _shouldRun = false;
                BotInput.AddKey(BotPlayerMain, PlayerInputKey.KeyID.Shoot);
            }

            if (_shouldThrow && BotPlayerMain.arms.selectedWeapon == 3 &&
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

        var bestSlot = -1;
        switch (_currentTarget)
        {
            case var _ when _shouldThrow && BotInventory.IsEquipSlotAvailable(BotPlayerMain, 3):
                bestSlot = 3;
                break;

            case var _ when _needHeal && BotInventory.IsEquipSlotAvailable(BotPlayerMain, 6):
                bestSlot = 6;
                break;

            case null when (_needEat || _needDrink) && BotInventory.IsEquipSlotAvailable(BotPlayerMain, 4) &&
                           BotInventory.HasMatchingConsumable(BotPlayerMain, _needEat, _needDrink):
                bestSlot = 4;
                break;

            case var _ when _hasMelee && _currentTarget is { IsBoss: false }
                                      && ClosestHordeCount <= 2 && !_alwaysUseGun:
            {
                if (BotInventory.IsEquipSlotAvailable(BotPlayerMain, 2)) bestSlot = 2;
                else if (BotInventory.IsEquipSlotAvailable(BotPlayerMain, 0)) bestSlot = 0;
                else if (BotInventory.IsEquipSlotAvailable(BotPlayerMain, 1)) bestSlot = 1;
                break;
            }

            default:
            {
                if (BotInventory.IsEquipSlotAvailable(BotPlayerMain, 0)) bestSlot = 0;
                else if (BotInventory.IsEquipSlotAvailable(BotPlayerMain, 1)) bestSlot = 1;
                else if (BotInventory.IsEquipSlotAvailable(BotPlayerMain, 2)) bestSlot = 2;
                break;
            }
        }

        if (BotPlayerMain.arms.selectedWeapon == bestSlot)
            return;

        if (bestSlot == -1)
            return;

        var key = bestSlot switch
        {
            0 => PlayerInputKey.KeyID.SelectPrimary,
            1 => PlayerInputKey.KeyID.SelectSecondary,
            2 => PlayerInputKey.KeyID.SelectMelee,
            3 => PlayerInputKey.KeyID.SelectThrowable,
            4 => PlayerInputKey.KeyID.SelectConsumable,
            _ => PlayerInputKey.KeyID.SelectHealing // Slot is 6
        };
        BotInput.AddKey(BotPlayerMain, key);
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
        BotPlayerMain.posSync?.SendUpdate();
    }
}
