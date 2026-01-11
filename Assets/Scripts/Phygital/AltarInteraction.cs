using UnityEngine;
using UnityEngine.UI;

namespace Axiom.Phygital
{
    /// <summary>
    /// Detects when the player enters the Altar's trigger zone and shows/hides the sacrifice button.
    /// Attach to a child GameObject of the Altar with a SphereCollider (IsTrigger = true).
    /// </summary>
    [DisallowMultipleComponent]
    public class AltarInteraction : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("The button that appears when player is in range. Should be hidden by default.")]
        [SerializeField] private Button _honorButton;

        [Tooltip("The camera UI controller that handles the sacrifice camera mode.")]
        [SerializeField] private SacrificeCameraUI _cameraUI;

        [Header("Detection")]
        [Tooltip("Tag used to identify the player.")]
        [SerializeField] private string _playerTag = "Player";

        private bool _playerInRange;

        private void Awake()
        {
            // Validate setup
            if (_honorButton == null)
            {
                Debug.LogError("[AltarInteraction] Honor Button is not assigned!");
                return;
            }

            if (_cameraUI == null)
            {
                Debug.LogError("[AltarInteraction] SacrificeCameraUI is not assigned!");
                return;
            }

            // Ensure button starts hidden
            _honorButton.gameObject.SetActive(false);

            // Hook up button click
            _honorButton.onClick.AddListener(OnHonorButtonClicked);
        }

        private void OnDestroy()
        {
            if (_honorButton != null)
            {
                _honorButton.onClick.RemoveListener(OnHonorButtonClicked);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(_playerTag)) return;

            _playerInRange = true;
            if (_honorButton != null && !_cameraUI.IsOpen)
            {
                _honorButton.gameObject.SetActive(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(_playerTag)) return;

            _playerInRange = false;
            if (_honorButton != null)
            {
                _honorButton.gameObject.SetActive(false);
            }
        }

        private void OnHonorButtonClicked()
        {
            if (_cameraUI == null) return;

            // Hide button while camera is open
            _honorButton.gameObject.SetActive(false);

            // Open camera UI
            _cameraUI.OpenCamera(() =>
            {
                // Callback when camera closes - show button again if still in range
                if (_playerInRange && _honorButton != null)
                {
                    _honorButton.gameObject.SetActive(true);
                }
            });
        }
    }
}
