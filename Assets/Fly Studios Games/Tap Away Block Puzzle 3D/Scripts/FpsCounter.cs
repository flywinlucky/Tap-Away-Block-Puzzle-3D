using UnityEngine;
using UnityEngine.UI; // Important: Asigură-te că ai importat asta pentru componenta Text
using System.Collections;

/// <summary>
/// Calculează și afișează FPS-ul într-o componentă UI Text.
/// </summary>
public class FpsCounter : MonoBehaviour
{
    [Header("UI")]
    [SerializeField]
    [Tooltip("Componenta Text unde va fi afișat FPS-ul. Trage-o aici din Canvas.")]
    private Text fpsText;

    [Header("Settings")]
    [SerializeField]
    [Tooltip("Cât de des să actualizeze textul (în secunde). 0.5f = de 2 ori pe secundă.")]
    private float updateInterval = 0.5f;

    // Variabile interne pentru calcul
    private float accumulatedTime = 0; // Timpul acumulat de la ultimul update
    private int frameCount = 0; // Frame-urile numărate de la ultimul update
    private float currentFps; // FPS-ul calculat

    private void Start()
    {
        if (fpsText == null)
        {
            Debug.LogError("Componenta Text pentru FPS nu a fost setată în Inspector!", this);
            enabled = false; // Dezactivăm scriptul dacă textul lipsește
            return;
        }
    }

    private void Update()
    {
        // Acumulăm timpul și frame-urile
        accumulatedTime += Time.unscaledDeltaTime;
        frameCount++;

        // Verificăm dacă a trecut intervalul de update setat
        if (accumulatedTime >= updateInterval)
        {
            // Calculăm FPS-ul actual
            // frameCount / accumulatedTime = frame-uri pe secundă
            currentFps = frameCount / accumulatedTime;

            // Afișăm FPS-ul în componenta Text
            // "F0" înseamnă formatare ca număr întreg (fără zecimale)
            fpsText.text = "FPS: " + currentFps.ToString("F0");

            // Resetăm contoarele pentru următorul interval
            accumulatedTime = 0;
            frameCount = 0;
        }
    }
}
