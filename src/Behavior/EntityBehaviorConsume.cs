using System;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Farmlife
{
    public class EntityBehaviorConsume : EntityBehavior
    {
        JsonObject attributes;
        POIRegistry porregistry;

        float maxSat
        {
            get { return entity.Properties.Attributes?["maxSaturation"].AsFloat(20f) ?? 20f; }
        }

        internal AssetLocation[] PoopCodes
        {
            get
            {
                string[] codes = attributes["poopCodes"].AsArray(new string[0]);
                AssetLocation[] locs = new AssetLocation[codes.Length];
                for (int i = 0; i < locs.Length; i++) locs[i] = new AssetLocation(codes[i]);
                return locs;
            }
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            if (entity.Api.Side == EnumAppSide.Client) return;
            this.attributes = attributes;
            porregistry = entity.Api.ModLoader.GetModSystem<POIRegistry>();

            if (entity.WatchedAttributes != null && entity.WatchedAttributes.GetDouble("consumptionTimer", -10000) <= 0) entity.WatchedAttributes.SetDouble("consumptionTimer", entity.World.Calendar.TotalHours);



        }

        public override void OnGameTick(float deltaTime)
        {
            if (!entity.Alive || entity.Api.Side == EnumAppSide.Client) return;

            ITreeAttribute hunger;


            if (entity.World.Calendar.TotalHours - entity.WatchedAttributes.GetDouble("consumptionTimer") < 24 || (hunger = entity.WatchedAttributes.GetTreeAttribute("hunger")) == null) return;

            entity.WatchedAttributes.SetDouble("consumptionTimer", entity.WatchedAttributes.GetDouble("consumptionTimer") + 24);

            float pastSat = hunger.GetFloat("saturation");

            if (entity.World.Calendar.TotalHours - entity.WatchedAttributes.GetDouble("consumptionTimer") < 24 || !GetTrough())
            {
                hunger.SetFloat("saturation", GameMath.Clamp(hunger.GetFloat("saturation") - entity.Stats.GetBlended("hungerrate"), 0, maxSat));
                entity.WatchedAttributes.MarkPathDirty("hunger");
            }

            if (entity.Api.Side == EnumAppSide.Server && pastSat > hunger.GetFloat("saturation") && PoopCodes.Length > 0 && entity.World.Calendar.TotalHours - entity.WatchedAttributes.GetDouble("consumptionTimer") <= 72)
            {
                Block block = entity.World.GetBlock(PoopCodes[entity.World.Rand.Next(PoopCodes.Length)]);
                if (block == null || block.Attributes?.IsTrue("nopoop") == true) return;

                bool placed =
                    TryPlace(block, 0, 0, 0) ||
                    TryPlace(block, 1, 0, 0) ||
                    TryPlace(block, 0, 0, -1) ||
                    TryPlace(block, -1, 0, 0) ||
                    TryPlace(block, 0, 0, 1)
                ;

                if (placed) entity.World.FrameProfiler.Mark("entity-createblock");
            }
        }

        private bool TryPlace(Block block, int dx, int dy, int dz)
        {
            if (entity.Swimming || entity.FeetInLiquid) return false;

            IBlockAccessor blockAccess = entity.World.BlockAccessor;
            BlockPos pos = entity.ServerPos.XYZ.AsBlockPos.Add(dx, dy, dz);
            Block blockAtPos = blockAccess.GetBlock(pos);

            if (blockAtPos.IsReplacableBy(block) && blockAccess.GetBlock(pos.X, pos.Y - 1, pos.Z).SideSolid[BlockFacing.UP.Index])
            {
                blockAccess.SetBlock(block.BlockId, pos);


                return true;
            }

            return false;
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            ITreeAttribute tree = entity.WatchedAttributes.GetTreeAttribute("hunger");
            if (tree != null)
            {
                infotext.AppendLine(Lang.Get("farmlife:saturation", tree.GetFloat("saturation"), entity.Properties?.Attributes?["maxSaturation"].AsFloat(20f) ?? 20f));

                if (entity.WatchedAttributes.GetBool("babyneedfood") && tree.GetFloat("saturation") < (entity.Properties?.Attributes?["maxSaturation"].AsFloat(20f) ?? 20f)) infotext.AppendLine(Lang.Get("farmlife:malnourished"));
            }
        }

        public bool GetTrough()
        {
            if (porregistry == null) return false;
            ITreeAttribute hunger = entity.WatchedAttributes.GetTreeAttribute("hunger");
            IAnimalFoodSource targetPoi = (IAnimalFoodSource)porregistry.GetNearestPoi(entity.ServerPos.XYZ, 48, (poi) =>
            {
                if (poi.Type != "food" || !(poi is BlockEntityTrough)) return false;
                IAnimalFoodSource foodPoi;

                if ((foodPoi = poi as IAnimalFoodSource)?.IsSuitableFor(entity) == true)
                {
                    float leftoverFood = entity.Stats.GetBlended("hungerrate") + (maxSat - hunger.GetFloat("saturation"));
                    
                    while (leftoverFood > 0 && foodPoi.IsSuitableFor(entity))
                    {
                        float eaten = foodPoi.ConsumeOnePortion();
                        leftoverFood -= eaten;
                        hunger.SetFloat("saturation", GameMath.Clamp(hunger.GetFloat("saturation") + eaten, 0, maxSat));
                    }

                    hunger.SetFloat("saturation", GameMath.Clamp(hunger.GetFloat("saturation") - leftoverFood, 0, maxSat));

                    return true;
                }

                return false;
            });

            if (targetPoi != null) return true;
                return false;
        }

        public EntityBehaviorConsume(Entity entity) : base(entity)
        {
        }

        public override string PropertyName()
        {
            return "consume";
        }

        public override void OnEntitySpawn()
        {
            if (entity.WatchedAttributes.GetTreeAttribute("hunger") == null) entity.WatchedAttributes.GetOrAddTreeAttribute("hunger");
        }

        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();
            if (entity.WatchedAttributes.GetTreeAttribute("hunger") == null) entity.WatchedAttributes.GetOrAddTreeAttribute("hunger");
        }
    }
}
