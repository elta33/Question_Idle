using UnityEngine;

/// <summary>
/// 매 틱(1초)마다 자원 생성·결합·전환 로직을 처리합니다.
/// GameManager.Instance의 파라미터를 읽고, 자원 수치를 직접 변경합니다.
///
/// 의존: GameManager (싱글턴)
/// 부착 위치: GameManager와 동일한 GameObject, 또는 별도 TickerObject
/// </summary>
public class ResourceTicker : MonoBehaviour
{
    // ── 틱 설정 ──────────────────────────────────────────────
    [Tooltip("자원 계산 주기 (초). 기본 1초.")]
    [SerializeField] private float tickInterval = 1f;

    // ── 고착 내부 상태 ────────────────────────────────────────
    // ? 고착 발생 카운터: ? 10개 생성마다 5% 확률 체크에 쓰임
    private float _questionAccumForFixation = 0f;

    // ── 내부 참조 ─────────────────────────────────────────────
    private GameManager _gm;

    // ─────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Awake()
    {
        _gm = GameManager.Instance;
        if (_gm == null)
            Debug.LogError("[ResourceTicker] GameManager 인스턴스를 찾을 수 없습니다.");
    }

    private void Start()
    {
        // 틱을 1초 단위 코루틴으로 구동 (Update 대신 사용해 오프라인 계산과 충돌 방지)
        InvokeRepeating(nameof(Tick), tickInterval, tickInterval);
    }

    #endregion

    // ─────────────────────────────────────────────────────────
    #region Core Tick

    /// <summary>
    /// tickInterval(기본 1초)마다 호출되는 메인 루프.
    /// 순서: ① ? 생성 → ② ! 결합 시도 → ③ ??? 전환 체크
    /// </summary>
    private void Tick()
    {
        if (_gm == null) return;

        float producedQ = ProduceQuestions();
        TryCombineIdea(producedQ);
    }

    #endregion

    // ─────────────────────────────────────────────────────────
    #region ① ? 생성 (Preparation)

    /// <summary>
    /// 현재 questionRate에 따라 이번 틱에 생성할 ? 수량을 계산해 추가합니다.
    /// questionRate = 개/초 단위. 기본 1/5 ≈ 0.2개/초 → 1틱(1초)에 0.2개 누적.
    /// </summary>
    /// <returns>이번 틱에 실제로 생성된 ? 수량 (소수 포함)</returns>
    private float ProduceQuestions()
    {
        float produced = _gm.QuestionRate * tickInterval;
        _gm.QuestionCount += produced;

        // 고착 발생 체크용 누적
        _questionAccumForFixation += produced;
        CheckQuestionFixationTrigger();

        return produced;
    }

    /// <summary>
    /// ? 10개 생성마다 5% 확률로 고착 이벤트를 발생시킵니다.
    /// 실제 고착 처리는 FixationManager가 담당합니다.
    /// </summary>
    private void CheckQuestionFixationTrigger()
    {
        const float fixationUnit = 10f;
        const float fixationChance = 0.05f;

        while (_questionAccumForFixation >= fixationUnit)
        {
            _questionAccumForFixation -= fixationUnit;

            if (Random.value < fixationChance)
            {
                Debug.Log("[ResourceTicker] ? 고착 발생 트리거");
                // TODO: FixationManager.Instance.TriggerQuestionFixation();
                // Phase 3에서 연결합니다.
            }
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────
    #region ② ! 결합 (Incubation → Illumination)

    /// <summary>
    /// ? 임계점 이상일 때 확률적으로 !를 생성합니다.
    /// GDD 6.2 수치 기준:
    ///   - 임계점: 10개
    ///   - 기본 확률: 8%/초
    ///   - 초과 보너스: ? 1개 초과당 +1%
    ///   - 소모 ? 수량: 5~8개 (랜덤)
    /// </summary>
    private void TryCombineIdea(float producedQ)
    {
        if (_gm.QuestionCount < _gm.IdeaCombineThreshold) return;

        float excess = _gm.QuestionCount - _gm.IdeaCombineThreshold;
        float chance = _gm.IdeaCombineChance + (excess * 0.01f); // +1% per excess ?
        chance = Mathf.Clamp01(chance);

        if (Random.value < chance)
        {
            // ? 소모: 5~8개 랜덤
            float consumed = Random.Range(5, 9); // 5,6,7,8
            consumed = Mathf.Min(consumed, _gm.QuestionCount); // 보유량 초과 방지
            _gm.QuestionCount -= consumed;

            // ??? 전환 체크 먼저
            bool becameInsight = TryConvertToInsight(isOfflineBonus: false);

            if (!becameInsight)
            {
                _gm.IdeaCount += 1f;
                Debug.Log($"[ResourceTicker] ! 생성. 소모 ? {consumed:F1}개 | 보유 ? {_gm.QuestionCount:F1} | 보유 ! {_gm.IdeaCount:F1}");
            }
        }
    }

    #endregion

    // ─────────────────────────────────────────────────────────
    #region ③ ??? 전환 (Opportunistic Assimilation)

    /// <summary>
    /// ! 생성 시 ???로 전환을 시도합니다.
    /// GDD 6.3 수치:
    ///   - 기본 5~10% (랜덤 범위 사용)
    ///   - 오프라인 보너스는 OfflineIncubator에서 주입됩니다.
    /// </summary>
    /// <param name="isOfflineBonus">오프라인 보너스가 적용된 전환인지</param>
    /// <returns>???로 전환되었으면 true</returns>
    public bool TryConvertToInsight(bool isOfflineBonus)
    {
        // 기본 확률: 3~8% 범위에서 균등 랜덤
        float baseChance = Random.Range(0.03f, 0.08f);
        float totalChance = baseChance + _gm.InsightBonusChance;
        totalChance = Mathf.Clamp01(totalChance);

        if (Random.value < totalChance)
        {
            _gm.InsightCount += 1f;
            string tag = isOfflineBonus ? "[오프라인]" : "";
            Debug.Log($"[ResourceTicker] ??? 전환! {tag} 보유 ??? {_gm.InsightCount:F1}");
            return true;
        }

        return false;
    }

    #endregion

    // ─────────────────────────────────────────────────────────
    #region Public API (외부에서 틱 파라미터 재설정용)

    /// <summary>
    /// 업그레이드 적용 후 즉시 틱 간격을 재설정할 때 사용합니다.
    /// </summary>
    public void ResetTickInterval(float newInterval)
    {
        tickInterval = Mathf.Max(0.1f, newInterval);
        CancelInvoke(nameof(Tick));
        InvokeRepeating(nameof(Tick), tickInterval, tickInterval);
    }

    #endregion
}
