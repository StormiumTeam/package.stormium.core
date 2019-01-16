using Unity.Entities;

namespace Stormium.Core.Networking
{
    public static class UpdateLoop
    {
        public class BeforeDataChange : ComponentSystem
        {
            protected override void OnUpdate()
            {

            }
        }

        [UpdateAfter(typeof(BeforeDataChange))]
        public class InDataChange : ComponentSystem
        {
            protected override void OnUpdate()
            {

            }
        }

        [UpdateAfter(typeof(InDataChange))]
        public class AfterDataChange : ComponentSystem
        {
            protected override void OnUpdate()
            {
                
            }
        }
    }
}