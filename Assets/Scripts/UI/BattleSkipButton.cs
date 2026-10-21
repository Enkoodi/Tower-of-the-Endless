using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗界面的「跳过」按钮：点了让本场直接跳到结算，界面上只留结果行。
/// 真正的快进在 <see cref="BattleManager.RequestSkip"/> / <c>EnterFastForward</c>，这里只负责转发。
/// </summary>
[RequireComponent(typeof(Button))]
public class BattleSkipButton : MonoBehaviour
{
    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() => BattleManager.Instance?.RequestSkip());
    }
}
