using System.Collections;
using UnityEngine.UI;
using UnityEngine;

namespace CubeEscape3D
{
    // Class to define the text states based on slider values
    [System.Serializable]
    public class TextState
    {
        public string[] texts; // Array of possible text options
        public Color color; // Color of the text
        public float min; // Minimum value for the slider
    }

    public class EncouragingText : MonoBehaviour
    {
        public TextState[] states; // Array of text states based on slider values
        public Animator anim; // Reference to the animator component
        public Text label; // Reference to the text label component
        public float simpleTextTimeDuration; // Duration for displaying simple text
        public float superItemsTimeDuration; // Duration for displaying super item text

        public void ShowText(int fillAmount)
        {
            if (fillAmount <= states.Length)
            {
                StartCoroutine(Show(states[fillAmount]));

                IEnumerator Show(TextState state)
                {
                    label.color = state.color;
                    label.text = state.texts[Random.Range(0, state.texts.Length)];
                    anim.SetBool("Show", true);
                    yield return new WaitForSeconds(simpleTextTimeDuration);
                    anim.SetBool("Show", false);
                }
            }
        }
    }
}