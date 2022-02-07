using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Farmlife
{
    public class EntityBehaviorConsume : EntityBehavior
    {
        POIRegistry porregistry;
        ITreeAttribute hunger;
        bool timeSkipProtection;
        EntityPartitioning entityUtil;
        float fullCheck;
        GasHelper gasPlug;
        Dictionary<string, float> wasteGas;

        public float MaxSat
        {
            get { return entity.Properties.Attributes?["maxSaturation"].AsFloat(20f) ?? 20f; } 
        }

        public float CurrentSat
        {
            get { return hunger.GetFloat("saturation"); }
            set { hunger.SetFloat("saturation", GameMath.Clamp(value, 0, MaxSat)); entity.WatchedAttributes.MarkPathDirty("hunger"); }
        }

        public float SatPerc
        {
            get { return CurrentSat / MaxSat; }
        }

        public bool IsHungry
        {
            get { return SatPerc < 0.75f * entity.Stats.GetBlended("vulenrability"); }
        }

        internal AssetLocation[] PoopCodes;

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            hunger = entity.WatchedAttributes.GetOrAddTreeAttribute("hunger");
            CurrentSat = hunger.GetFloat("saturation");
            porregistry = entity.Api.ModLoader.GetModSystem<POIRegistry>();
            entityUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();


            if (entity.WatchedAttributes != null && entity.WatchedAttributes.GetDouble("consumptionTimer", -10000) <= 0) entity.WatchedAttributes.SetDouble("consumptionTimer", entity.World.Calendar.TotalHours);

            string[] codes = attributes["poopCodes"].AsArray(new string[0]);
            AssetLocation[] locs = new AssetLocation[codes.Length];
            for (int i = 0; i < locs.Length; i++) locs[i] = new AssetLocation(codes[i]);
            PoopCodes = locs;

            gasPlug = entity.Api.ModLoader.GetModSystem<GasHelper>();
            wasteGas = new Dictionary<string, float>();
            wasteGas.Add("hydrogensulfide", 0.25f);
            wasteGas.Add("methane", 0.25f);
        }

        public override void OnGameTick(float deltaTime)
        {
            if (!entity.Alive || entity.Api.Side != EnumAppSide.Server) return;

            fullCheck += deltaTime;
            if (fullCheck >= 120)
            {
                fullCheck = 0;
                if (SatPerc >= 1)
                {
                    entity.GetBehavior<EntityBehaviorEmotionStates>()?.TryTriggerState("saturated", entity.EntityId);
                }
            }

            if (entity.World.Calendar.TotalHours - entity.WatchedAttributes.GetDouble("consumptionTimer") < 24 || hunger == null) return;
            double addedTime = FarmerConfig.Loaded.UnloadedHungerEnabled ? entity.WatchedAttributes.GetDouble("consumptionTimer") + 24 : entity.World.Calendar.TotalHours;

            entity.WatchedAttributes.SetDouble("consumptionTimer", addedTime);

            if (entity.World.Calendar.TotalHours - addedTime >= 24) timeSkipProtection = true;
            float pastSat = CurrentSat;

            if (entity.World.Calendar.TotalHours - addedTime < 24 || !GetTrough())
            {
                if (!timeSkipProtection || !GetTrough())
                {
                    CurrentSat -= entity.Stats.GetBlended("hungerrate");
                }
                else
                {
                    timeSkipProtection = false;
                }
            }

            if (FarmerConfig.Loaded.PoopEnabled && entity.Api.Side == EnumAppSide.Server && pastSat > 0 && PoopCodes?.Length > 0 && entity.World.Calendar.TotalHours - addedTime <= 72)
            {
                Block block = entity.World.GetBlock(PoopCodes[entity.World.Rand.Next(PoopCodes.Length)]);
                if (block == null) return;

                bool placed =
                    TryPlace(block, 0, 0, 0) ||
                    TryPlace(block, 1, 0, 0) ||
                    TryPlace(block, 0, 0, -1) ||
                    TryPlace(block, -1, 0, 0) ||
                    TryPlace(block, 0, 0, 1)
                ;

                if (placed)
                {
                    gasPlug.SendGasSpread(entity.SidedPos.AsBlockPos, wasteGas);
                    entity.World.FrameProfiler.Mark("entity-createblock");
                }
            }
        }

        private bool TryPlace(Block block, int dx, int dy, int dz)
        {
            if (entity.Swimming || entity.FeetInLiquid) return false;

            IBlockAccessor blockAccess = entity.World.BlockAccessor;
            BlockPos pos = entity.ServerPos.XYZ.AsBlockPos.Add(dx, dy, dz);
            Block blockAtPos = blockAccess.GetBlock(pos);

            if (blockAtPos.IsReplacableBy(block) && !blockAtPos.IsLiquid() && blockAccess.GetBlock(pos.X, pos.Y - 1, pos.Z).SideSolid[BlockFacing.UP.Index])
            {
                blockAccess.SetBlock(block.BlockId, pos);


                return true;
            }

            return false;
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);

            if (byEntity is EntityPlayer && byEntity.Api.Side == EnumAppSide.Server && byEntity.Controls.Sneak && byEntity.Controls.Sprint && itemslot.Empty)
            {
                IServerPlayer splr = (byEntity as EntityPlayer).Player as IServerPlayer;
                int[] factors = (entity.WatchedAttributes.GetTreeAttribute("genome")["sequence"] as IntArrayAttribute).value;
                if (factors?.Length < 5) return;

                splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("farmlife:genetitle", entity.GetName()), EnumChatType.Notification);
                splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("farmlife:genehealth", Lang.Get("farmlife:genelevel-" + factors[0])), EnumChatType.Notification);
                splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("farmlife:genespeed", Lang.Get("farmlife:genelevel-" + factors[1])), EnumChatType.Notification);
                splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("farmlife:genedigest", Lang.Get("farmlife:genelevel-" + factors[2])), EnumChatType.Notification);
                splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("farmlife:genehunger", Lang.Get("farmlife:genelevel-" + factors[3])), EnumChatType.Notification);
                splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("farmlife:generesist", Lang.Get("farmlife:genelevel-" + factors[4])), EnumChatType.Notification);

                EntityBehaviorFoodGrow fg = entity.GetBehavior<EntityBehaviorFoodGrow>();
                if (fg != null) splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("farmlife:growthInfo", fg.Age), EnumChatType.Notification);

                EntityBehaviorMultiplyBase fm = entity.GetBehavior<EntityBehaviorMultiplyBase>();
                if (fm != null) 
                {
                    int births = entity.WatchedAttributes.GetTreeAttribute("multiply")?.GetInt("birthingEvents") ?? 0;
                    splr.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get("farmlife:birthingInfo", births, (entity.Properties.Attributes?["maxBirths"].AsInt(1) ?? 1)), EnumChatType.Notification); 
                }
            }
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            if (!entity.Alive) return;
            hunger = entity.WatchedAttributes.GetTreeAttribute("hunger");
            if (hunger != null)
            {
                infotext.AppendLine(Lang.Get("farmlife:saturation", CurrentSat, MaxSat, Math.Floor(SatPerc*100f)));

                if (IsHungry) infotext.AppendLine(Lang.Get("farmlife:malnourished"));

                if (entity.WatchedAttributes.GetInt("generation") < 1)
                {
                    if (!entity.WatchedAttributes.GetBool("playerFed")) infotext.AppendLine(Lang.Get("farmlife:feral"));
                }
            }
        }

        public bool GetTrough()
        {
            if (entity.Api.Side != EnumAppSide.Server || porregistry == null || !entity.WatchedAttributes.GetBool("playerFed")) return false;
            
            IAnimalFoodSource targetPoi = (IAnimalFoodSource)porregistry.GetNearestPoi(entity.ServerPos.XYZ, 48, (poi) =>
            {
                if (poi.Type != "food" || !(poi is BlockEntityTrough)) return false;
                IAnimalFoodSource foodPoi;

                if ((foodPoi = poi as IAnimalFoodSource)?.IsSuitableFor(entity) == true)
                {
                    float leftoverFood = entity.Stats.GetBlended("hungerrate") + (MaxSat - CurrentSat);
                    
                    while (leftoverFood > 0 && foodPoi.IsSuitableFor(entity))
                    {
                        float eaten = foodPoi.ConsumeOnePortion() * entity.Stats.GetBlended("digestion");
                        leftoverFood -= eaten;
                        CurrentSat += eaten;
                    }

                    CurrentSat = hunger.GetFloat("saturation") - leftoverFood;

                    return true;
                }

                return false;
            });

            if (targetPoi != null) return true;
                return false;
        }

        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();

            CreateGenome();
        }

        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();

            CreateGenome();
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            if (entity.WatchedAttributes.GetInt("generation") >= 3 && damageSource?.SourceEntity != null && damageSource.SourceEntity.HasBehavior<EntityBehaviorHealth>())
            {
                if (entityUtil == null) entityUtil = entity.Api.ModLoader.GetModSystem<EntityPartitioning>();

                if (entityUtil != null)
                {
                    entityUtil.WalkEntities(entity.SidedPos.XYZ, 30, (e) => {

                        EntityBehaviorPetCommand pc = e.GetBehavior<EntityBehaviorPetCommand>();

                        if (pc != null) pc.AddThreat(damageSource.SourceEntity, EnumPetAggro.Protect);

                        return true;
                    });
                }
            }

            base.OnEntityReceiveDamage(damageSource, ref damage);
        }

        private void CreateGenome()
        {
            ITreeAttribute genome;

            if ((genome = entity.WatchedAttributes.GetTreeAttribute("genome")) == null)
            {
                genome = entity.WatchedAttributes.GetOrAddTreeAttribute("genome");

                int[] spread = new int[] { 3, 3, 3, 3, 3 };

                for (int i = 0; i < 20; i++)
                {
                    int takefrom = entity.World.Rand.Next(spread.Length);
                    int giveto = entity.World.Rand.Next(spread.Length);

                    if (takefrom != giveto && spread[takefrom] > 1 && spread[giveto] < 5)
                    {
                        int diff = entity.World.Rand.Next(1, Math.Min(spread[takefrom] - 1, 5 - spread[giveto]));
                        spread[takefrom] -= diff;
                        spread[giveto] += diff;
                    }
                }

                genome["sequence"] = new IntArrayAttribute(spread.Shuffle(entity.World.Rand));
            }

            int[] sequence = (genome["sequence"] as IntArrayAttribute).value;

            entity.Stats.Set("maxhealthExtraPoints", "genes", -1.5f + (sequence[0] * 0.5f), true); // 1 = -1, 2 = -0.5 3: 0, 4 = +0.5, 5 = +1
            entity.Stats.Set("walkspeed", "genes", -0.3f + (sequence[1] * 0.1f), true); // 1 = -0.2, 2 = -0.1 3: 0, 4 = +0.1, 5 = +0.2
            entity.Stats.Set("digestion", "genes", -0.75f + (sequence[2] * 0.25f), true); // 1 = -0.5, 2 = -0.25 3: 0, 4 = +0.25, 5 = +0.5
            entity.Stats.Set("hungerrate", "genes", 0.75f + (sequence[3] * -0.25f), true); // 1 = +0.5, 2 = +0.25 3: 0, 4 = -0.25, 5 = -0.5
            entity.Stats.Set("vulenrability", "genes", 0.375f + (sequence[4] * -0.125f), true); // 1 = +0.25, 2 = +0.125 3: 0, 4 = -0.125, 5 = -0.25
        }

        public EntityBehaviorConsume(Entity entity) : base(entity)
        {
        }

        public override string PropertyName()
        {
            return "consume";
        }
    }
}
