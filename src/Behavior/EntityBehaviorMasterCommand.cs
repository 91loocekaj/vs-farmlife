using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Vintagestory.API.Common.Entities;

namespace Farmlife
{
    public class EntityBehaviorMasterCommand : EntityBehavior
    {
        EntityPartitioning entityUtil;
        
        public IPlayer petowner
        {
            get { return (entity as EntityPlayer).Player; }
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            if (damageSource?.SourceEntity != null && damageSource.SourceEntity.HasBehavior<EntityBehaviorHealth>())
            {
                if (entityUtil == null) entityUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();

                if (entityUtil != null)
                {
                    entityUtil.WalkEntities(entity.SidedPos.XYZ, 30, (e) => {

                        EntityBehaviorPetCommand pc = e.GetBehavior<EntityBehaviorPetCommand>();

                        if (pc != null && petowner.PlayerUID == pc.master?.PlayerUID) pc.AddThreat(damageSource.SourceEntity, EnumPetAggro.Protect);

                        return true;
                    });
                }
            }
                base.OnEntityReceiveDamage(damageSource, ref damage);
        }

        public override void DidAttack(DamageSource source, EntityAgent targetEntity, ref EnumHandling handled)
        {
            if (targetEntity != null && targetEntity.HasBehavior<EntityBehaviorHealth>())
            {
                if (entityUtil == null) entityUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();

                if (entityUtil != null)
                {
                    entityUtil.WalkEntities(entity.SidedPos.XYZ, 30, (e) => {

                        EntityBehaviorPetCommand pc = e.GetBehavior<EntityBehaviorPetCommand>();

                        if (pc != null && petowner.PlayerUID == pc.master?.PlayerUID) pc.AddThreat(targetEntity, EnumPetAggro.Hunt);

                        return true;
                    });
                }
            }

            base.DidAttack(source, targetEntity, ref handled);
        }

        public EntityBehaviorMasterCommand(Entity entity) : base(entity)
        {
        }

        public override string PropertyName()
        {
            return "mastercommand";
        }
    }
}
