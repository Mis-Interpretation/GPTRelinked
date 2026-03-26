using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BattleUI : MonoBehaviour
{
    #region Editor (Serialized)
    [Header("HUDs")]
    [SerializeField] private BattleHUD playerHUD;
    [SerializeField] private BattleHUD enemyHUD;

    [Header("Dialog")]
    [SerializeField] private BattleDialogBox dialogBox;

    [Header("Action Panel")]
    [SerializeField] private GameObject actionPanel;
    [SerializeField] private Button fightButton;
    [SerializeField] private Button talkButton;
    [SerializeField] private Button switchButton;
    [SerializeField] private Button catchButton;

    [Header("Move Panel")]
    [SerializeField] private GameObject movePanel;
    [SerializeField] private Transform moveButtonContainer;
    [SerializeField] private MoveButtonUI moveButtonPrefab;
    [SerializeField] private Button moveBackButton;

    [Header("Party Panel")]
    [SerializeField] private GameObject partyPanel;
    [SerializeField] private Transform partyButtonContainer;
    [SerializeField] private PartyButtonUI partyButtonPrefab;
    [SerializeField] private Button partyBackButton;

    [Header("Chat Panel")]
    [SerializeField] private GameObject chatPanel;
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private Button chatSendButton;
    [SerializeField] private Button chatBackButton;
    #endregion

    #region Events
    public event Action<int> OnMoveSelected;
    public event Action OnFightSelected;
    public event Action OnTalkSelected;
    public event Action OnSwitchSelected;
    public event Action OnCatchSelected;
    public event Action<int> OnPartyMemberSelected;
    public event Action<string> OnChatSubmitted;
    public event Action OnBackToActions;
    #endregion

    #region Public Functions
    public BattleHUD PlayerHUD => playerHUD;
    public BattleHUD EnemyHUD => enemyHUD;
    public BattleDialogBox DialogBox => dialogBox;

    public void Init()
    {
        fightButton.onClick.AddListener(() => OnFightSelected?.Invoke());
        talkButton.onClick.AddListener(() => OnTalkSelected?.Invoke());
        switchButton.onClick.AddListener(() => OnSwitchSelected?.Invoke());
        catchButton.onClick.AddListener(() => OnCatchSelected?.Invoke());

        moveBackButton.onClick.AddListener(() => OnBackToActions?.Invoke());
        partyBackButton.onClick.AddListener(() => OnBackToActions?.Invoke());

        chatSendButton.onClick.AddListener(SubmitChat);
        chatBackButton.onClick.AddListener(() => OnBackToActions?.Invoke());

        HideAllPanels();
    }

    public void ShowActionPanel()
    {
        HideAllPanels();
        actionPanel.SetActive(true);
    }

    public void ShowMovePanel(MoveSlot[] moves)
    {
        HideAllPanels();
        movePanel.SetActive(true);

        ClearContainer(moveButtonContainer);

        for (int i = 0; i < moves.Length; i++)
        {
            if (moves[i] == null) continue;

            MoveButtonUI btn = Instantiate(moveButtonPrefab, moveButtonContainer);
            btn.Setup(moves[i].Data.MoveName, moves[i].CurrentPP, moves[i].Data.MaxPP, moves[i].HasPP);

            int index = i;
            btn.Button.onClick.AddListener(() => OnMoveSelected?.Invoke(index));
        }
    }

    public void ShowPartyPanel(Monster[] party, int currentIndex)
    {
        HideAllPanels();
        partyPanel.SetActive(true);

        ClearContainer(partyButtonContainer);

        for (int i = 0; i < party.Length; i++)
        {
            if (party[i] == null) continue;

            PartyButtonUI btn = Instantiate(partyButtonPrefab, partyButtonContainer);
            string displayName = $"{party[i].Data.MonsterName} Lv{party[i].Level} HP:{party[i].CurrentHp}/{party[i].MaxHp}";
            btn.Setup(displayName, i != currentIndex && !party[i].IsFainted);

            int index = i;
            btn.Button.onClick.AddListener(() => OnPartyMemberSelected?.Invoke(index));
        }
    }

    public void ShowChatPanel()
    {
        HideAllPanels();
        chatPanel.SetActive(true);
        chatInputField.text = "";
        chatInputField.ActivateInputField();
    }

    public void HideAllPanels()
    {
        actionPanel.SetActive(false);
        movePanel.SetActive(false);
        partyPanel.SetActive(false);
        chatPanel.SetActive(false);
    }

    public void UpdateEnemyIntent(string moveName)
    {
        enemyHUD.SetIntent(moveName);
    }

    public void ClearEnemyIntent()
    {
        enemyHUD.ClearIntent();
    }
    #endregion

    #region Private Functions
    private void SubmitChat()
    {
        string message = chatInputField.text;
        if (string.IsNullOrWhiteSpace(message)) return;
        OnChatSubmitted?.Invoke(message);
    }

    private void ClearContainer(Transform container)
    {
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
    }
    #endregion
}
