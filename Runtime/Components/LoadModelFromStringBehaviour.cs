using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Runtime
{
    public class LoadModelFromStringBehaviour : MonoBehaviour
    {
        private string    m_AssetId;
        
        public Transform SpawnRoot;

        public string AssetId
        {
            get => m_AssetId;
            set
            {
                if (m_AssetId == value)
                    return;

                m_AssetId = value;
                Pop();
            }
        }

        private GameObject m_Result;

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(m_AssetId))
            {
                return;
            }

            Pop();
        }

        private void Pop()
        {
            Depop();
            
            Addressables.Instantiate(m_AssetId, SpawnRoot).Completed += (o) => m_Result = o.Result;
        }

        private void Depop()
        {
            if (m_Result)
                Addressables.ReleaseInstance(m_Result);

            m_Result = null;
        }

        private void OnDisable()
        {
            Depop();
        }
    }
}