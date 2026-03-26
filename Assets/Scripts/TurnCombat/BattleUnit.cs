using UnityEngine;
using UnityEngine.UI;

public class BattleUnit : MonoBehaviour
{
    #region Editor (Serialized)
    [SerializeField] private Image spriteRenderer;
    [SerializeField] private bool isPlayerUnit;
    #endregion

    #region Private Variables
    private Monster monster;
    #endregion

    #region Public Functions
    public Monster Monster => monster;
    public bool IsPlayerUnit => isPlayerUnit;

    public void Setup(Monster monster)
    {
        this.monster = monster;
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = isPlayerUnit ? monster.Data.BackSprite : monster.Data.FrontSprite;
            if (spriteRenderer.sprite == null)
                spriteRenderer.sprite = isPlayerUnit ? monster.Data.FrontSprite : monster.Data.BackSprite;
        }
    }
    #endregion
}
