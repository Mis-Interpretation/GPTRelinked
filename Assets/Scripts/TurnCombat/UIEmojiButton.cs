using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class UIEmojiButton : MonoBehaviour
{
    [Tooltip("The input field where the emoji will be added.")]
    public TMP_InputField targetInputField;
    
    private Button _button;
    private string _emoji;

    private void Awake()
    {
        _button = GetComponent<Button>();
        
        // Find TMP text in children
        var tmpText = GetComponentInChildren<TMP_Text>();
        if (tmpText != null)
        {
            _emoji = tmpText.text;
        }
        else
        {
            Debug.LogWarning($"UIEmojiButton: No TMP_Text found in children of {gameObject.name}.");
        }

        _button.onClick.AddListener(OnButtonClicked);
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnButtonClicked);
        }
    }

    private void OnButtonClicked()
    {
        if (targetInputField != null && !string.IsNullOrEmpty(_emoji))
        {
            // Append the emoji to the end of the text
            targetInputField.text += _emoji;
            
            // Move caret to the end of the text
            targetInputField.caretPosition = targetInputField.text.Length;
            
            // Keep the input field focused so the user can continue typing
            targetInputField.ActivateInputField();
        }
        else if (targetInputField == null)
        {
            Debug.LogWarning("UIEmojiButton: Target InputField is not assigned.");
        }
    }
}
