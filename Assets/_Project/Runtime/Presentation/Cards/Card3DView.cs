#nullable enable

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using Uno.Core.Enums;
using Uno.Core.Models;

namespace Uno.Presentation.Cards
{
    /// <summary>
    /// 3D card view: unlit suit coloring, compact labels, hover lift, and local/world tweens.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Card3DView : MonoBehaviour
    {
        [SerializeField] private MeshRenderer _frontRenderer = null!;
        [SerializeField] private MeshRenderer _backRenderer = null!;
        [SerializeField] private BoxCollider _cardCollider = null!;
        [SerializeField] private TextMeshPro? _frontCenterText;
        [SerializeField] private TextMeshPro? _backUnoText;

        [SerializeField] private float _hoverElevationY = 0.28f;
        [SerializeField] private float _hoverScaleMultiplier = 1.1f;
        [SerializeField] private float _hoverLerpSpeed = 14f;

        private Vector3 _baseLocalPosition;
        private Vector3 _targetLocalPosition;
        private Vector3 _baseScale = Vector3.one;
        private Coroutine? _movementCoroutine;
        private bool _isAnimating;

        public Card CardData { get; private set; }
        public bool IsHovered { get; private set; }
        public bool IsFaceUp { get; private set; } = true;

        public event Action<Card3DView>? OnCardClicked;
        public event Action<Card3DView, bool>? OnCardHoverStateChanged;

        private void Awake()
        {
            if (_cardCollider == null)
            {
                _cardCollider = GetComponent<BoxCollider>();
            }

            if (_frontRenderer == null)
            {
                _frontRenderer = GetComponentInChildren<MeshRenderer>();
            }

            _baseScale = transform.localScale.sqrMagnitude > 0.0001f ? transform.localScale : Vector3.one;
            EnsureProceduralTextLabels();
        }

        public void ConfigureRenderers(MeshRenderer frontRenderer, MeshRenderer backRenderer, BoxCollider collider)
        {
            _frontRenderer = frontRenderer;
            _backRenderer = backRenderer;
            _cardCollider = collider;
            EnsureProceduralTextLabels();
        }

        private void EnsureProceduralTextLabels()
        {
            // Face textures carry the readable rank. Keep optional TMP stubs for debugging only.
            if (_frontCenterText == null)
            {
                GameObject centerObj = new GameObject("FrontCenterText");
                centerObj.transform.SetParent(transform, false);
                centerObj.transform.localPosition = new Vector3(0f, 0f, -0.04f);
                centerObj.transform.localRotation = Quaternion.identity;
                centerObj.transform.localScale = Vector3.one * 0.08f;
                _frontCenterText = centerObj.AddComponent<TextMeshPro>();
                _frontCenterText.fontSize = 8f;
                _frontCenterText.alignment = TextAlignmentOptions.Center;
                _frontCenterText.textWrappingMode = TextWrappingModes.NoWrap;
                AssignTmpFont(_frontCenterText);
                centerObj.SetActive(false);
            }

            if (_backUnoText == null)
            {
                GameObject backObj = new GameObject("BackUnoText");
                backObj.transform.SetParent(transform, false);
                backObj.transform.localPosition = new Vector3(0f, 0f, 0.04f);
                backObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                backObj.transform.localScale = Vector3.one * 0.08f;
                _backUnoText = backObj.AddComponent<TextMeshPro>();
                _backUnoText.fontSize = 8f;
                _backUnoText.alignment = TextAlignmentOptions.Center;
                _backUnoText.text = "UNO";
                _backUnoText.textWrappingMode = TextWrappingModes.NoWrap;
                AssignTmpFont(_backUnoText);
                backObj.SetActive(false);
            }
        }

        private static void AssignTmpFont(TextMeshPro tmp)
        {
            if (TMP_Settings.defaultFontAsset != null)
            {
                tmp.font = TMP_Settings.defaultFontAsset;
            }
        }

        public void Initialize(Card data, Texture2D? frontTexture = null)
        {
            CardData = data;
            EnsureProceduralTextLabels();

            Texture2D face = frontTexture != null ? frontTexture : CardFaceBaker.GetFaceTexture(data);

            if (_frontRenderer != null)
            {
                // Unique material instance so color/texture changes never leak across pooled cards.
                Material mat = _frontRenderer.material;
                CardFactory.SetMaterialTexture(mat, face);
                CardFactory.SetMaterialColor(mat, Color.white);
            }

            if (_backRenderer != null)
            {
                Material backMat = _backRenderer.material;
                CardFactory.SetMaterialTexture(backMat, CardFaceBaker.GetBackTexture());
                CardFactory.SetMaterialColor(backMat, Color.white);
            }

            UpdateCardTextLabel(data);
            _baseLocalPosition = transform.localPosition;
            _targetLocalPosition = _baseLocalPosition;
            _baseScale = transform.localScale.sqrMagnitude > 0.0001f ? transform.localScale : Vector3.one;
            SetFaceUp(IsFaceUp);
        }

        public void SetVisualScale(float scale)
        {
            _baseScale = Vector3.one * Mathf.Max(0.1f, scale);
            transform.localScale = _baseScale;
        }

        private void UpdateCardTextLabel(Card data)
        {
            if (_frontCenterText == null)
            {
                return;
            }

            // Texture already carries the rank; keep TMP as a faint backup only.
            _frontCenterText.text = CardFaceBaker.GetShortLabel(data);
            _frontCenterText.color = new Color(1f, 1f, 1f, 0.01f);
        }

        public static Color GetProceduralSuitColor(CardColor color)
        {
            return CardFaceBaker.GetSuitColor(color);
        }

        public void SetBaseTransform(Vector3 localPosition, Quaternion localRotation)
        {
            _baseLocalPosition = localPosition;
            _targetLocalPosition = localPosition;
            transform.localRotation = localRotation;
        }

        public void SetFaceUp(bool showFront)
        {
            IsFaceUp = showFront;
            if (_frontRenderer != null)
            {
                _frontRenderer.enabled = showFront;
            }

            if (_backRenderer != null)
            {
                _backRenderer.enabled = !showFront;
            }

            if (_frontCenterText != null)
            {
                _frontCenterText.gameObject.SetActive(showFront);
            }

            if (_backUnoText != null)
            {
                _backUnoText.gameObject.SetActive(!showFront);
            }
        }

        public void SetHighlightGlow(bool isHighlighted, Color highlightColor = default)
        {
            if (_frontRenderer == null)
            {
                return;
            }

            // Faces are textured; keep albedo near-white and only tint for highlight.
            if (isHighlighted)
            {
                Color glow = highlightColor == default ? new Color(0.55f, 1f, 0.7f, 1f) : highlightColor;
                CardFactory.SetMaterialColor(_frontRenderer.material, Color.Lerp(Color.white, glow, 0.35f));
            }
            else
            {
                CardFactory.SetMaterialColor(_frontRenderer.material, Color.white);
            }
        }

        public void AnimateTo(Vector3 targetWorldPos, Quaternion targetWorldRot, float duration = 0.35f)
        {
            if (_movementCoroutine != null)
            {
                StopCoroutine(_movementCoroutine);
            }

            if (duration <= 0f)
            {
                _isAnimating = false;
                transform.position = targetWorldPos;
                transform.rotation = targetWorldRot;
                SetBaseTransform(transform.localPosition, transform.localRotation);
                return;
            }

            _isAnimating = true;
            _movementCoroutine = StartCoroutine(AnimateWorldRoutine(targetWorldPos, targetWorldRot, duration));
        }

        /// <summary>
        /// Animates in local space relative to the current parent (hand anchor).
        /// </summary>
        public void AnimateToLocal(Vector3 targetLocalPos, Quaternion targetLocalRot, float duration = 0.35f)
        {
            if (_movementCoroutine != null)
            {
                StopCoroutine(_movementCoroutine);
            }

            if (duration <= 0f)
            {
                _isAnimating = false;
                transform.localPosition = targetLocalPos;
                transform.localRotation = targetLocalRot;
                SetBaseTransform(targetLocalPos, targetLocalRot);
                return;
            }

            _isAnimating = true;
            _movementCoroutine = StartCoroutine(AnimateLocalRoutine(targetLocalPos, targetLocalRot, duration));
        }

        private IEnumerator AnimateWorldRoutine(Vector3 targetWorldPos, Quaternion targetWorldRot, float duration)
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                transform.position = Vector3.Lerp(startPos, targetWorldPos, t);
                transform.rotation = Quaternion.Slerp(startRot, targetWorldRot, t);
                yield return null;
            }

