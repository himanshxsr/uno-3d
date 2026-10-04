#nullable enable

using UnityEngine;
using Uno.Presentation.Hands;

namespace Uno.Presentation.Table
{
    /// <summary>
    /// Singleton scene manager providing Transform spatial anchors for 3D card dealing trajectories,
    /// player hand zones, center draw/discard stacks, and opponent seating positions.
    /// </summary>
    public class TableAnchorManager : MonoBehaviour
    {
        [Header("Center Table Anchors")]
        [SerializeField] private Transform _drawPileAnchor = null!;
        [SerializeField] private Transform _discardPileAnchor = null!;

        [Header("Player Seating Anchors")]
        [SerializeField] private Transform _playerHandAnchor = null!;
        [SerializeField] private Transform _bot1Anchor = null!; // Left seat
        [SerializeField] private Transform _bot2Anchor = null!; // Top seat
        [SerializeField] private Transform _bot3Anchor = null!; // Right seat

        /// <summary>
        /// Singleton instance reference.
        /// </summary>
        public static TableAnchorManager? Instance { get; private set; }

        // Prevent redundant re-initialization on every property getter call.
        // Full position correction happens once in Awake(); all subsequent calls early-return.
        private bool _initialized;

        public Transform DrawPileAnchor
        {
            get
            {
                EnsureAnchorsInitialized();
                return _drawPileAnchor;
            }
        }

        public Transform DiscardPileAnchor
        {
            get
            {
                EnsureAnchorsInitialized();
                return _discardPileAnchor;
            }
        }

        public Transform PlayerHandAnchor
        {
            get
            {
                EnsureAnchorsInitialized();
                return _playerHandAnchor;
            }
        }

        public Transform Bot1Anchor
        {
            get
            {
                EnsureAnchorsInitialized();
                return _bot1Anchor;
            }
        }

        public Transform Bot2Anchor
        {
            get
            {
                EnsureAnchorsInitialized();
                return _bot2Anchor;
            }
        }

        public Transform Bot3Anchor
        {
            get
            {
                EnsureAnchorsInitialized();
                return _bot3Anchor;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _initialized = false;          // Force full position correction on first call
            EnsureAnchorsInitialized();
        }

        public void EnsureAnchorsInitialized()
        {
            // Only run the full anchor correction ONCE per play session (in Awake).
            // Subsequent calls from property getters during dealing would otherwise run
            // 56+ times, each calling transform.Find() 6× = 336+ hierarchy searches.
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            // Always call CreateOrFindChild — it finds existing child GameObjects AND corrects
            // their localPosition. Without this, stale serialized positions (from old wizard runs)
            // silently survive into Play mode and place cards underground (Y < 0).

            _drawPileAnchor    = CreateOrFindChild("DrawPileAnchor",    new Vector3(-1.2f, 0.05f,  0f  ), Quaternion.identity);
            _discardPileAnchor = CreateOrFindChild("DiscardPileAnchor", new Vector3( 1.2f, 0.05f,  0f  ), Quaternion.identity);

            // Player hand — closer for portrait phone framing
            _playerHandAnchor = CreateOrFindChild("PlayerHandAnchor", new Vector3(0f, 0.55f, -3.6f), Quaternion.identity);
            HandLayout3D playerLayout = _playerHandAnchor.GetComponent<HandLayout3D>() ?? _playerHandAnchor.gameObject.AddComponent<HandLayout3D>();
            playerLayout.UprightPitchAngleDeg = 62.0f;
            playerLayout.ArcRadius = 3.2f;
            playerLayout.MaxFanAngleDeg = 48.0f;
            playerLayout.CardAngularStepDeg = 8.0f;

            _bot1Anchor = CreateOrFindChild("Bot1Anchor_Left", new Vector3(-3.6f, 0.5f, 0.6f), Quaternion.Euler(0f, 90f, 0f));
            HandLayout3D bot1Layout = _bot1Anchor.GetComponent<HandLayout3D>() ?? _bot1Anchor.gameObject.AddComponent<HandLayout3D>();
            bot1Layout.UprightPitchAngleDeg = 50.0f;
            bot1Layout.ArcRadius = 1.8f;
            bot1Layout.MaxFanAngleDeg = 40.0f;
            bot1Layout.CardAngularStepDeg = 7.0f;

            _bot2Anchor = CreateOrFindChild("Bot2Anchor_Top", new Vector3(0f, 0.5f, 3.4f), Quaternion.Euler(0f, 180f, 0f));
            HandLayout3D bot2Layout = _bot2Anchor.GetComponent<HandLayout3D>() ?? _bot2Anchor.gameObject.AddComponent<HandLayout3D>();
            bot2Layout.UprightPitchAngleDeg = 50.0f;
            bot2Layout.ArcRadius = 1.8f;
            bot2Layout.MaxFanAngleDeg = 40.0f;
            bot2Layout.CardAngularStepDeg = 7.0f;

            _bot3Anchor = CreateOrFindChild("Bot3Anchor_Right", new Vector3(3.6f, 0.5f, 0.6f), Quaternion.Euler(0f, -90f, 0f));
            HandLayout3D bot3Layout = _bot3Anchor.GetComponent<HandLayout3D>() ?? _bot3Anchor.gameObject.AddComponent<HandLayout3D>();
            bot3Layout.UprightPitchAngleDeg = 50.0f;
            bot3Layout.ArcRadius = 1.8f;
            bot3Layout.MaxFanAngleDeg = 40.0f;
            bot3Layout.CardAngularStepDeg = 7.0f;

            Debug.Log($"[UNO 3D] TableAnchorManager: all anchors corrected. Player hand = {_playerHandAnchor.position}");
        }


        private Transform CreateOrFindChild(string childName, Vector3 localPos, Quaternion localRot)
        {
            Transform? existing = transform.Find(childName);
            if (existing != null)
            {
                existing.localPosition = localPos;
                existing.localRotation = localRot;
                return existing;
            }

            GameObject childObj = new GameObject(childName);
            childObj.transform.SetParent(transform, false);
            childObj.transform.localPosition = localPos;
            childObj.transform.localRotation = localRot;
            return childObj.transform;
        }

        /// <summary>
        /// Returns the transform anchor for a given seat index (0 = Human Player, 1 = Left Bot 1, 2 = Top Bot 2, 3 = Right Bot 3).
        /// </summary>
        public Transform GetSeatAnchor(int seatIndex)
        {
            EnsureAnchorsInitialized();
            return seatIndex switch
            {
                0 => _playerHandAnchor,
                1 => _bot1Anchor,
                2 => _bot2Anchor,
                3 => _bot3Anchor,
                _ => _playerHandAnchor
            };
        }
    }
}
