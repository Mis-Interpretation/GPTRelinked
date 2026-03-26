using UnityEngine;

[CreateAssetMenu(fileName = "SpellBook", menuName = "Scriptable Objects/SpellBook")]
public class SpellBook : ScriptableObject
{
    [SerializeField, TextArea(2,5)] private string extraContext;
    [SerializeField] private SpellEntry[] spellEntries;
    public SpellEntry[] SpellEntries => spellEntries;
    public string ExtraContext => extraContext;
}

[System.Serializable]
public struct SpellEntry
{
    public string triggerWord;
    [TextArea(2,5)] public string condition;
    [SpellName] public string spellName;
}
