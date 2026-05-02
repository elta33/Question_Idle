using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Canvas의 텍스트 4개와 버튼 3개를 GameManager / UpgradeManager에 매핑합니다.
///
/// ■ 텍스트 연결 (Inspector에서 할당)
///   questionText   → "? : {n}"
///   ideaText       → "! : {n}"
///   insightText    → "??? : {n}"
///   dotText        → ". : {n}"
///
/// ■ 버튼 텍스트 연결 (Inspector에서 할당, 선택 사항)
///   btn1CostText   → "? 생산속도 업\n비용: {n}!"
///   btn2CostText   → "! 결합 확률 업\n비용: {n}!"
///   btn3CostText   → "임계점 감소\n비용: {n}!"
///
/// ■ 부착 위치: Canvas 하위 빈 GameObject (예: "UIManager")
/// ■ 의존: GameManager, UpgradeManager
/// </summary>
public class ResourceDisplayUI : MonoBehaviour
{
    // ── 자원 텍스트 (4개) ─────────────────────────────────────
    [Header("자원 텍스트 (4개 할당 필수)")]
    [SerializeField] private TMP_Text questionText;
    [SerializeField] private TMP_Text ideaText;
    [SerializeField] private TMP_Text insightText;
    [SerializeField] private TMP_Text dotText;

    // ── 버튼 비용 텍스트 (선택) ───────────────────────────────
    [Header("버튼 비용 텍스트 (선택 — 없으면 비용 갱신 생략)")]
    [SerializeField] private TMP_Text btn1CostText;   // ? 생산속도 업
    [SerializeField] private TMP_Text btn2CostText;   // ! 결합 확률 업
    [SerializeField] private TMP_Text btn3CostText;   // 임계점 감소

    // ── 갱신 주기 ─────────────────────────────────────────────
    [Header("갱신 주기")]
    [Tooltip("UI를 갱신할 초 간격. 0이면 매 프레임 갱신.")]
    [SerializeField] private float refreshInterval = 0.1f;   // 10fps 갱신으로 성능 절약

    // ── 소수점 표시 자릿수 ────────────────────────────────────
    [Header("표시 옵션")]
    [Tooltip("자원 수치의 소수점 자릿수. 0 = 정수 표시.")]
    [SerializeField] private int decimalPlaces = 1;

    // ── 내부 참조 ─────────────────────────────────────────────
    private GameManager    _gm;
    private UpgradeManager _um;
    private float          _elapsed;

    // ─────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Start()
    {
        _gm = GameManager.Instance;
        _um = FindFirstObjectByType<UpgradeManager>();   // Unity 6 권장 API

        if (_gm == null) Debug.LogError("[ResourceDisplayUI] GameManager를 찾을 수 없습니다.");
        if (_um == null) Debug.LogWarning("[ResourceDisplayUI] UpgradeManager를 찾을 수 없습니다. 버튼 비용이 표시되지 않습니다.");

        Refresh();   // 첫 프레임 즉시 갱신
    }

    private void Update()
    {
        if (refreshInterval <= 0f)
        {
            Refresh();
            return;
        }

        _elapsed += Time.deltaTime;
        if (_elapsed >= refreshInterval)
        {
            _elapsed = 0f;
            Refresh();
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────
    #region 갱신

    private void Refresh()
    {
        if (_gm == null) return;

        UpdateResourceTexts();
        if (_um != null) UpdateButtonCostTexts();
    }

    private void UpdateResourceTexts()
    {
        string fmt = $"F{decimalPlaces}";

        SetText(questionText, "?",   _gm.QuestionCount, fmt);
        SetText(ideaText,     "!",   _gm.IdeaCount,     fmt);
        SetText(insightText,  "???", _gm.InsightCount,  fmt);
        SetText(dotText,      ".",   _gm.DotCount,      fmt);
    }

    private void UpdateButtonCostTexts()
    {
        SetCostText(btn1CostText, "? 생산속도 업",  _um.QuestionRateUpCost);
        SetCostText(btn2CostText, "! 결합 확률 업", _um.CombineChanceUpCost);
        SetCostText(btn3CostText, "임계점 감소",    _um.ThresholdDownCost);
    }

    // ── 헬퍼 ──────────────────────────────────────────────────

    private void SetText(TMP_Text target, string label, float value, string fmt)
    {
        if (target == null) return;
        target.text = $"{label} : {value.ToString(fmt)}";
    }

    private void SetCostText(TMP_Text target, string label, float cost)
    {
        if (target == null) return;
        target.text = $"{label}\n<size=80%>비용: {cost:F0} !</size>";
    }

    #endregion
}
