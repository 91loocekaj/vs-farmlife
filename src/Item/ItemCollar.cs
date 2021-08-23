using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;

namespace Farmlife
{
    public class ItemCollar : Item
    {
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            interactions = new WorldInteraction[] {

                new WorldInteraction()
                {
                    HotKeyCode = "sneak",
                    MouseButton = EnumMouseButton.Right,
                    ActionLangCode = "farmlife:itemhelp-claimpet"
                },
            };
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);

            if (!FarmerConfig.Loaded.PetsEnabled) return;

            if (byEntity.Controls.Sneak && entitySel != null && entitySel.Entity.WatchedAttributes.GetInt("generation") >= 10 && entitySel.Entity.Properties.Attributes?["petForm"].Exists == true)
            {
                IPlayer master = (byEntity as EntityPlayer)?.Player;

                if (master == null) return;

                if (!(byEntity is EntityPlayer) || master.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    slot.TakeOut(1);
                    slot.MarkDirty();
                }


                AssetLocation location = new AssetLocation(entitySel.Entity.Properties.Attributes["petForm"].AsString());
                EntityProperties type = byEntity.World.GetEntityType(location);
                if (type == null)
                {
                    byEntity.World.Logger.Error("Pet: No such pet form - {0}", location);
                    if (api.World.Side == EnumAppSide.Client)
                    {
                        (api as ICoreClientAPI).TriggerIngameError(this, "nosuchentity", "No such entity '{0}' loaded.");
                    }
                    return;
                }

                Entity old = entitySel.Entity;
                Entity entity = byEntity.World.ClassRegistry.CreateEntity(type);

                if (entity != null)
                {
                    ITreeAttribute multiply = old.WatchedAttributes.GetTreeAttribute("multiply");
                    ITreeAttribute grow = old.WatchedAttributes.GetTreeAttribute("grow");
                    ITreeAttribute hunger = old.WatchedAttributes.GetTreeAttribute("hunger");
                    ITreeAttribute genome = old.WatchedAttributes.GetTreeAttribute("genome");

                    if (multiply != null) entity.WatchedAttributes.SetAttribute("multipy", multiply.Clone());
                    if (grow != null) entity.WatchedAttributes.SetAttribute("grow", grow.Clone());
                    if (hunger != null) entity.WatchedAttributes.SetAttribute("hunger", hunger.Clone());
                    if (genome != null) entity.WatchedAttributes.SetAttribute("genome", genome.Clone());
                    entity.WatchedAttributes.SetInt("generation", old.WatchedAttributes.GetInt("generation", 10) - 10);
                    entity.WatchedAttributes.GetOrAddTreeAttribute("command").SetString("masterUID", master.PlayerUID);
                    entity.WatchedAttributes.SetBool("playerFed", true);

                    entity.ServerPos.SetFrom(old.ServerPos);
                    entity.Pos.SetFrom(entity.ServerPos);

                    old.Die(EnumDespawnReason.Expire, null);

                    byEntity.World.SpawnEntity(entity);
                    handling = EnumHandHandling.PreventDefaultAction;
                }
            }
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return interactions;
        }
    }
}
