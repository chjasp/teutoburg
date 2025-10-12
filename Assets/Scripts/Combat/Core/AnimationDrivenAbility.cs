using UnityEngine;
using UnityEngine.Serialization;

namespace Teutoburg.Combat
{
    /// <summary>
    /// Base helper for abilities that are optionally driven by an Animator trigger and
    /// completed via animation events. Handles the boilerplate of resolving the animator
    /// reference, firing the trigger, and falling back to an immediate execution path when
    /// no animator or trigger is configured.
    /// </summary>
    public abstract class AnimationDrivenAbility : MonoBehaviour
    {
        [Header("Animation")]
        [FormerlySerializedAs("animator")]
        [SerializeField] private Animator animator;

        [FormerlySerializedAs("meleeTriggerName")]
        [FormerlySerializedAs("castTriggerName")]
        [SerializeField] private string triggerName = string.Empty;

        [Tooltip("If true the ability waits for an animation event before executing.\n"
                 + "If false the ability executes immediately after the trigger is fired.")]
        [SerializeField] private bool waitForAnimationEvent = true;

        /// <summary> Cached animator reference for derived classes. </summary>
        protected Animator Animator => animator;

        /// <summary> Allows derived classes to read or override the trigger name. </summary>
        protected string TriggerName
        {
            get => triggerName;
            set => triggerName = value;
        }

        /// <summary> Allows derived classes to control whether an animation event is required. </summary>
        protected bool WaitForAnimationEvent
        {
            get => waitForAnimationEvent;
            set => waitForAnimationEvent = value;
        }

        protected virtual void Awake()
        {
            EnsureAnimatorReference();
        }

        protected virtual void Reset()
        {
            EnsureAnimatorReference();
        }

        /// <summary>
        /// Starts the ability. If an animator and trigger are configured we fire the trigger.
        /// When no animation is available the ability is executed immediately.
        /// </summary>
        public void Perform()
        {
            if (!enabled)
            {
                return;
            }

            Prepare();

            if (animator != null && !string.IsNullOrEmpty(triggerName))
            {
                animator.SetTrigger(triggerName);
                if (!waitForAnimationEvent)
                {
                    Execute();
                }
            }
            else
            {
                Execute();
            }
        }

        /// <summary>
        /// Invoked from animation events to complete the ability. Safe to call manually when
        /// no animation is set up.
        /// </summary>
        public void ExecuteFromAnimationEvent()
        {
            if (!enabled)
            {
                return;
            }

            Execute();
        }

        /// <summary>
        /// Allows derived classes to perform setup before the trigger fires (e.g. selecting a target).
        /// </summary>
        protected virtual void Prepare()
        {
        }

        /// <summary>
        /// Implement the actual ability behaviour here.
        /// </summary>
        protected abstract void Execute();

        /// <summary>
        /// Ensures the animator reference is resolved from children when missing.
        /// </summary>
        protected void EnsureAnimatorReference()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        /// <summary>
        /// Ensures the trigger name has a sensible default when left empty.
        /// </summary>
        protected void EnsureTriggerName(string defaultName)
        {
            if (string.IsNullOrEmpty(triggerName))
            {
                triggerName = defaultName;
            }
        }
    }
}
