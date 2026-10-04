#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Uno.Core.Models;

namespace Uno.Presentation.Cards
{
    /// <summary>
    /// Prefab factory and object pooling manager for 3D Card views.
    /// Uses Unlit materials + baked face textures so ranks stay readable.
    /// </summary>
    public class CardFactory : MonoBehaviour
    {
        [Header("Prefab & Visual Assets")]
        [SerializeField] private GameObject? _cardPrefab;
        [SerializeField] private CardVisualProvider? _visualProvider;

        [Header("Pool Config")]
        [SerializeField] private int _defaultPoolCapacity = 108;
        [SerializeField] private int _maxPoolSize = 120;

        private IObjectPool<Card3DView>? _pool;
        private readonly List<Card3DView> _activeCards = new List<Card3DView>(108);
        private Shader? _cardShader;

        public CardVisualProvider? VisualProvider
        {
            get => _visualProvider;
            set => _visualProvider = value;
        }

        private void Awake()
        {
            _cardShader = ResolveCardShader();
            InitializePool();
        }

        private void InitializePool()
        {
            _pool = new ObjectPool<Card3DView>(
                createFunc: CreateCardInstance,
                actionOnGet: OnGetFromPool,
                actionOnRelease: OnReleaseToPool,
                actionOnDestroy: OnDestroyPoolItem,
                collectionCheck: true,
                defaultCapacity: _defaultPoolCapacity,
                maxSize: _maxPoolSize
            );
        }

        private Card3DView CreateCardInstance()
        {
            GameObject instance;
            if (_cardPrefab != null)
            {
                instance = Instantiate(_cardPrefab, transform);
                return instance.GetComponent<Card3DView>() ?? instance.AddComponent<Card3DView>();
            }

            instance = new GameObject("Card3D_Instance");
            instance.transform.SetParent(transform);

            const float cardW = 1.25f;
            const float cardH = 1.8f;

            BoxCollider boxCol = instance.AddComponent<BoxCollider>();
            boxCol.size = new Vector3(cardW, cardH, 0.04f);

            // Quads give clean UVs for painted faces; thin cube adds side thickness.
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "CardBody";
            body.transform.SetParent(instance.transform, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(cardW, cardH, 0.03f);
            Destroy(body.GetComponent<Collider>());
            MeshRenderer bodyRenderer = body.GetComponent<MeshRenderer>();

            GameObject frontObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            frontObj.name = "FrontFace";
            frontObj.transform.SetParent(instance.transform, false);
            frontObj.transform.localPosition = new Vector3(0f, 0f, -0.018f);
            frontObj.transform.localRotation = Quaternion.identity;
            frontObj.transform.localScale = new Vector3(cardW, cardH, 1f);
            Destroy(frontObj.GetComponent<Collider>());

            GameObject backObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backObj.name = "BackFace";
            backObj.transform.SetParent(instance.transform, false);
            backObj.transform.localPosition = new Vector3(0f, 0f, 0.018f);
            backObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            backObj.transform.localScale = new Vector3(cardW, cardH, 1f);
            Destroy(backObj.GetComponent<Collider>());

            _cardShader ??= ResolveCardShader();
            if (_cardShader == null)
            {
                throw new InvalidOperationException("No usable shader found for UNO cards.");
            }

            Material bodyMat = new Material(_cardShader);
            Material frontMat = new Material(_cardShader);
            Material backMat = new Material(_cardShader);
            SetMaterialColor(bodyMat, new Color(0.92f, 0.92f, 0.94f, 1f));
            SetMaterialTexture(backMat, CardFaceBaker.GetBackTexture());
            SetMaterialColor(backMat, Color.white);

            bodyRenderer.sharedMaterial = bodyMat;
            MeshRenderer frontRenderer = frontObj.GetComponent<MeshRenderer>();
            frontRenderer.sharedMaterial = frontMat;
            MeshRenderer backRenderer = backObj.GetComponent<MeshRenderer>();
            backRenderer.sharedMaterial = backMat;

            Card3DView view = instance.AddComponent<Card3DView>();
            view.ConfigureRenderers(frontRenderer, backRenderer, boxCol);
            return view;
        }

        private void OnGetFromPool(Card3DView view)
        {
            view.gameObject.SetActive(true);
            _activeCards.Add(view);
        }

        private void OnReleaseToPool(Card3DView view)
        {
            view.gameObject.SetActive(false);
            view.transform.SetParent(transform, false);
            _activeCards.Remove(view);
        }

        private void OnDestroyPoolItem(Card3DView view)
        {
            if (view != null && view.gameObject != null)
            {
                Destroy(view.gameObject);
            }
        }

        public Card3DView GetCardView(Card cardData, Vector3 spawnPosition, Quaternion spawnRotation)
        {
            if (_pool == null)
            {
                InitializePool();
            }

            Card3DView view = _pool!.Get();
            view.transform.SetParent(transform, false);
            view.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            view.transform.localScale = Vector3.one;

            // Prefer baked readable faces; fall back to provider textures only if present.
            Texture2D frontTexture = _visualProvider != null
                ? _visualProvider.GetCardFrontTexture(cardData) ?? CardFaceBaker.GetFaceTexture(cardData)
                : CardFaceBaker.GetFaceTexture(cardData);

            view.Initialize(cardData, frontTexture);
            return view;
        }

        public void ReleaseCardView(Card3DView cardView)
        {
            if (cardView != null && _pool != null)
            {
                _pool.Release(cardView);
            }
        }

        public void ReleaseAllActiveCards()
        {
            if (_pool == null)
            {
                return;
            }

            for (int i = _activeCards.Count - 1; i >= 0; i--)
            {
                _pool.Release(_activeCards[i]);
            }

            _activeCards.Clear();
        }

        private static Shader? ResolveCardShader()
        {
            string[] candidates =
            {
                "Universal Render Pipeline/Unlit",
                "Unlit/Texture",
                "Unlit/Color",
                "Universal Render Pipeline/Lit",
                "Sprites/Default",
                "UI/Default",
                "Standard"
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                Shader? shader = Shader.Find(candidates[i]);
                if (shader != null)
                {
                    return shader;
                }
            }

            return null;
        }

        public static void SetMaterialColor(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", color);
            }

            mat.color = color;
        }

        public static void SetMaterialTexture(Material mat, Texture2D texture)
        {
            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", texture);
            }

            if (mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", texture);
            }
        }
    }
}
