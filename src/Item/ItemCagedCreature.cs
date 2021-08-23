using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Farmlife
{
    public class ItemCagedCreature : ItemShiftCreature
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            if (blockSel == null || !byEntity.Controls.Sneak) return;

            IPlayer player = byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID);

            if (!byEntity.World.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                return;
            }

            if (!(byEntity is EntityPlayer) || player.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                slot.TakeOut(1);
                slot.MarkDirty();
            }


            AssetLocation location = new AssetLocation(Code.Domain, CodeEndWithoutParts(1));
            EntityProperties type = byEntity.World.GetEntityType(location);
            if (type == null)
            {
                byEntity.World.Logger.Error("ItemCreature: No such entity - {0}", location);
                if (api.World.Side == EnumAppSide.Client)
                {
                    (api as ICoreClientAPI).TriggerIngameError(this, "nosuchentity", "No such entity '{0}' loaded.");
                }
                return;
            }

            Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);

            if (entity != null)
            {
                entity.WatchedAttributes.SetInt("generation", 10);
                entity.ServerPos.X = blockSel.Position.X + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.X) + 0.5f;
                entity.ServerPos.Y = blockSel.Position.Y + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Y);
                entity.ServerPos.Z = blockSel.Position.Z + (blockSel.DidOffset ? 0 : blockSel.Face.Normali.Z) + 0.5f;
                entity.ServerPos.Yaw = (float)byEntity.World.Rand.NextDouble() * 2 * GameMath.PI;

                entity.Pos.SetFrom(entity.ServerPos);
                entity.PositionBeforeFalling.Set(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);

                entity.Attributes.SetString("origin", "playerplaced");

                //entity.WatchedAttributes.SetInt("generation", 10);

                byEntity.World.SpawnEntity(entity);
                handHandling = EnumHandHandling.PreventDefaultAction;
            }
        }

    }
}
