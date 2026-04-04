using System.Collections;
using UnityEngine;

/// <summary>
/// Handles move execution, status-effect processing, and stat-stage changes.
/// Pure C# class — iterator methods are started via StartCoroutine on the owning MonoBehaviour.
/// </summary>
public class BattleMoveExecutor
{
    private readonly BattleUI ui;
    private readonly TypeChart typeChart;

    public float LastCatchRateModifier { get; private set; }

    public BattleMoveExecutor(BattleUI ui, TypeChart typeChart)
    {
        this.ui = ui;
        this.typeChart = typeChart;
    }

    public IEnumerator PerformMove(Monster attacker, Monster defender,
                                   MoveSlot move, BattleHUD defenderHUD, bool isPlayer)
    {
        LastCatchRateModifier = 0f;

        if (attacker.IsFainted || move == null) yield break;

        // Paralysis
        if (attacker.Status == StatusCondition.Paralysis && Random.value < 0.25f)
        {
            yield return ui.DialogBox.TypeDialog($"{attacker.Data.MonsterName} is paralyzed and can't move!");
            yield return new WaitForSeconds(1f);
            yield break;
        }

        // Sleep
        if (attacker.Status == StatusCondition.Sleep)
        {
            attacker.IncrementStatusTurns();
            if (attacker.StatusTurns < 3 && Random.value > 0.33f)
            {
                yield return ui.DialogBox.TypeDialog($"{attacker.Data.MonsterName} is fast asleep...");
                yield return new WaitForSeconds(1f);
                yield break;
            }
            attacker.CureStatus();
            yield return ui.DialogBox.TypeDialog($"{attacker.Data.MonsterName} woke up!");
            yield return new WaitForSeconds(0.5f);
        }

        // Freeze
        if (attacker.Status == StatusCondition.Freeze)
        {
            if (Random.value > 0.2f)
            {
                yield return ui.DialogBox.TypeDialog($"{attacker.Data.MonsterName} is frozen solid!");
                yield return new WaitForSeconds(1f);
                yield break;
            }
            attacker.CureStatus();
            yield return ui.DialogBox.TypeDialog($"{attacker.Data.MonsterName} thawed out!");
            yield return new WaitForSeconds(0.5f);
        }

        move.UsePP();
        yield return ui.DialogBox.TypeDialog($"{attacker.Data.MonsterName} used {move.Data.MoveName}!");
        yield return new WaitForSeconds(0.5f);

        // Accuracy
        if (!DamageCalculator.AccuracyCheck(move.Data))
        {
            yield return ui.DialogBox.TypeDialog("But it missed!");
            yield return new WaitForSeconds(1f);
            yield break;
        }

        LastCatchRateModifier = move.Data.CatchRateModifier;
        if (LastCatchRateModifier != 0f)
            Debug.LogWarning($"[CatchBonus] {move.Data.MoveName} has catchRateModifier: {LastCatchRateModifier}");

        // Status-only moves
        if (move.Data.Category == MoveCategory.Status)
        {
            yield return ApplyMoveEffects(attacker, defender, move.Data, defenderHUD, isPlayer);
            yield break;
        }

        // Damage
        DamageResult result = DamageCalculator.Calculate(attacker, defender, move.Data, typeChart);
        defender.TakeDamage(result.damage);
        yield return defenderHUD.AnimateHP(defender.CurrentHp);

        if (result.isCritical)
        {
            yield return ui.DialogBox.TypeDialog("A critical hit!");
            yield return new WaitForSeconds(0.5f);
        }

        if (result.effectiveness > 1.5f)
        {
            yield return ui.DialogBox.TypeDialog("It's super effective!");
            yield return new WaitForSeconds(0.5f);
        }
        else if (result.effectiveness > 0f && result.effectiveness < 0.75f)
        {
            yield return ui.DialogBox.TypeDialog("It's not very effective...");
            yield return new WaitForSeconds(0.5f);
        }
        else if (result.effectiveness == 0f)
        {
            yield return ui.DialogBox.TypeDialog("It had no effect...");
            yield return new WaitForSeconds(0.5f);
        }

        // Secondary status
        if (move.Data.StatusEffect != StatusCondition.None && move.Data.StatusChance > 0)
        {
            if (Random.Range(0, 100) < move.Data.StatusChance)
                yield return ApplyMoveEffects(attacker, defender, move.Data, defenderHUD, isPlayer);
        }

        // Stat changes from damaging moves
        if (move.Data.StatStageChanges != null && move.Data.StatStageChanges.Length > 0)
            yield return ApplyStatChanges(attacker, defender, move.Data, isPlayer);

        if (defender.IsFainted)
        {
            yield return ui.DialogBox.TypeDialog($"{defender.Data.MonsterName} fainted!");
            yield return new WaitForSeconds(1f);
        }
    }

