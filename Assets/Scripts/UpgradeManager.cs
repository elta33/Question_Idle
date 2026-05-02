using UnityEngine;

/// <summary>
/// 업그레이드 구매 로직을 처리합니다.
/// 비용 공식: baseCost × 1.10^n  (n = 현재 구매 횟수)
///
/// ■ Phase 2 업그레이드 3종 (GDD 5.2)
///   1. ? 생산속도 업  — QuestionRate 증가
///   2. ! 결합 확률 업 — IdeaCombineChance 증가
///   3. 임계점 감소    — IdeaCombineThreshold 감소
///
/// ■ 의존: GameManager
/// ■ 부착 위치: GameManager와 동일한 GameObject
/// </summary>
public class UpgradeManager : MonoBehaviour
{
    // ── 업그레이드 정의 ───────────────────────────────────────

    [System.Serializable]
    public class UpgradeDef
    {
        [Tooltip("업그레이드 이름 (디버그/UI용)")]
        public string label;

        [Tooltip("1회 구매 기본 비용 (! 소모). n=0일 때 적용됩니다.")]
        public float baseCost;

        [Tooltip("비용 증가 배율. 기본 1.10 (10% 복리 증가)")]
        public float costMultiplier = 1.10f;

        [Tooltip("현재까지 구매한 횟수. Inspector에서 런타임 확인 가능.")]
        public int purchaseCount = 0;

        [Tooltip("파라미터 1회 증감량")]
        public float delta;

        /// <summary>현재 구매 비용</summary>
        public float CurrentCost =>
            baseCost * Mathf.Pow(costMultiplier, purchaseCount);
    }

    [Header("업그레이드 정의 (GDD 5.2)")]
    [SerializeField] private UpgradeDef questionRateUp = new UpgradeDef
    {
        label          = "? 생산속도 업",
        baseCost       = 5f,
        costMultiplier = 1.10f,
        delta          = 0.05f,   // +0.05개/초
    };

    [SerializeField] private UpgradeDef combineChanceUp = new UpgradeDef
    {
        label          = "! 결합 확률 업",
        baseCost       = 20f,
        costMultiplier = 1.10f,
        delta          = 0.02f,   // +2%
    };

    [SerializeField] private UpgradeDef thresholdDown = new UpgradeDef
    {
        label          = "임계점 감소",
        baseCost       = 15f,
        costMultiplier = 1.10f,
        delta          = -1f,     // -1개
    };

    // ── 내부 참조 ─────────────────────────────────────────────
    private GameManager _gm;

    // ─────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Start()
    {
        _gm = GameManager.Instance;
        if (_gm == null)
            Debug.LogError("[UpgradeManager] GameManager 인스턴스를 찾을 수 없습니다.");
    }

    #endregion

    // ─────────────────────────────────────────────────────────
    #region 버튼 콜백 (Inspector의 OnClick에 연결)

    /// <summary>버튼 1: ? 생산속도 업</summary>
    public void OnClickQuestionRateUp()   => TryPurchase(questionRateUp, ApplyQuestionRateUp);

    /// <summary>버튼 2: ! 결합 확률 업</summary>
    public void OnClickCombineChanceUp()  => TryPurchase(combineChanceUp, ApplyCombineChanceUp);

    /// <summary>버튼 3: 임계점 감소</summary>
    public void OnClickThresholdDown()    => TryPurchase(thresholdDown, ApplyThresholdDown);

    #endregion

    // ─────────────────────────────────────────────────────────
    #region 구매 처리

    private void TryPurchase(UpgradeDef def, System.Action applyFn)
    {
        if (_gm == null) return;

        float cost = def.CurrentCost;

        if (_gm.IdeaCount < cost)
        {
            Debug.Log($"[UpgradeManager] '{def.label}' 구매 실패 — ! 부족 (필요 {cost:F1}, 보유 {_gm.IdeaCount:F1})");
            return;
        }

        _gm.IdeaCount -= cost;
        def.purchaseCount++;
        applyFn();

        Debug.Log($"[UpgradeManager] '{def.label}' 구매 완료 (n={def.purchaseCount}, 다음 비용 {def.CurrentCost:F1}!)");
    }

    // ── 파라미터 적용 ─────────────────────────────────────────

    private void ApplyQuestionRateUp()
    {
        _gm.QuestionRate += questionRateUp.delta;
    }

    private void ApplyCombineChanceUp()
    {
        _gm.IdeaCombineChance = Mathf.Clamp01(_gm.IdeaCombineChance + combineChanceUp.delta);
    }

    private void ApplyThresholdDown()
    {
        // 최솟값 1 보장 (GameManager setter에서도 보호하지만 명시)
        _gm.IdeaCombineThreshold = Mathf.Max(1f, _gm.IdeaCombineThreshold + thresholdDown.delta);
    }

    #endregion

    // ─────────────────────────────────────────────────────────
    #region Public API (ResourceDisplayUI가 비용 표시에 사용)

    public float QuestionRateUpCost  => questionRateUp.CurrentCost;
    public float CombineChanceUpCost => combineChanceUp.CurrentCost;
    public float ThresholdDownCost   => thresholdDown.CurrentCost;

    #endregion
}
