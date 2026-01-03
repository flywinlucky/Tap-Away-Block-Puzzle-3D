using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class FootstepSet
{
    public List<AudioClip> clips = new List<AudioClip>();
    [Header("Playback")]
    [Range(0f, 1f)] public float volumeMin = 0.8f;
    [Range(0f, 1f)] public float volumeMax = 1f;
    [Range(0.5f, 2f)] public float pitchMin = 0.95f;
    [Range(0.5f, 2f)] public float pitchMax = 1.05f;
}

[System.Serializable]
public class FootstepCategory
{
    [Tooltip("Denumirea categoriei (vizual).")]
    public string name = "Ground";
    [Tooltip("Tag-ul colliderului suprafeței (ex: Ground, Grass etc).")]
    public string tag = "Ground";
    [Tooltip("Setările și clipurile pentru această categorie.")]
    public FootstepSet set = new FootstepSet();
}

[RequireComponent(typeof(AudioSource))]
public class SoundFootsteps : MonoBehaviour
{
    [Header("Categorii (dinamic)")]
    [Tooltip("Adaugă (+) oricâte categorii vrei. Fiecare are un nume, tag și setări.")]
    public List<FootstepCategory> categories = new List<FootstepCategory>();

    [Tooltip("Indexul de fallback dacă nu se potrivește niciun tag. 0 este de obicei Ground.")]
    public int fallbackCategoryIndex = 0;

    [Header("Detectare suprafață")]
    public LayerMask surfaceMask = ~0;
    public float raycastLength = 1.5f;
    public Vector3 raycastOffset = new Vector3(0f, 0.1f, 0f);

    [Header("Intervale pași (global)")]
    public float stepIntervalMin = 0.3f;
    public float stepIntervalMax = 0.6f;
    public float referenceWalkSpeed = 16f;
    public float minMoveSpeed = 0.2f;

    private AudioSource audioSource;
    private float nextStepTime;
    private readonly Dictionary<string, int> tagToIndex = new Dictionary<string, int>();

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f; // 3D
        BuildTagIndex();
        EnsureDefaultsIfEmpty();
    }

    void OnValidate()
    {
        if (fallbackCategoryIndex < 0) fallbackCategoryIndex = 0;
        if (fallbackCategoryIndex >= categories.Count && categories.Count > 0)
            fallbackCategoryIndex = 0;
        BuildTagIndex();
    }

    // Call din PlayerControler.Update()
    public void UpdateFootsteps(bool isGrounded, Vector3 horizontalVelocity)
    {
        if (!isGrounded)
        {
            nextStepTime = Time.time;
            return;
        }

        float speed = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;
        if (speed < minMoveSpeed)
        {
            nextStepTime = Time.time;
            return;
        }

        float t = Mathf.Clamp01(referenceWalkSpeed > 0f ? speed / referenceWalkSpeed : 1f);
        float interval = Mathf.Lerp(stepIntervalMax, stepIntervalMin, t);

        if (Time.time >= nextStepTime)
        {
            PlayForCurrentSurface();
            nextStepTime = Time.time + interval;
        }
    }

    // API opțional pentru runtime: adăugare categorie
    public int AddCategory(string name, string tag)
    {
        var cat = new FootstepCategory { name = name, tag = tag };
        categories.Add(cat);
        BuildTagIndex();
        return categories.Count - 1;
    }

    private void PlayForCurrentSurface()
    {
        int index = DetectCategoryIndex();
        if (index < 0 || index >= categories.Count) return;

        var set = categories[index].set;
        if (set == null || set.clips == null || set.clips.Count == 0) return;

        var clip = set.clips[Random.Range(0, set.clips.Count)];
        audioSource.pitch = Random.Range(set.pitchMin, set.pitchMax);
        audioSource.PlayOneShot(clip, Random.Range(set.volumeMin, set.volumeMax));
    }

    private int DetectCategoryIndex()
    {
        Vector3 origin = transform.position + raycastOffset;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastLength, surfaceMask, QueryTriggerInteraction.Ignore))
        {
            string tag = hit.collider.tag;
            if (!string.IsNullOrEmpty(tag) && tagToIndex.TryGetValue(tag, out int idx))
                return idx;
        }
        return (categories.Count > 0) ? fallbackCategoryIndex : -1;
    }

    private void BuildTagIndex()
    {
        tagToIndex.Clear();
        for (int i = 0; i < categories.Count; i++)
        {
            string tag = categories[i]?.tag;
            if (!string.IsNullOrEmpty(tag) && !tagToIndex.ContainsKey(tag))
                tagToIndex[tag] = i;
        }
    }

    private void EnsureDefaultsIfEmpty()
    {
        if (categories.Count == 0)
        {
            categories.Add(new FootstepCategory { name = "Ground", tag = "Ground" });
            categories.Add(new FootstepCategory { name = "Grass", tag = "Grass" });
            categories.Add(new FootstepCategory { name = "DeepWater", tag = "DeepWater" });
            categories.Add(new FootstepCategory { name = "Sample", tag = "Sample" });
            fallbackCategoryIndex = 0;
            BuildTagIndex();
        }
    }
}
