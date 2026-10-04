#nullable enable

using UnityEngine;

namespace Uno.Presentation.CameraControl
{
    /// <summary>
    /// Frames the table + player hand for landscape desktop and portrait phone simulator.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ResponsiveCameraController : MonoBehaviour
    {
        [Header("Landscape Preset (Aspect >= 1.0)")]
        [SerializeField] private Vector3 _landscapePosition = new Vector3(0f, 6.8f, -5.2f);
        [SerializeField] private Vector3 _landscapeRotation = new Vector3(52f, 0f, 0f);
        [SerializeField] private float _landscapeFov = 60f;

        [Header("Portrait Preset (Aspect < 1.0)")]
        [SerializeField] private Vector3 _portraitPosition = new Vector3(0f, 9.2f, -7.4f);
        [SerializeField] private Vector3 _portraitRotation = new Vector3(54f, 0f, 0f);
        [SerializeField] private float _portraitFov = 58f;

        [Header("Lerp Speed")]
        [SerializeField] private float _adaptSpeed = 12f;

        private Camera _cam = null!;
        private float _lastAspect = -1f;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            AdaptCameraToAspect(forceImmediate: true);
        }

        private void LateUpdate()
        {
            float currentAspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            bool aspectChanged = Mathf.Abs(currentAspect - _lastAspect) > 0.01f;
            AdaptCameraToAspect(forceImmediate: aspectChanged && _lastAspect < 0f);
            if (aspectChanged)
            {
                _lastAspect = currentAspect;
            }
            else
            {
                AdaptCameraToAspect(forceImmediate: false);
            }
        }

        public void AdaptCameraToAspect(bool forceImmediate = false)
        {
            if (_cam == null)
            {
                _cam = GetComponent<Camera>();
            }

            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            bool isPortrait = aspect < 1.0f;

            Vector3 targetPos = isPortrait ? _portraitPosition : _landscapePosition;
            Quaternion targetRot = Quaternion.Euler(isPortrait ? _portraitRotation : _landscapeRotation);
            float targetFov = isPortrait ? _portraitFov : _landscapeFov;

            if (forceImmediate)
            {
                transform.position = targetPos;
                transform.rotation = targetRot;
                _cam.fieldOfView = targetFov;
                return;
            }

            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * _adaptSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * _adaptSpeed);
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFov, Time.deltaTime * _adaptSpeed);
        }
    }
}
