using UnityEngine;
using System;

/// <summary>
/// A module used for augmenting, listening, and cleaning spells
/// </summary>
public class SpellModule : MonoBehaviour
{
    [SerializeField] private SpellBook spellBook;
    [SerializeField] private LLMBrain brain;

    private string contextSetnece = "请在action里面使用特征词。";

    public static event Action<string> EventKeywordDetected;
    public event Action<string> EventInstanceKeywordDetected;
    
    void OnEnable()
    {
        brain.EventChatCompletion += HandleChatCompletion;
    }

    void OnDisable()
    {
        brain.EventChatCompletion -= HandleChatCompletion;
    }

    private void Start()
    {
        AugmentSpellPrompt();
    }

    void AugmentSpellPrompt()
    {
        string result = $"{contextSetnece} {spellBook.ExtraContext}";
        foreach (SpellEntry spellEntry in spellBook.SpellEntries)
        {
            result += $"\n如果{spellEntry.condition}，请使用特征词{spellEntry.triggerWord}。";
        }
        brain.AddPrompt(result);
    }

    void HandleChatCompletion(string msg)
    {
        ParseSpellTriggerWords(msg);
    }

    void ParseSpellTriggerWords(string msg)
    {
        string actionText = msg;
        try
        {
            var parsed = JsonUtility.FromJson<LLMResponseFormat>(msg);
            if (!string.IsNullOrEmpty(parsed.action))
                actionText = parsed.action;
        }
        catch { }

        foreach (var spellEntry in spellBook.SpellEntries)
        {
            if (actionText.Contains(spellEntry.triggerWord))
            {
                Debug.Log("Spell Detected: " + spellEntry.triggerWord);
                EventKeywordDetected?.Invoke(spellEntry.triggerWord);
                EventInstanceKeywordDetected?.Invoke(spellEntry.triggerWord);
            }
        }
    }
}