    public IEnumerator ApplyMoveEffects(Monster attacker, Monster defender,
                                        MoveData move, BattleHUD defenderHUD, bool isPlayer)
    {
        if (move.StatusEffect != StatusCondition.None && defender.Status == StatusCondition.None)
        {
            Monster target = move.TargetsUser ? attacker : defender;
            target.ApplyStatus(move.StatusEffect);

            BattleHUD targetHUD = move.TargetsUser == isPlayer ? ui.PlayerHUD : ui.EnemyHUD;
            targetHUD.UpdateStatus(target.Status);

            string statusName = BattleRules.GetStatusName(move.StatusEffect);
            yield return ui.DialogBox.TypeDialog($"{target.Data.MonsterName} {statusName}!");
            yield return new WaitForSeconds(1f);
        }

        if (move.StatStageChanges != null)
            yield return ApplyStatChanges(attacker, defender, move, isPlayer);
    }

    public IEnumerator ApplyStatChanges(Monster attacker, Monster defender,
                                        MoveData move, bool isPlayer)
    {
        foreach (var change in move.StatStageChanges)
        {
            Monster target = move.TargetsUser ? attacker : defender;
            int actual = target.ApplyStatStageChange(change.stat, change.stages);
            if (actual != 0)
            {
                string direction = actual > 0 ? "rose" : "fell";
                string amount = Mathf.Abs(actual) > 1 ? "sharply " : "";
                yield return ui.DialogBox.TypeDialog(
                    $"{target.Data.MonsterName}'s {BattleRules.GetStatName(change.stat)} {amount}{direction}!");
                yield return new WaitForSeconds(0.5f);
            }
            else
            {
                string limit = change.stages > 0 ? "can't go any higher" : "can't go any lower";
                yield return ui.DialogBox.TypeDialog(
                    $"{target.Data.MonsterName}'s {BattleRules.GetStatName(change.stat)} {limit}!");
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    public IEnumerator ProcessStatusEffects(Monster monster, BattleHUD hud)
    {
        if (monster.IsFainted) yield break;

        switch (monster.Status)
        {
            case StatusCondition.Poison:
                int poisonDmg = Mathf.Max(1, monster.MaxHp / 8);
                monster.TakeDamage(poisonDmg);
                yield return hud.AnimateHP(monster.CurrentHp);
                yield return ui.DialogBox.TypeDialog($"{monster.Data.MonsterName} is hurt by poison!");
                yield return new WaitForSeconds(0.5f);
                break;

            case StatusCondition.Burn:
                int burnDmg = Mathf.Max(1, monster.MaxHp / 16);
                monster.TakeDamage(burnDmg);
                yield return hud.AnimateHP(monster.CurrentHp);
                yield return ui.DialogBox.TypeDialog($"{monster.Data.MonsterName} is hurt by its burn!");
                yield return new WaitForSeconds(0.5f);
                break;

            case StatusCondition.Vulnerable:
                monster.IncrementStatusTurns();
                if (monster.StatusTurns >= 3)
                {
                    monster.CureStatus();
                    hud.UpdateStatus(StatusCondition.None);
                    yield return ui.DialogBox.TypeDialog($"{monster.Data.MonsterName}'s vulnerability wore off!");
                    yield return new WaitForSeconds(0.5f);
                }
                break;
        }

        if (monster.IsFainted)
        {
            yield return ui.DialogBox.TypeDialog($"{monster.Data.MonsterName} fainted!");
            yield return new WaitForSeconds(1f);
        }
    }
}
