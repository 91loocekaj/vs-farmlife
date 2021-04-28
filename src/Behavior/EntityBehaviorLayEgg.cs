using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API;

namespace Farmlife
{
    public class EntityBehaviorLayEgg : EntityBehavior
    {
        ITreeAttribute createBlockTree;
        JsonObject attributes;
        long callbackId;

        internal float MinHourDelay
        {
            get { return attributes["minHourDelay"].AsFloat(24); }
        }

        internal float MaxHourDelay
        {
            get { return attributes["maxHourDelay"].AsFloat(26); }
        }

        internal float RndHourDelay
        {
            get
            {
                float min = MinHourDelay;
                float max = MaxHourDelay;
                return min + (float)entity.World.Rand.NextDouble() * (max - min);
            }
        }

        internal float eggSaturation
        {
            get { return attributes["eggSaturation"].AsFloat(0.75f); }
        }

        internal int nestRange
        {
            get { return attributes["nestRange"].AsInt(6); }
        }

        internal AssetLocation[] eggCodes
        {
            get
            {
                string[] codes = attributes["eggCodes"].AsArray(new string[0]);
                AssetLocation[] locs = new AssetLocation[codes.Length];
                for (int i = 0; i < locs.Length; i++) locs[i] = new AssetLocation(codes[i]);
                return locs;
            }
        }



        internal double TotalHoursUntilPlace
        {
            get { return createBlockTree.GetDouble("TotalHoursUntilPlace"); }
            set { createBlockTree.SetDouble("TotalHoursUntilPlace", value); }
        }



        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            this.attributes = attributes;

            createBlockTree = entity.WatchedAttributes.GetTreeAttribute("behaviorLayEgg");

            if (createBlockTree == null)
            {
                entity.WatchedAttributes.SetAttribute("behaviorLayEgg", createBlockTree = new TreeAttribute());
                TotalHoursUntilPlace = entity.World.Calendar.TotalHours + RndHourDelay;
            }

            callbackId = entity.World.RegisterCallback(CheckShouldPlace, 3000);
        }

        private void CheckShouldPlace(float dt)
        {
            if (!entity.Alive) return;
            callbackId = entity.World.RegisterCallback(CheckShouldPlace, 3000);
            if (entity.Swimming || entity.FeetInLiquid) return; // Quick fix for chicken laying eggs in water


            if (entity.World.Calendar == null || entity.World.Calendar.TotalHours < TotalHoursUntilPlace) return;

            TotalHoursUntilPlace += RndHourDelay;
            float sat = GetSaturation();

            if (sat >= (entity.Properties.Attributes?["maxSaturation"].AsFloat(20f) ?? 20f) * eggSaturation)
            {
                AssetLocation[] codes = eggCodes;
                Block block = entity.World.GetBlock(codes[entity.World.Rand.Next(codes.Length)]);
                if (block == null) return;

                bool placed;

                if (!TryToNest()) placed =
                    TryPlace(block, 0, 0, 0) ||
                    TryPlace(block, 1, 0, 0) ||
                    TryPlace(block, 0, 0, -1) ||
                    TryPlace(block, -1, 0, 0) ||
                    TryPlace(block, 0, 0, 1)
                ;
                else placed = true;

                if (!placed) return;
            }


            entity.World.FrameProfiler.Mark("entity-createblock");
        }

        private bool TryPlace(Block block, int dx, int dy, int dz)
        {
            IBlockAccessor blockAccess = entity.World.BlockAccessor;
            BlockPos pos = entity.ServerPos.XYZ.AsBlockPos.Add(dx, dy, dz);
            Block blockAtPos = blockAccess.GetBlock(pos);

            if (blockAtPos.IsReplacableBy(block) && blockAccess.GetBlock(pos.X, pos.Y - 1, pos.Z).SideSolid[BlockFacing.UP.Index])
            {
                blockAccess.SetBlock(block.BlockId, pos);

                // Instantly despawn the block again if it expired already
                BlockEntityTransient betran = blockAccess.GetBlockEntity(pos) as BlockEntityTransient;
                betran?.SetPlaceTime(TotalHoursUntilPlace);

                if (betran?.IsDueTransition() == true)
                {
                    blockAccess.SetBlock(0, pos);
                }

                return true;
            }

            return false;
        }

        public bool TryToNest()
        {
            BlockPos searchEnd = new BlockPos(entity.ServerPos.AsBlockPos.X + nestRange, entity.ServerPos.AsBlockPos.Y + 3, entity.ServerPos.AsBlockPos.Z + nestRange);
            BlockPos searchStart = new BlockPos(entity.ServerPos.AsBlockPos.X - nestRange, entity.ServerPos.AsBlockPos.Y - 3, entity.ServerPos.AsBlockPos.Z - nestRange);
            bool result = false;

            entity.World.BlockAccessor.WalkBlocks(searchStart, searchEnd, (Block nest, BlockPos nestPos) =>
            {
                if (result) return;
                if (nest.Code.Path == "henbox-empty")
                {
                    Block upgrade = entity.World.BlockAccessor.GetBlock(new AssetLocation("game:henbox-1egg"));
                    entity.World.BlockAccessor.SetBlock(upgrade.Id, nestPos);
                    result = true;
                }
                else if (nest.Code.Path == "henbox-1egg")
                {
                    Block upgrade = entity.World.BlockAccessor.GetBlock(new AssetLocation("game:henbox-2eggs"));
                    entity.World.BlockAccessor.SetBlock(upgrade.Id, nestPos);
                    result = true;
                }
                else if (nest.Code.Path == "henbox-2eggs")
                {
                    Block upgrade = entity.World.BlockAccessor.GetBlock(new AssetLocation("game:henbox-3eggs"));
                    entity.World.BlockAccessor.SetBlock(upgrade.Id, nestPos);
                    result = true;
                }
                else if (nest.Code.Path == "egg-chicken-1")
                {
                    Block upgrade = entity.World.BlockAccessor.GetBlock(new AssetLocation("game:egg-chicken-2"));
                    entity.World.BlockAccessor.SetBlock(upgrade.Id, nestPos);
                    result = true;
                }
                else if (nest.Code.Path == "egg-chicken-2")
                {
                    Block upgrade = entity.World.BlockAccessor.GetBlock(new AssetLocation("game:egg-chicken-3"));
                    entity.World.BlockAccessor.SetBlock(upgrade.Id, nestPos);
                    result = true;
                }
            });

            return result;
        }

        float GetSaturation()
        {
            ITreeAttribute tree = entity.WatchedAttributes.GetTreeAttribute("hunger");
            if (tree == null) return 0;

            return tree.GetFloat("saturation", 0);
        }

        public override void OnEntityDespawn(EntityDespawnReason despawn)
        {
            entity.World.UnregisterCallback(callbackId);
        }

        public EntityBehaviorLayEgg(Entity entity) : base(entity)
        {
        }

        public override string PropertyName()
        {
            return "layegg";
        }
    }

}
