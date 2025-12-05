using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SettingsManager : MonoBehaviour
{
	[Header("UI References")]
	[Tooltip("Butonul pentru sound (poate fi Button sau doar Image).")]
	public Button soundButton;
	[Tooltip("Butonul pentru music (poate fi Button sau doar Image).")]
	public Button musicButton;
	public Button restartButton;
	public Button openSoundPanelButton;
	public GameObject settingsPanel;

	[Header("Sound Sprites")]
	public Sprite soundOnSprite;
	public Sprite soundOffSprite;

	[Header("Music Sprites")]
	public Sprite musicOnSprite;
	public Sprite musicOffSprite;

	[Header("Audio Integration")]
	[Tooltip("Referință la AudioManager din scenă (opțional).")]
	public AudioManager audioManager;

	// stările curente
	private bool _soundEnabled = true;
	private bool _musicEnabled = true;
	private string soundKey = "SoundEnabled";
	private string musicKey = "MusicEnabled";

	// NOU: iconițe pentru butonul de open/close settings
	[Header("Settings Button Icons")]
	[Tooltip("Iconița implicită pentru butonul de settings (open).")]
	public Sprite settingsIconSprite;
	[Tooltip("Iconița pentru butonul de settings când panelul este deschis (close).")]
	public Sprite closeIconSprite;

	// NOU: referință la Image din child-ul butonului (folosită pentru icon)
	[Tooltip("Image component din child-ul butonului openSoundPanelButton utilizată ca icon.")]
	public Image openSoundPanelIcon;

	private void Start()
	{
		// Încarcă stările din PlayerPrefs (implicit ON)
		_soundEnabled = PlayerPrefs.GetInt(soundKey, 1) == 1;
		_musicEnabled = PlayerPrefs.GetInt(musicKey, 1) == 1;

		// Asigură referințele la Image dacă butoane sunt folosite
		EnsureButtonImages();

		// Aplică sprite-urile inițiale
		ApplySoundSprite();
		ApplyMusicSprite();

		// --- Adăugăm listeneri la butoane (dacă sunt setate) ---
		if (soundButton != null)
		{
			// evităm adăugarea dublă
			soundButton.onClick.RemoveListener(ToggleSound);
			soundButton.onClick.AddListener(ToggleSound);
		}
		if (musicButton != null)
		{
			musicButton.onClick.RemoveListener(ToggleMusic);
			musicButton.onClick.AddListener(ToggleMusic);
		}

		// --- Aplicăm stările în AudioManager (dacă există) ---
		if (audioManager != null)
		{
			audioManager.SetSoundEnabled(_soundEnabled);
			audioManager.SetMusicEnabled(_musicEnabled);
		}

		// NOU: la start închidem settings panel
		if (settingsPanel != null)
			settingsPanel.SetActive(false);

		// NOU: legăm butonul pentru a deschide/închide panelul de setări
		if (openSoundPanelButton != null)
		{
			openSoundPanelButton.onClick.RemoveListener(ToggleSettingsPanel);
			openSoundPanelButton.onClick.AddListener(ToggleSettingsPanel);
		}

		if(restartButton)
		{
			restartButton.onClick.RemoveListener(RestartCurrentScene);
			restartButton.onClick.AddListener(RestartCurrentScene);
		}

		// NOU: setăm iconița inițială a butonului (settings) folosind Image copil prioritar
		if (openSoundPanelIcon == null && openSoundPanelButton != null)
		{
			// fallback: dacă nu avem Image copil detectat, încercăm să folosim image-ul butonului
			if (openSoundPanelButton.image != null)
				openSoundPanelIcon = openSoundPanelButton.image;
		}

		if (openSoundPanelIcon != null && settingsIconSprite != null)
		{
			openSoundPanelIcon.sprite = settingsIconSprite;
		}
	}

	private void EnsureButtonImages()
	{
		// Dacă soundButton există, ne asigurăm că are Image și îl asignăm butonului
		if (soundButton != null)
		{
			if (soundButton.image == null)
			{
				Image img = soundButton.GetComponent<Image>();
				if (img == null) img = soundButton.gameObject.AddComponent<Image>();
				soundButton.image = img;
			}
		}

		if (musicButton != null)
		{
			if (musicButton.image == null)
			{
				Image img = musicButton.GetComponent<Image>();
				if (img == null) img = musicButton.gameObject.AddComponent<Image>();
				musicButton.image = img;
			}
		}

		// NOU: asigurăm Image pentru openSoundPanelButton (buton) și detectăm Image copil pentru icon
		if (openSoundPanelButton != null)
		{
			// dacă butonul însăși nu are image, ne asigurăm că are (păstrăm compatibilitatea)
			if (openSoundPanelButton.image == null)
			{
				Image img = openSoundPanelButton.GetComponent<Image>();
				if (img == null) img = openSoundPanelButton.gameObject.AddComponent<Image>();
				openSoundPanelButton.image = img;
			}

			// Dacă nu s-a setat manual openSoundPanelIcon, încercăm să găsim un Image copil (exclude imaginea butonului)
			if (openSoundPanelIcon == null)
			{
				Image[] images = openSoundPanelButton.GetComponentsInChildren<Image>(true);
				foreach (var img in images)
				{
					if (img.gameObject != openSoundPanelButton.gameObject)
					{
						openSoundPanelIcon = img;
						break;
					}
				}
			}
		}
	}

	// Eliminăm listenerii la distrugere pentru siguranță
	private void OnDestroy()
	{
		if (soundButton != null) soundButton.onClick.RemoveListener(ToggleSound);
		if (musicButton != null) musicButton.onClick.RemoveListener(ToggleMusic);

		// NOU: scoatem și listenerul pentru openSoundPanelButton
		if (openSoundPanelButton != null) openSoundPanelButton.onClick.RemoveListener(ToggleSettingsPanel);
	}

	// Funcții publice pentru OnClick (legate din Inspector)
	public void ToggleSound()
	{
		_soundEnabled = !_soundEnabled;
		PlayerPrefs.SetInt(soundKey, _soundEnabled ? 1 : 0);
		PlayerPrefs.Save();
		ApplySoundSprite();

		// Aplicăm imediat în AudioManager (dacă e setat)
		if (audioManager != null)
		{
			audioManager.SetSoundEnabled(_soundEnabled);
		}
		// TODO: aici poți adăuga logică reală pentru a opri/porni sunetul global
	}

	public void ToggleMusic()
	{
		_musicEnabled = !_musicEnabled;
		PlayerPrefs.SetInt(musicKey, _musicEnabled ? 1 : 0);
		PlayerPrefs.Save();
		ApplyMusicSprite();

		// Aplicăm imediat în AudioManager (dacă e setat)
		if (audioManager != null)
		{
			audioManager.SetMusicEnabled(_musicEnabled);
		}
		// TODO: aici poți adăuga logică reală pentru a opri/porni muzica
	}

	// Helpers pentru schimbat sprite pe buton / image
	private void ApplySoundSprite()
	{
		if (soundButton != null && soundButton.image != null)
		{
			soundButton.image.sprite = _soundEnabled ? soundOnSprite : soundOffSprite;
		}
		else
		{
			// fallback: căutăm o Image cu tag "SoundImage" (opțional)
			Image img = TryFindImageByName("SoundImage");
			if (img != null) img.sprite = _soundEnabled ? soundOnSprite : soundOffSprite;
		}
	}

	private void ApplyMusicSprite()
	{
		if (musicButton != null && musicButton.image != null)
		{
			musicButton.image.sprite = _musicEnabled ? musicOnSprite : musicOffSprite;
		}
		else
		{
			Image img = TryFindImageByName("MusicImage");
			if (img != null) img.sprite = _musicEnabled ? musicOnSprite : musicOffSprite;
		}
	}

	// mic helper pentru fallback; returnează prima Image găsită cu numele dat
	private Image TryFindImageByName(string name)
	{
		GameObject go = GameObject.Find(name);
		if (go != null) return go.GetComponent<Image>();
		return null;
	}

	// NOU: Toggle pentru settings panel (lege-l la butonul openSoundPanelButton)
	public void ToggleSettingsPanel()
	{
		if (settingsPanel == null) return;
		bool willBeActive = !settingsPanel.activeSelf;
		settingsPanel.SetActive(willBeActive);

		// Actualizăm iconița butonului în funcție de stare — folosim Image copil dacă există
		Image imgToUse = openSoundPanelIcon != null ? openSoundPanelIcon : (openSoundPanelButton != null ? openSoundPanelButton.image : null);
		if (imgToUse != null)
		{
			if (willBeActive)
			{
				if (closeIconSprite != null) imgToUse.sprite = closeIconSprite;
			}
			else
			{
				if (settingsIconSprite != null) imgToUse.sprite = settingsIconSprite;
			}
		}
	}

	public void RestartCurrentScene()
	{
		Scene currentScene = SceneManager.GetActiveScene();
		SceneManager.LoadScene(currentScene.name);
	}
}
