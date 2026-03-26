public enum ElementType
{
    Normal,
    Fire,
    Water,
    Grass,
    Electric,
    Ice,
    Fighting,
    Poison,
    Ground,
    Flying,
    Psychic,
    Bug,
    Rock,
    Ghost,
    Dragon,
    Dark,
    Steel,
    Fairy
}

public enum MoveCategory
{
    Physical,
    Special,
    Status
}

public enum StatusCondition
{
    None,
    Poison,
    Burn,
    Paralysis,
    Sleep,
    Freeze,
    Vulnerable
}

public enum StatType
{
    Attack,
    Defense,
    SpAttack,
    SpDefense,
    Speed
}

public enum BattleState
{
    Start,
    PlayerAction,
    PlayerMove,
    EnemyMove,
    PlayerChat,
    PlayerSwitch,
    PlayerCatch,
    Busy,
    BattleOver
}
