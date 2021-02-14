using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Util;
using Vintagestory.API.MathTools;
using System.Collections.Generic;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API;

namespace Farmlife
{
    public class EntityBehaviorCarry : EntityBehavior, IMountable
    {
        bool mounted;
        EntityAgent mounty;
        string[] seekEntityCodesExact = new string[0];
        string[] seekEntityCodesBeginsWith = new string[0];
        public bool alreadyCarrying
        {
            get { return mounty != null; }
        }

        public Vec3d MountPosition
        {
            get { return entity.SidedPos.AheadCopy(1).XYZ.Add(0, entity.LocalEyePos.Y, 0); }
        }

        public float? MountYaw { get { return null; } }

        public string SuggestedAnimation { get { return null; } }

        public EntityControls Controls { get { return null; } }

        public static IMountable GetMountable(IWorldAccessor world, TreeAttribute tree)
        {
            long holder = tree.GetLong("id");
            foreach (Entity cand in (world as IServerWorldAccessor).LoadedEntities.Values)
            {
                if (cand.EntityId == holder) return cand.GetBehavior<EntityBehaviorCarry>();
            }

            return null;
        }

        public void DidMount(EntityAgent entityAgent)
        {
            mounty = entityAgent;
            mounted = true;
        }

        public void DidUnmount(EntityAgent entityAgent)
        {
            mounty = null;
            mounted = false;
        }

        public void MountableToTreeAttributes(TreeAttribute tree)
        {
            tree.SetString("className", "carrier");
            tree.SetLong("id", entity.EntityId);
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
            if (attributes["entityCodes"] != null)
            {
                string[] codes = attributes["entityCodes"].AsArray<string>(new string[] { "player" });

                List<string> exact = new List<string>();
                List<string> beginswith = new List<string>();

                for (int i = 0; i < codes.Length; i++)
                {
                    string code = codes[i];
                    if (code.EndsWith("*")) beginswith.Add(code.Substring(0, code.Length - 1));
                    else exact.Add(code);
                }

                seekEntityCodesExact = exact.ToArray();
                seekEntityCodesBeginsWith = beginswith.ToArray();
            }
        }

        public bool CanMount(string path)
        {
            if (mounty != null) return false;

            if (seekEntityCodesExact.Contains(path)) return true;

            for (int i = 0; i < seekEntityCodesBeginsWith.Length; i++)
            {
                if (path.StartsWithFast(seekEntityCodesBeginsWith[i])) return true;
            }

            return false;
        }

        public void dropRider()
        {
            if (alreadyCarrying) mounty.TryUnmount();
        }

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);
            if (mounty != null)
            {
                //mounty.SidedPos.Motion.Set(0, 0, 0);
                mounty.SidedPos.SetPos(MountPosition);
                IPlayer byPlayer = null;
                if (entity is EntityPlayer) byPlayer = entity.World.PlayerByUid(((EntityPlayer)entity).PlayerUID);

                if (!(byPlayer?.InventoryManager?.ActiveHotbarSlot?.Itemstack?.Collectible is ItemHandlingGloves)) mounty.TryUnmount();
            }
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            base.OnEntityDeath(damageSourceForDeath);
            if (alreadyCarrying) mounty.TryUnmount();
        }

        public override void OnEntityDespawn(EntityDespawnReason despawn)
        {
            base.OnEntityDespawn(despawn);
            if (alreadyCarrying) mounty.TryUnmount();
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, float damage)
        {
            base.OnEntityReceiveDamage(damageSource, damage);
            if (damageSource.Type == EnumDamageType.Heal) return;
            if (alreadyCarrying) mounty.TryUnmount();
        }

        public override void DidAttack(DamageSource source, EntityAgent targetEntity, ref EnumHandling handled)
        {
            base.DidAttack(source, targetEntity, ref handled);
            if (alreadyCarrying) mounty.TryUnmount();
        }

        public override string PropertyName()
        {
            return "carry";
        }

        public EntityBehaviorCarry(Entity entity) : base(entity)
        {
        }
    }

}
