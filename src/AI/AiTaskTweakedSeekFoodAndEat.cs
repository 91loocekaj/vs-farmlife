using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API;
using System;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Util;

namespace Farmlife
{
    public class AiTaskTweakedSeekFoodAndEat : AiTaskBase
    {
        AssetLocation eatSound;

        POIRegistry porregistry;
        int slotNumber;
        int heldID;
        IAnimalFoodSource targetPoi;
        EntityBehaviorConsume bc;

        float moveSpeed = 0.02f;
        long stuckatMs = 0;
        bool nowStuck = false;
        double grazedLast = 0;
        double grazeDigest;

        float eatTime = 1f;

        float eatTimeNow = 0;
        bool soundPlayed = false;
        bool doConsumePortion = true;
        bool eatAnimStarted = false;
        bool playEatAnimForLooseItems = true;

        bool eatLooseItems;
        bool searchPlayerInv;

        HashSet<EnumFoodCategory> eatItemCategories = new HashSet<EnumFoodCategory>();
        HashSet<AssetLocation> eatItemCodes = new HashSet<AssetLocation>();

        float quantityEaten;

        AnimationMetaData eatAnimMeta;
        AnimationMetaData eatAnimMetaLooseItems;

        Dictionary<IAnimalFoodSource, TFailedAttempt> failedSeekTargets = new Dictionary<IAnimalFoodSource, TFailedAttempt>();
        Dictionary<long, long?> failedItems = new Dictionary<long, long?>();
        long currentItem;

        float extraTargetDist;
        long lastPOISearchTotalMs;

        public float MaxSat { get { return (entity.Properties?.Attributes?["maxSaturation"].AsFloat(20f) ?? 20f); } }

        public AiTaskTweakedSeekFoodAndEat(EntityAgent entity) : base(entity)
        {
            porregistry = entity.Api.ModLoader.GetModSystem<POIRegistry>();

            entity.WatchedAttributes.SetBool("doesEat", true);
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);
            if (taskConfig["eatSound"] != null)
            {
                string eatsoundstring = taskConfig["eatSound"].AsString(null);
                if (eatsoundstring != null) eatSound = new AssetLocation(eatsoundstring).WithPathPrefix("sounds/");
            }

            if (taskConfig["movespeed"] != null)
            {
                moveSpeed = taskConfig["movespeed"].AsFloat(0.02f);
            }

            if (taskConfig["searchPlayerInv"] != null)
            {
                searchPlayerInv = taskConfig["searchPlayerInv"].AsBool(false);
            }

            if (taskConfig["eatTime"] != null)
            {
                eatTime = taskConfig["eatTime"].AsFloat(1.5f);
            }

            if (taskConfig["doConsumePortion"] != null)
            {
                doConsumePortion = taskConfig["doConsumePortion"].AsBool(true);
            }

            if (taskConfig["eatLooseItems"] != null)
            {
                eatLooseItems = taskConfig["eatLooseItems"].AsBool(true);
            }

            if (taskConfig["playEatAnimForLooseItems"] != null)
            {
                playEatAnimForLooseItems = taskConfig["playEatAnimForLooseItems"].AsBool(true);
            }

            if (taskConfig["eatItemCategories"] != null)
            {
                foreach (var val in taskConfig["eatItemCategories"].AsArray<EnumFoodCategory>(new EnumFoodCategory[0]))
                {
                    eatItemCategories.Add(val);
                }
            }

            if (taskConfig["eatItemCodes"] != null)
            {
                foreach (var val in taskConfig["eatItemCodes"].AsArray(new AssetLocation[0]))
                {
                    eatItemCodes.Add(val);
                }
            }

            if (taskConfig["eatAnimation"].Exists)
            {
                eatAnimMeta = new AnimationMetaData()
                {
                    Code = taskConfig["eatAnimation"].AsString()?.ToLowerInvariant(),
                    Animation = taskConfig["eatAnimation"].AsString()?.ToLowerInvariant(),
                    AnimationSpeed = taskConfig["eatAnimationSpeed"].AsFloat(1f)
                }.Init();
            }

