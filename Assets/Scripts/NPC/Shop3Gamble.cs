using UnityEngine;

/// <summary>17层赌场老板的对话选项入口。</summary>
public class Shop3Gamble : MonoBehaviour
{
    public void OpenGamble()
    {
        PlayerData player = FindAnyObjectByType<PlayerData>();
        GambleChoiceUI ui = GambleChoiceUI.Instance;
        if (player == null || ui == null)
        {
            Debug.LogError("[Shop3Gamble] 未找到玩家或 ChoiceCanvas");
            return;
        }

        ui.Open(player);
    }
}
