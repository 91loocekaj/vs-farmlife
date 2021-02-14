﻿using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API;
using System;
using Vintagestory.API.Common.Entities;

namespace Farmlife
{
    public class AiTaskTweakedSeekFoodAndEat : AiTaskBase
    {
        AssetLocation eatSound;

        POIRegistry porregistry;
        int slotNumber;
        IAnimalFoodSource targetPoi;

        float moveSpeed = 0.02f;
        long stuckatMs = 0;
        bool nowStuck = false;

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
            ITreeAttribute hunger = entity.WatchedAttributes.GetTreeAttribute("hunger");

            if (hunger == null || hunger.GetFloat("saturation") >= (entity.Properties?.Attributes?["maxSaturation"].AsFloat(20f) ?? 20f)) return false;
            // Don't search more often than every 15 seconds
            if (lastPOISearchTotalMs + 3000 > entity.World.ElapsedMilliseconds) return false;
            if (cooldownUntilMs > entity.World.ElapsedMilliseconds) return false;
            if (cooldownUntilTotalHours > entity.World.Calendar.TotalHours) return false;
            if (whenInEmotionState != null && !entity.HasEmotionState(whenInEmotionState)) return false;
            if (whenNotInEmotionState != null && entity.HasEmotionState(whenNotInEmotionState)) return false;

            EntityBehaviorMultiply bh = entity.GetBehavior<EntityBehaviorMultiply>();
            if (bh != null && !bh.ShouldEat && entity.World.Rand.NextDouble() < 0.996) return false; // 0.4% chance go to the food source anyway just because (without eating anything).

            targetPoi = null;
            extraTargetDist = 0;
            lastPOISearchTotalMs = entity.World.ElapsedMilliseconds;

            entity.World.Api.ModLoader.GetModSystem<EntityPartitioning>().WalkEntities(entity.ServerPos.XYZ, 10, (e) =>
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
                    EnumFoodCategory? cat = active.Itemstack?.Collectible?.NutritionProps?.FoodCategory;
                    if (!active.Empty && (eatItemCodes.Contains(active.Itemstack.Collectible.Code) || (cat != null && eatItemCategories.Contains((EnumFoodCategory)cat))))
                    {
                        targetPoi = new MPlayerPoi(eplr);
                    }
                }

                return true;
            });

            if (targetPoi == null)
            {
                targetPoi = porregistry.GetNearestPoi(entity.ServerPos.XYZ, 48, (poi) =>
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

            /*if (targetPoi != null)
            {
                if (targetPoi is BlockEntity || targetPoi is Block)
                {
                    Block block = entity.World.BlockAccessor.GetBlock(targetPoi.Position.AsBlockPos);
                    Cuboidf[] collboxes = block.GetCollisionBoxes(entity.World.BlockAccessor, targetPoi.Position.AsBlockPos);
                    if (collboxes != null && collboxes.Length != 0 && collboxes[0].Y2 > 0.3f)
                    {
                        extraTargetDist = 0.15f;
                    }
                }
            }*/

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
                if ((targetPoi as MPlayerPoi).plr.Player.InventoryManager.ActiveHotbarSlotNumber != slotNumber)
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

                EntityBehaviorMultiply bh = entity.GetBehavior<EntityBehaviorMultiply>();
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

                    if (doConsumePortion)
                    {
                        if (targetPoi is LooseItemFoodSource)
                        {
                            float sat = targetPoi.ConsumeOnePortion() * entity.Stats.GetBlended("digestion");
                            quantityEaten += sat;
                            tree.SetFloat("saturation", sat + tree.GetFloat("saturation", 0));
                            entity.WatchedAttributes.SetDouble("lastMealEatenTotalHours", entity.World.Calendar.TotalHours);
                            entity.WatchedAttributes.MarkPathDirty("hunger");
                        }
                        else
                        {
                            while (targetPoi?.IsSuitableFor(entity) == true && tree.GetFloat("saturation") < (entity.Properties?.Attributes?["maxSaturation"].AsFloat(20f) ?? 20f))
                            {
                                float sat = targetPoi.ConsumeOnePortion() * entity.Stats.GetBlended("digestion");
                                quantityEaten += sat;
                                tree.SetFloat("saturation", sat + tree.GetFloat("saturation", 0));
                                entity.WatchedAttributes.SetDouble("lastMealEatenTotalHours", entity.World.Calendar.TotalHours);
                                entity.WatchedAttributes.MarkPathDirty("hunger");
                            }
                        }
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


        float GetSaturation()
        {
            ITreeAttribute tree = entity.WatchedAttributes.GetTreeAttribute("hunger");
            if (tree == null) entity.WatchedAttributes["hunger"] = tree = new TreeAttribute();

            return tree.GetFloat("saturation");
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
}