            if (taskConfig["eatAnimationLooseItems"].Exists)
            {
                eatAnimMetaLooseItems = new AnimationMetaData()
                {
                    Code = taskConfig["eatAnimationLooseItems"].AsString()?.ToLowerInvariant(),
                    Animation = taskConfig["eatAnimationLooseItems"].AsString()?.ToLowerInvariant(),
                    AnimationSpeed = taskConfig["eatAnimationSpeedLooseItems"].AsFloat(1f)
                }.Init();
            }
        }

        public override bool ShouldExecute()
        {
            ITreeAttribute hunger = entity.WatchedAttributes.GetOrAddTreeAttribute("hunger");

            // Don't search more often than every 3 seconds
            if (lastPOISearchTotalMs + 3000 > entity.World.ElapsedMilliseconds) return false;
            if (cooldownUntilMs > entity.World.ElapsedMilliseconds) return false;
            if (cooldownUntilTotalHours > entity.World.Calendar.TotalHours) return false;
            if (whenInEmotionState != null && !entity.GetBehavior<EntityBehaviorEmotionStates>().IsInEmotionState(whenInEmotionState)) return false;
            if (whenNotInEmotionState != null && entity.GetBehavior<EntityBehaviorEmotionStates>().IsInEmotionState(whenNotInEmotionState)) return false;

            bc = entity.GetBehavior<EntityBehaviorConsume>();

            if (!FarmerConfig.Loaded.GluttonyEnabled && bc != null && bc.CurrentSat >= bc.MaxSat) return false;
            

            targetPoi = null;
            extraTargetDist = 0;
            lastPOISearchTotalMs = entity.World.ElapsedMilliseconds;

            entity.World.Api.ModLoader.GetModSystem<EntityPartitioning>().WalkEntities(entity.ServerPos.XYZ, FarmerConfig.Loaded.PathItemRange, (e) =>
            {
                long? previousItem;
                failedItems.TryGetValue(e.EntityId, out previousItem);
                if (previousItem != null && previousItem > world.ElapsedMilliseconds) return true;

                if (e is EntityItem)
                {

                    EntityItem ei = (EntityItem)e;
                    EnumFoodCategory? cat = ei.Itemstack?.Collectible?.NutritionProps?.FoodCategory;
                    if (cat != null && eatItemCategories.Contains((EnumFoodCategory)cat))
                    {

                        targetPoi = new LooseItemFoodSource(ei);
                        currentItem = e.EntityId;
                        return false;
                    }

                    AssetLocation code = ei.Itemstack?.Collectible?.Code;
                    if (code != null && eatItemCodes.Contains(code))
                    {
                        targetPoi = new LooseItemFoodSource(ei);
                        currentItem = e.EntityId;
                        return false;
                    }
                }

                if (e is EntityPlayer eplr)
                {
                    ItemSlot active = eplr.Player.InventoryManager.ActiveHotbarSlot;
                    slotNumber = eplr.Player.InventoryManager.ActiveHotbarSlotNumber;
                    EnumFoodCategory? cat = active?.Itemstack?.Collectible?.NutritionProps?.FoodCategory;
                    if (active != null && !active.Empty && (eatItemCodes.Contains(active.Itemstack.Collectible.Code) || (cat != null && eatItemCategories.Contains((EnumFoodCategory)cat))))
                    {
                        heldID = active.Itemstack.Collectible.Id;
                        targetPoi = new MPlayerPoi(eplr);
                    }
                }

                return true;
            });

            if (targetPoi == null && (bc == null || bc.CurrentSat < bc.MaxSat))
            {
                targetPoi = tryGrazing();

                if (targetPoi == null)
                {
                    targetPoi = porregistry.GetNearestPoi(entity.ServerPos.XYZ, FarmerConfig.Loaded.PathRange, (poi) =>
                    {
                        if (poi.Type != "food") return false;
                        IAnimalFoodSource foodPoi;

                        if ((foodPoi = poi as IAnimalFoodSource)?.IsSuitableFor(entity) == true)
                        {
                            TFailedAttempt attempt;
                            failedSeekTargets.TryGetValue(foodPoi, out attempt);

                            if (attempt == null && FarmerConfig.Loaded.RestrictPathfinding && !lineOfSight(foodPoi))
                            {
                                failedSeekTargets[foodPoi] = attempt = new TFailedAttempt();
                                attempt.Count++;
                                attempt.LastTryMs = world.ElapsedMilliseconds + 60000;
                                return false;
                            }

                            if (attempt == null) return true;

                            if (attempt.LastTryMs < world.ElapsedMilliseconds && (!attempt.closedOff || attempt.LastTryMs + 240000 < world.ElapsedMilliseconds))
                            {
                                return true;
                            }
                        }

                        return false;
                    }) as IAnimalFoodSource;
                }

                
            }

            return targetPoi != null;
        }

