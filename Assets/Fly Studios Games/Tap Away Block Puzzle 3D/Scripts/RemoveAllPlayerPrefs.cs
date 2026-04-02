using UnityEngine;

public class RemoveAllPlayerPrefs : MonoBehaviour
{
    public void RemoveAllPlayerPrefsData()
    {
        PlayerPrefs.DeleteAll();
    }
}