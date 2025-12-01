using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;

[RequireComponent(typeof(NavMeshAgent))]
public class ZombieAI : MonoBehaviour
{
    [Header("Setări Urmărire")]
    public float stopDistance = 1.5f;    // Distanța la care se oprește lângă țintă
    public float attackInterval = 1.0f;  // Cât de des atacă (secunde)

    [Header("Movement Variance")]
    public Vector2 speedRange = new Vector2(2.2f, 3.6f);
    public Vector2 accelRange = new Vector2(8f, 14f);
    public Vector2 angularSpeedRange = new Vector2(110f, 220f);
    public Vector2 pathUpdateIntervalRange = new Vector2(0.35f, 0.9f); // Throttled SetDestination

    [Header("Stumble / Lunge")]
    public float staggerChance = 0.08f;
    public float lungeChance = 0.05f;
    public Vector2 staggerDurationRange = new Vector2(0.4f, 1.0f);
    public Vector2 lungeDurationRange = new Vector2(0.25f, 0.45f);
    public float staggerSpeedMultiplier = 0.55f;
    public float lungeSpeedMultiplier = 1.75f;

    public Animator isSwiming;
    [Header("Swimming")]
    public float swimmingSpeed = 2.5f;

    [Header("Wave Scaling")]
    public float attackDamage = 10f; // set by ZombieManager per wave
    public int currentWave = 1;
    public DestroyableEntity _destroyable; // referință la componentul de health

    [Header("Melee Settings (Hands-like)")]
    // replaced multiple hands with a single hand transform + collider
    public Transform HandPosition;
    public CircleCollider2D handColider2D;
    public float attackCooldown = 0.35f;
    public float activeHitDuration = 0.12f;
    [Tooltip("Mică distanță de punch pe axa X pentru feedback.")]
    public float punchDistance = 0.15f;
    public float punchDuration = 0.1f;
    public LayerMask hitMask = ~0;
    public AudioClip meleeHitSound;

    private NavMeshAgent _agent;
    private Transform _player;
    private float _nextAttackTime;
    private Vector3 _targetPosition; // Poziția tactică primită de la Manager

    private float _nextPathUpdateTime;
    private float _pathInterval;
    private float _baseSpeed;
    private Coroutine _varianceRoutine;

    private bool _inWater = false;
    private float _prevSpeed; // speed before entering water

    private Tween _punchTween;
    private Coroutine _scanRoutine;
    private HashSet<PlayerHealth> _hitThisAttack = new HashSet<PlayerHealth>();
    private bool _meleeActive = false;
    private Vector3 _handInitialLocalPos;

    private void Awake()
    {
        // Cache DestroyableEntity early (Instantiate -> ApplyWaveStats happens before Start)
        _destroyable = GetComponent<DestroyableEntity>();
    }

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (_agent == null) _agent = gameObject.AddComponent<NavMeshAgent>();

        _agent.updateRotation = false;
        _agent.updateUpAxis = false;

        if (_destroyable == null) _destroyable = GetComponent<DestroyableEntity>();

        FindPlayer();
        if (_baseSpeed <= 0f) InitializeVariance();

