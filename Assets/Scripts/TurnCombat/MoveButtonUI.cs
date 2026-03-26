using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MoveButtonUI : MonoBehaviour
{
    #region Editor (Serialized)
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI ppText;
    #endregion

    #region Public Functions
    public Button Button => button;

    public void Setup(string moveName, int currentPP, int maxPP, bool hasPP)
    {
        if (nameText == null || ppText == null || button == null)
        {
            Debug.LogError($"[MoveButtonUI] Missing references on {gameObject.name}. " +
                "Please assign nameText, ppText, and button in the prefab Inspector.", this);
            return;
        }
        nameText.text = moveName;
        ppText.text = $"{currentPP}/{maxPP}";
        button.interactable = hasPP;
    }
    #endregion
}
