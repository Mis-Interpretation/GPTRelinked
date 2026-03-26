using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_InputField))]
public class UIInputField : MonoBehaviour
{
    [SerializeField] private KeyCode sendKey = KeyCode.Return;

    [SerializeField] private LLMBrain brain;
    
    private TMP_InputField _inputField;
    void Awake()
    {
        _inputField = GetComponent<TMP_InputField>();
    }

    void OnEnable()
    {
        ASRService.EventASRCompleted += HandleASRCompleted;
    }

    void OnDisable()
    {
        ASRService.EventASRCompleted -= HandleASRCompleted;
    }

    void HandleASRCompleted(string result)
    {
        _inputField.text = result;
    }

    void Update()
    {
        if (Input.GetKeyDown(sendKey))
        {
            brain.HandleInput(_inputField.text);
        }
    }
}
