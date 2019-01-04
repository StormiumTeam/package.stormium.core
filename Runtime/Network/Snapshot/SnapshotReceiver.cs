using Unity.Entities;

namespace Stormium.Core.Networking
{
    public struct SnapshotReceiver
    {
        public Entity Client;
        public byte WantFullSnapshot;

        public SnapshotReceiver(Entity client, bool wantFullSnapshot)
        {
            Client = client;
            WantFullSnapshot = (byte)(wantFullSnapshot ? 1 : 0);
        }
    }
}