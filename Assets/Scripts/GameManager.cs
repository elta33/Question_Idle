using UnityEngine;

/// <summary>
/// 게임 상태 및 자원 수치를 보관하는 싱글턴입니다.
/// ResourceTicker, OfflineIncubator, UpgradeManager(Phase 3)가 이 클래스를 참조합니다.
///
/// ■ Phase 1 포함 항목
///   - 자원 수치 (?, !, ???, .)
///   - 자원 생산 파라미터 (생산속도, 임계점, 결합 확률 등)
///   - ??? 오프라인 보너스 확률 (OfflineIncubator → ResourceTicker 전달용)
/// </summary>
public class GameManager : MonoBehaviour
{
    // ── 싱글턴 ────────────────────────────────────────────────
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 자원 수치 ─────────────────────────────────────────────
    [Header("자원 (런타임에서 직접 확인 가능)")]
    [SerializeField] private float _questionCount = 0f;   // ?
    [SerializeField] private float _ideaCount     = 0f;   // !
    [SerializeField] private float _insightCount  = 0f;   // ???
    [SerializeField] private float _dotCount      = 0f;   // .

    public float QuestionCount
    {
        get => _questionCount;
        set => _questionCount = Mathf.Max(0f, value);
    }
    public float IdeaCount
    {
        get => _ideaCount;
        set => _ideaCount = Mathf.Max(0f, value);
    }
    public float InsightCount
    {
        get => _insightCount;
        set => _insightCount = Mathf.Max(0f, value);
    }
    public float DotCount
    {
        get => _dotCount;
        set => _dotCount = Mathf.Max(0f, value);
    }

    // ── ? 생산 파라미터 ───────────────────────────────────────
    [Header("? 생산 파라미터 (GDD 6.1)")]
    [Tooltip("초당 ? 생성 수. 기본 1/5 ≈ 0.2개/초 (= 1개/5초)")]
    [SerializeField] private float _questionRate = 1f / 5f;

    public float QuestionRate
    {
        get => _questionRate;
        set => _questionRate = Mathf.Max(0f, value);
    }

    // ── ! 결합 파라미터 ───────────────────────────────────────
    [Header("! 결합 파라미터 (GDD 6.2)")]
    [Tooltip("결합이 활성화되는 ? 최소 보유량. 기본 10.")]
    [SerializeField] private float _ideaCombineThreshold = 10f;

    [Tooltip("틱(1초)당 기본 결합 확률. 기본 0.08 (8%).")]
    [SerializeField] private float _ideaCombineChance = 0.08f;

    public float IdeaCombineThreshold
    {
        get => _ideaCombineThreshold;
        set => _ideaCombineThreshold = Mathf.Max(1f, value);
    }
    public float IdeaCombineChance
    {
        get => _ideaCombineChance;
        set => _ideaCombineChance = Mathf.Clamp(value, 0f, 1f);
    }

    // ── ??? 보너스 확률 ───────────────────────────────────────
    [Header("??? 보너스 (OfflineIncubator가 설정, ResourceTicker가 소비)")]
    [Tooltip("OfflineIncubator가 설정하는 임시 보너스 확률. ResourceTicker가 TryConvertToInsight에서 소비 후 0으로 초기화합니다.")]
    [SerializeField] private float _insightBonusChance = 0f;

    public float InsightBonusChance
    {
        get => _insightBonusChance;
        set => _insightBonusChance = Mathf.Clamp01(value);
    }

    /// <summary>
    /// ResourceTicker가 ??? 전환 시도 후 보너스를 소비하면 호출합니다.
    /// </summary>
    public void ConsumeInsightBonus()
    {
        _insightBonusChance = 0f;
    }
}
