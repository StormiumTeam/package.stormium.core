using package.stormiumteam.shared;
using Unity.Entities;
using UnityEngine.Assertions;

namespace package.stormium.core
{
    public abstract class StEventDiffuseCommand
    {
        public struct Arguments : IDelayComponentArguments
        {
            public Entity Cmd;
            public Entity CmdResult;
            public CmdState CmdState;

            public Arguments(Entity command, Entity commandResult, CmdState state)
            {
                Cmd = command;
                CmdResult = commandResult;
                CmdState = state;
            }

            public void SetResult(bool state)
            {
                EntityManager em = Unity.Entities.World.Active.GetExistingManager<EntityManager>();
                
                Assert.IsTrue(em.HasComponent<EntityCommandResult>(CmdResult), "em.HasComponent<EntityCommandResult>(CmdResult)");
                
                em.SetComponentData(CmdResult, new EntityCommandResult()
                {
                    IsAuthorized = (byte)(state ? 1 : 0)
                });
            }
        }

        public interface IEv : IAppEvent
        {
            void OnCommandDiffuse(Arguments args);
        }

        internal abstract void Sealed();
    }
}