        public float MinDistanceToTarget()
        {
            return Math.Max(extraTargetDist + 0.6f, (entity.CollisionBox.X2 - entity.CollisionBox.X1) / 2 + 0.05f);
        }

        public override void StartExecute()
        {
            base.StartExecute();
            stuckatMs = -9999;
            nowStuck = false;
            soundPlayed = false;
            eatTimeNow = 0;
            pathTraverser.NavigateTo(targetPoi.Position, moveSpeed, MinDistanceToTarget() - 0.1f, OnGoalReached, OnStuck, false, 1000);
            eatAnimStarted = false;
        }

        public override bool ContinueExecute(float dt)
        {
            if (targetPoi == null)
            {
                pathTraverser.Stop();
                return false;
            }

            Vec3d pos = targetPoi.Position;

            if (targetPoi is MPlayerPoi)
            {
                IPlayerInventoryManager inv = (targetPoi as MPlayerPoi).plr.Player.InventoryManager;
                if (inv.ActiveHotbarSlotNumber != slotNumber || inv.ActiveHotbarSlot.Empty || inv.ActiveHotbarSlot.Itemstack.Collectible.Id != heldID)
                {
                    
                    return false;
                }
            }

            pathTraverser.CurrentTarget.X = pos.X;
            pathTraverser.CurrentTarget.Y = pos.Y;
            pathTraverser.CurrentTarget.Z = pos.Z;

            Cuboidd targetBox = entity.CollisionBox.ToDouble().Translate(entity.ServerPos.X, entity.ServerPos.Y, entity.ServerPos.Z);
            double distance = targetBox.ShortestDistanceFrom(pos);

            float minDist = MinDistanceToTarget();

            if (distance <= minDist)
            {
                pathTraverser.Stop();

                EntityBehaviorFoodMultiply bh = entity.GetBehavior<EntityBehaviorFoodMultiply>();
                if (bh != null && !bh.ShouldEat) return false;

                if (targetPoi.IsSuitableFor(entity) != true) return false;

                if (eatAnimMeta != null && !eatAnimStarted)
                {
                    if (animMeta != null)
                    {
                        entity.AnimManager.StopAnimation(animMeta.Code);
                    }

                    entity.AnimManager.StartAnimation((targetPoi is LooseItemFoodSource && eatAnimMetaLooseItems != null) ? eatAnimMetaLooseItems : eatAnimMeta);

                    eatAnimStarted = true;
                }

                eatTimeNow += dt;

                if (targetPoi is LooseItemFoodSource foodSource)
                {
                    entity.World.SpawnCubeParticles(entity.ServerPos.XYZ, foodSource.ItemStack, 0.25f, 1, 0.25f + 0.5f * (float)entity.World.Rand.NextDouble());
                }


                if (eatTimeNow > eatTime * 0.75f && !soundPlayed)
                {
                    soundPlayed = true;
                    if (eatSound != null) entity.World.PlaySoundAt(eatSound, entity, null, true, 16, 1);
                }


                if (eatTimeNow >= eatTime)
                {
                    ITreeAttribute tree = entity.WatchedAttributes.GetTreeAttribute("hunger");
                    if (tree == null) entity.WatchedAttributes["hunger"] = tree = new TreeAttribute();

                    if (doConsumePortion && bc != null)
                    {
                        if (targetPoi is LooseItemFoodSource)
                        {
                            float sat = targetPoi.ConsumeOnePortion() * entity.Stats.GetBlended("digestion");
                            quantityEaten += sat;
                            tree.SetFloat("saturation", GameMath.Clamp(sat + tree.GetFloat("saturation", 0), 0, MaxSat));
                            entity.WatchedAttributes.SetDouble("lastMealEatenTotalHours", entity.World.Calendar.TotalHours);
                            entity.WatchedAttributes.MarkPathDirty("hunger");
                            entity.WatchedAttributes.SetBool("playerFed", true);
                        }
                        else
                        {
                            while (targetPoi?.IsSuitableFor(entity) == true && tree.GetFloat("saturation") < MaxSat)
                            {
                                float sat = targetPoi.ConsumeOnePortion() * entity.Stats.GetBlended("digestion");
                                quantityEaten += sat;
                                tree.SetFloat("saturation", GameMath.Clamp(sat + tree.GetFloat("saturation", 0), 0, MaxSat));
                                entity.WatchedAttributes.SetDouble("lastMealEatenTotalHours", entity.World.Calendar.TotalHours);
                                entity.WatchedAttributes.MarkPathDirty("hunger");
                                if (!(targetPoi is GrazingPOI)) entity.WatchedAttributes.SetBool("playerFed", true);
                            }
                        }
                    }
                    else if (doConsumePortion)
                    {
                        float sat = targetPoi.ConsumeOnePortion();
                        quantityEaten += sat;
                        tree.SetFloat("saturation", sat + tree.GetFloat("saturation", 0));
                        entity.WatchedAttributes.SetDouble("lastMealEatenTotalHours", entity.World.Calendar.TotalHours);
                        entity.WatchedAttributes.MarkPathDirty("hunger");
                    }
                    else quantityEaten = 1;

                    failedSeekTargets.Remove(targetPoi);

                    return false;
                }
            }
            else
            {
                if (!pathTraverser.Active)
                {
                    float rndx = (float)entity.World.Rand.NextDouble() * 0.3f - 0.15f;
                    float rndz = (float)entity.World.Rand.NextDouble() * 0.3f - 0.15f;
                    if (!pathTraverser.NavigateTo(targetPoi.Position.AddCopy(rndx, 0, rndz), moveSpeed, MinDistanceToTarget() - 0.15f, OnGoalReached, OnStuck, false, 500))
                    {
                        return false;
                    }
                }
            }


            if (nowStuck && entity.World.ElapsedMilliseconds > stuckatMs + eatTime * 1000)
            {
                return false;
            }


            return true;
        }


        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
            pathTraverser.Stop();

