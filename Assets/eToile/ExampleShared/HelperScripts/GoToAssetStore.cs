using UnityEngine;

namespace eToile_example
{
    public class GoToAssetStore : MonoBehaviour
    {
        [SerializeField] string url = "https://www.assetstore.unity3d.com/#!/content/67183";

        public void GoToTheAssetStore()
        {
            Application.OpenURL(url);
        }
    }
}
