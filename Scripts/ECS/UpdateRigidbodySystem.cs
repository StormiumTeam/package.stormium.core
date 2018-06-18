using System;
using Unity.Entities;
using UnityEngine;

namespace package.stormium.core
{
    [UpdateAfter(typeof(STUpdateOrder.UORigidbodyUpdateBefore.End)),
     UpdateBefore(typeof(STUpdateOrder.UORigidbodyUpdateAfter))]
    [AlwaysUpdateSystem]
    public class UpdateRigidbodySystem : ComponentSystem
    {
        public static event Action OnBeforeSimulate;
        public static event Action<float> OnBeforeSimulateItem;
        public static event Action<float> OnAfterSimulateItem;
        public static event Action OnAfterSimulate;

        private float m_Timer;
        
        protected override void OnUpdate()
        {
            m_Timer += Time.deltaTime;

            OnBeforeSimulate?.Invoke();

            var delta = Time.fixedDeltaTime;
            
            while (m_Timer >= delta)
            {
                m_Timer -= delta;
                
                OnBeforeSimulateItem?.Invoke(delta);
                
                Physics.Simulate(delta);
                
                OnAfterSimulateItem?.Invoke(delta);
            }
            
            Physics.SyncTransforms();
            
            OnAfterSimulate?.Invoke();
        }
    }
}