using UnityEngine;

public struct DamageResult
{
    public int damage;
    public float effectiveness;
    public bool isCritical;
}

public static class DamageCalculator
{
    public static DamageResult Calculate(Monster attacker, Monster defender, MoveData move, TypeChart typeChart)
    {
        var result = new DamageResult();

        if (move.Category == MoveCategory.Status)
        {
            result.damage = 0;
            result.effectiveness = 1f;
            return result;
        }

        float attack, defense;
        if (move.Category == MoveCategory.Physical)
        {
            attack = attacker.GetStat(StatType.Attack);
            defense = defender.GetStat(StatType.Defense);
            if (attacker.Status == StatusCondition.Burn)
                attack *= 0.5f;
        }
        else
        {
            attack = attacker.GetStat(StatType.SpAttack);
            defense = defender.GetStat(StatType.SpDefense);
        }

        float baseDamage = ((2f * attacker.Level / 5f + 2f) * move.Power * (attack / defense)) / 50f + 2f;

        // Critical hit (1/16 chance)
        result.isCritical = Random.Range(0, 16) == 0;
        if (result.isCritical)
            baseDamage *= 1.5f;

        // STAB
        bool stab = move.MoveType == attacker.Data.PrimaryType ||
                     (attacker.Data.HasSecondaryType && move.MoveType == attacker.Data.SecondaryType);
        if (stab)
            baseDamage *= 1.5f;

        // Type effectiveness
        result.effectiveness = typeChart.GetEffectiveness(
            move.MoveType,
            defender.Data.PrimaryType,
            defender.Data.SecondaryType,
            defender.Data.HasSecondaryType);
        baseDamage *= result.effectiveness;

        // Vulnerable: +50% damage taken
        if (defender.Status == StatusCondition.Vulnerable)
            baseDamage *= 1.5f;

        // Random factor (85%-100%)
        float randomFactor = Random.Range(0.85f, 1f);
        baseDamage *= randomFactor;

        result.damage = Mathf.Max(1, Mathf.FloorToInt(baseDamage));
        if (result.effectiveness == 0f) result.damage = 0;

        return result;
    }

    public static bool AccuracyCheck(MoveData move)
    {
        if (move.Accuracy <= 0) return true;
        return Random.Range(1, 101) <= move.Accuracy;
    }
}
