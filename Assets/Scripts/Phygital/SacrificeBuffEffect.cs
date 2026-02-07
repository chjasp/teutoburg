using UnityEngine;

namespace Axiom.Phygital
{
    /// <summary>
    /// Legacy compatibility shim. Sacrifice buffs and VFX are intentionally disabled.
    /// </summary>
    [DisallowMultipleComponent]
    public class SacrificeBuffEffect : MonoBehaviour
    {
        private static SacrificeBuffEffect _instance;

        public static SacrificeBuffEffect Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<SacrificeBuffEffect>();
                }

                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
        }

        public void TriggerEffect(float score, string category = "")
        {
            // Intentionally left blank: sacrifice buff effects are disabled.
        }
    }
}
