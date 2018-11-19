using package.stormiumteam.shared;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine.Jobs;

namespace package.stormium.core
{
    // TODO: This should be moved into the SHARED package
    public class EndFrameCopyTransformToPositionSystem : JobComponentSystem
    {
        struct Group
        {
            public ComponentDataArray<Position> Positions;
            public TransformAccessArray Transforms;
            public ComponentDataArray<EndFrameCopyTransformToPosition> Data;
        }

        [Inject] private Group m_Group;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            return new JobUpdatePosition
            {
                PositionComponents = m_Group.Positions
            }.Schedule(m_Group.Transforms, inputDeps);
        }

        struct JobUpdatePosition : IJobParallelForTransform
        {
            public ComponentDataArray<Position> PositionComponents;
            
            public void Execute(int index, TransformAccess transform)
            {
                PositionComponents[index] = new Position
                {
                    Value = transform.position
                };
            }
        }
    }
}