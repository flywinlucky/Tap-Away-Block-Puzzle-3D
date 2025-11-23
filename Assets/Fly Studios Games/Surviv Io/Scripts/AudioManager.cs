using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
	[Header("Audio Sources")]
	[Tooltip("Background music AudioSource (looping).")]
	public AudioSource musicSource;
	[Tooltip("AudioSource pentru sunetul de click al blocului (atașat direct cu clip).")]
	public AudioSource blockClickSource; // redenumit din sfxSource

	// Stări curente (cache)
	private bool _musicEnabled = true;
	private bool _soundEnabled = true;

	// Chei PlayerPrefs (dacă dorești să le folosești din AudioManager direct)
	public const string MusicPrefKey = "MusicEnabled";
	public const string SoundPrefKey = "SoundEnabled";

	[Header("Footstep SFX")]
	[Tooltip("Clipuri de pași (uscat).")]
	public List<AudioClip> footstepClips = new List<AudioClip>();
	[Tooltip("Clipuri de pași în apă.")]
	public List<AudioClip> waterFootstepClips = new List<AudioClip>();
	[Range(0f, 1f)] public float footstepVolume = 1f;
	private AudioSource _footstepSource;

	private void Start()
	{
		// Asigurăm o sursă pentru pași pe acest GameObject
		_footstepSource = GetComponent<AudioSource>();
		if (_footstepSource == null) _footstepSource = gameObject.AddComponent<AudioSource>();
		_footstepSource.playOnAwake = false;
		_footstepSource.loop = false;
		_footstepSource.spatialBlend = 0f;

		// Opțional: inițializăm din PlayerPrefs dacă nu setăm din exterior
		_musicEnabled = PlayerPrefs.GetInt(MusicPrefKey, 1) == 1;
		_soundEnabled = PlayerPrefs.GetInt(SoundPrefKey, 1) == 1;

		ApplyMusicState();
		ApplySoundState();
	}

	// Play / Stop pentru muzica de background (Play păstrează comportamentul de start)
	public void PlayMusic()
	{
		if (musicSource == null) return;
		if (!_musicEnabled) return;
		if (!musicSource.isPlaying)
		{
			// Dacă a fost pus pe pauză, UnPause; altfel Play
			if (musicSource.time > 0f && musicSource.clip != null)
			{
				musicSource.UnPause();
			}
			else
			{
				musicSource.Play();
			}
		}
	}

	public void StopMusic()
	{
		if (musicSource == null) return;
		if (musicSource.isPlaying)
		{
			musicSource.Stop(); // forțează oprirea și resetează poziția
		}
	}

	// Redă sunetul de block click folosind clip-ul atașat pe blockClickSource
	public void PlayBlockClick()
	{
		if (blockClickSource == null || !_soundEnabled) return;
		AudioClip clip = blockClickSource.clip;
		if (clip == null)
		{
			// fallback: încercăm Play() dacă nu este clip dar e configurat AudioSource
			if (!blockClickSource.isPlaying) blockClickSource.Play();
			return;
		}
		blockClickSource.PlayOneShot(clip);
	}

	// Redă un pas (uscat) folosind AudioSource-ul local și un clip aleator din listă.
	public void PlayFootstep()
	{
		if (!_soundEnabled) return;
		if (footstepClips == null || footstepClips.Count == 0) return;
		if (_footstepSource == null) _footstepSource = GetComponent<AudioSource>();
		if (_footstepSource == null) return;

		AudioClip clip = footstepClips[Random.Range(0, footstepClips.Count)];
		if (clip == null) return;

		_footstepSource.PlayOneShot(clip, footstepVolume);
	}

	// Redă un pas în apă.
	public void PlayWaterFootstep()
	{
		if (!_soundEnabled) return;
		if (waterFootstepClips == null || waterFootstepClips.Count == 0) return;
		if (_footstepSource == null) _footstepSource = GetComponent<AudioSource>();
		if (_footstepSource == null) return;

		AudioClip clip = waterFootstepClips[Random.Range(0, waterFootstepClips.Count)];
		if (clip == null) return;

		_footstepSource.PlayOneShot(clip, footstepVolume);
	}

	// Setări enable/disable apelate din SettingsManager
	public void SetMusicEnabled(bool enabled)
	{
		_musicEnabled = enabled;
		PlayerPrefs.SetInt(MusicPrefKey, enabled ? 1 : 0);
		PlayerPrefs.Save();
		ApplyMusicState();
	}

	public void SetSoundEnabled(bool enabled)
	{
		_soundEnabled = enabled;
		PlayerPrefs.SetInt(SoundPrefKey, enabled ? 1 : 0);
		PlayerPrefs.Save();
		ApplySoundState();
	}

	// NOU: Expuse direct pentru control explicit (opțional)
	public void PauseMusic()
	{
		if (musicSource == null) return;
		if (musicSource.isPlaying)
		{
			musicSource.Pause();
		}
	}

	public void ResumeMusic()
	{
		if (musicSource == null) return;
		// Resume doar dacă există clip și a fost pus pe pauză anterior
		if (!musicSource.isPlaying && musicSource.clip != null)
		{
			musicSource.UnPause();
		}
	}

	private void ApplyMusicState()
	{
		if (musicSource == null) return;

		// Dacă muzica este activată -> reluăm (UnPause) sau pornim dacă nu a fost pornită
		if (_musicEnabled)
		{
			musicSource.mute = false;
			// Dacă sursa are clip și este într-o poziție > 0, reluăm; altfel pornim normal
			if (musicSource.clip != null && musicSource.time > 0f)
			{
				musicSource.UnPause();
			}
			else
			{
				// Play va porni de la început dacă nu a fost pornită anterior
				if (!musicSource.isPlaying)
					musicSource.Play();
			}
		}
		// Dacă muzica este dezactivată -> punem pe pauză (nu oprim complet, astfel păstrăm progresul)
		else
		{
			if (musicSource.isPlaying)
			{
				musicSource.Pause();
			}
			musicSource.mute = true;
		}
	}

	private void ApplySoundState()
	{
		if (blockClickSource != null)
			blockClickSource.mute = !_soundEnabled;

		// mute/unmute sursa de pași
		if (_footstepSource != null)
			_footstepSource.mute = !_soundEnabled;
	}
}
