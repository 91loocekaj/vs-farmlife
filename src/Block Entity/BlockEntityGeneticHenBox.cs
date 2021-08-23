using Vintagestory.API.Common;
using Vintagestory.GameContent;
using System.Text;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Common.Entities;
using System;
using Vintagestory.API.MathTools;
using Vintagestory.API.Client;

namespace Farmlife
{
    public class BlockEntityGeneticHenBox : BlockEntity, IAnimalNest
    {
        internal InventoryGeneric inventory;
        string fullCode = "1egg";

        public Size2i AtlasSize => (Api as ICoreClientAPI).BlockTextureAtlas.Size;

        public Vec3d Position => Pos.ToVec3d().Add(0.5, 0.5, 0.5);
        public string Type => "nest";

        public Entity occupier;

        ITreeAttribute[] motherGenes = new ITreeAttribute[10];
        ITreeAttribute[] fatherGenes = new ITreeAttribute[10];
        private int[] parentGenerations = new int[10];
        private AssetLocation[] chickNames = new AssetLocation[10];
        private double timeToIncubate;
        private double occupiedTimeLast;


        public BlockEntityGeneticHenBox()
        {
        }


        public bool IsSuitableFor(Entity entity)
        {
            return entity is EntityAgent && entity.Code.Path == "chicken-hen";
        }

        public bool Occupied(Entity entity)
        {
            return occupier != null && occupier != entity;
        }

        public void SetOccupier(Entity entity)
        {
            occupier = entity;
        }

        public float DistanceWeighting => 2 / (CountEggs() + 2);


        public bool TryAddEgg(Entity entity, string chickCode, double incubationTime)
        {
            if (Block.LastCodePart() == fullCode)
            {
                if (timeToIncubate == 0)
                {
                    timeToIncubate = incubationTime;
                    occupiedTimeLast = entity.World.Calendar.TotalDays;
                }
                this.MarkDirty();
                return false;
            }

            timeToIncubate = 0;
            int eggs = CountEggs();
            parentGenerations[eggs] = entity.WatchedAttributes.GetInt("generation", 0);
            chickNames[eggs] = chickCode == null ? null : entity.Code.CopyWithPath(chickCode);
            //Add genetic material
            motherGenes[eggs] = entity.WatchedAttributes.GetTreeAttribute("genome") as TreeAttribute;
            fatherGenes[eggs] = entity.WatchedAttributes.GetTreeAttribute("multiply")?.GetTreeAttribute("sperm") as TreeAttribute;
            eggs++;
            Block replacementBlock = Api.World.GetBlock(new AssetLocation(Block.FirstCodePart() + "-" + eggs + (eggs > 1 ? "eggs" : "egg")));
            if (replacementBlock == null)
            {
                return false;
            }
            Api.World.BlockAccessor.ExchangeBlock(replacementBlock.Id, this.Pos);
            this.Block = replacementBlock;
            this.MarkDirty();

            return true;
        }

        private int CountEggs()
        {
            int eggs = Block.LastCodePart()[0];
            return eggs <= '9' && eggs >= '0' ? eggs - '0' : 0;
        }

