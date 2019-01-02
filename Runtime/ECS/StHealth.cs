using Unity.Entities;

namespace package.stormium.core
{
    // TODO: Maybe implement it in a different way?
    public struct StHealthRecoveryProgress : IComponentData
    {
        public float Value;

        public StHealthRecoveryProgress(float value)
        {
            Value = value;
        }

        public bool CanGetToNext()
        {
            return Value >= 1f;
        }

        public bool IsInvalid()
        {
            return Value < 0f;
        }
    }
    
    public struct StHealth : IComponentData
    {
        public int Value;

        public StHealth(int value)
        {
            Value = value;
        }
    }

    public struct StMaxHealth : IComponentData
    {
        public int Value;

        public StMaxHealth(int value)
        {
            Value = value;
        }
    }
}