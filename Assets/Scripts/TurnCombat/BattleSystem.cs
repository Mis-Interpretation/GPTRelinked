using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleSystem : MonoBehaviour
{
    #region Editor (Serialized)
    [SerializeField] private BattleUnit playerUnit;
    [SerializeField] private BattleUnit enemyUnit;
    [SerializeField] private BattleUI ui;
    [SerializeField] private BattleLLMBrain llmBrain;
    [SerializeField] private TypeChart typeChart;
    #endregion

    #region Private Variables
    private BattleState state;
    private Monster[] playerParty;
    private int currentMonsterIndex;
    private Monster enemyMonster;
    private bool hasChatThisTurn;
    private string chatMessageThisTurn;
    private int pendingEnemyMoveIndex;
    private bool waitingForLLM;
    private int intentMoveIndex = -1;
    private bool intentReady;
    #endregion

    #region Events
    public event Action<bool> OnBattleOver;
    #endregion

    #region Public Functions
    public void StartBattle(Monster[] party, Monster enemy)
    {
        playerParty = party;
        enemyMonster = enemy;
        currentMonsterIndex = FindFirstAliveIndex();

        if (llmBrain != null)
        {
            llmBrain.Init();
            llmBrain.SetEnemyPersonality(enemy.Data.MonsterName, enemy.Data.PrimaryType, enemy.Data.Personality);
            llmBrain.EventAIActionDecided += OnAIActionDecided;
            llmBrain.EventIntentDecided += OnIntentDecided;
            llmBrain.EventChatResponseReceived += OnChatResponseReceived;
        }

        ui.Init();
        ui.OnFightSelected += HandleFightSelected;
        ui.OnTalkSelected += HandleTalkSelected;
        ui.OnSwitchSelected += HandleSwitchSelected;
        ui.OnCatchSelected += HandleCatchSelected;
        ui.OnMoveSelected += HandleMoveSelected;
        ui.OnPartyMemberSelected += HandlePartyMemberSelected;
        ui.OnChatSubmitted += HandleChatSubmitted;
        ui.OnBackToActions += HandleBackToActions;

        StartCoroutine(SetupBattle());
    }
    #endregion

    #region Private Functions
    private IEnumerator SetupBattle()
    {
        state = BattleState.Start;

        playerUnit.Setup(playerParty[currentMonsterIndex]);
        enemyUnit.Setup(enemyMonster);

        ui.PlayerHUD.SetData(playerParty[currentMonsterIndex]);
        ui.EnemyHUD.SetData(enemyMonster);

        yield return ui.DialogBox.TypeDialog($"野生的 {enemyMonster.Data.MonsterName} 出现了！");
        yield return new WaitForSeconds(1f);
        yield return ui.DialogBox.TypeDialog($"去吧，{PlayerMonster.Data.MonsterName}！");
        yield return new WaitForSeconds(1f);

        PlayerActionPhase();
    }

    private void PlayerActionPhase()
    {
        state = BattleState.PlayerAction;
        hasChatThisTurn = false;
        chatMessageThisTurn = null;
        ui.DialogBox.SetDialog("选择一个行动：");
        ui.ShowActionPanel();
        FetchEnemyIntent();
    }

    private void ResumeActionPhase()
    {
        state = BattleState.PlayerAction;
        ui.DialogBox.SetDialog("选择一个行动：");
        ui.ShowActionPanel();
        FetchEnemyIntent();
    }

    private void HandleFightSelected()
    {
        if (state != BattleState.PlayerAction) return;
        ui.ShowMovePanel(PlayerMonster.Moves);
    }

    private void HandleTalkSelected()
    {
        if (state != BattleState.PlayerAction) return;
        if (hasChatThisTurn)
        {
            ui.DialogBox.SetDialog("这回合已经聊过了！");
            return;
        }
        ui.ShowChatPanel();
    }

    private void HandleSwitchSelected()
    {
        if (state != BattleState.PlayerAction) return;
        ui.ShowPartyPanel(playerParty, currentMonsterIndex);
    }

    private void HandleCatchSelected()
    {
        if (state != BattleState.PlayerAction) return;
        state = BattleState.PlayerCatch;
        ui.ClearEnemyIntent();
        StartCoroutine(PlayerCatchCoroutine());
    }

    private void HandleMoveSelected(int moveIndex)
    {
        if (state != BattleState.PlayerAction) return;
        MoveSlot slot = PlayerMonster.Moves[moveIndex];
        if (slot == null || !slot.HasPP) return;

        state = BattleState.PlayerMove;
        ui.HideAllPanels();
        ui.ClearEnemyIntent();
        StartCoroutine(ExecuteTurn(moveIndex));
    }

    private void HandlePartyMemberSelected(int partyIndex)
    {
        if (state != BattleState.PlayerAction) return;
        if (partyIndex == currentMonsterIndex || playerParty[partyIndex].IsFainted) return;

        state = BattleState.PlayerSwitch;
        ui.HideAllPanels();
        ui.ClearEnemyIntent();
        StartCoroutine(SwitchMonsterCoroutine(partyIndex, true));
    }

    private void HandleChatSubmitted(string message)
    {
        if (state != BattleState.PlayerAction) return;
        state = BattleState.PlayerChat;
        hasChatThisTurn = true;
        chatMessageThisTurn = message;
        ui.HideAllPanels();
        ui.ClearEnemyIntent();
        StartCoroutine(ChatCoroutine(message));
    }

    private void HandleBackToActions()
    {
        if (state != BattleState.PlayerAction) return;
        PlayerActionPhase();
    }

    // ── Turn Execution ──

    private IEnumerator ExecuteTurn(int playerMoveIndex)
    {
        MoveSlot playerMove = PlayerMonster.Moves[playerMoveIndex];

        while (!intentReady) yield return null;

        int enemyMoveIdx = ClampMoveIndex(enemyMonster, intentMoveIndex);
        MoveSlot enemyMove = enemyMonster.Moves[enemyMoveIdx];

        // Determine order
        bool playerFirst = ShouldPlayerGoFirst(playerMove.Data, enemyMove?.Data);

        if (playerFirst)
        {
            yield return PerformMove(PlayerMonster, enemyMonster, playerMove, ui.EnemyHUD, true);
            if (CheckBattleEnd()) yield break;
            yield return PerformMove(enemyMonster, PlayerMonster, enemyMove, ui.PlayerHUD, false);
            if (CheckBattleEnd()) yield break;
        }
        else
        {
            yield return PerformMove(enemyMonster, PlayerMonster, enemyMove, ui.PlayerHUD, false);
            if (CheckBattleEnd()) yield break;
            yield return PerformMove(PlayerMonster, enemyMonster, playerMove, ui.EnemyHUD, true);
            if (CheckBattleEnd()) yield break;
        }

        yield return ProcessStatusEffects(PlayerMonster, ui.PlayerHUD);
        if (CheckBattleEnd()) yield break;
        yield return ProcessStatusEffects(enemyMonster, ui.EnemyHUD);
        if (CheckBattleEnd()) yield break;

        PlayerActionPhase();
    }

    private IEnumerator PerformMove(Monster attacker, Monster defender, MoveSlot move, BattleHUD defenderHUD, bool isPlayer)
    {
        if (attacker.IsFainted || move == null) yield break;

        // Paralysis check
        if (attacker.Status == StatusCondition.Paralysis && UnityEngine.Random.value < 0.25f)
        {
            yield return ui.DialogBox.TypeDialog($"{attacker.Data.MonsterName} 因麻痹而无法行动！");
            yield return new WaitForSeconds(1f);
            yield break;
        }

        // Sleep check
        if (attacker.Status == StatusCondition.Sleep)
        {
            attacker.IncrementStatusTurns();
            if (attacker.StatusTurns < 3 && UnityEngine.Random.value > 0.33f)
            {
                yield return ui.DialogBox.TypeDialog($"{attacker.Data.MonsterName} 正在睡觉...");
                yield return new WaitForSeconds(1f);
                yield break;
            }
            attacker.CureStatus();
            yield return ui.DialogBox.TypeDialog($"{attacker.Data.MonsterName} 醒来了！");
            yield return new WaitForSeconds(0.5f);
        }

        // Freeze check
        if (attacker.Status == StatusCondition.Freeze)
        {
            if (UnityEngine.Random.value > 0.2f)
            {
                yield return ui.DialogBox.TypeDialog($"{attacker.Data.MonsterName} 被冰冻住了！");
                yield return new WaitForSeconds(1f);
                yield break;
            }
            attacker.CureStatus();
            yield return ui.DialogBox.TypeDialog($"{attacker.Data.MonsterName} 解冻了！");
            yield return new WaitForSeconds(0.5f);
        }

        move.UsePP();
        yield return ui.DialogBox.TypeDialog($"{attacker.Data.MonsterName} 使用了 {move.Data.MoveName}！");
        yield return new WaitForSeconds(0.5f);

        // Accuracy check
        if (!DamageCalculator.AccuracyCheck(move.Data))
        {
            yield return ui.DialogBox.TypeDialog("但是没有命中！");
            yield return new WaitForSeconds(1f);
            yield break;
        }

        // Status moves
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
            yield return ui.DialogBox.TypeDialog("击中了要害！");
            yield return new WaitForSeconds(0.5f);
        }

        if (result.effectiveness > 1.5f)
        {
            yield return ui.DialogBox.TypeDialog("效果拔群！");
            yield return new WaitForSeconds(0.5f);
        }
        else if (result.effectiveness > 0f && result.effectiveness < 0.75f)
        {
            yield return ui.DialogBox.TypeDialog("效果不太好...");
            yield return new WaitForSeconds(0.5f);
        }
        else if (result.effectiveness == 0f)
        {
            yield return ui.DialogBox.TypeDialog("完全没有效果...");
            yield return new WaitForSeconds(0.5f);
        }

        // Secondary status effect
        if (move.Data.StatusEffect != StatusCondition.None && move.Data.StatusChance > 0)
        {
            if (UnityEngine.Random.Range(0, 100) < move.Data.StatusChance)
            {
                yield return ApplyMoveEffects(attacker, defender, move.Data, defenderHUD, isPlayer);
            }
        }

        // Stat stage changes from damaging moves
        if (move.Data.StatStageChanges != null && move.Data.StatStageChanges.Length > 0)
        {
            yield return ApplyStatChanges(attacker, defender, move.Data, isPlayer);
        }

        if (defender.IsFainted)
        {
            yield return ui.DialogBox.TypeDialog($"{defender.Data.MonsterName} 倒下了！");
            yield return new WaitForSeconds(1f);
        }
    }

    private IEnumerator ApplyMoveEffects(Monster attacker, Monster defender, MoveData move, BattleHUD defenderHUD, bool isPlayer)
    {
        if (move.StatusEffect != StatusCondition.None && defender.Status == StatusCondition.None)
        {
            Monster target = move.TargetsUser ? attacker : defender;
            target.ApplyStatus(move.StatusEffect);
            BattleHUD targetHUD = move.TargetsUser == isPlayer ? ui.PlayerHUD : ui.EnemyHUD;
            targetHUD.UpdateStatus(target.Status);

            string statusName = GetStatusName(move.StatusEffect);
            yield return ui.DialogBox.TypeDialog($"{target.Data.MonsterName} {statusName}了！");
            yield return new WaitForSeconds(1f);
        }

        if (move.StatStageChanges != null)
        {
            yield return ApplyStatChanges(attacker, defender, move, isPlayer);
        }
    }

    private IEnumerator ApplyStatChanges(Monster attacker, Monster defender, MoveData move, bool isPlayer)
    {
        foreach (var change in move.StatStageChanges)
        {
            Monster target = move.TargetsUser ? attacker : defender;
            int actual = target.ApplyStatStageChange(change.stat, change.stages);
            if (actual != 0)
            {
                string direction = actual > 0 ? "提升" : "降低";
                string amount = Mathf.Abs(actual) > 1 ? "大幅" : "";
                yield return ui.DialogBox.TypeDialog($"{target.Data.MonsterName} 的{GetStatName(change.stat)}{amount}{direction}了！");
                yield return new WaitForSeconds(0.5f);
            }
            else
            {
                string limit = change.stages > 0 ? "已经无法再提升" : "已经无法再降低";
                yield return ui.DialogBox.TypeDialog($"{target.Data.MonsterName} 的{GetStatName(change.stat)}{limit}了！");
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    private IEnumerator ProcessStatusEffects(Monster monster, BattleHUD hud)
    {
        if (monster.IsFainted) yield break;

        switch (monster.Status)
        {
            case StatusCondition.Poison:
                int poisonDmg = Mathf.Max(1, monster.MaxHp / 8);
                monster.TakeDamage(poisonDmg);
                yield return hud.AnimateHP(monster.CurrentHp);
                yield return ui.DialogBox.TypeDialog($"{monster.Data.MonsterName} 受到了中毒伤害！");
                yield return new WaitForSeconds(0.5f);
                break;

            case StatusCondition.Burn:
                int burnDmg = Mathf.Max(1, monster.MaxHp / 16);
                monster.TakeDamage(burnDmg);
                yield return hud.AnimateHP(monster.CurrentHp);
                yield return ui.DialogBox.TypeDialog($"{monster.Data.MonsterName} 受到了灼伤伤害！");
                yield return new WaitForSeconds(0.5f);
                break;

            case StatusCondition.Vulnerable:
                monster.IncrementStatusTurns();
                if (monster.StatusTurns >= 3)
                {
                    monster.CureStatus();
                    hud.UpdateStatus(StatusCondition.None);
                    yield return ui.DialogBox.TypeDialog($"{monster.Data.MonsterName} 的易伤状态消退了！");
                    yield return new WaitForSeconds(0.5f);
                }
                break;
        }

        if (monster.IsFainted)
        {
            yield return ui.DialogBox.TypeDialog($"{monster.Data.MonsterName} 倒下了！");
            yield return new WaitForSeconds(1f);
        }
    }

    // ── Chat System ──

    private IEnumerator ChatCoroutine(string message)
    {
        yield return ui.DialogBox.TypeDialog($"你对 {enemyMonster.Data.MonsterName} 说：'{message}'");
        yield return new WaitForSeconds(0.5f);

        waitingForLLM = true;
        llmBrain.RequestChatResponse(message, enemyMonster, PlayerMonster);
        yield return ui.DialogBox.TypeDialog($"{enemyMonster.Data.MonsterName} 正在思考...");
        while (waitingForLLM) yield return null;
    }

    private void OnChatResponseReceived(ChatBuffResult result)
    {
        waitingForLLM = false;
        StartCoroutine(ProcessChatResult(result));
    }

    private IEnumerator ProcessChatResult(ChatBuffResult result)
    {
        yield return ui.DialogBox.TypeDialog($"{enemyMonster.Data.MonsterName}：「{result.response}」");
        yield return new WaitForSeconds(1f);

        if (result.stages != 0 && Enum.TryParse<StatType>(result.statType, out StatType stat))
        {
            Monster target = result.buffTarget == "enemy" ? enemyMonster : PlayerMonster;
            int actual = target.ApplyStatStageChange(stat, result.stages);
            if (actual != 0)
            {
                string direction = actual > 0 ? "提升" : "降低";
                string amount = Mathf.Abs(actual) > 1 ? "大幅" : "";
                yield return ui.DialogBox.TypeDialog($"{target.Data.MonsterName} 的{GetStatName(stat)}{amount}{direction}了！");
                yield return new WaitForSeconds(0.5f);
            }
        }

        ResumeActionPhase();
    }

    // ── Switch ──

    private IEnumerator SwitchMonsterCoroutine(int newIndex, bool enemyGetsFreeTurn)
    {
        PlayerMonster.ResetStatStages();
        currentMonsterIndex = newIndex;
        playerUnit.Setup(PlayerMonster);
        ui.PlayerHUD.SetData(PlayerMonster);

        yield return ui.DialogBox.TypeDialog($"去吧，{PlayerMonster.Data.MonsterName}！");
        yield return new WaitForSeconds(1f);

        if (enemyGetsFreeTurn)
        {
            waitingForLLM = true;
            pendingEnemyMoveIndex = 0;
            RequestAIMove();
            while (waitingForLLM) yield return null;

            int idx = ClampMoveIndex(enemyMonster, pendingEnemyMoveIndex);
            MoveSlot enemyMove = enemyMonster.Moves[idx];
            yield return PerformMove(enemyMonster, PlayerMonster, enemyMove, ui.PlayerHUD, false);
            if (CheckBattleEnd()) yield break;

            yield return ProcessStatusEffects(PlayerMonster, ui.PlayerHUD);
            if (CheckBattleEnd()) yield break;
            yield return ProcessStatusEffects(enemyMonster, ui.EnemyHUD);
            if (CheckBattleEnd()) yield break;
        }

        PlayerActionPhase();
    }

    // ── Catch ──

    private IEnumerator PlayerCatchCoroutine()
    {
        yield return ui.DialogBox.TypeDialog("你投出了捕获球！");
        yield return new WaitForSeconds(0.5f);

        CatchResult result = CatchCalculator.TryCatch(enemyMonster);

        for (int i = 0; i < result.shakeCount; i++)
        {
            yield return ui.DialogBox.TypeDialog("..." + new string('●', i + 1));
            yield return new WaitForSeconds(0.6f);
        }

        if (result.success)
        {
            yield return ui.DialogBox.TypeDialog($"成功捕获了 {enemyMonster.Data.MonsterName}！");
            yield return new WaitForSeconds(1.5f);
            BattleEnd(true);
        }
        else
        {
            yield return ui.DialogBox.TypeDialog($"{enemyMonster.Data.MonsterName} 挣脱了！");
            yield return new WaitForSeconds(1f);

            // Enemy free turn
            waitingForLLM = true;
            pendingEnemyMoveIndex = 0;
            RequestAIMove();
            while (waitingForLLM) yield return null;

            int idx = ClampMoveIndex(enemyMonster, pendingEnemyMoveIndex);
            MoveSlot enemyMove = enemyMonster.Moves[idx];
            yield return PerformMove(enemyMonster, PlayerMonster, enemyMove, ui.PlayerHUD, false);
            if (CheckBattleEnd()) yield break;

            yield return ProcessStatusEffects(PlayerMonster, ui.PlayerHUD);
            if (CheckBattleEnd()) yield break;
            yield return ProcessStatusEffects(enemyMonster, ui.EnemyHUD);
            if (CheckBattleEnd()) yield break;

            PlayerActionPhase();
        }
    }

    // ── Battle End ──

    private bool CheckBattleEnd()
    {
        if (enemyMonster.IsFainted)
        {
            StartCoroutine(HandleEnemyFainted());
            return true;
        }

        if (PlayerMonster.IsFainted)
        {
            int nextAlive = FindFirstAliveIndex();
            if (nextAlive < 0)
            {
                StartCoroutine(HandleAllPlayerFainted());
                return true;
            }
            StartCoroutine(HandlePlayerMonsterFainted(nextAlive));
            return true;
        }

        return false;
    }

    private IEnumerator HandleEnemyFainted()
    {
        state = BattleState.BattleOver;

        int expGain = ExpCalculator.GetExpGain(enemyMonster.Data, enemyMonster.Level);
        yield return ui.DialogBox.TypeDialog($"{PlayerMonster.Data.MonsterName} 获得了 {expGain} 点经验值！");
        yield return new WaitForSeconds(1f);

        int levelsGained = PlayerMonster.AddExp(expGain);
        ui.PlayerHUD.UpdateExpBar();

        if (levelsGained > 0)
        {
            ui.PlayerHUD.RefreshLevel();
            yield return ui.DialogBox.TypeDialog($"{PlayerMonster.Data.MonsterName} 升到了 Lv{PlayerMonster.Level}！");
            yield return new WaitForSeconds(1f);

            List<MoveData> newMoves = PlayerMonster.GetNewMovesForCurrentLevel();
            foreach (var move in newMoves)
            {
                bool learned = PlayerMonster.LearnMove(move);
                if (learned)
                {
                    yield return ui.DialogBox.TypeDialog($"{PlayerMonster.Data.MonsterName} 学会了 {move.MoveName}！");
                    yield return new WaitForSeconds(1f);
                }
                else
                {
                    yield return ui.DialogBox.TypeDialog($"{PlayerMonster.Data.MonsterName} 想要学习 {move.MoveName}，但是招式已满！");
                    yield return new WaitForSeconds(1f);
                }
            }
        }

        yield return ui.DialogBox.TypeDialog("战斗胜利！");
        yield return new WaitForSeconds(1f);
        BattleEnd(true);
    }

    private IEnumerator HandlePlayerMonsterFainted(int nextAlive)
    {
        yield return ui.DialogBox.TypeDialog($"{PlayerMonster.Data.MonsterName} 无法战斗了！");
        yield return new WaitForSeconds(1f);
        yield return ui.DialogBox.TypeDialog("请选择下一只怪兽！");
        ui.ShowPartyPanel(playerParty, currentMonsterIndex);

        state = BattleState.PlayerAction;
    }

    private IEnumerator HandleAllPlayerFainted()
    {
        state = BattleState.BattleOver;
        yield return ui.DialogBox.TypeDialog("所有怪兽都无法战斗了...");
        yield return new WaitForSeconds(1f);
        yield return ui.DialogBox.TypeDialog("战斗失败...");
        yield return new WaitForSeconds(1f);
        BattleEnd(false);
    }

    private void BattleEnd(bool playerWon)
    {
        state = BattleState.BattleOver;
        ui.HideAllPanels();
        ui.ClearEnemyIntent();

        if (llmBrain != null)
        {
            llmBrain.EventAIActionDecided -= OnAIActionDecided;
            llmBrain.EventIntentDecided -= OnIntentDecided;
            llmBrain.EventChatResponseReceived -= OnChatResponseReceived;
        }

        ui.OnFightSelected -= HandleFightSelected;
        ui.OnTalkSelected -= HandleTalkSelected;
        ui.OnSwitchSelected -= HandleSwitchSelected;
        ui.OnCatchSelected -= HandleCatchSelected;
        ui.OnMoveSelected -= HandleMoveSelected;
        ui.OnPartyMemberSelected -= HandlePartyMemberSelected;
        ui.OnChatSubmitted -= HandleChatSubmitted;
        ui.OnBackToActions -= HandleBackToActions;

        OnBattleOver?.Invoke(playerWon);
    }

    // ── Intent ──

    private void FetchEnemyIntent()
    {
        intentReady = false;
        intentMoveIndex = -1;
        ui.UpdateEnemyIntent(null);

        if (llmBrain == null)
        {
            OnIntentDecided(FallbackAIMove());
            return;
        }

        BattleContext ctx = BuildBattleContext();
        llmBrain.RequestIntent(ctx);
    }

    private void OnIntentDecided(int moveIndex)
    {
        intentMoveIndex = moveIndex;
        intentReady = true;

        if (state != BattleState.PlayerAction) return;
        int idx = ClampMoveIndex(enemyMonster, moveIndex);
        MoveSlot move = enemyMonster.Moves[idx];
        if (move != null)
            ui.UpdateEnemyIntent(move.Data.MoveName);
    }

    // ── AI ──

    private void RequestAIMove()
    {
        if (llmBrain == null)
        {
            OnAIActionDecided(FallbackAIMove());
            return;
        }

        BattleContext ctx = BuildBattleContext();
        llmBrain.RequestAIAction(ctx);
    }

    private void OnAIActionDecided(int moveIndex)
    {
        pendingEnemyMoveIndex = moveIndex;
        waitingForLLM = false;
    }

    private BattleContext BuildBattleContext()
    {
        var ctx = new BattleContext
        {
            playerName = PlayerMonster.Data.MonsterName,
            playerLevel = PlayerMonster.Level,
            playerCurrentHp = PlayerMonster.CurrentHp,
            playerMaxHp = PlayerMonster.MaxHp,
            playerType = PlayerMonster.Data.PrimaryType.ToString(),
            enemyName = enemyMonster.Data.MonsterName,
            enemyLevel = enemyMonster.Level,
            enemyCurrentHp = enemyMonster.CurrentHp,
            enemyMaxHp = enemyMonster.MaxHp,
            enemyType = enemyMonster.Data.PrimaryType.ToString(),
            enemyPersonality = enemyMonster.Data.Personality,
            playerChatMessage = chatMessageThisTurn
        };

        var moves = enemyMonster.Moves;
        int count = 0;
        foreach (var m in moves) { if (m != null) count++; }

        ctx.moveNames = new string[count];
        ctx.movePowers = new int[count];
        ctx.moveTypes = new string[count];
        ctx.movePPs = new int[count];

        int idx = 0;
        for (int i = 0; i < moves.Length; i++)
        {
            if (moves[i] == null) continue;
            ctx.moveNames[idx] = moves[i].Data.MoveName;
            ctx.movePowers[idx] = moves[i].Data.Power;
            ctx.moveTypes[idx] = moves[i].Data.MoveType.ToString();
            ctx.movePPs[idx] = moves[i].CurrentPP;
            idx++;
        }

        return ctx;
    }

    private int FallbackAIMove()
    {
        var moves = enemyMonster.Moves;
        var validIndices = new List<int>();
        for (int i = 0; i < moves.Length; i++)
        {
            if (moves[i] != null && moves[i].HasPP)
                validIndices.Add(i);
        }
        if (validIndices.Count == 0) return 0;
        return validIndices[UnityEngine.Random.Range(0, validIndices.Count)];
    }
    #endregion

    #region Helper Functions
    private Monster PlayerMonster => playerParty[currentMonsterIndex];

    private int FindFirstAliveIndex()
    {
        for (int i = 0; i < playerParty.Length; i++)
        {
            if (playerParty[i] != null && !playerParty[i].IsFainted)
                return i;
        }
        return -1;
    }

    private bool ShouldPlayerGoFirst(MoveData playerMove, MoveData enemyMove)
    {
        if (playerMove == null || enemyMove == null) return true;
        if (playerMove.Priority != enemyMove.Priority)
            return playerMove.Priority > enemyMove.Priority;

        int playerSpeed = PlayerMonster.GetStat(StatType.Speed);
        int enemySpeed = enemyMonster.GetStat(StatType.Speed);

        if (PlayerMonster.Status == StatusCondition.Paralysis)
            playerSpeed /= 2;
        if (enemyMonster.Status == StatusCondition.Paralysis)
            enemySpeed /= 2;

        if (playerSpeed == enemySpeed)
            return UnityEngine.Random.value > 0.5f;
        return playerSpeed > enemySpeed;
    }

    private int ClampMoveIndex(Monster m, int index)
    {
        if (m.Moves == null) return 0;
        if (index < 0 || index >= m.Moves.Length || m.Moves[index] == null || !m.Moves[index].HasPP)
            return FallbackAIMove();
        return index;
    }

    private string GetStatusName(StatusCondition condition)
    {
        return condition switch
        {
            StatusCondition.Poison => "中毒",
            StatusCondition.Burn => "灼伤",
            StatusCondition.Paralysis => "麻痹",
            StatusCondition.Sleep => "睡着",
            StatusCondition.Freeze => "冰冻",
            StatusCondition.Vulnerable => "易伤",
            _ => ""
        };
    }

    private string GetStatName(StatType stat)
    {
        return stat switch
        {
            StatType.Attack => "攻击",
            StatType.Defense => "防御",
            StatType.SpAttack => "特攻",
            StatType.SpDefense => "特防",
            StatType.Speed => "速度",
            _ => ""
        };
    }
    #endregion
}