            transform.position = targetWorldPos;
            transform.rotation = targetWorldRot;
            SetBaseTransform(transform.localPosition, transform.localRotation);
            _isAnimating = false;
            _movementCoroutine = null;
        }

        private IEnumerator AnimateLocalRoutine(Vector3 targetLocalPos, Quaternion targetLocalRot, float duration)
        {
            Vector3 startPos = transform.localPosition;
            Quaternion startRot = transform.localRotation;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                transform.localPosition = Vector3.Lerp(startPos, targetLocalPos, t);
                transform.localRotation = Quaternion.Slerp(startRot, targetLocalRot, t);
                yield return null;
            }

            transform.localPosition = targetLocalPos;
            transform.localRotation = targetLocalRot;
            SetBaseTransform(targetLocalPos, targetLocalRot);
            _isAnimating = false;
            _movementCoroutine = null;
        }

        private void OnMouseEnter()
        {
            if (!IsFaceUp)
            {
                return;
            }

            IsHovered = true;
            _targetLocalPosition = _baseLocalPosition + Vector3.up * _hoverElevationY;
            OnCardHoverStateChanged?.Invoke(this, true);
        }

        private void OnMouseExit()
        {
            IsHovered = false;
            _targetLocalPosition = _baseLocalPosition;
            OnCardHoverStateChanged?.Invoke(this, false);
        }

        private void OnMouseDown()
        {
            OnCardClicked?.Invoke(this);
        }

        private void Update()
        {
            if (!_isAnimating && Vector3.Distance(transform.localPosition, _targetLocalPosition) > 0.001f)
            {
                transform.localPosition = Vector3.Lerp(
                    transform.localPosition,
                    _targetLocalPosition,
                    Time.deltaTime * _hoverLerpSpeed);
            }

            Vector3 targetScale = IsHovered ? _baseScale * _hoverScaleMultiplier : _baseScale;
            if (Vector3.Distance(transform.localScale, targetScale) > 0.001f)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * _hoverLerpSpeed);
            }
        }
    }
}
