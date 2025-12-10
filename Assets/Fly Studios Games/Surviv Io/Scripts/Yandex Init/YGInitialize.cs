using UnityEngine;
//using YG;

public class YGInitialize : MonoBehaviour
{
    private void Start()
    {
        //YG2.GameReadyAPI();
        Debug.Log("Game is ready.");

        ChangeLanguage("en");
    }

    public void ChangeLanguage(string prefixLanguage)
    {
        Debug.Log("INTER YG : Language changed to " + prefixLanguage);
        //YG2.SwitchLanguage(prefixLanguage);
    }
}