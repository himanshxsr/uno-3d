#nullable enable

using System;
using System.Collections;
using UnityEngine;
using Uno.Core.Enums;
using Uno.Core.Models;
using Uno.Core.Rules;
using Uno.Presentation.Cards;
using Uno.Presentation.Table;

namespace Uno.Presentation.Input
{
    /// <summary>
    /// Player Input Manager casting 3D screen-space raycasts to detect card selections,
    /// validate rule legality, trigger card shake/glow warnings on invalid input, and handle draw stack clicks.
    /// Includes interactive keyboard fallbacks (Keys 1-7 for card play, Key D/Space for Draw, Key U for UNO).
    /// </summary>
    public class PlayerCardRaycaster : MonoBehaviour
    {
        [SerializeField] private Camera? _mainCamera;
        [SerializeField] private LayerMask _cardLayerMask = ~0;

        private IRuleValidator? _ruleValidator;
        private CardColor _activeColor = CardColor.Red;
        private Card _topDiscardCard;
        private bool _isInputEnabled = true;

        public event Action<Card3DView>? OnLegalCardClicked;
        public event Action<Card3DView>? OnIllegalCardClicked;
        public event Action<int>? OnKeyboardCardIndexRequested;
        public event Action? OnDrawPileClicked;
        public event Action? OnUnoRequested;

        private void Awake()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }
        }

        public void Configure(IRuleValidator ruleValidator, CardColor activeColor, Card topDiscardCard)
        {
            _ruleValidator = ruleValidator;
            _activeColor = activeColor;
            _topDiscardCard = topDiscardCard;
        }

        public void UpdateGameState(CardColor activeColor, Card topDiscardCard)
        {
            _activeColor = activeColor;
            _topDiscardCard = topDiscardCard;
        }

        public void SetInputEnabled(bool enabled)
        {
            _isInputEnabled = enabled;
        }

        private void Update()
        {
            if (!_isInputEnabled)
            {
                return;
            }

            // Mouse / Touch 3D Raycasting
            if (_mainCamera != null && UnityEngine.Input.GetMouseButtonDown(0))
            {
                HandlePointerClick(UnityEngine.Input.mousePosition);
            }

            // Keyboard Fallback Controls
            HandleKeyboardInput();
        }

        private void HandleKeyboardInput()
        {
            // Number keys 1-9 and 0 (index 9) for hand card selection
            for (int i = 0; i < 9; i++)
            {
                KeyCode numKey = KeyCode.Alpha1 + i;
                if (UnityEngine.Input.GetKeyDown(numKey))
                {
                    OnKeyboardCardIndexRequested?.Invoke(i);
                    return;
                }
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha0))
            {
                OnKeyboardCardIndexRequested?.Invoke(9);
                return;
            }

            // Key D or Space to Draw Card
            if (UnityEngine.Input.GetKeyDown(KeyCode.D) || UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                OnDrawPileClicked?.Invoke();
                return;
            }

            // Key U to call "UNO!"
            if (UnityEngine.Input.GetKeyDown(KeyCode.U))
            {
                OnUnoRequested?.Invoke();
            }
        }

        private void HandlePointerClick(Vector3 screenPosition)
        {
            Ray ray = _mainCamera!.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 50.0f, _cardLayerMask))
            {
                Card3DView cardView = hit.collider.GetComponent<Card3DView>();
                if (cardView != null)
                {
                    EvaluateCardClick(cardView);
                    return;
                }

                TableAnchorManager? anchorManager = TableAnchorManager.Instance;
                if (anchorManager != null && anchorManager.DrawPileAnchor != null)
                {
                    if (hit.transform == anchorManager.DrawPileAnchor || hit.transform.IsChildOf(anchorManager.DrawPileAnchor))
                    {
                        OnDrawPileClicked?.Invoke();
                    }
                }
            }
        }

        private void EvaluateCardClick(Card3DView cardView)
        {
            if (_ruleValidator == null)
            {
                OnLegalCardClicked?.Invoke(cardView);
                return;
            }

            bool isLegal = _ruleValidator.IsMoveLegal(cardView.CardData, _topDiscardCard, _activeColor);
            if (isLegal)
            {
                OnLegalCardClicked?.Invoke(cardView);
            }
            else
            {
                StartCoroutine(TriggerIllegalMoveFeedback(cardView));
                OnIllegalCardClicked?.Invoke(cardView);
            }
        }

        private IEnumerator TriggerIllegalMoveFeedback(Card3DView cardView)
        {
            cardView.SetHighlightGlow(true, new Color(0.9f, 0.1f, 0.2f, 1f));

            Vector3 originalPos = cardView.transform.localPosition;
            float elapsed = 0f;
            float shakeDuration = 0.25f;
            float shakeIntensity = 0.08f;

            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float offsetX = Mathf.Sin(elapsed * 50f) * shakeIntensity;
                cardView.transform.localPosition = originalPos + new Vector3(offsetX, 0f, 0f);
                yield return null;
            }

            cardView.transform.localPosition = originalPos;
            cardView.SetHighlightGlow(false);
        }
    }
}
