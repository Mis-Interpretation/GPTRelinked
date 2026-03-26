using UnityEngine;

[CreateAssetMenu(fileName = "MoveData", menuName = "Scriptable Objects/TurnCombat/MoveData")]
public class MoveData : ScriptableObject
{
    #region Editor (Serialized)
    [SerializeField] private string moveName;
    [SerializeField] private ElementType moveType;
    [SerializeField] private MoveCategory category;
    [SerializeField] private int power;
    [SerializeField, Range(0, 100)] private int accuracy = 100;
    [SerializeField] private int maxPP = 10;
    [SerializeField] private int priority;
    [SerializeField] private StatusCondition statusEffect;
    [SerializeField, Range(0, 100)] private int statusChance;
    [SerializeField] private StatStageChange[] statStageChanges;
    [SerializeField] private bool targetsUser;
    #endregion

    #region Public Functions
    public string MoveName => moveName;
    public ElementType MoveType => moveType;
    public MoveCategory Category => category;
    public int Power => power;
    public int Accuracy => accuracy;
    public int MaxPP => maxPP;
    public int Priority => priority;
    public StatusCondition StatusEffect => statusEffect;
    public int StatusChance => statusChance;
    public StatStageChange[] StatStageChanges => statStageChanges;
    public bool TargetsUser => targetsUser;
    #endregion
}

[System.Serializable]
public struct StatStageChange
{
    public StatType stat;
    public int stages;
}
