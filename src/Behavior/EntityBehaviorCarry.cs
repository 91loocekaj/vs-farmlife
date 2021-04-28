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
    public class EntityBehaviorCarry : EntityBehavior
    {
        string[] seekEntityCodesExact = new string[0];
        string[] seekEntityCodesBeginsWith = new string[0];
        public string mounty
        {
            get { return entity.WatchedAttributes.GetString("carrying"); }
            set {
                if (value != null)
                {
                    entity.WatchedAttributes.SetString("carrying", value);
                    entity.WatchedAttributes.MarkPathDirty("carrying");
                }
                else
                {
                    entity.WatchedAttributes.RemoveAttribute("carrying");
                }
            }
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
            if (mounty != null)
            {
                string debug = "mounting";
                BlockSchematic es = BlockSchematic.LoadFromString(mounty, ref debug);
                if (es == null) return;
                es.PlaceEntitiesAndBlockEntities(entity.World.BlockAccessor, entity.World, entity.SidedPos.AheadCopy(2).AsBlockPos.Up(2));
                mounty = null;
            }
        }

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);
            if (mounty != null)
            {
                IPlayer byPlayer = null;
                if (entity is EntityPlayer) byPlayer = entity.World.PlayerByUid(((EntityPlayer)entity).PlayerUID);

                if (!(byPlayer?.InventoryManager?.ActiveHotbarSlot?.Itemstack?.Collectible is ItemHandlingGloves)) dropRider();
            }
        }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            base.OnEntityDeath(damageSourceForDeath);
            dropRider();
        }

        public override void OnEntityDespawn(EntityDespawnReason despawn)
        {
            base.OnEntityDespawn(despawn);
            dropRider();
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, float damage)
        {
            base.OnEntityReceiveDamage(damageSource, damage);
            if (damageSource.Type == EnumDamageType.Heal) return;
            dropRider();
        }

        public override void DidAttack(DamageSource source, EntityAgent targetEntity, ref EnumHandling handled)
        {
            base.DidAttack(source, targetEntity, ref handled);
            dropRider();
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