        private void On1500msTick(float dt)
        {
            if (timeToIncubate == 0) return;

            double newTime = Api.World.Calendar.TotalDays;
            if (occupier != null && occupier.Alive)   //Does this need a more sophisticated check, i.e. is the occupier's position still here?  (Also do we reset the occupier variable to null if save and re-load?)
            {
                if (newTime > occupiedTimeLast)
                {
                    timeToIncubate -= newTime - occupiedTimeLast;
                    this.MarkDirty();
                }
            }
            occupiedTimeLast = newTime;

            if (timeToIncubate <= 0)
            {
                timeToIncubate = 0;
                int eggs = CountEggs();
                Random rand = Api.World.Rand;

                for (int c = 0; c < eggs; c++)
                {
                    AssetLocation chickName = chickNames[c];
                    if (chickName == null) continue;
                    int generation = parentGenerations[c];

                    int[] dadGenes = null;
                    int[] momGenes = null;

                    if (fatherGenes[c] != null && motherGenes[c] != null)
                    {
                        dadGenes = (fatherGenes[c]["sequence"] as IntArrayAttribute).value;
                        momGenes = (motherGenes[c]["sequence"] as IntArrayAttribute).value;
                    }

                    EntityProperties childType = Api.World.GetEntityType(chickName);
                    if (childType == null) continue;
                    Entity childEntity = Api.World.ClassRegistry.CreateEntity(childType);
                    if (childEntity == null) continue;

                    if (momGenes != null && dadGenes != null)
                    {
                        ITreeAttribute childGenome = childEntity.WatchedAttributes.GetOrAddTreeAttribute("genome");
                        int[] childSequence = new int[Math.Min(momGenes.Length, dadGenes.Length)];

                        for (int i = 0; i < childSequence.Length; i++)
                        {
                            if (momGenes[i] != dadGenes[i]) childSequence[i] = Api.World.Rand.Next(Math.Min(momGenes[i], dadGenes[i]), Math.Max(momGenes[i], dadGenes[i]) + 1); else childSequence[i] = momGenes[i];
                        }

                        childGenome["sequence"] = new IntArrayAttribute(childSequence);
                    }

                    childEntity.ServerPos.SetFrom(new EntityPos(this.Position.X + (rand.NextDouble() - 0.5f) / 5f, this.Position.Y, this.Position.Z + (rand.NextDouble() - 0.5f) / 5f, (float)rand.NextDouble() * GameMath.TWOPI));
                    childEntity.ServerPos.Motion.X += (rand.NextDouble() - 0.5f) / 200f;
                    childEntity.ServerPos.Motion.Z += (rand.NextDouble() - 0.5f) / 200f;

                    childEntity.Pos.SetFrom(childEntity.ServerPos);
                    Api.World.SpawnEntity(childEntity);
                    childEntity.Attributes.SetString("origin", "reproduction");
                    if (generation > 0 || occupier.WatchedAttributes.GetBool("playerFed")) childEntity.WatchedAttributes.SetInt("generation", generation + 1);
                }


                Block replacementBlock = Api.World.GetBlock(new AssetLocation(Block.FirstCodePart() + "-empty"));
                Api.World.BlockAccessor.ExchangeBlock(replacementBlock.Id, this.Pos);
                this.Api.World.SpawnCubeParticles(Pos.ToVec3d().Add(0.5, 0.5, 0.5), new ItemStack(this.Block), 1, 20, 1, null);
                this.Block = replacementBlock;
            }
        }


        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            fullCode = this.Block.Attributes?["fullVariant"]?.AsString(null);
            if (fullCode == null) fullCode = "1egg";

            if (api.Side == EnumAppSide.Server)
            {
                api.ModLoader.GetModSystem<POIRegistry>().AddPOI(this);
                RegisterGameTickListener(On1500msTick, 1500);
            }
        }


        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
        }


        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
            }
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            if (Api?.Side == EnumAppSide.Server)
            {
                Api.ModLoader.GetModSystem<POIRegistry>().RemovePOI(this);
            }
        }


        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetDouble("inc", timeToIncubate);
            tree.SetDouble("occ", occupiedTimeLast);
            for (int i = 0; i < 10; i++)
            {
                tree.SetInt("gen" + i, parentGenerations[i]);
                AssetLocation chickName = chickNames[i];
                if (chickName != null) tree.SetString("chick" + i, chickName.ToShortString());
                if (fatherGenes[i] != null) tree["father" + i] = fatherGenes[i];
                if (motherGenes[i] != null) tree["mother" + i] = motherGenes[i];
            }
            
        }


        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            timeToIncubate = tree.GetDouble("inc");
            occupiedTimeLast = tree.GetDouble("occ");
            for (int i = 0; i < 10; i++)
            {
                parentGenerations[i] = tree.GetInt("gen" + i);
                string chickName = tree.GetString("chick" + i);
                chickNames[i] = chickName == null ? null : new AssetLocation(chickName);
                motherGenes[i] = tree.GetTreeAttribute("mother" + i);
                fatherGenes[i] = tree.GetTreeAttribute("father" + i);
            }
        }


        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            int eggCount = CountEggs();
            int fertileCount = 0;
            for (int i = 0; i < eggCount; i++) if (chickNames[i] != null) fertileCount++;
            if (fertileCount > 0)
            {
                if (fertileCount > 1)
                    dsc.AppendLine(Lang.Get("{0} fertile eggs", fertileCount));
                else
                    dsc.AppendLine(Lang.Get("1 fertile egg"));

                if (timeToIncubate >= 1.5)
                    dsc.AppendLine(Lang.Get("Incubation time remaining: {0:0} days", timeToIncubate));
                else if (timeToIncubate >= 0.75)
                    dsc.AppendLine(Lang.Get("Incubation time remaining: 1 day"));
                else if (timeToIncubate > 0)
                    dsc.AppendLine(Lang.Get("Incubation time remaining: {0:0} hours", timeToIncubate * 24));

                if (occupier == null && Block.LastCodePart() == fullCode)
                    dsc.AppendLine(Lang.Get("A broody hen is needed!"));
            }
            else if (eggCount > 0)
            {
                dsc.AppendLine(Lang.Get("No eggs are fertilized"));
            }
        }
    }
}
