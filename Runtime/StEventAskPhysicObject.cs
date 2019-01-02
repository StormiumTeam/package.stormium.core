using package.stormiumteam.shared;
using Unity.Collections;
using Unity.Entities;

namespace Stormium.Core
{
    public abstract class StEventAskPhysicObjects
    {
        public struct StartArguments : IDelayComponentArguments
        {
            [ReadOnly] public Entity Caller;
            [ReadOnly] public Entity Reason;

            public StartArguments(Entity caller, [ReadOnly] Entity reason)
            {
                Caller = caller;
                Reason = reason;
            }
        }

        public struct EndArguments : IDelayComponentArguments
        {
            [ReadOnly] public Entity Caller;
            [ReadOnly] public Entity Reason;

            public EndArguments(Entity caller, [ReadOnly] Entity reason)
            {
                Caller = caller;
                Reason = reason;
            }
        }

        public interface IEv : IAppEvent
        {
            void CallbackStartOnAskPhysicObjects(StartArguments args);
            void CallbackEndOnAskPhysicObjects(EndArguments args);
        }

        internal abstract void Sealed();
    }
}