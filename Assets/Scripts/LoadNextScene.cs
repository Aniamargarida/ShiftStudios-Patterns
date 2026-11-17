using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadNextScene : MonoBehaviour
{
    public string sceneName;
    //Only player can trigger it
    public string requiredTag = "Player";
    //can only load once 
    bool loading;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(requiredTag)) return;

        //if its already loading then return
        if (loading) return;
        loading = true;

        if (string.IsNullOrEmpty(sceneName))
        {
            //load the next scene in the Build Settings list
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}

    
