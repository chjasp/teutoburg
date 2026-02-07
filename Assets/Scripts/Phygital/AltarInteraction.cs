using UnityEngine;
using UnityEngine.UI;

namespace Axiom.Phygital
{
    /// <summary>
    /// Legacy compatibility shim. The altar interaction feature is intentionally disabled.
    /// </summary>
    [DisallowMultipleComponent]
    public class AltarInteraction : MonoBehaviour
    {
        [SerializeField] private Button _honorButton;

        private void Awake()
        {
            if (_honorButton != null)
            {
                _honorButton.gameObject.SetActive(false);
                _honorButton.interactable = false;
            }

            var trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.enabled = false;
            }

            enabled = false;
        }
    }
}