            if (eatAnimMeta != null)
            {
                entity.AnimManager.StopAnimation(eatAnimMeta.Code);
            }

            if (cancelled)
            {
                cooldownUntilTotalHours = 0;
            }

            if (quantityEaten < 1)
            {
                cooldownUntilTotalHours = 0;
            }
            else
            {
                quantityEaten = 0;
            }
        }



        private void OnStuck()
        {
            stuckatMs = entity.World.ElapsedMilliseconds;
            nowStuck = true;

            TFailedAttempt attempt = null;
            failedSeekTargets.TryGetValue(targetPoi, out attempt);
            if (attempt == null)
            {
                failedSeekTargets[targetPoi] = attempt = new TFailedAttempt();
            }

            attempt.Count++;
            attempt.LastTryMs = world.ElapsedMilliseconds + 60000;

            BlockSelection blockSel = null;
            EntitySelection entitySel = null;

            world.RayTraceForSelection(entity.ServerPos.XYZ, targetPoi.Position, ref blockSel, ref entitySel);

            if (targetPoi is LooseItemFoodSource)
            {
                if (entitySel?.Entity?.ServerPos?.XYZ != targetPoi.Position)
                {
                    failedItems[currentItem] = world.ElapsedMilliseconds + 60000;
                    //System.Diagnostics.Debug.WriteLine("Closed Item ");
                }
            }
            else
            {
                if (blockSel == null || blockSel.Position != targetPoi.Position.AsBlockPos) attempt.closedOff = true;
                //System.Diagnostics.Debug.WriteLine(attempt.closedOff + "/" + blockSel);
            }

            targetPoi = null;

        }

        private void OnGoalReached()
        {
            pathTraverser.Active = true;
            failedSeekTargets.Remove(targetPoi);
        }

        private bool lineOfSight(IAnimalFoodSource target)
        {
            BlockSelection blockSel = null;
            EntitySelection entitySel = null;
            bool doubletrough = entity.World.BlockAccessor.GetBlock(target.Position.AsBlockPos) is BlockTroughDoubleBlock;

            world.RayTraceForSelection(entity.ServerPos.XYZ.AddCopy(entity.LocalEyePos), target.Position, ref blockSel, ref entitySel, null, new EntityFilter((ent) => ent == null));

            //System.Diagnostics.Debug.WriteLine(blockSel?.Position + "/" + target.Position.AsBlockPos + "   ");

            if (blockSel != null && doubletrough && entity.World.BlockAccessor.GetBlock(blockSel.Position) is BlockTroughDoubleBlock && target.Position.SquareDistanceTo(blockSel.Position.ToVec3d()) <= 1)
            {
                return true;
            }

            return blockSel?.Position == target.Position.AsBlockPos;
        }

        private IAnimalFoodSource tryGrazing()
        {
            if (entity.World.Calendar.TotalHours - grazeDigest < 1) return null;
            GrazingPOI munchon = null;
            
            BlockPos tmpPos = entity.SidedPos.AsBlockPos.Copy();
            Block nearby;

            for (int x = -1; x < 2; x++)
            {
                for (int z = -1; z < 2; z++)
                {
                    for (int y = 0; y > -2; y--)
                    {
                        //if (Math.Abs(x) + Math.Abs(z) > 1) continue;
                        tmpPos.Set(entity.SidedPos.AsBlockPos);
                        nearby = world.BlockAccessor.GetBlock(tmpPos.Add(x,z,y));

                        if (nearby.Attributes?["grazingProperties"].Exists == true)
                        {
                            Dictionary<string, double> eatList = nearby.Attributes["grazingProperties"]["canEat"].AsObject<Dictionary<string, double>>();

                            if (eatList == null) return null;

                            foreach(var val in eatList)
                            {
                                AssetLocation ani = new AssetLocation(val.Key);
                                if (ani.Equals(entity.Code) || (ani.IsWildCard && WildcardUtil.Match(ani, entity.Code)))
                                {
                                    if (entity.World.Calendar.TotalHours - grazedLast >= val.Value)
                                    {
                                        grazedLast = entity.World.Calendar.TotalHours;
                                        grazeDigest = entity.World.Calendar.TotalHours;
                                        return new GrazingPOI(tmpPos, entity.World.GetBlock(new AssetLocation(nearby.Attributes["grazingProperties"]["eatenBlock"].AsString("air"))).BlockId, entity.World.BlockAccessor,
                                            nearby.Attributes["grazingProperties"]["saturation"].AsFloat(1), nearby.Attributes["grazingProperties"]["health"].AsFloat(0));
                                    }
                                }
                            }
                        }
                    }
                }
            }



            return munchon;
        }
    }

    public class TFailedAttempt
    {
        public long LastTryMs;
        public int Count;
        public bool closedOff;
    }

    public class MPlayerPoi : IAnimalFoodSource
    {
        public EntityPlayer plr;
        Vec3d pos = new Vec3d();

        public MPlayerPoi(EntityPlayer plr)
        {
            this.plr = plr;
        }

        public Vec3d Position
        {
            get
            {
                pos.Set(plr.Pos.X, plr.Pos.Y, plr.Pos.Z);
                return pos;
            }
        }

        public string Type => "food";

        public float ConsumeOnePortion()
        {
            return 0;
        }

        public bool IsSuitableFor(Entity entity)
        {
            return false;
        }
    }

    public class GrazingPOI : IAnimalFoodSource
    {
        public Vec3d Position => grazingBlock.ToVec3d().Add(0.5, 0.5, 0.5);

        public string Type => "food";

        Entity lastChecked;
        BlockPos grazingBlock;
        int afterGraze;
        float health;
        IBlockAccessor blockAccessor;
        bool alreadyMunched = false;
        float food = 1;

        public GrazingPOI(BlockPos pos, int id, IBlockAccessor builder, float nutr = 1, float hp = 0)
        {
            grazingBlock = pos;
            afterGraze = id;
            blockAccessor = builder;
            food = nutr;
            health = hp;
        }

        public float ConsumeOnePortion()
        {
            blockAccessor.SetBlock(afterGraze, grazingBlock);
            alreadyMunched = true;
            if (health != 0) lastChecked.ReceiveDamage(new DamageSource() { Type = health > 0 ? EnumDamageType.Heal : EnumDamageType.Poison, Source = EnumDamageSource.Internal }, health);
            return food;
        }

        public bool IsSuitableFor(Entity entity)
        {
            lastChecked = entity;
            return !alreadyMunched;
        }
    }
}
