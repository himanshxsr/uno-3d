#nullable enable

using UnityEngine;
using Uno.Application.Events;

namespace Uno.Audio
{
    /// <summary>
    /// Lightweight event-driven audio router. Plays optional clips when assigned;
    /// always emits console feedback so the game remains playable without SFX assets.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [Header("Optional Clips")]
        [SerializeField] private AudioClip? _cardSlideClip;
        [SerializeField] private AudioClip? _cardFlipClip;
        [SerializeField] private AudioClip? _deckShuffleClip;
        [SerializeField] private AudioClip? _buttonClickClip;
        [SerializeField] private AudioClip? _unoShoutClip;
        [SerializeField] private AudioClip? _winFanfareClip;

        [SerializeField] private AudioSource? _sfxSource;

        private IEventBus? _eventBus;

        private void Awake()
        {
            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.playOnAwake = false;
                _sfxSource.spatialBlend = 0f;
            }
        }

        /// <summary>
        /// Binds gameplay events to SFX triggers.
        /// </summary>
        public void BindEventBus(IEventBus eventBus)
        {
            UnbindEventBus();
            _eventBus = eventBus;
            _eventBus.Subscribe<CardPlayedEvent>(OnCardPlayed);
            _eventBus.Subscribe<CardDrawnEvent>(OnCardDrawn);
            _eventBus.Subscribe<UnoDeclaredEvent>(OnUnoDeclared);
            _eventBus.Subscribe<RoundEndedEvent>(OnRoundEnded);
            _eventBus.Subscribe<ColorChangedEvent>(_ => PlayClip(_buttonClickClip));
        }

        public void UnbindEventBus()
        {
            if (_eventBus == null)
            {
                return;
            }

            _eventBus.Unsubscribe<CardPlayedEvent>(OnCardPlayed);
            _eventBus.Unsubscribe<CardDrawnEvent>(OnCardDrawn);
            _eventBus.Unsubscribe<UnoDeclaredEvent>(OnUnoDeclared);
            _eventBus.Unsubscribe<RoundEndedEvent>(OnRoundEnded);
            _eventBus = null;
        }

        private void OnCardPlayed(CardPlayedEvent evt)
        {
            PlayClip(evt.PlayerId < 0 ? _cardFlipClip : _cardSlideClip);
        }

        private void OnCardDrawn(CardDrawnEvent evt)
        {
            PlayClip(_cardSlideClip);
        }

        private void OnUnoDeclared(UnoDeclaredEvent evt)
        {
            PlayClip(_unoShoutClip);
        }

        private void OnRoundEnded(RoundEndedEvent evt)
        {
            PlayClip(_winFanfareClip);
        }

        /// <summary>
        /// Plays shuffle SFX during bootstrap/deal.
        /// </summary>
        public void PlayShuffle()
        {
            PlayClip(_deckShuffleClip);
        }

        private void PlayClip(AudioClip? clip)
        {
            if (clip == null || _sfxSource == null)
            {
                return;
            }

            _sfxSource.PlayOneShot(clip);
        }

        private void OnDestroy()
        {
            UnbindEventBus();
        }
    }
}
