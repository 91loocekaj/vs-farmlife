using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Farmlife
{
    [HarmonyPatch(typeof(EntityBehaviorHunger), "OnEntityReceiveSaturation")]
    class SeekFoodPatch
    {
        [HarmonyPrepare]
        static bool Prepare()
        {
            return true;
        }

        [HarmonyPrefix]
        static void Prefix(ref float saturation, EntityBehaviorHunger __instance)
        {
            saturation *= __instance.entity.Stats.GetBlended("digestion");
        }
    }

    [HarmonyPatch(typeof(LooseItemFoodSource), "ConsumeOnePortion")]
    class OneAtATimePatch
    {
        [HarmonyPrepare]
        static bool Prepare()
        {
            return true;
        }

        [HarmonyPrefix]
        static bool Prefix(LooseItemFoodSource __instance, ref EntityItem ___entity, ref float __result)
        {
            if (___entity.Itemstack.StackSize <= 0)
            {
                ___entity.Die();
                __result = 0f;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Entity))]
    public class FeedAfterDeath
    {
        [HarmonyPrepare]
        static bool Prepare(MethodBase original, Harmony harmony)
        {
            //From Melchoir
            if (original != null)
            {
                foreach (var patched in harmony.GetPatchedMethods())
                {
                    if (patched.Name == original.Name) return false;
                }
            }

            return true;
        }

        [HarmonyPatch("Die")]
        [HarmonyPostfix]
        static void EatPrey(Entity __instance, DamageSource damageSourceForDeath = null)
        {
            EntityBehaviorConsume consume;
            if ((consume = damageSourceForDeath?.SourceEntity?.GetBehavior<EntityBehaviorConsume>()) != null)
            {
                consume.CurrentSat += __instance.Properties.Attributes?["nutrition"].AsFloat(1f) ?? 1f;
            }
        }
    }

    [HarmonyPatch(typeof(EntityBehaviorMultiplyBase))]
    public class MultiplyBaseOverride
    {
        [HarmonyPrepare]
        static bool Prepare()
        {
            return true;
        }

        [HarmonyPatch("GetInfoText")]
        [HarmonyPrefix]
        static bool IgnoreText(EntityBehaviorMultiplyBase __instance, StringBuilder infotext)
        {
            if (__instance.entity.HasBehavior<EntityBehaviorConsume>())
            {
                ITreeAttribute tree = __instance.entity.WatchedAttributes.GetTreeAttribute("multiply");
                if (tree != null)
                {
                    if (tree.GetInt("birthingEvents") >= (__instance.entity.Properties.Attributes?["maxBirths"].AsInt(1) ?? 1))
                    {
                        infotext.AppendLine(Lang.Get("farmlife:infertile"));
                    }
                }

                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(EntityBehaviorMultiply))]
    public class MultiplyOverride
    {
        [HarmonyPrepare]
        static bool Prepare()
        {
            return true;
        }

        [HarmonyPatch("CheckMultiply")]
        [HarmonyPrefix]
        static bool IgnoreBaseCheckMultiply(EntityBehaviorConsume __instance)
        {
            return !__instance.entity.HasBehavior<EntityBehaviorConsume>();
        }
    }

    [HarmonyPatch(typeof(EntityBehaviorGrow))]
    public class GrowOverride
    {
        [HarmonyPrepare]
        static bool Prepare()
        {
            return true;
        }

        [HarmonyPatch("CheckGrowth")]
        [HarmonyPrefix]
        static bool IgnoreBaseCheckGrowth(EntityBehaviorGrow __instance)
        {
            return !__instance.entity.HasBehavior<EntityBehaviorConsume>();
        }
    }

    [HarmonyPatch(typeof(EntityBehaviorMilkable))]
    public class MilkPatches
    {
        [HarmonyPrepare]
        static bool Prepare(MethodBase original, Harmony harmony)
        {
            //From Melchoir
            if (original != null)
            {
                foreach (var patched in harmony.GetPatchedMethods())
                {
                    if (patched.Name == original.Name) return false;
                }
            }

            return true;
        }

        [HarmonyPatch("TryBeginMilking")]
        [HarmonyPrefix]
        static bool FoodMilking(EntityBehaviorMilkable __instance, ref bool __result, ref long ___lastIsMilkingStateTotalMs, ref EntityBehaviorMultiply ___bhmul, ref float ___aggroChance, ref bool ___aggroTested, ref bool ___clientCanContinueMilking, ref float ___lactatingDaysAfterBirth, ref double ___lastMilkedTotalHours, ref ILoadedSound ___milkSound)
        {
            EntityBehaviorConsume bc = __instance.entity.GetBehavior<EntityBehaviorConsume>();
            if (bc == null) return true;

            ___lastIsMilkingStateTotalMs = __instance.entity.World.ElapsedMilliseconds;

            if (bc.IsHungry)
            {
                if (__instance.entity.World.Api is ICoreClientAPI capi)
                {
                    capi.TriggerIngameError(__instance, "notready", Lang.Get("farmlife:milkerror"));
                }
                return false;
            }


            ___bhmul = __instance.entity.GetBehavior<EntityBehaviorMultiply>();
            // Can not be milked when stressed (= caused by aggressive or fleeing emotion states)
            float stressLevel = __instance.entity.WatchedAttributes.GetFloat("stressLevel");
            if (stressLevel > 0.1)
            {
                if (__instance.entity.World.Api is ICoreClientAPI capi)
                {
                    capi.TriggerIngameError(__instance, "notready", Lang.Get("Currently too stressed to be milkable"));
                }
                __result = false;
                return false;
            }

            // Can only be milked for 21 days after giving birth
            double daysSinceBirth = Math.Max(0, __instance.entity.World.Calendar.TotalDays - ___bhmul.TotalDaysLastBirth);
            if (___bhmul != null && daysSinceBirth >= ___lactatingDaysAfterBirth) { __result = false; return false; }

            // Can only be milked every 24 hours
            if (__instance.entity.World.Calendar.TotalHours - ___lastMilkedTotalHours < __instance.entity.World.Calendar.HoursPerDay) { __result = false; return false; }
            int generation = __instance.entity.WatchedAttributes.GetInt("generation", 0);
            ___aggroChance = Math.Min(1 - generation / 3f, 0.95f);
            ___aggroTested = false;
            ___clientCanContinueMilking = true;


            if (__instance.entity.World.Side == EnumAppSide.Server)
            {
                AiTaskManager tmgr = __instance.entity.GetBehavior<EntityBehaviorTaskAI>().TaskManager;
                tmgr.StopTask(typeof(AiTaskWander));
                tmgr.StopTask(typeof(AiTaskSeekEntity));
                tmgr.StopTask(typeof(AiTaskSeekFoodAndEat));
                tmgr.StopTask(typeof(AiTaskStayCloseToEntity));
            }
            else
            {
                if (__instance.entity.World is IClientWorldAccessor cworld)
                {
                    ___milkSound?.Dispose();
                    ___milkSound = cworld.LoadSound(new SoundParams()
                    {
                        DisposeOnFinish = true,
                        Location = new AssetLocation("game:sounds/creature/sheep/milking.ogg"),
                        Position = __instance.entity.Pos.XYZFloat,
                        SoundType = EnumSoundType.Sound
                    });

                    ___milkSound.Start();
                }
            }

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(BlockEntityHenBox))]
    public class HenBoxEntityPatches
    {
        [HarmonyPrepare]
        static bool Prepare(MethodBase original, Harmony harmony)
        {
            //From Melchoir
            if (original != null)
            {
                foreach (var patched in harmony.GetPatchedMethods())
                {
                    if (patched.Name == original.Name) return false;
                }
            }

            return true;
        }

        [HarmonyPatch("On1500msTick")]
        [HarmonyPrefix]
        static bool GenePasson(float dt, BlockEntityHenBox __instance, ref double ___timeToIncubate, ref double ___occupiedTimeLast, ref AssetLocation[] ___chickNames, ref int[] ___parentGenerations)
        {
            if (__instance.occupier == null || ___timeToIncubate == 0) return true;
            
            double newTime = __instance.Api.World.Calendar.TotalDays;
            if (__instance.occupier != null && __instance.occupier.Alive)   //Does this need a more sophisticated check, i.e. is the occupier's position still here?  (Also do we reset the occupier variable to null if save and re-load?)
            {
                if (newTime > ___occupiedTimeLast)
                {
                    ___timeToIncubate -= newTime - ___occupiedTimeLast;
                    __instance.MarkDirty();
                }
            }
            ___occupiedTimeLast = newTime;

            if (___timeToIncubate <= 0)
            {
                ___timeToIncubate = 0;

                int eggs = __instance.Block.LastCodePart()[0];
                eggs = eggs <= '9' && eggs >= '0' ? eggs - '0' : 0;

                Random rand = __instance.Api.World.Rand;

                ITreeAttribute Egg = __instance.occupier.WatchedAttributes.GetTreeAttribute("genome");
                ITreeAttribute Sperm = __instance.occupier.WatchedAttributes.GetTreeAttribute("multiply")?.GetTreeAttribute("sperm");

                int[] dadGenes = null;
                int[] momGenes = null;

                if (Sperm != null && Egg != null)
                {
                    dadGenes = (Sperm["sequence"] as IntArrayAttribute).value;
                    momGenes = (Egg["sequence"] as IntArrayAttribute).value;
                }

                for (int c = 0; c < eggs; c++)
                {
                    AssetLocation chickName = ___chickNames[c];
                    if (chickName == null) continue;
                    int generation = ___parentGenerations[c];
                    
                    EntityProperties childType = __instance.Api.World.GetEntityType(chickName);
                    if (childType == null) continue;
                    Entity childEntity = __instance.Api.World.ClassRegistry.CreateEntity(childType);
                    if (childEntity == null) continue;

                    if (momGenes != null && dadGenes != null)
                    {
                        ITreeAttribute childGenome = childEntity.WatchedAttributes.GetOrAddTreeAttribute("genome");
                        int[] childSequence = new int[Math.Min(momGenes.Length, dadGenes.Length)];

                        for (int i = 0; i < childSequence.Length; i++)
                        {
                            if (momGenes[i] != dadGenes[i]) childSequence[i] = __instance.Api.World.Rand.Next(Math.Min(momGenes[i], dadGenes[i]), Math.Max(momGenes[i], dadGenes[i]) + 1); else childSequence[i] = momGenes[i];
                        }

                        childGenome["sequence"] = new IntArrayAttribute(childSequence);
                    }

                    childEntity.ServerPos.SetFrom(new EntityPos(__instance.Position.X + (rand.NextDouble() - 0.5f) / 5f, __instance.Position.Y, __instance.Position.Z + (rand.NextDouble() - 0.5f) / 5f, (float)rand.NextDouble() * GameMath.TWOPI));
                    childEntity.ServerPos.Motion.X += (rand.NextDouble() - 0.5f) / 200f;
                    childEntity.ServerPos.Motion.Z += (rand.NextDouble() - 0.5f) / 200f;

                    childEntity.Pos.SetFrom(childEntity.ServerPos);
                    __instance.Api.World.SpawnEntity(childEntity);
                    childEntity.Attributes.SetString("origin", "reproduction");
                    if (generation > 0 || __instance.occupier.WatchedAttributes.GetBool("playerFed")) childEntity.WatchedAttributes.SetInt("generation", generation + 1);
                }


                Block replacementBlock = __instance.Api.World.GetBlock(new AssetLocation(__instance.Block.FirstCodePart() + "-empty"));
                __instance.Api.World.BlockAccessor.ExchangeBlock(replacementBlock.Id, __instance.Pos);
                __instance.Api.World.SpawnCubeParticles(__instance.Pos.ToVec3d().Add(0.5, 0.5, 0.5), new ItemStack(__instance.Block), 1, 20, 1, null);
                __instance.Block = replacementBlock;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(BlockEntityTrough))]
    public class TroughEntityPatches
    {
        [HarmonyPrepare]
        static bool Prepare(MethodBase original, Harmony harmony)
        {
            //From Melchoir
            if (original != null)
            {
                foreach (var patched in harmony.GetPatchedMethods())
                {
                    if (patched.Name == original.Name) return false;
                }
            }

            return true;
        }

        [HarmonyPatch("OnInteract")]
        [HarmonyPrefix]
        static bool MultiIn(IPlayer byPlayer, BlockSelection blockSel, BlockEntityTrough __instance, ref bool __result)
        {
            if (!byPlayer.Entity.Controls.Sneak) return true;

            ItemSlot handSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (handSlot.Empty) { __result = false; return false; }

            ItemStack[] stacks = __instance.GetNonEmptyContentStacks();


            ContentConfig contentConf = ItemSlotTrough.getContentConfig(__instance.Api.World, __instance.contentConfigs, handSlot);
            if (contentConf == null) { __result = false; return false; }

            // Add new
            if (stacks.Length == 0)
            {
                if (handSlot.StackSize >= contentConf.QuantityPerFillLevel * 4)
                {
                    __instance.Inventory[0].Itemstack = handSlot.TakeOut(contentConf.QuantityPerFillLevel * 4);
                    __instance.Inventory[0].MarkDirty();
                    { __result = true; return false; }
                }

                { __result = false; return false; }
            }

            // Or merge
            bool canAdd =
                handSlot.Itemstack.Equals(__instance.Api.World, stacks[0], GlobalConstants.IgnoredStackAttributes) &&
                handSlot.StackSize >= contentConf.QuantityPerFillLevel * 4 &&
                stacks[0].StackSize <= (contentConf.QuantityPerFillLevel * contentConf.MaxFillLevels) -(contentConf.QuantityPerFillLevel * 4)
            ;

            if (canAdd)
            {
                handSlot.TakeOut(contentConf.QuantityPerFillLevel * 4);
                __instance.Inventory[0].Itemstack.StackSize += contentConf.QuantityPerFillLevel * 4;
                __instance.Inventory[0].MarkDirty();
                { __result = true; return false; }
            }

            { __result = false; return false; }
        }
    }
}
