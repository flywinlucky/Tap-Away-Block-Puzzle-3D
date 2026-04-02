// Block.cs
using UnityEngine;
using System;
using System.Collections;

public class Block : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("Durata animației de dispariție (scale out) în secunde.")]
    [HideInInspector]
    public float dissolveDuration = 0.2f;

    private MoveDirection _moveDirection;
    private LevelManager _levelManager;
    private bool _isMoving = false;
    private BoxCollider _collider;
    private float _gridUnitSize;
    private bool _isShaking = false;

    [HideInInspector]
    public bool _isInteractible;

    [Header("Default Data")]
    public ShopSkinData defaultSkin; // Citim datele implicite din ShopSkinData
    [HideInInspector]
    public Color arrowCollor;

    public MeshRenderer[] cubeArows;

    // NOU: Eveniment declanșat când blocul este activat
    public static event Action<Block> OnBlockActivated;

    // NOU: păstrează poziția pe grid pentru refacere la undo
    private Vector3Int _gridPosition;

    private void Awake()
    {
        _collider = GetComponent<BoxCollider>();
        _isInteractible = true;
    }

    public void Initialize(MoveDirection dir, LevelManager manager, float gridUnitSize, Vector3Int gridPosition)
    {
        _moveDirection = dir;
        _levelManager = manager;
        _gridUnitSize = gridUnitSize;
        _gridPosition = gridPosition;

        // Aplicăm skin-ul curent sau valorile implicite dacă nu există skin selectat
        if (_levelManager != null && _levelManager.shopManager != null && _levelManager.shopManager.selectedSkin != null)
        {
            ShopSkinData currentSkin = _levelManager.shopManager.selectedSkin;
            ApplySkin(currentSkin.material);
            arrowCollor = currentSkin.arrowColor; // Setăm culoarea săgeții din skin
        }
        else if (defaultSkin != null)
        {
            ApplySkin(defaultSkin.material); // Aplicăm materialul implicit din ShopSkinData
            arrowCollor = defaultSkin.arrowColor; // Setăm culoarea săgeții implicită din ShopSkinData
        }

        ApplyArrowColor(); // Aplicăm culoarea în shader
    }

    private void OnMouseUpAsButton()
    {
        if (_isMoving || !_isInteractible) return;

        // Declanșăm evenimentul când blocul este activat
        OnBlockActivated?.Invoke(this);

        // DACA suntem in Remove-mode, confirmam remove pentru acest block si iesim
        if (_levelManager != null && _levelManager.IsAwaitingRemove())
        {
            _levelManager.ConfirmRemoveAtBlock(this);
            return;
        }

        // Redăm sunetul de block (dacă există AudioManager legat în LevelManager)
        if (_levelManager != null && _levelManager.audioManager != null)
        {
            _levelManager.audioManager.PlayBlockClick();
        }

        Vector3 startPos = transform.position;
        Vector3 direction = transform.forward;
        Vector3 targetPosition = startPos;
        bool shouldBeDestroyed = false;

        // Calculate the furthest valid position in the grid
        while (true)
        {
            Vector3 nextPosition = targetPosition + direction * _gridUnitSize;
            if (Physics.Raycast(targetPosition, direction, out RaycastHit hit, _gridUnitSize))
            {
                // Stop at the obstacle
                break;
            }

            targetPosition = nextPosition;

            // Check if the next position is out of bounds (optional, based on your grid limits)
            if (Vector3.Distance(startPos, targetPosition) > 10f) // Example: limit to 10 grid units
            {
                shouldBeDestroyed = true;
                break;
            }
        }

        if (_levelManager != null)
        {
            _levelManager.RegisterMove(this, startPos, targetPosition, shouldBeDestroyed, _gridPosition, _moveDirection, transform.localRotation, transform.localScale);
        }

        if (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            _isMoving = true;
            if (shouldBeDestroyed)
            {
                _collider.enabled = false;
                _levelManager.OnBlockRemoved(this);
            }
            StartCoroutine(MoveWithDamping(targetPosition, shouldBeDestroyed));
        }
        else
        {
            StartCoroutine(ShakeScale());
        }

        // Check for available moves after the block finishes moving
        _levelManager.CheckForAvailableMoves();
    }

    // ▼▼▼ MODIFICARE CHEIE AICI ▼▼▼
    private IEnumerator MoveWithDamping(Vector3 targetPosition, bool shouldBeDestroyed)
    {
        Vector3 startPosition = transform.position;
        float duration = Vector3.Distance(startPosition, targetPosition) * 0.1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(startPosition, targetPosition, Mathf.SmoothStep(0, 1, elapsed / duration));
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPosition;

        // Dacă blocul trebuie distrus, pornim animația de dizolvare.
        if (shouldBeDestroyed)
        {
            // Așteptăm finalizarea noii animații, care se va ocupa și de distrugerea obiectului.
            yield return StartCoroutine(ScaleOutAndDestroy());
        }
        // Altfel, dacă doar lovește un alt bloc, facem animația de impact.
        else
        {
            yield return StartCoroutine(ImpactBounce());
            _isMoving = false;
        }
    }

    // ▼▼▼ FUNCȚIE NOUĂ ▼▼▼
    /// <summary>
    /// Animație lină de micșorare a scării până la zero, urmată de distrugerea obiectului.
    /// </summary>
    private IEnumerator ScaleOutAndDestroy()
    {
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = Vector3.zero;
        float elapsed = 0f;

        while (elapsed < dissolveDuration)
        {
            // Interpolăm scara de la cea originală la zero
            transform.localScale = Vector3.Lerp(originalScale, targetScale, elapsed / dissolveDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // La final, distrugem obiectul
        Destroy(gameObject);
    }

    // NOU: folosit de LevelManager pentru Smash (shop / powerup)
    public void Smash()
    {
        if (_isMoving) return;
        _collider.enabled = false;
        // Înregistrăm starea de distrugere pentru undo (dacă nu a fost deja înregistrată)
        if (_levelManager != null)
        {
            _levelManager.RegisterMove(this, transform.position, transform.position, true, _gridPosition, _moveDirection, transform.localRotation, transform.localScale);
            _levelManager.OnBlockRemoved(this);
        }
        StartCoroutine(ScaleOutAndDestroy());
    }

    // NOU: evidențiază un block ca hint (apelabil din LevelManager)
    public void FlashHint(float duration = 0.6f, float scaleFactor = 1.25f)
    {
        StartCoroutine(FlashHintCoroutine(duration, scaleFactor));
    }

    private IEnumerator FlashHintCoroutine(float duration, float scaleFactor)
    {
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * scaleFactor;
        float half = duration * 0.5f;
        float t = 0f;
        while (t < half)
        {
            transform.localScale = Vector3.Lerp(originalScale, targetScale, t / half);
            t += Time.deltaTime;
            yield return null;
        }
        t = 0f;
        while (t < half)
        {
            transform.localScale = Vector3.Lerp(targetScale, originalScale, t / half);
            t += Time.deltaTime;
            yield return null;
        }
        transform.localScale = originalScale;
    }

    public void ApplySkin(Material mat)
    {
        if (mat != null)
        {
            Renderer rend = GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material = new Material(mat); // Creăm o instanță nouă a materialului pentru a evita conflictele globale
            }
        }

        // Aplicăm culoarea săgeții
        ApplyArrowColor();
    }

    public void ApplyArrowColor()
    {
        if (cubeArows != null && cubeArows.Length > 0)
        {
            foreach (var arrowRenderer in cubeArows)
            {
                if (arrowRenderer != null)
                {
                    if (arrowRenderer.material == null)
                    {
                        Debug.LogWarning("Arrow material is missing. Creating a new material instance.");
                        arrowRenderer.material = new Material(Shader.Find("Custom/UnlitTransparentColor")); // Asigurăm că materialul există
                    }

                    arrowRenderer.material.SetColor("_Color", arrowCollor); // Setăm culoarea în shader-ul custom
                    arrowRenderer.material.SetFloat("_Alpha", 1f); // Asigurăm că alfa este complet vizibil
                }
            }
        }
    }

    // --- Restul scriptului rămâne neschimbat ---

    private IEnumerator ShakeScale()
    {
        if (_isShaking) yield break;
        _isShaking = true;
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * 1.15f;
        float duration = 0.1f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            transform.localScale = Vector3.Lerp(originalScale, targetScale, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < duration)
        {
            transform.localScale = Vector3.Lerp(targetScale, originalScale, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localScale = originalScale;
        _isShaking = false;
    }

    private IEnumerator ImpactBounce()
    {
        if (_isShaking) yield break;
        _isShaking = true;
        Vector3 originalPos = transform.position;
        Vector3 bouncePos = originalPos - transform.forward * 0.05f;
        float duration = 0.08f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(originalPos, bouncePos, Mathf.SmoothStep(0, 1, elapsed / duration));
            elapsed += Time.deltaTime;
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < duration)
        {
            transform.position = Vector3.Lerp(bouncePos, originalPos, Mathf.SmoothStep(0, 1, elapsed / duration));
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = originalPos;
        _isShaking = false;
    }

    private Vector3 GetWorldDirection()
    {
        switch (_moveDirection)
        {
            case MoveDirection.Forward: return Vector3.forward;
            case MoveDirection.Back: return Vector3.back;
            case MoveDirection.Up: return Vector3.up;
            case MoveDirection.Down: return Vector3.down;
            case MoveDirection.Left: return Vector3.left;
            case MoveDirection.Right: return Vector3.right;
        }
        return Vector3.zero;
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        Vector3 direction = transform.forward;
        RaycastHit hit;
        if (Physics.Raycast(transform.position, direction, out hit, 100f))
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, hit.point);
        }
        else
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, direction * 10f);
        }
    }
}