using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class NotificationManager : MonoBehaviour
{
    public GameObject notificationPopUp;
    public Text notificationTextMesage;

	[Header("Fade Settings")]
	[Tooltip("Timp (s) pentru fade-in.")]
	public float fadeInTime = 0.25f;
	[Tooltip("Timp (s) pentru fade-out.")]
	public float fadeOutTime = 0.25f;

    private Coroutine _hideCoroutine;
	private CanvasGroup _canvasGroup;

    private void Awake()
    {
        // Asigurăm popup-ul dezactivat la start
        if (notificationPopUp != null)
            notificationPopUp.SetActive(false);

		// încercăm să obținem CanvasGroup; dacă lipsește, îl adăugăm
		if (notificationPopUp != null)
		{
			_canvasGroup = notificationPopUp.GetComponent<CanvasGroup>();
			if (_canvasGroup == null)
				_canvasGroup = notificationPopUp.AddComponent<CanvasGroup>();

			_canvasGroup.alpha = 0f;
			_canvasGroup.interactable = false;
			_canvasGroup.blocksRaycasts = false;
		}
    }

    /// <summary>
    /// Afișează o notificare pentru o durată specificată (seconds). Popup este activat automat și apoi dezactivat.
    /// </summary>
    public void ShowNotification(string message, float duration = 2f)
    {
        if (notificationPopUp == null || notificationTextMesage == null)
        {
            Debug.LogWarning("NotificationManager: popup or text not assigned.");
            return;
        }

        // Oprire coroutine anterioară dacă exista
        if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);

        notificationTextMesage.text = message;
        notificationPopUp.SetActive(true);

		// asigurăm CanvasGroup
		if (_canvasGroup == null)
			_canvasGroup = notificationPopUp.GetComponent<CanvasGroup>() ?? notificationPopUp.AddComponent<CanvasGroup>();

        _hideCoroutine = StartCoroutine(FadeInThenOut(duration));
    }

    /// <summary>
    /// Dezactivează imediat notificarea curentă (face fade-out rapid).
    /// </summary>
    public void HideNotification()
    {
        if (notificationPopUp == null) return;

        if (_hideCoroutine != null)
        {
            StopCoroutine(_hideCoroutine);
            _hideCoroutine = null;
        }

		// pornește fade-out rapid
		if (_canvasGroup == null) _canvasGroup = notificationPopUp.GetComponent<CanvasGroup>();
		if (_canvasGroup != null)
		{
			StartCoroutine(FadeOutAndDisable(fadeOutTime * 0.6f));
		}
		else
		{
			notificationPopUp.SetActive(false);
		}
    }

    private IEnumerator HideAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (notificationPopUp != null) notificationPopUp.SetActive(false);
        _hideCoroutine = null;
    }

	private IEnumerator FadeInThenOut(float totalDuration)
	{
		// Fade in
		float t = 0f;
		if (_canvasGroup == null) _canvasGroup = notificationPopUp.GetComponent<CanvasGroup>();
		while (t < fadeInTime)
		{
			t += Time.deltaTime;
			_canvasGroup.alpha = Mathf.Clamp01(t / fadeInTime);
			yield return null;
		}
		_canvasGroup.alpha = 1f;
		_canvasGroup.interactable = true;
		_canvasGroup.blocksRaycasts = true;

		// Stay time (protejat să fie >= 0)
		float stay = Mathf.Max(0f, totalDuration - fadeInTime - fadeOutTime);
		if (stay > 0f)
			yield return new WaitForSeconds(stay);

		// Fade out
		yield return FadeOutAndDisableCoroutine(fadeOutTime);
		_hideCoroutine = null;
	}

	private IEnumerator FadeOutAndDisableCoroutine(float outTime)
	{
		if (_canvasGroup == null) _canvasGroup = notificationPopUp.GetComponent<CanvasGroup>();
		_canvasGroup.interactable = false;
		_canvasGroup.blocksRaycasts = false;

		float startAlpha = _canvasGroup != null ? _canvasGroup.alpha : 1f;
		float t = 0f;
		while (t < outTime)
		{
			t += Time.deltaTime;
			if (_canvasGroup != null)
				_canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t / outTime);
			yield return null;
		}
		if (notificationPopUp != null) notificationPopUp.SetActive(false);
		if (_canvasGroup != null) _canvasGroup.alpha = 0f;
	}

	private IEnumerator FadeOutAndDisable(float outTime)
	{
		yield return FadeOutAndDisableCoroutine(outTime);
	}
}
