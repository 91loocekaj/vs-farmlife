using Vintagestory.API.Common;
using HarmonyLib;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Farmlife
{
    public class EntityBehaviorPickup : EntityBehavior
    {
        JsonItemStack item;

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            item = attributes["item"].AsObject<JsonItemStack>();
            item?.Resolve(entity.World, "entity pickup", false);
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            if (byEntity.Controls.Sneak && item.ResolvedItemstack != null)
            {
                IPlayer player = (byEntity as EntityPlayer).Player;
                ItemStack give = item.ResolvedItemstack.Clone();

                if (player == null || !player.InventoryManager.TryGiveItemstack(give))
                {
                    entity.World.SpawnItemEntity(give, entity.SidedPos.XYZ);
                }

                entity.Die(EnumDespawnReason.PickedUp);
            }

            base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);
        }

        public EntityBehaviorPickup(Entity entity) : base(entity)
        {
        }

        public override string PropertyName()
        {
            return "pickup";
        }
    }

}
