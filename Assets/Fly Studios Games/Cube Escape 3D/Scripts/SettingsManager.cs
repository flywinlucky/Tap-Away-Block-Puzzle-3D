using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace CubeEscape3D
{
    public class SettingsManager : MonoBehaviour
    {
        [Header("Settings")]
        [Space]
        public AudioMixerGroup audioMixerGroup;
        public Toggle SoundToggle;
        public Toggle MusicToggle;

        [HideInInspector] public bool SoundEnabled; // Sound Effects toggle state
        [HideInInspector] public bool MusicEnabled; // Music toggle state

        private void Start()
        {
            LoadSettings();
        }

        public void ToggleSound()
        {
            SoundEnabled = SoundToggle.isOn;
            audioMixerGroup.audioMixer.SetFloat("sfx_volume", SoundEnabled ? 0 : -80); // Set SFX volume
            PlayerPrefs.SetInt("SoundFXToggle", SoundEnabled ? 1 : 0);
        }

        public void ToggleMusic()
        {
            MusicEnabled = MusicToggle.isOn;
            audioMixerGroup.audioMixer.SetFloat("music_volume", MusicEnabled ? 0 : -80); // Set music volume
            PlayerPrefs.SetInt("MusicToggle", MusicEnabled ? 1 : 0);
        }

        private void LoadSettings()
        {
            if (PlayerPrefs.HasKey("SoundFXToggle"))
            {
                int soundFXValue = PlayerPrefs.GetInt("SoundFXToggle");
                SoundEnabled = soundFXValue == 1;
                audioMixerGroup.audioMixer.SetFloat("sfx_volume", SoundEnabled ? 0 : -80); // Set SFX volume
                SoundToggle.isOn = SoundEnabled;
            }

            if (PlayerPrefs.HasKey("MusicToggle"))
            {
                int musicValue = PlayerPrefs.GetInt("MusicToggle");
                MusicEnabled = musicValue == 1;
                audioMixerGroup.audioMixer.SetFloat("music_volume", MusicEnabled ? 0 : -80); // Set music volume
                MusicToggle.isOn = MusicEnabled;
            }
        }
    }
}
