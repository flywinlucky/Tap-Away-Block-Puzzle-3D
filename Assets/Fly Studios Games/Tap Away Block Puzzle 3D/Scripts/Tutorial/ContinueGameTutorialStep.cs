using UnityEngine;
using UnityEngine.UI;

public class ContinueGameTutorialStep : MonoBehaviour
{
    [Tooltip("Unique key to save the 'has been shown' state in PlayerPrefs.")]
    public string tutorialKey;

    public Button buttonContinue;
    public GameObject tutorialStepUI;

    private bool _hasBeenShown;

    void Start()
    {
        // Verificăm dacă tutorialul a fost deja afișat
        _hasBeenShown = PlayerPrefs.GetInt(tutorialKey, 0) == 1;

        if (_hasBeenShown)
        {
            tutorialStepUI.SetActive(false);
            return;
        }

        // Legăm evenimentul de click pentru butonul Continue
        if (buttonContinue != null)
        {
            buttonContinue.onClick.RemoveAllListeners();
            buttonContinue.onClick.AddListener(OnContinueButtonClicked);
        }

        // Verificăm starea butonului Continue
        if (buttonContinue != null && buttonContinue.gameObject.activeSelf)
        {
            tutorialStepUI.SetActive(true);
        }
    }

    private void OnContinueButtonClicked()
    {
        // Salvăm starea în PlayerPrefs pentru a preveni afișarea repetată
        PlayerPrefs.SetInt(tutorialKey, 1);
        PlayerPrefs.Save();

        // Dezactivăm UI-ul tutorialului
        if (tutorialStepUI != null)
        {
            tutorialStepUI.SetActive(false);
        }

        Debug.Log("Tutorial completed and hidden.");
    }
}

