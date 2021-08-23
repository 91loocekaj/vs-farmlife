using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common.Entities;

namespace Farmlife
{
    public class AiTaskStayCloseToMaster : AiTaskBase
    {
        Entity targetEntity;
        EntityBehaviorPetCommand pc;
        float moveSpeed = 0.03f;
        float range = 8f;
        float maxDistance = 3f;
        string entityCode;
        bool stuck = false;
        bool onlyIfLowerId = false;

        Vec3d targetOffset = new Vec3d();

        public AiTaskStayCloseToMaster(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            if (taskConfig["movespeed"] != null)
            {
                moveSpeed = taskConfig["movespeed"].AsFloat(0.03f);
            }

            if (taskConfig["searchRange"] != null)
            {
                range = taskConfig["searchRange"].AsFloat(8f);
            }

            if (taskConfig["maxDistance"] != null)
            {
                maxDistance = taskConfig["maxDistance"].AsFloat(3f);
            }

            if (taskConfig["onlyIfLowerId"] != null)
            {
                onlyIfLowerId = taskConfig["onlyIfLowerId"].AsBool();
            }

            entityCode = taskConfig["entityCode"].AsString();
        }


        public override bool ShouldExecute()
        {
            if (rand.NextDouble() > 0.33f) return false;

            if (pc == null) pc = entity.GetBehavior<EntityBehaviorPetCommand>();
            if (pc?.master?.Entity == null || pc.CurrentOrder != EnumPetOrder.Follow) return false;

            targetEntity = pc.master.Entity;

            if (targetEntity != null && (!targetEntity.Alive || targetEntity.ShouldDespawn)) targetEntity = null;
            if (targetEntity == null) return false;

            double x = targetEntity.ServerPos.X;
            double y = targetEntity.ServerPos.Y;
            double z = targetEntity.ServerPos.Z;

            double dist = entity.ServerPos.SquareDistanceTo(x, y, z);

            return dist > maxDistance * maxDistance;
        }


        public override void StartExecute()
        {
            base.StartExecute();

            float size = targetEntity.CollisionBox.XSize;

            pathTraverser.NavigateTo(targetEntity.ServerPos.XYZ, moveSpeed, size + 0.2f, OnGoalReached, OnStuck, false, 1000, true);

            targetOffset.Set(entity.World.Rand.NextDouble() * 2 - 1, 0, entity.World.Rand.NextDouble() * 2 - 1);

            stuck = false;
        }


        public override bool ContinueExecute(float dt)
        {
            if (pc?.master?.Entity == null || pc.CurrentOrder != EnumPetOrder.Follow) return false;

            double x = targetEntity.ServerPos.X + targetOffset.X;
            double y = targetEntity.ServerPos.Y;
            double z = targetEntity.ServerPos.Z + targetOffset.Z;

            if (entity.ServerPos.SquareDistanceTo(x, y, z) > (range + 10) * (range + 10))
            {
                float pitch = entity.SidedPos.Pitch;
                float roll = entity.SidedPos.Roll;
                entity.TeleportTo(targetEntity.SidedPos);
                pathTraverser.Stop();
                entity.SidedPos.Pitch = pitch;
                entity.SidedPos.Roll = roll;
                return false;
            }

            pathTraverser.CurrentTarget.X = x;
            pathTraverser.CurrentTarget.Y = y;
            pathTraverser.CurrentTarget.Z = z;

            if (entity.ServerPos.SquareDistanceTo(x, y, z) < maxDistance * maxDistance / 4)
            {
                pathTraverser.Stop();
                return false;
            }

            return targetEntity.Alive && !stuck && pathTraverser.Active;
        }

        private void OnStuck()
        {
            stuck = true;
        }

        private void OnGoalReached()
        {

        }
    }
}
