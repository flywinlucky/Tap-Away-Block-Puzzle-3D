using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class AutoResolveCube : MonoBehaviour
{
    public Transform levelTarget;
    public Button playAutoLevelResolve_Button;
    public Button stopAutoLevelResolve_Button;

    [Tooltip("Intervalul de timp (în secunde) între mișcările fiecărui bloc.")]
    public float moveInterval = 1.0f;

    private bool _isResolving = false;

    void Start()
    {
        // Legăm butoanele la funcțiile corespunzătoare
        playAutoLevelResolve_Button.onClick.AddListener(StartAutoResolve);
        stopAutoLevelResolve_Button.onClick.AddListener(StopAutoResolve);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            StartAutoResolve();
        }
        if (Input.GetKeyDown(KeyCode.Z))
        {
            StopAutoResolve();
        }
    }

    /// <summary>
    /// Pornește procesul de rezolvare automată a cubului.
    /// </summary>
    public void StartAutoResolve()
    {
        if (!_isResolving)
        {
            StartCoroutine(ResolveCube());
        }
    }

    /// <summary>
    /// Oprește procesul de rezolvare automată.
    /// </summary>
    public void StopAutoResolve()
    {
        StopAllCoroutines();
        _isResolving = false;
    }

    private IEnumerator ResolveCube()
    {
        _isResolving = true;

        while (levelTarget.childCount > 0) // Continuăm până când nu mai există copii în levelTarget
        {
            for (int i = 0; i < levelTarget.childCount; i++)
            {
                Transform child = levelTarget.GetChild(i);
                Block block = child.GetComponent<Block>();

                if (block != null)
                {
                    // Simulăm un click pe bloc pentru a forța mișcarea
                    block.SendMessage("OnMouseUpAsButton", SendMessageOptions.DontRequireReceiver);
                }

                // Așteptăm intervalul specificat înainte de a trece la următorul bloc
                yield return new WaitForSeconds(moveInterval);
            }
        }

        _isResolving = false;
        Debug.Log("Auto-resolve stopped: no more blocks.");
    }
}
