using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PartyButtonUI : MonoBehaviour
{
    #region Editor (Serialized)
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI nameText;
    #endregion

    #region Public Functions
    public Button Button => button;

    public void Setup(string displayName, bool interactable)
    {
        nameText.text = displayName;
        button.interactable = interactable;
    }
    #endregion
}
