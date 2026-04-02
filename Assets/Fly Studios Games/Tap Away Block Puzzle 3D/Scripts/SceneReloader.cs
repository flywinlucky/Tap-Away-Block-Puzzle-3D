using UnityEngine;
using UnityEngine.SceneManagement; // Este necesar să incluzi acest namespace pentru a lucra cu scenele

public class SceneReloader : MonoBehaviour
{
    /// <summary>
    /// Această metodă publică reîncarcă scena activă în prezent.
    /// Poate fi apelată de la un buton UI sau din alt script.
    /// </summary>
    public void RestartCurrentScene()
    {
        // Obține indexul de build al scenei curente.
        // Folosirea indexului este o metodă robustă și eficientă.
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;

        // Încarcă scena folosind indexul obținut, ceea ce duce la un restart.
        SceneManager.LoadScene(currentSceneIndex);
    }
}
