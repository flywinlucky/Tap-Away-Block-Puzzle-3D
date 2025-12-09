using UnityEngine;
using UnityEngine.UI;

namespace CubeEscape3D
{
    public class OpenClosePanel : MonoBehaviour
    {
        [Header("Propritie")]

        public GameObject[] UIpanel;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(OpenOrCloseUIPanels);
        }

        private void OpenOrCloseUIPanels()
        {
            foreach (GameObject panel in UIpanel)
            {
                panel.SetActive(!panel.activeSelf);
            }
        }
    }

}