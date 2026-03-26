using System.Collections;
using UnityEngine;
using TMPro;

public class BattleDialogBox : MonoBehaviour
{
    #region Editor (Serialized)
    [SerializeField] private TextMeshProUGUI dialogText;
    [SerializeField] private float typeSpeed = 0.03f;
    #endregion

    #region Private Variables
    private Coroutine typingCoroutine;
    #endregion

    #region Public Functions
    public bool IsTyping { get; private set; }

    public void SetDialog(string text)
    {
        StopTyping();
        dialogText.text = text;
    }

    public Coroutine TypeDialog(string text)
    {
        StopTyping();
        typingCoroutine = StartCoroutine(TypeDialogCoroutine(text));
        return typingCoroutine;
    }

    public void Clear()
    {
        StopTyping();
        dialogText.text = "";
    }
    #endregion

    #region Private Functions
    private IEnumerator TypeDialogCoroutine(string text)
    {
        IsTyping = true;
        dialogText.text = "";
        foreach (char c in text)
        {
            dialogText.text += c;
            yield return new WaitForSeconds(typeSpeed);
        }
        IsTyping = false;
        typingCoroutine = null;
    }

    private void StopTyping()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        IsTyping = false;
    }
    #endregion
}