        // cache hand initial local pos if present
        if (HandPosition != null)
            _handInitialLocalPos = HandPosition.localPosition;
    }

    public void InitializeVariance()
    {
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();

        _agent.speed = Random.Range(speedRange.x, speedRange.y);
        _agent.acceleration = Random.Range(accelRange.x, accelRange.y);
        _agent.angularSpeed = Random.Range(angularSpeedRange.x, angularSpeedRange.y);

        _baseSpeed = _agent.speed;
        // If currently in water keep swim speed
        if (_inWater) _agent.speed = swimmingSpeed;
        _pathInterval = Random.Range(pathUpdateIntervalRange.x, pathUpdateIntervalRange.y);
        _nextPathUpdateTime = Time.time + Random.Range(0f, _pathInterval); // Desync first update

        if (_varianceRoutine == null)
            _varianceRoutine = StartCoroutine(VarianceRoutine());
    }

    IEnumerator VarianceRoutine()
    {
        // Periodically trigger stagger or lunge, then restore speed
        while (true)
        {
            // Skip speed variance while swimming
            if (!_inWater)
            {
                float roll = Random.value;
                if (roll < staggerChance)
                {
                    float dur = Random.Range(staggerDurationRange.x, staggerDurationRange.y);
                    _agent.speed = _baseSpeed * staggerSpeedMultiplier;
                    yield return new WaitForSeconds(dur);
                    if (!_inWater) _agent.speed = _baseSpeed;
                }
                else if (roll < staggerChance + lungeChance)
                {
                    float dur = Random.Range(lungeDurationRange.x, lungeDurationRange.y);
                    _agent.speed = _baseSpeed * lungeSpeedMultiplier;
                    yield return new WaitForSeconds(dur);
                    if (!_inWater) _agent.speed = _baseSpeed;
                }
            }
            yield return new WaitForSeconds(Random.Range(0.6f, 1.4f));
        }
    }

    void Update()
    {
        if (_player == null)
        {
            FindPlayer();
            return;
        }

        // Ensure agent exists and is valid on navmesh before using it
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();
        if (_agent == null || !_agent.isOnNavMesh || !_agent.isActiveAndEnabled)
        {
            // Agent not ready — skip movement/attack logic this frame to avoid errors
            return;
        }

        // 1. Rotație Vizuală spre Player (Face Target)
        // Chiar dacă merge spre un punct lateral, vrem să se uite la jucător
        Vector2 dir = _player.position - transform.position;
        if (dir.sqrMagnitude > 0.01f)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, ang);
        }

        // 2. Verificare Distanță pentru Atac
        // Calculăm distanța reală până la jucător, nu până la punctul tactic
        float distToPlayer = Vector3.Distance(transform.position, _player.position);

        if (distToPlayer <= stopDistance)
        {
            // Dacă e foarte aproape de player, se oprește și atacă
            if (!_agent.isStopped) _agent.isStopped = true;

            // compute simple local sign to punch toward player
            float localX = transform.InverseTransformPoint(_player.position).x;
            bool flipped = localX < 0f;
            MeleeAttack(flipped);
        }
        else
        {
            // Altfel, merge spre ținta tactică setată de Manager (încercuire)
            if (_agent.isStopped) _agent.isStopped = false;

            // Throttle path updates (reduces CPU + breaks robotic sync)
            if (Time.time >= _nextPathUpdateTime)
            {
                _agent.SetDestination(_targetPosition);
                // Slight randomization each cycle
                _pathInterval = Random.Range(pathUpdateIntervalRange.x, pathUpdateIntervalRange.y);
                _nextPathUpdateTime = Time.time + _pathInterval;
            }
        }
    }

    // Această funcție este apelată constant de ZombieManager pentru a actualiza poziția de încercuire
    public void SetTacticalTarget(Vector3 pos)
    {
        // Lazily ensure we have an agent reference
        if (_agent == null) _agent = GetComponent<NavMeshAgent>();

        // Guard: only operate if agent exists, active and placed on NavMesh
        if (_agent == null || !_agent.isOnNavMesh || !_agent.isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        _targetPosition = pos;

        // Only call SetDestination if the new target differs sufficiently to justify path recalculation
        if (!_agent.isStopped)
        {
            Vector3 currentDest = _agent.hasPath ? _agent.destination : _agent.transform.position;
            if ((currentDest - pos).sqrMagnitude > 4f) // threshold to avoid thrash
            {
                _agent.SetDestination(_targetPosition);
            }
        }
    }

    void FindPlayer()
    {
        var p = FindObjectOfType<Player>();
        if (p != null)
        {
            _player = p.transform;
            // Ca fallback, dacă managerul nu a setat încă nimic, mergem direct la player
            _targetPosition = _player.position; 
        }
    }

    public void ApplyWaveStats(int waveNumber)
    {
        currentWave = waveNumber;
        attackDamage = waveNumber*10/1;
        if (_destroyable == null)
            _destroyable = GetComponent<DestroyableEntity>();
        if (_destroyable != null)
        {
            _destroyable.health = 20 * currentWave;
        }
        else
        {
            Debug.LogWarning("[ZombieAI] DestroyableEntity missing on " + name);
        }
    }

    // NEW: Melee attack using single HandPosition animation and area check via handColider2D
    private void MeleeAttack(bool flipped)
    {
        if (Time.time < _nextAttackTime) return;
        _nextAttackTime = Time.time + attackCooldown;

        _meleeActive = true;
        _hitThisAttack.Clear();

        // Kill any previous punch tween
        if (_punchTween != null && _punchTween.IsActive())
            _punchTween.Kill();

        float dir = flipped ? -1f : 1f;

        // Build sequence: push out then back for hands (or fallback to whole transform)
        Sequence seq = DOTween.Sequence();

        if (HandPosition != null)
        {
            Vector3 start = _handInitialLocalPos;
            Vector3 target = start + new Vector3(dir * punchDistance, 0f, 0f);
            // push out then back
            seq.Append(HandPosition.DOLocalMove(target, punchDuration * 0.5f).SetEase(Ease.OutQuad));
            seq.Append(HandPosition.DOLocalMove(start, punchDuration * 0.5f).SetEase(Ease.InQuad));
        }
        else
        {
            // fallback to whole transform
            Vector3 startPos = transform.localPosition;
            Vector3 punchTarget = startPos + new Vector3(dir * punchDistance, 0f, 0f);
            seq.Append(transform.DOLocalMove(punchTarget, punchDuration * 0.5f).SetEase(Ease.OutQuad));
            seq.Append(transform.DOLocalMove(startPos, punchDuration * 0.5f).SetEase(Ease.InQuad));
        }

        _punchTween = seq;

        // start scanning only using handColider2D area
        if (_scanRoutine != null) StopCoroutine(_scanRoutine);
        _scanRoutine = StartCoroutine(ScanDuringActiveWindow());

        // End attack window after duration
        Invoke(nameof(EndMeleeWindow), activeHitDuration);
    }

    private IEnumerator ScanDuringActiveWindow()
    {
        float endTime = Time.time + activeHitDuration;
        while (_meleeActive && Time.time < endTime)
        {
            PerformAreaScan(); // uses only handColider2D
            yield return null;
        }
    }

    // simplified: damage comes only from what overlaps the configured handColider2D
    private void PerformAreaScan()
    {
        if (handColider2D == null) return;

        Vector3 center = handColider2D.transform.position;
        float radius = handColider2D.radius * Mathf.Abs(handColider2D.transform.lossyScale.x);
        if (radius <= 0f) radius = 0.2f;

        Collider2D[] cols = Physics2D.OverlapCircleAll(center, radius, hitMask);
        if (cols == null || cols.Length == 0) return;

        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            // avoid hitting self collider
            if (c == handColider2D) continue;

            PlayerHealth ph = c.GetComponentInParent<PlayerHealth>();
            if (ph == null) ph = c.GetComponentInChildren<PlayerHealth>();

            if (ph != null)
            {
                if (_hitThisAttack.Contains(ph)) continue;
                _hitThisAttack.Add(ph);
                ph.TakeDamage(attackDamage);
                PlayMeleeHitSound();
            }
        }
    }

    private void EndMeleeWindow()
    {
        _meleeActive = false;
        if (_scanRoutine != null)
        {
            StopCoroutine(_scanRoutine);
            _scanRoutine = null;
        }
        // restore hand position exactly
        if (HandPosition != null) HandPosition.localPosition = _handInitialLocalPos;
    }

    private void PlayMeleeHitSound()
    {
        if (meleeHitSound == null) return;
        if (AudioManager.Instance != null)
            AudioManager.Instance.Play2DSound(meleeHitSound);
        else
            AudioSource.PlayClipAtPoint(meleeHitSound, Vector3.zero);
    }

    private void EnterWater()
    {
        if (_inWater) return;
        _inWater = true;
        _prevSpeed = _agent != null ? _agent.speed : swimmingSpeed;
        if (_agent != null) _agent.speed = swimmingSpeed;
        if (isSwiming != null) isSwiming.SetBool("isSwiming", true);
    }

    private void ExitWater()
    {
        if (!_inWater) return;
        _inWater = false;
        if (_agent != null) _agent.speed = _prevSpeed > 0f ? _prevSpeed : _baseSpeed;
        if (isSwiming != null) isSwiming.SetBool("isSwiming", false);
    }

    // Când zombiul este distrus (moare), anunțăm Managerul să îl scoată din listă
    void OnDestroy()
    {
        if (ZombieManager.Instance != null)
        {
            ZombieManager.Instance.UnregisterZombie(this);
        }
    }

    // Desenăm linii de debug în editor pentru a vedea unde vrea să meargă
    void OnDrawGizmos()
    {
        // Raza de atac
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
        
        // Linia verde arată unde îi spune Managerul să meargă (punctul de pe cerc)
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, _targetPosition);
        Gizmos.DrawSphere(_targetPosition, 0.2f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other != null && other.CompareTag("Water")) EnterWater();
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other != null && other.CompareTag("Water")) ExitWater();
    }

    // Also support staying in the trigger (continuous contact)
    // REMOVED: calls to TryContactAttack — melee handled via handColider2D during MeleeAttack
    private void OnTriggerStay2D(Collider2D other)
    {
        if (other == null) return;
        if (other.CompareTag("Water")) { ExitWater(); return; }
        // no direct contact damage here — damage is applied from PerformAreaScan using handColider2D
    }

    private void OnTriggerStay(Collider other)
    {
        if (other == null) return;
        if (other.CompareTag("Water")) { ExitWater(); return; }
        // no direct contact damage here — damage is applied from PerformAreaScan using handColider2D
    }
}