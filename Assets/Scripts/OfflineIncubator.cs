using UnityEngine;

/// <summary>
/// 오프라인 부화기 (GDD 4.1)
///
/// 앱이 꺼진 시간 동안 쌓인 자원을 재접속 시 일괄 지급합니다.
/// 세계관적 의미: 부화(Incubation) 이론 + DMN 휴식 시 처리의 실체화.
///
/// ■ 수치 (GDD 4.1 기준)
///   오프라인 생산 효율  : 온라인의 65%
///   최대 누적 한도      : 8시간 (소프트 캡)
///   계산식              : 경과시간(초) × 현재 생산속도 × 0.65  (8h 초과분 버림)
///   8h 초과 보너스      : ??? 전환 확률 추가 상승 (InsightBonusChance에 임시 적용)
///
/// ■ 의존
///   GameManager   (자원 수치 및 파라미터 보관)
///   ResourceTicker (??? 전환 시도 위임)
///
/// ■ 부착 위치
///   GameManager와 동일한 GameObject 권장
/// </summary>
public class OfflineIncubator : MonoBehaviour
{
    // ── 상수 ──────────────────────────────────────────────────
    private const string LastQuitKey   = "OfflineIncubator_LastQuitTime";

    private const float OfflineEfficiency = 0.5f;    // GDD: 온라인의 50%
    private const float MaxOfflineHours   = 8f;       // GDD: 소프트 캡 8시간
    private const float MaxOfflineSec     = MaxOfflineHours * 3600f;

    // ── 오프라인 보너스 임계 (GDD 6.3) ──────────────────────
    // 구조: (최소 오프라인 초, 추가 확률)
    private static readonly (float seconds, float bonus)[] InsightBonusTiers =
    {
        (8f   * 3600f, 0.08f),  // 8h 이상  → +8%
        (4f   * 3600f, 0.05f),  // 4h 이상  → +5%
        (1f   * 3600f, 0.02f),  // 1h 이상  → +2%
    };

    // ── Inspector 노출 ────────────────────────────────────────
    [Header("디버그")]
    [Tooltip("활성화 시 오프라인 경과 시간을 에디터에서 임의로 주입할 수 있습니다.")]
    [SerializeField] private bool debugOverride = false;

    [Tooltip("debugOverride가 true일 때 사용할 오프라인 경과 시간 (초)")]
    [SerializeField] private float debugElapsedSeconds = 0f;

    // ── 내부 참조 ─────────────────────────────────────────────
    private GameManager    _gm;
    private ResourceTicker _ticker;

    // ── 결과 캐시 (로그/UI용) ─────────────────────────────────
    private OfflineResult _lastResult;

    // ─────────────────────────────────────────────────────────
    #region Unity Lifecycle

    private void Awake()
    {
        _gm     = GameManager.Instance;
        _ticker = GetComponent<ResourceTicker>();

        if (_gm == null)
            Debug.LogError("[OfflineIncubator] GameManager 인스턴스를 찾을 수 없습니다.");
        if (_ticker == null)
            Debug.LogWarning("[OfflineIncubator] ResourceTicker를 찾을 수 없습니다. ??? 전환이 생략됩니다.");
    }

    private void Start()
    {
        ApplyOfflineGain();
    }

    // ── 앱 일시정지 / 종료 감지 ──────────────────────────────

    private void OnApplicationPause(bool pause)
    {
        if (pause)  SaveQuitTime();
        else        ApplyOfflineGain();   // 백그라운드 복귀 시에도 처리
    }

    private void OnApplicationQuit()
    {
        SaveQuitTime();
    }

    #endregion

    // ─────────────────────────────────────────────────────────
    #region 핵심 로직

    /// <summary>
    /// 현재 시각을 PlayerPrefs에 저장합니다.
    /// 저장 형식: ISO 8601 (yyyy-MM-ddTHH:mm:ss)
    /// </summary>
    private void SaveQuitTime()
    {
        string now = System.DateTime.UtcNow.ToString("o"); // Round-trip format
        PlayerPrefs.SetString(LastQuitKey, now);
        PlayerPrefs.Save();
        Debug.Log($"[OfflineIncubator] 종료 시각 저장: {now}");
    }

    /// <summary>
    /// 저장된 종료 시각과 현재 시각 차이로 오프라인 자원을 계산하고 지급합니다.
    /// </summary>
    private void ApplyOfflineGain()
    {
        if (_gm == null) return;

        float elapsed = GetElapsedSeconds();
        if (elapsed < 1f) return;   // 1초 미만은 무시

        _lastResult = Calculate(elapsed);
        Apply(_lastResult);
        LogResult(_lastResult);

        // 처리 후 현재 시각으로 갱신 (재진입 방지)
        SaveQuitTime();
    }

    /// <summary>
    /// 경과 시간(초)을 반환합니다.
    /// debugOverride가 켜져 있으면 debugElapsedSeconds를 사용합니다.
    /// </summary>
    private float GetElapsedSeconds()
    {
        if (debugOverride)
        {
            Debug.LogWarning($"[OfflineIncubator] DEBUG 모드: {debugElapsedSeconds}초 경과 주입");
            return debugElapsedSeconds;
        }

        string savedStr = PlayerPrefs.GetString(LastQuitKey, string.Empty);
        if (string.IsNullOrEmpty(savedStr)) return 0f;

        if (!System.DateTime.TryParse(savedStr, null,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out System.DateTime lastQuit))
        {
            Debug.LogWarning("[OfflineIncubator] 저장된 시각을 파싱할 수 없습니다.");
            return 0f;
        }

        float elapsed = (float)(System.DateTime.UtcNow - lastQuit).TotalSeconds;
        return Mathf.Max(0f, elapsed);
    }

