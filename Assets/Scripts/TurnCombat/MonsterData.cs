using UnityEngine;

[CreateAssetMenu(fileName = "MonsterData", menuName = "Scriptable Objects/TurnCombat/MonsterData")]
public class MonsterData : ScriptableObject
{
    #region Editor (Serialized)
    [SerializeField] private string monsterName;
    [SerializeField, TextArea(2, 4)] private string description;
    [SerializeField] private ElementType primaryType;
    [SerializeField] private ElementType secondaryType;
    [SerializeField] private bool hasSecondaryType;
    [SerializeField] private Sprite frontSprite;
    [SerializeField] private Sprite backSprite;
    [SerializeField] private BaseStats baseStats;
    [SerializeField] private LearnableMove[] learnableMoves;
    [SerializeField] private int baseExpYield = 64;
    [Header("Catch Settings")]
    [SerializeField, Range(1, 255)] private int catchRate = 45;
    [SerializeField] private float chatCatchBonusMax = 250f;
    [SerializeField] private float chatCatchBonusMin = -50f;
    [SerializeField, Range(1, 5)] private int requiredShakes = 3;

    [Header("Status Condition Catch Multipliers")]
    [SerializeField] private float sleepCatchMultiplier = 2.5f;
    [SerializeField] private float freezeCatchMultiplier = 2.5f;
    [SerializeField] private float paralysisCatchMultiplier = 1.5f;
    [SerializeField] private float poisonCatchMultiplier = 1.5f;
    [SerializeField] private float burnCatchMultiplier = 1.5f;

    [SerializeField, TextArea(2, 4)] private string personality;
    #endregion

    #region Public Functions
    public string MonsterName => monsterName;
    public string Description => description;
    public string Personality => personality;
    public ElementType PrimaryType => primaryType;
    public ElementType SecondaryType => secondaryType;
    public bool HasSecondaryType => hasSecondaryType;
    public Sprite FrontSprite => frontSprite;
    public Sprite BackSprite => backSprite;
    public BaseStats BaseStats => baseStats;
    public LearnableMove[] LearnableMoves => learnableMoves;
    public int BaseExpYield => baseExpYield;
    public int CatchRate => catchRate;
    public float ChatCatchBonusMax => chatCatchBonusMax;
    public float ChatCatchBonusMin => chatCatchBonusMin;
    public int RequiredShakes => requiredShakes;
    public float SleepCatchMultiplier => sleepCatchMultiplier;
    public float FreezeCatchMultiplier => freezeCatchMultiplier;
    public float ParalysisCatchMultiplier => paralysisCatchMultiplier;
    public float PoisonCatchMultiplier => poisonCatchMultiplier;
    public float BurnCatchMultiplier => burnCatchMultiplier;

    public int GetExpForLevel(int level)
    {
        return level * level * level;
    }
    #endregion
}

[System.Serializable]
public struct BaseStats
{
    public int hp;
    public int attack;
    public int defense;
    public int spAttack;
    public int spDefense;
    public int speed;
}

[System.Serializable]
public struct LearnableMove
{
    public MoveData move;
    public int levelRequired;
}
