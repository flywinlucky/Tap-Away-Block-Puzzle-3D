using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class RemoverPowerSimpleStepTutorialHand : MonoBehaviour
{
    [Tooltip("Unique key to save the 'has been shown' state in PlayerPrefs.")]
    public string tutorialKey;
    public GameObject tutorialStepUI;

    private bool _hasBeenShown;

    public void ShowHand()
    {
        // Verificăm dacă tutorialul a fost deja afișat
        _hasBeenShown = PlayerPrefs.GetInt(tutorialKey, 0) == 1;

        if (_hasBeenShown)
        {
            tutorialStepUI.SetActive(false);
            return;
        }
        else
        {
            StartCoroutine(ActiveSimpleHandTutorialDelay());
        }
    }

    private IEnumerator ActiveSimpleHandTutorialDelay()
    {
        tutorialStepUI.SetActive(true);

        yield return new WaitForSeconds(4);
        OnContinueButtonClicked();

    }
    private void OnContinueButtonClicked()
    {
        // Salvăm starea în PlayerPrefs pentru a preveni afișarea repetată
        PlayerPrefs.SetInt(tutorialKey, 1);
        PlayerPrefs.Save();

        // Dezactivăm UI-ul tutorialului
        tutorialStepUI.SetActive(false);

        Debug.Log("Tutorial completed and hidden.");
    }
}