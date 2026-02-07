using System;
using UnityEngine;

namespace Axiom.Phygital
{
    /// <summary>
    /// Legacy compatibility shim. Sacrifice camera and image upload flow are intentionally disabled.
    /// </summary>
    [DisallowMultipleComponent]
    public class SacrificeCameraUI : MonoBehaviour
    {
        [SerializeField] private GameObject _cameraPanel;

        public bool IsOpen => false;

        private void Awake()
        {
            if (_cameraPanel != null)
            {
                _cameraPanel.SetActive(false);
            }
        }

        public void OpenCamera(Action onClose = null)
        {
            if (_cameraPanel != null)
            {
                _cameraPanel.SetActive(false);
            }

            onClose?.Invoke();
        }

        public void CloseCamera()
        {
            if (_cameraPanel != null)
            {
                _cameraPanel.SetActive(false);
            }
        }
    }
}
