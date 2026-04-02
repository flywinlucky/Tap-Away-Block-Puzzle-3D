using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BackgroundImageManager : MonoBehaviour
{
    public Sprite[] backgrounds; // Lista de sprite-uri pentru fundaluri
    public int changeEveryLovelCount = 5; // La câte nivele să schimbăm fundalul
    public Image backgroundImage; // Referință la componenta Image pentru fundal

    private int _currentLevel = 0; // Nivelul curent
    private Sprite _lastBackground; // Ultimul fundal folosit

    /// <summary>
    /// Apelată pentru a actualiza fundalul în funcție de nivel.
    /// </summary>
    public void UpdateBackground(int levelNumber)
    {
        _currentLevel = levelNumber;

        // Verificăm dacă este timpul să schimbăm fundalul
        if (_currentLevel % changeEveryLovelCount == 0)
        {
            Sprite newBackground = GetRandomBackground();
            if (newBackground != null && backgroundImage != null)
            {
                backgroundImage.sprite = newBackground;
                _lastBackground = newBackground;
            }
        }
    }

    /// <summary>
    /// Selectează aleatoriu un fundal din listă, evitând repetarea ultimului fundal.
    /// </summary>
    private Sprite GetRandomBackground()
    {
        if (backgrounds == null || backgrounds.Length == 0)
        {
            Debug.LogWarning("BackgroundImageManager: No backgrounds available.");
            return null;
        }

        List<Sprite> availableBackgrounds = new List<Sprite>(backgrounds);

        // Eliminăm ultimul fundal folosit pentru a evita repetarea
        if (_lastBackground != null)
        {
            availableBackgrounds.Remove(_lastBackground);
        }

        // Selectăm aleatoriu un fundal din lista rămasă
        if (availableBackgrounds.Count > 0)
        {
            int randomIndex = Random.Range(0, availableBackgrounds.Count);
            return availableBackgrounds[randomIndex];
        }

        return null;
    }
}