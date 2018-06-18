using package.guerro.shared;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace package.stormium.core
{
    public class StCharacterCameraSystem : ComponentSystem
    {
        struct CameraGroup
        {
            public ComponentDataArray<CameraData> DataCameras;

            public int Length;
        }

        [Inject] private CameraGroup m_CameraGroup;

        protected override void OnUpdate()
        {
            for (int i = 0; i != m_CameraGroup.Length; i++)
            {
                var camera = m_CameraGroup.DataCameras[i];

                var targetExist = EntityManager.Exists(camera.TargetId)
                    && EntityManager.HasComponent<CameraTargetData>(camera.TargetId);
                if (targetExist)
                {
                    var target = EntityManager.GetComponentData<CameraTargetData>(camera.TargetId);

                    camera.Position = target.Position + target.PositionOffset;
                    camera.Rotation = Quaternion.Euler(target.Rotation) * Quaternion.Euler(target.RotationOffset);
                    camera.FieldOfView = target.FieldOfView;

                    m_CameraGroup.DataCameras[i] = camera;
                }
            }
        }
    }
}