using UnityEngine;

namespace IdleTycoon.Core
{
    /// <summary>
    /// 오디오 관리 싱글턴.
    ///
    /// BGM : mainClip을 Start에서 루프 재생.
    /// SFX : PlaySFX(clip)으로 단발 재생.
    ///
    /// EventBus 자동 구독:
    ///   OnOrderCompleted  → deliveryClip
    ///   OnMineSpeedUpgraded → upgradeCompleteClip
    ///
    /// 직접 호출 메서드:
    ///   PlayMachineSFX()        ← ProcessingMachine.ProduceOutput
    ///   PlaySpendMoneySFX()     ← CurrencyManager.SpendMoney, UpgradeZone, NpcSpawnZone
    ///   PlayMoneyGainSFX()      ← CurrencyManager.AddMoney
    ///   PlayUpgradeCompleteSFX()← UpgradeManager.TryUpgradeMiningTool
    ///   PlayMineSFX(clip)       ← PlayerInteractor.RefreshMiningSlots (MiningToolData.mineClip 전달)
    ///
    /// Hierarchy:
    ///   씬에 빈 GameObject → AudioManager 컴포넌트 부착.
    ///   AudioSource 2개 (bgmSource, sfxSource) 자동 생성 또는 Inspector 직접 할당.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        // ─── Singleton ──────────────────────────────────────────────

        public static AudioManager Instance { get; private set; }

        // ─── Inspector ──────────────────────────────────────────────

        [Header("BGM")]
        [SerializeField] private AudioClip mainClip;
        [SerializeField] [Range(0f, 1f)] private float bgmVolume  = 0.5f;

        [Header("SFX Clips")]
        [SerializeField] private AudioClip machineClip;
        [SerializeField] private AudioClip deliveryClip;
        [SerializeField] private AudioClip spendMoneyClip;
        [SerializeField] private AudioClip upgradeCompleteClip;
        [SerializeField] private AudioClip moneyGainClip;
        [SerializeField] private AudioClip getItemClip;
        [SerializeField] private AudioClip giveItemClip;
        [SerializeField] private AudioClip mineNPCClip;

        [Header("SFX Volume")]
        [SerializeField] [Range(0f, 1f)] private float sfxVolume  = 1f;

        // ─── Audio Sources ───────────────────────────────────────────

        private AudioSource _bgmSource;
        private AudioSource _sfxSource;

        // ─── Lifecycle ──────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SetupAudioSources();
        }

        private void Start()
        {
            if (mainClip != null)
            {
                _bgmSource.clip = mainClip;
                _bgmSource.Play();
            }
        }

        private void OnEnable()
        {
            EventBus.OnOrderCompleted     += HandleOrderCompleted;
            EventBus.OnMineSpeedUpgraded  += HandleMineSpeedUpgraded;
        }

        private void OnDisable()
        {
            EventBus.OnOrderCompleted     -= HandleOrderCompleted;
            EventBus.OnMineSpeedUpgraded  -= HandleMineSpeedUpgraded;
        }

        // ─── EventBus 핸들러 ─────────────────────────────────────────

        private void HandleOrderCompleted(int reward)   => PlaySFX(deliveryClip);
        private void HandleMineSpeedUpgraded(int level, float speed) => PlaySFX(upgradeCompleteClip);

        // ─── Public API — 직접 호출용 ────────────────────────────────

        /// <summary>ProcessingMachine.ProduceOutput에서 호출.</summary>
        public void PlayMachineSFX()         => PlaySFX(machineClip);

        /// <summary>CurrencyManager.SpendMoney 등에서 호출.</summary>
        public void PlaySpendMoneySFX()      => PlaySFX(spendMoneyClip);

        /// <summary>CurrencyManager.AddMoney에서 호출.</summary>
        public void PlayMoneyGainSFX()       => PlaySFX(moneyGainClip);

        /// <summary>UpgradeManager.TryUpgradeMiningTool 성공 시 호출.</summary>
        public void PlayUpgradeCompleteSFX() => PlaySFX(upgradeCompleteClip);
        public void PlayGetItemSFX()        => PlaySFX(getItemClip);
        public void PlayGiveItemSFX()           => PlaySFX(giveItemClip);
        public void PlayMineNPCSFX() => PlaySFX(mineNPCClip);

        /// <summary>
        /// PlayerInteractor.RefreshMiningSlots에서 호출.
        /// clip은 MiningToolData.mineClip을 전달한다.
        /// </summary>
        public void PlayMineSFX(AudioClip clip) => PlaySFX(clip);

        /// <summary>모든 named 메서드의 기반. clip이 null이면 무시.</summary>
        public void PlaySFX(AudioClip clip)
        {
            if (clip == null || _sfxSource == null) return;
            _sfxSource.PlayOneShot(clip, sfxVolume);
        }

        // ─── 초기화 ──────────────────────────────────────────────────

        private void SetupAudioSources()
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            
            // BGM Source
            _bgmSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();
            _bgmSource.loop     = true;
            _bgmSource.volume   = bgmVolume;
            _bgmSource.playOnAwake = false;

            // SFX Source
            _sfxSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();
            _sfxSource.loop     = false;
            _sfxSource.playOnAwake = false;
        }
        
        
    }
}
