using Vintagestory.API.Common;
using Vintagestory.API.Client;

namespace Farmlife
{
    public class ItemHandlingGloves : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            handling = EnumHandHandling.PreventDefaultAction;

            if (byEntity.GetBehavior<EntityBehaviorCarry>() == null) return;

            EntityBehaviorCarry carry = byEntity.GetBehavior<EntityBehaviorCarry>();
            if (carry.alreadyCarrying)
            {
                carry.dropRider();
                return;
            }

            if (entitySel != null)
            {
                EntityAgent ent = entitySel.Entity as EntityAgent;
                


                if (ent.MountedOn == null && carry.CanMount(ent.Code.Path)) { ent.TryMount(carry); return; }
            }
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[]
                {
                    new WorldInteraction()
                    {
                        ActionLangCode = "farmlife:itemhelp-grabanimal",
                        MouseButton = EnumMouseButton.Right,
                    }
                };
        }
    }

}
