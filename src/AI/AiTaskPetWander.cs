using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Farmlife
{
    public class AiTaskPetWander : AiTaskWander
    {
        public override bool ShouldExecute()
        {
            EntityBehaviorPetCommand pc = entity.GetBehavior<EntityBehaviorPetCommand>();
            if (pc == null || pc.CurrentOrder == EnumPetOrder.Idle) return false;
            return base.ShouldExecute();
        }

        public AiTaskPetWander(EntityAgent entity) : base(entity)
        {
        }
    }
}
