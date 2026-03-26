using UnityEngine;

public struct CatchResult
{
    public bool success;
    public int shakeCount;
}

public static class CatchCalculator
{
    public static CatchResult TryCatch(Monster target)
    {
        float hpRatio = (float)target.CurrentHp / target.MaxHp;
        float catchRate = target.Data.CatchRate;

        // Lower HP = easier to catch
        float modifiedRate = (3f * target.MaxHp - 2f * target.CurrentHp) * catchRate / (3f * target.MaxHp);

        // Status bonus
        float statusBonus = target.Status switch
        {
            StatusCondition.Sleep => 2.5f,
            StatusCondition.Freeze => 2.5f,
            StatusCondition.Paralysis => 1.5f,
            StatusCondition.Poison => 1.5f,
            StatusCondition.Burn => 1.5f,
            _ => 1f
        };
        modifiedRate *= statusBonus;

        var result = new CatchResult();

        if (modifiedRate >= 255f)
        {
            result.success = true;
            result.shakeCount = 3;
            return result;
        }

        // Shake check probability
        float shakeProbability = modifiedRate / 255f;

        result.shakeCount = 0;
        for (int i = 0; i < 3; i++)
        {
            if (Random.value <= shakeProbability)
                result.shakeCount++;
            else
                break;
        }

        result.success = result.shakeCount >= 3;
        return result;
    }
}
