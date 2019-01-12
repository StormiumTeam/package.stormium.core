using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace package.stormium.core
{
    public struct StActionContainer : IBufferElementData
    {
        public Entity Target;

        public StActionContainer(Entity actionTarget)
        {
            Target = actionTarget;
        }

        public bool TargetValid()
        {
            return World.Active.GetExistingManager<EntityManager>().Exists(Target);
        }
    }

    public struct StActionTag : IComponentData
    {
        public int ActionTypeIndex;
        
        public StActionTag(int actionTypeIndex)
        {
            ActionTypeIndex = actionTypeIndex;
        }

        public Type GetActionType()
        {
            return TypeManager.GetType(ActionTypeIndex);
        }
    }

    public struct StActionOwner : IComponentData
    {
        public Entity Target;

        public StActionOwner(Entity owner)
        {
            Target = owner;
        }
        
        public bool TargetValid()
        {
            return World.Active.GetExistingManager<EntityManager>().Exists(Target);
        }
    }

    /// <summary>
    /// (Recommended) Use it if you don't want the order of the action container
    /// </summary>
    public struct StActionSlot : IComponentData
    {
        public int Value;

        public StActionSlot(int slot)
        {
            Value = slot;
        }

        public bool IsValid()
        {
            return Value >= 0;
        }

        public bool IsHidden()
        {
            return Value == -1;
        }
    }

    public struct StActionAmmo : IComponentData
    {
        public int Ammo;
        public int AmmoUsage;
        public int AmmoMax;

        public StActionAmmo(int ammoUsage, int ammoMax)
        {
            Ammo = 0;
            AmmoUsage = ammoUsage;
            AmmoMax = ammoMax;
        }
        
        public StActionAmmo(int ammoUsage, int ammoMax, int ammo)
        {
            Ammo      = ammo;
            AmmoUsage = ammoUsage;
            AmmoMax   = ammoMax;
        }

        public int GetRealAmmo()
        {
            if (AmmoMax <= 0)
                return 0;
            if (AmmoUsage <= 0)
                return 1;
            
            var usage = math.max(AmmoUsage, 1);
            var max = math.max(AmmoMax, 1);

            return max / usage;
        }
    }

    public struct StActionAmmoCooldown : IComponentData
    {
        public float StartTime;
        public float Cooldown;

        public StActionAmmoCooldown(float startTime)
        {
            StartTime = startTime;
            Cooldown = -1f;
        }

        public StActionAmmoCooldown(float startTime, float cooldown)
        {
            StartTime = startTime;
            Cooldown = cooldown;
        }

        public bool CooldownFinished()
        {
            return StartTime < 0 || Time.time > (StartTime + Cooldown);
        }
    }

    public struct StActionDualSwitch : IComponentData
    {
        public Entity PrimaryTarget;
        public Entity SecondaryTarget;

        public Entity this[int index] => index == 1 ? SecondaryTarget : PrimaryTarget;

        public StActionDualSwitch(Entity primary, Entity secondary)
        {
            PrimaryTarget = primary;
            SecondaryTarget = secondary;
        }
    }
}