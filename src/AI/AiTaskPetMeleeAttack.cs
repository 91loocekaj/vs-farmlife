using Vintagestory.API.Common;
using Vintagestory.GameContent;
using System;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace Farmlife
{
    public class AiTaskPetMeleeAttack : AiTaskBase, IWorldIntersectionSupplier
    {
        Entity targetEntity;
        EntityBehaviorPetCommand pc;

        long lastCheckOrAttackMs;

        float damage = 2f;
        float minDist = 1.5f;
        float minVerDist = 1f;


        bool damageInflicted = false;

        int attackDurationMs = 1500;
        int damagePlayerAtMs = 500;

        BlockSelection blockSel = new BlockSelection();
        EntitySelection entitySel = new EntitySelection();

        public EnumDamageType damageType = EnumDamageType.BluntAttack;
        public int damageTier = 0;
        float tamingGenerations = 10f;

        public Vec3i MapSize { get { return entity.World.BlockAccessor.MapSize; } }

        public AiTaskPetMeleeAttack(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            if (taskConfig["tamingGenerations"] != null)
            {
                tamingGenerations = taskConfig["tamingGenerations"].AsFloat(10f);
            }

            this.damage = taskConfig["damage"].AsFloat(2);
            this.attackDurationMs = taskConfig["attackDurationMs"].AsInt(1500);
            this.damagePlayerAtMs = taskConfig["damagePlayerAtMs"].AsInt(1000);

            this.minDist = taskConfig["minDist"].AsFloat(2f);
            this.minVerDist = taskConfig["minVerDist"].AsFloat(1f);

            string strdt = taskConfig["damageType"].AsString();
            if (strdt != null)
            {
                this.damageType = (EnumDamageType)Enum.Parse(typeof(EnumDamageType), strdt, true);
            }
            this.damageTier = taskConfig["damageTier"].AsInt(0);

            ITreeAttribute tree = entity.WatchedAttributes.GetTreeAttribute("extraInfoText");
            tree.SetString("dmgTier", Lang.Get("Damage tier: {0}", damageTier));
        }

        public override bool ShouldExecute()
        {
            if (pc == null) pc = entity.GetBehavior<EntityBehaviorPetCommand>();
            if (pc == null || pc.CurrentAggro == EnumPetAggro.Passive) return false;
            long ellapsedMs = entity.World.ElapsedMilliseconds;
            if (ellapsedMs - lastCheckOrAttackMs < attackDurationMs || cooldownUntilMs > ellapsedMs)
            {
                return false;
            }
            if (whenInEmotionState != null && !entity.HasEmotionState(whenInEmotionState)) return false;
            if (whenNotInEmotionState != null && entity.HasEmotionState(whenNotInEmotionState)) return false;

            Vec3d pos = entity.ServerPos.XYZ.Add(0, entity.CollisionBox.Y2 / 2, 0).Ahead(entity.CollisionBox.XSize / 2, 0, entity.ServerPos.Yaw);

            targetEntity = entity.World.GetNearestEntity(pos, 3f, 3f, (e) => {
                if (!e.Alive || e.EntityId == this.entity.EntityId) return false;

                if (pc.GetThreat(e)) return true;

                return false;
            });

            lastCheckOrAttackMs = entity.World.ElapsedMilliseconds;
            damageInflicted = false;

            return targetEntity != null;
        }


        float curTurnRadPerSec;

        public override void StartExecute()
        {
            base.StartExecute();
            curTurnRadPerSec = entity.GetBehavior<EntityBehaviorTaskAI>().PathTraverser.curTurnRadPerSec;
        }

        public override bool ContinueExecute(float dt)
        {
            if (pc.CurrentAggro == EnumPetAggro.Passive) return false;
            
            EntityPos own = entity.ServerPos;
            EntityPos his = targetEntity.ServerPos;

            float desiredYaw = (float)Math.Atan2(his.X - own.X, his.Z - own.Z);
            float yawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, desiredYaw);
            entity.ServerPos.Yaw += GameMath.Clamp(yawDist, -curTurnRadPerSec * dt * GlobalConstants.OverallSpeedMultiplier, curTurnRadPerSec * dt * GlobalConstants.OverallSpeedMultiplier);
            entity.ServerPos.Yaw = entity.ServerPos.Yaw % GameMath.TWOPI;


            if (lastCheckOrAttackMs + damagePlayerAtMs > entity.World.ElapsedMilliseconds) return true;

            if (!damageInflicted)
            {
                if (!hasDirectContact(targetEntity)) return false;

                bool alive = targetEntity.Alive;
                int gen = entity.WatchedAttributes.GetInt("generation");

                targetEntity.ReceiveDamage(
                    new DamageSource()
                    {
                        Source = EnumDamageSource.Entity,
                        SourceEntity = pc.master?.Entity ?? entity,
                        Type = damageType,
                        DamageTier = damageTier + (gen % 10)
                    },
                    (damage + (gen * 0.1f)) * GlobalConstants.CreatureDamageModifier
                );

                if (alive && !targetEntity.Alive)
                {
                    pc.RemoveThreat(targetEntity);
                    this.entity.GetBehavior<EntityBehaviorEmotionStates>()?.TryTriggerState("saturated");
                }

                damageInflicted = true;
            }

            if (lastCheckOrAttackMs + attackDurationMs > entity.World.ElapsedMilliseconds) return true;
            return false;
        }



        bool hasDirectContact(Entity targetEntity)
        {
            Cuboidd targetBox = targetEntity.CollisionBox.ToDouble().Translate(targetEntity.ServerPos.X, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z);
            Vec3d pos = entity.ServerPos.XYZ.Add(0, entity.CollisionBox.Y2 / 2, 0).Ahead(entity.CollisionBox.XSize / 2, 0, entity.ServerPos.Yaw);
            double dist = targetBox.ShortestDistanceFrom(pos);
            double vertDist = Math.Abs(targetBox.ShortestVerticalDistanceFrom(pos.Y));
            if (dist >= minDist || vertDist >= minVerDist) return false;

            Vec3d rayTraceFrom = entity.ServerPos.XYZ;
            rayTraceFrom.Y += 1 / 32f;
            Vec3d rayTraceTo = targetEntity.ServerPos.XYZ;
            rayTraceTo.Y += 1 / 32f;
            bool directContact = false;

            entity.World.RayTraceForSelection(this, rayTraceFrom, rayTraceTo, ref blockSel, ref entitySel);
            directContact = blockSel == null;

            if (!directContact)
            {
                rayTraceFrom.Y += entity.CollisionBox.Y2 * 7 / 16f;
                rayTraceTo.Y += targetEntity.CollisionBox.Y2 * 7 / 16f;
                entity.World.RayTraceForSelection(this, rayTraceFrom, rayTraceTo, ref blockSel, ref entitySel);
                directContact = blockSel == null;
            }

            if (!directContact)
            {
                rayTraceFrom.Y += entity.CollisionBox.Y2 * 7 / 16f;
                rayTraceTo.Y += targetEntity.CollisionBox.Y2 * 7 / 16f;
                entity.World.RayTraceForSelection(this, rayTraceFrom, rayTraceTo, ref blockSel, ref entitySel);
                directContact = blockSel == null;
            }

            if (!directContact) return false;

            return true;
        }


        public Block GetBlock(BlockPos pos)
        {
            return entity.World.BlockAccessor.GetBlock(pos);
        }

        public Cuboidf[] GetBlockIntersectionBoxes(BlockPos pos)
        {
            return entity.World.BlockAccessor.GetBlock(pos).GetCollisionBoxes(entity.World.BlockAccessor, pos);
        }

        public bool IsValidPos(BlockPos pos)
        {
            return entity.World.BlockAccessor.IsValidPos(pos);
        }


        public Entity[] GetEntitiesAround(Vec3d position, float horRange, float vertRange, ActionConsumable<Entity> matches = null)
        {
            return new Entity[0];
        }
    }
}
