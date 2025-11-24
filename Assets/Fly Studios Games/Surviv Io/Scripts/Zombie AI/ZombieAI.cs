using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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

    private void Awake()
    {
        // Cache DestroyableEntity early (Instantiate -> ApplyWaveStats happens before Start)
        _destroyable = GetComponent<DestroyableEntity>();
    }

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        
        // Adăugăm NavMeshAgent dacă lipsește (pentru siguranță)
        if (_agent == null) _agent = gameObject.AddComponent<NavMeshAgent>();

        // --- CONFIGURARE OBLIGATORIE PENTRU 2D ---
        // NavMeshAgent este nativ 3D, așa că trebuie să îi interzicem să rotească axele X/Y
        _agent.updateRotation = false;
        _agent.updateUpAxis = false;

        if (_destroyable == null) _destroyable = GetComponent<DestroyableEntity>();

        FindPlayer();
        // If manager did not call InitializeVariance yet (e.g. placed manually in scene)
        if (_baseSpeed <= 0f) InitializeVariance();
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
            TryAttack();
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
        _targetPosition = pos;
        // Optional immediate refresh if far off (avoid big drift)
        if (!_agent.isStopped && (pos - _agent.destination).sqrMagnitude > 4f)
        {
            _agent.SetDestination(_targetPosition);
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

    void TryAttack()
    {
        if (Time.time < _nextAttackTime) return;
        _nextAttackTime = Time.time + attackInterval;
        // Damage player if health component exists
        if (_player != null)
        {
            var ph = _player.GetComponentInChildren<PlayerHealth>();
            if (ph != null)
            {
                ph.TakeDamage(attackDamage);
            }
        }
        // ...existing debug...
        Debug.Log($"[ZombieAi] Attacked for {attackDamage} dmg (Wave). Time: {Time.time}");
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
    private void OnTriggerEnter(Collider other)
    {
        if (other != null && other.CompareTag("Water")) EnterWater();
    }
    private void OnTriggerExit(Collider other)
    {
        if (other != null && other.CompareTag("Water")) ExitWater();
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
}