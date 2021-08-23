using Vintagestory.API.Common;

namespace Farmlife
{
    public class AiTaskPetSeekFoodAndEat : AiTaskTweakedSeekFoodAndEat
    {
        public override bool ShouldExecute()
        {
            EntityBehaviorPetCommand pc = entity.GetBehavior<EntityBehaviorPetCommand>();
            EntityBehaviorConsume bc = entity.GetBehavior<EntityBehaviorConsume>();
            if (pc == null || bc == null || (pc.CurrentOrder == EnumPetOrder.Idle && !bc.IsHungry)) return false;
            return base.ShouldExecute();
        }

        public AiTaskPetSeekFoodAndEat(EntityAgent entity) : base(entity)
        {
        }
    }
}
