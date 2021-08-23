using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;
using System;

namespace Farmlife
{
    public class BlockEntityBehaviorMaggots : BlockEntityBehavior
    {
        double timer;

        AssetLocation maggot = new AssetLocation("game:insect-grub");
        int minSpawn = 3;
        int maxSpawn = 10;
        int minHours = 72;
        int maxHours = 120;

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);

            if (properties != null)
            {
                maggot = new AssetLocation(properties["maggot"].AsString("game:insect-grub"));
                minSpawn = properties["minSpawn"].AsInt(3);
                maxSpawn = properties["maxSpawn"].AsInt(10);
                minHours = properties["minHours"].AsInt(72);
                maxHours = properties["maxHours"].AsInt(120);
            }

            if (timer == 0) timer = api.World.Calendar.TotalHours;
            Blockentity.RegisterGameTickListener(AttractFlies, 3000);
        }

        public void AttractFlies(float dt)
        {
            if (Api.Side != EnumAppSide.Server || timer > Api.World.Calendar.TotalHours) return;

            BlockEntityContainer bin = Blockentity as BlockEntityContainer;
            BlockPos chutePos = Blockentity.Pos.DownCopy();
            BlockEntityContainer chute = (Api.World.BlockAccessor.GetBlockEntity(chutePos) as BlockEntityContainer);
            ClimateCondition conds = Api.World.BlockAccessor.GetClimateAt(Blockentity.Pos);

            if (!(Api.World.BlockAccessor.GetBlock(chutePos) is BlockHopper) || chute == null || conds.Temperature <= -10)
            {
                timer = Api.World.Calendar.TotalHours + 48;
                return;
            }

            int maggotGroup = Api.World.Rand.Next(minSpawn, maxSpawn + 1);
            int pupaeSwarm = 0;

            for (int i = 0; i < bin.Inventory.Count; i++)
            {
                if (bin.Inventory[i].Itemstack?.Collectible.Code.Path == "rot")
                {
                    pupaeSwarm = Math.Min(bin.Inventory[i].StackSize, maggotGroup);
                    bin.Inventory[i].TakeOut(pupaeSwarm);
                    bin.Inventory[i].MarkDirty();
                    break;
                }
            }

            if (pupaeSwarm > 0)
            {
                ItemStack pupae = new ItemStack(Api.World.GetItem(maggot), pupaeSwarm);
                System.Diagnostics.Debug.WriteLine(pupae);
                DummySlot cocoon = new DummySlot(pupae);

                for (int i = 0; i < chute.Inventory.Count; i++)
                {
                    if (chute.Inventory[i].Empty)
                    {
                        chute.Inventory[i].Itemstack = cocoon.TakeOutWhole();
                        chute.Inventory[i].MarkDirty();
                    }
                    else if (cocoon.TryPutInto(Api.World, chute.Inventory[i], chute.Inventory[i].StackSize) > 0) chute.Inventory[i].MarkDirty();

                    if (cocoon.Empty) break;
                }
            }

            timer = Api.World.Calendar.TotalHours + Api.World.Rand.Next(minHours, maxHours + 1);

        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetDouble("timer", timer);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            timer = tree.GetDouble("timer");
        }

        public BlockEntityBehaviorMaggots(BlockEntity blockentity) : base(blockentity)
        {

        }
    }

    

}
