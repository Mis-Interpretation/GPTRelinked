using UnityEngine;
using System;
using System.Linq;

[CreateAssetMenu(fileName = "SpellDefinition", menuName = "Scriptable Objects/SpellDefinition")]
public class SpellDefinition : ScriptableObject
{
    #region Editor (Serialized)
    [SerializeField] private SpellData[] spells;
    #endregion

    #region Public Functions
    public SpellData[] Spells => spells;

    public string[] SpellNames =>
        spells?.Select(s => s.spellName).Where(n => !string.IsNullOrEmpty(n)).ToArray();

    public SpellData? GetSpell(string spellName)
    {
        if (spells == null) return null;
        foreach (var spell in spells)
        {
            if (string.Equals(spell.spellName, spellName, StringComparison.Ordinal))
                return spell;
        }
        return null;
    }
    #endregion
}

[Serializable]
public struct SpellData
{
    public string spellName;
    public BaseMagic spellPrefab;
    public float cooldown;
    public int maxStash;
}
