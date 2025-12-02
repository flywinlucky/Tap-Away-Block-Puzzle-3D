using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; // added

public class RestartGame : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

	// Restart the currently active scene
	public void RestartScene()
    {
		Scene current = SceneManager.GetActiveScene();
		SceneManager.LoadScene(current.name);
    }

}
