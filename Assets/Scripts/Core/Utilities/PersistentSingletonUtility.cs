using UnityEngine;

namespace Axiom.Core
{
    /// <summary>
    /// Shared lifecycle helpers for persistent singleton MonoBehaviours.
    /// </summary>
    public static class PersistentSingletonUtility
    {
        public static T EnsureInstance<T>(ref T instance, string objectName) where T : MonoBehaviour
        {
            if (instance == null)
            {
                GameObject go = new GameObject(objectName);
                instance = go.AddComponent<T>();
                Object.DontDestroyOnLoad(go);
            }

            return instance;
        }

        public static bool TryInitialize<T>(MonoBehaviour self, ref T instance) where T : MonoBehaviour
        {
            if (instance == null)
            {
                instance = self as T;
                Object.DontDestroyOnLoad(self.gameObject);
                return true;
            }

            if (instance != self)
            {
                Object.Destroy(self.gameObject);
                return false;
            }

            return true;
        }

        public static void ClearIfOwned<T>(MonoBehaviour self, ref T instance) where T : MonoBehaviour
        {
            if (instance == self)
            {
                instance = null;
            }
        }
    }
}
