using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using System;
using Vintagestory.API.Config;
using System.Text;

namespace Farmlife
{
    public class ItemPetWhistle : Item
    {
        EntityPartitioning entityUtil;
        WorldInteraction[] interactions;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            entityUtil = api.ModLoader.GetModSystem<EntityPartitioning>();

            interactions = new WorldInteraction[] { 
            
                new WorldInteraction()
                {
                    HotKeyCode = "sneak",
                    MouseButton = EnumMouseButton.Left,
                    ActionLangCode = "farmlife:itemhelp-changeorder"
                },
                new WorldInteraction()
                {
                    HotKeyCode = "sprint",
                    MouseButton = EnumMouseButton.Left,
                    ActionLangCode = "farmlife:itemhelp-changeaggro"
                },
                new WorldInteraction()
                {
                    HotKeyCode = "sneak",
                    MouseButton = EnumMouseButton.Right,
                    ActionLangCode = "farmlife:itemhelp-command"
                }
            };
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            IPlayer master = (byEntity as EntityPlayer)?.Player;

            if (!FarmerConfig.Loaded.PetsEnabled) return;

            if (master == null) return;

            if (byEntity.Controls.Sneak)
            {
                EntityBehaviorPetCommand pc = entitySel?.Entity?.GetBehavior<EntityBehaviorPetCommand>();
                EnumPetOrder order = (EnumPetOrder)slot.Itemstack.Attributes.GetInt("order");
                EnumPetAggro aggro = (EnumPetAggro)slot.Itemstack.Attributes.GetInt("aggro");

                if (pc?.master != null && pc.master.PlayerUID == master.PlayerUID)
                {
                    pc.CurrentAggro = aggro;
                    pc.CurrentOrder = order;
                    pc.ClearHitList();

                    IServerPlayer splr = (byEntity as EntityPlayer)?.Player as IServerPlayer;

                    if (splr != null)
                    {
                        splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("farmlife:commandsingular", entitySel.Entity.GetName(), Lang.Get("farmlife:order-" + (int)order), Lang.Get("farmlife:aggro-" + (int)aggro)), EnumChatType.Notification);
                    }

                }
                else
                {
                    int ordered = 0;
                    entityUtil.WalkEntities(byEntity.SidedPos.XYZ, 30, (e) => {
                        pc = e.GetBehavior<EntityBehaviorPetCommand>();

                        if (pc?.master != null && pc.master.PlayerUID == master.PlayerUID)
                        {
                            pc.CurrentAggro = aggro;
                            pc.CurrentOrder = order;
                            pc.ClearHitList();
                            ordered++;
                        }

                        return true;
                    });

                    IServerPlayer splr = (byEntity as EntityPlayer)?.Player as IServerPlayer;

                    if (splr != null)
                    {
                        splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("farmlife:commandplural", ordered, Lang.Get("farmlife:order-" + (int)order), Lang.Get("farmlife:aggro-" + (int)aggro)), EnumChatType.Notification);
                    }

                }

                handling = EnumHandHandling.PreventDefault;
            }
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (!FarmerConfig.Loaded.PetsEnabled) return;

            int aggro = slot.Itemstack.Attributes.GetInt("aggro");
            int order = slot.Itemstack.Attributes.GetInt("order");

            if (byEntity.Controls.Sneak)
            {
                
                order++;
                if (!Enum.IsDefined(typeof(EnumPetOrder), order)) order = 0;

                slot.Itemstack.Attributes.SetInt("order", order);
            }
            else if (byEntity.Controls.Sprint)
            {
                
                aggro++;
                if (!Enum.IsDefined(typeof(EnumPetAggro), aggro)) aggro = 0;

                slot.Itemstack.Attributes.SetInt("aggro", aggro);
            }

            handling = EnumHandHandling.PreventDefault;

            IServerPlayer splr = (byEntity as EntityPlayer)?.Player as IServerPlayer;

            if (splr != null)
            {
                splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("farmlife:commandsdisplay", Lang.Get("farmlife:order-" + order), Lang.Get("farmlife:aggro-" + aggro)), EnumChatType.Notification);
            }

            base.OnHeldAttackStart(slot, byEntity, blockSel, entitySel, ref handling);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            int aggro = inSlot.Itemstack.Attributes.GetInt("aggro");
            int order = inSlot.Itemstack.Attributes.GetInt("order");

            dsc.AppendLine(Lang.Get("farmlife:commandsdisplay", Lang.Get("farmlife:order-" + order), Lang.Get("farmlife:aggro-" + aggro)));

            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return interactions;
        }
    }
}
