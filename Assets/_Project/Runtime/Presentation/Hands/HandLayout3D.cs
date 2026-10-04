#nullable enable

using System.Collections.Generic;
using UnityEngine;
using Uno.Presentation.Cards;

namespace Uno.Presentation.Hands
{
    /// <summary>
    /// Fans cards on a parabolic arc and parents them under this seat anchor for stable local animation.
    /// </summary>
    public class HandLayout3D : MonoBehaviour
    {
        [SerializeField] private float _arcRadius = 4.0f;
        [SerializeField] private float _maxFanAngleDeg = 50.0f;
        [SerializeField] private float _cardAngularStepDeg = 9.0f;
        [SerializeField] private float _zDepthOffsetPerCard = 0.012f;
        [SerializeField] private float _uprightPitchAngleDeg = 58.0f;

        public float ArcRadius
        {
            get => _arcRadius;
            set => _arcRadius = Mathf.Max(0.1f, value);
        }

        public float MaxFanAngleDeg
        {
            get => _maxFanAngleDeg;
            set => _maxFanAngleDeg = Mathf.Clamp(value, 5f, 180f);
        }

        public float CardAngularStepDeg
        {
            get => _cardAngularStepDeg;
            set => _cardAngularStepDeg = Mathf.Max(0.5f, value);
        }

        public float UprightPitchAngleDeg
        {
            get => _uprightPitchAngleDeg;
            set => _uprightPitchAngleDeg = value;
        }

        public Vector3 CalculateCardPosition(int index, int totalCards)
        {
            if (totalCards <= 1)
            {
                return Vector3.zero;
            }

            float thetaRad = CalculateCardAngleRad(index, totalCards);
            float x = _arcRadius * Mathf.Sin(thetaRad);
            float y = -_arcRadius * (1.0f - Mathf.Cos(thetaRad));
            float z = -index * _zDepthOffsetPerCard;
            return new Vector3(x, y, z);
        }

        public Quaternion CalculateCardRotation(int index, int totalCards)
        {
            if (totalCards <= 1)
            {
                return Quaternion.Euler(_uprightPitchAngleDeg, 0f, 0f);
            }

            float thetaRad = CalculateCardAngleRad(index, totalCards);
            float angleDeg = thetaRad * Mathf.Rad2Deg;
            return Quaternion.Euler(_uprightPitchAngleDeg, 0f, -angleDeg);
        }

        public void ArrangeHand(IReadOnlyList<Card3DView> cards, float animateDuration = 0.35f)
        {
            if (cards == null || cards.Count == 0)
            {
                return;
            }

            int totalCards = cards.Count;
            for (int i = 0; i < totalCards; i++)
            {
                Card3DView cardView = cards[i];
                if (cardView == null)
                {
                    continue;
                }

                // Keep world pose while re-parenting, then tween in local space.
                cardView.transform.SetParent(transform, true);

                Vector3 localPos = CalculateCardPosition(i, totalCards);
                Quaternion localRot = CalculateCardRotation(i, totalCards);
                cardView.AnimateToLocal(localPos, localRot, animateDuration);
            }
        }

        private float CalculateCardAngleRad(int index, int totalCards)
        {
            if (totalCards <= 1)
            {
                return 0f;
            }

            float desiredSpanDeg = (totalCards - 1) * _cardAngularStepDeg;
            float totalSpanDeg = Mathf.Min(_maxFanAngleDeg, desiredSpanDeg);
            float stepDeg = totalSpanDeg / (totalCards - 1);
            float startAngleDeg = -totalSpanDeg * 0.5f;
            return (startAngleDeg + index * stepDeg) * Mathf.Deg2Rad;
        }
    }
}
