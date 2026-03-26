using UnityEngine;

namespace eToile_example
{
    public class ExitApp : MonoBehaviour
    {
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Application.Quit();
                print("Quit app");
            }
        }
    }
}