    /// <summary>
    /// 경과 시간을 바탕으로 지급할 자원과 보너스를 계산합니다.
    /// </summary>
    private OfflineResult Calculate(float rawElapsed)
    {
        // ── 소프트 캡 적용 ──────────────────────────────────
        bool exceeded8h       = rawElapsed > MaxOfflineSec;
        float cappedElapsed   = Mathf.Min(rawElapsed, MaxOfflineSec);

        // ── ? 생산량 계산 ──────────────────────────────────
        // 공식: 경과시간(초) × 현재 생산속도 × 0.65
        float questionGain    = cappedElapsed * _gm.QuestionRate * OfflineEfficiency;

        // ── ! 결합 시뮬레이션 ─────────────────────────────
        // 틱 단위 시뮬레이션 대신 확률 기댓값으로 근사합니다.
        // (모바일 재접속 시 무거운 루프 방지)
        float ticks           = cappedElapsed; // 1틱 = 1초
        float effectiveQ      = _gm.QuestionCount + questionGain;
        float ideaGain        = 0f;

        if (effectiveQ >= _gm.IdeaCombineThreshold)
        {
            float excess     = effectiveQ - _gm.IdeaCombineThreshold;
            float avgChance  = _gm.IdeaCombineChance + (excess * 0.01f * 0.5f); // 평균 보정
            avgChance        = Mathf.Clamp01(avgChance);
            ideaGain         = ticks * avgChance;

            // ! 생성에 소모된 ? 를 빼줍니다 (평균 소모 6.5개 기준)
            float consumedQ  = ideaGain * 6.5f;
            questionGain     = Mathf.Max(0f, questionGain - consumedQ);
        }

        // ── ??? 전환 횟수 ─────────────────────────────────
        float insightBonusChance = GetInsightBonusChance(rawElapsed, exceeded8h);
        float insightGain        = 0f;
        if (ideaGain > 0f)
        {
            float baseInsightChance = 0.075f; // 5~10% 중간값
            float totalInsightChance = Mathf.Clamp01(baseInsightChance + insightBonusChance);
            insightGain = ideaGain * totalInsightChance;
            ideaGain   -= insightGain; // ??? 전환된 만큼 ! 감소
        }

        return new OfflineResult
        {
            RawElapsedSec     = rawElapsed,
            CappedElapsedSec  = cappedElapsed,
            Exceeded8Hours    = exceeded8h,
            QuestionGain      = questionGain,
            IdeaGain          = ideaGain,
            InsightGain       = insightGain,
            InsightBonusChance = insightBonusChance,
        };
    }

    /// <summary>
    /// 계산된 결과를 GameManager 자원에 적용합니다.
    /// </summary>
    private void Apply(OfflineResult r)
    {
        _gm.QuestionCount += r.QuestionGain;
        _gm.IdeaCount     += r.IdeaGain;
        _gm.InsightCount  += r.InsightGain;

        // 8시간 초과 시 다음 온라인 틱에서 ??? 확률 보너스 임시 적용
        // ResourceTicker.TryConvertToInsight()가 이 값을 참조합니다.
        if (r.Exceeded8Hours)
            _gm.InsightBonusChance += r.InsightBonusChance;
    }

    /// <summary>
    /// GDD 6.3 기준 오프라인 시간에 따른 ??? 확률 보너스를 반환합니다.
    /// </summary>
    private float GetInsightBonusChance(float elapsed, bool exceeded8h)
    {
        foreach (var tier in InsightBonusTiers)
        {
            if (elapsed >= tier.seconds)
                return tier.bonus;
        }
        return 0f;
    }

    #endregion

    // ─────────────────────────────────────────────────────────
    #region 로그

    private void LogResult(OfflineResult r)
    {
        string hours   = FormatTime(r.CappedElapsedSec);
        string capNote = r.Exceeded8Hours ? $" (실제 경과 {FormatTime(r.RawElapsedSec)}, 8h 캡 적용)" : "";

        Debug.Log(
            $"[OfflineIncubator] 오프라인 보상 지급\n" +
            $"  경과 시간   : {hours}{capNote}\n" +
            $"  ? 지급      : +{r.QuestionGain:F1}\n" +
            $"  ! 지급      : +{r.IdeaGain:F1}\n" +
            $"  ??? 지급    : +{r.InsightGain:F1}\n" +
            $"  ??? 보너스  : +{r.InsightBonusChance * 100f:F0}%"
        );
    }

    private string FormatTime(float seconds)
    {
        int h = Mathf.FloorToInt(seconds / 3600);
        int m = Mathf.FloorToInt((seconds % 3600) / 60);
        int s = Mathf.FloorToInt(seconds % 60);
        return $"{h:D2}h {m:D2}m {s:D2}s";
    }

    #endregion

    // ─────────────────────────────────────────────────────────
    #region Public API

    /// <summary>
    /// 외부(UI 버튼 등)에서 마지막 오프라인 결과를 참조할 때 사용합니다.
    /// </summary>
    public OfflineResult LastResult => _lastResult;

    #endregion

    // ─────────────────────────────────────────────────────────
    #region 결과 구조체

    /// <summary>
    /// Calculate()의 반환값. UI 표시나 단위 테스트에 재사용 가능합니다.
    /// </summary>
    public struct OfflineResult
    {
        public float RawElapsedSec;
        public float CappedElapsedSec;
        public bool  Exceeded8Hours;
        public float QuestionGain;
        public float IdeaGain;
        public float InsightGain;
        public float InsightBonusChance;
    }

    #endregion
}
