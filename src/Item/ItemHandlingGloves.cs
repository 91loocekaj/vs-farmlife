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

            if (byEntity.GetBehavior<EntityBehaviorCarry>() == null || byEntity.Api.Side != EnumAppSide.Server) return;

            EntityBehaviorCarry carry = byEntity.GetBehavior<EntityBehaviorCarry>();
            if (carry.mounty != null)
            {
                carry.dropRider();
                return;
            }

            if (entitySel?.Entity?.Alive == true && carry.CanMount(entitySel.Entity.Code.Path))
            {
                BlockSchematic es = new BlockSchematic();
                es.EntitiesUnpacked.Add(entitySel.Entity);
                es.Pack(byEntity.World, entitySel.Position.AsBlockPos);
                carry.mounty = es.ToJson();
                entitySel.Entity.Die(EnumDespawnReason.PickedUp);
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
