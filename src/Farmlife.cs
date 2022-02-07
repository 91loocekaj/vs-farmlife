using System.Threading.Tasks;
using Vintagestory.API.Common;
using HarmonyLib;
using Vintagestory.GameContent;
using System.Reflection;
using System.Collections.Generic;

namespace Farmlife
{
    public class Farmlife : ModSystem
    {

        private Harmony harmony;

        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);

            try
            {
                FarmerConfig FromDisk;
                if ((FromDisk = api.LoadModConfig<FarmerConfig>("FarmLifeConfig.json")) == null)
                {
                    api.StoreModConfig<FarmerConfig>(FarmerConfig.Loaded, "FarmLifeConfig.json");
                }
                else FarmerConfig.Loaded = FromDisk;
            }
            catch
            {
                api.StoreModConfig<FarmerConfig>(FarmerConfig.Loaded, "FarmLifeConfig.json");
            }

            api.World.Config.SetBool("FLlichenEnabled", FarmerConfig.Loaded.LichenEnabled);
            api.World.Config.SetBool("FLmedievalexpansionEnabled", FarmerConfig.Loaded.MedievalExpansionEnabled);
            api.World.Config.SetBool("FLprimitivesurvivalEnabled", FarmerConfig.Loaded.PrimitiveSurvivalEnabled);
            api.World.Config.SetBool("FLwildfarmingEnabled", FarmerConfig.Loaded.WildFarmingEnabled);
            api.World.Config.SetBool("FLpetsEnabled", FarmerConfig.Loaded.PetsEnabled && !api.ModLoader.IsModEnabled("wolftaming"));
            api.World.Config.SetBool("FLshearingEnabled", FarmerConfig.Loaded.ShearingEnabled);
            api.World.Config.SetBool("FLgrazingEnabled", FarmerConfig.Loaded.GrazingEnabled);
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.RegisterEntityBehaviorClass("consume", typeof(EntityBehaviorConsume));
            api.RegisterEntityBehaviorClass("foodmultiply", typeof(EntityBehaviorFoodMultiply));
            api.RegisterEntityBehaviorClass("foodgrow", typeof(EntityBehaviorFoodGrow));
            api.RegisterEntityBehaviorClass("carry", typeof(EntityBehaviorCarry));
            api.RegisterEntityBehaviorClass("pickup", typeof(EntityBehaviorPickup));
            api.RegisterEntityBehaviorClass("petcommand", typeof(EntityBehaviorPetCommand));
            api.RegisterEntityBehaviorClass("mastercommand", typeof(EntityBehaviorMasterCommand));
            api.RegisterEntityBehaviorClass("shearable", typeof(EntityBehaviorShearable));

            api.RegisterBlockEntityClass("GeneticHenBox", typeof(BlockEntityGeneticHenBox));
            api.RegisterBlockEntityClass("CompostBin", typeof(BlockEntityCompostBin));

            api.RegisterBlockEntityBehaviorClass("Maggots", typeof(BlockEntityBehaviorMaggots));
            api.RegisterBlockEntityBehaviorClass("FoodSource", typeof(BlockEntityBehaviorFoodSource));

            api.RegisterItemClass("ItemHandlingGloves", typeof(ItemHandlingGloves));
            api.RegisterItemClass("ItemShiftCreature", typeof(ItemShiftCreature));
            api.RegisterItemClass("ItemCagedCreature", typeof(ItemCagedCreature));
            api.RegisterItemClass("ItemCollar", typeof(ItemCollar));
            api.RegisterItemClass("ItemPetWhistle", typeof(ItemPetWhistle));


            AiTaskRegistry.Register("seekfoodandeat", typeof(AiTaskTweakedSeekFoodAndEat));
            AiTaskRegistry.Register("seekblockandlay", typeof(AiTaskTweakedSeekBlockAndLay));
            AiTaskRegistry.Register("petseekfoodandeat", typeof(AiTaskPetSeekFoodAndEat));
            AiTaskRegistry.Register("petwander", typeof(AiTaskPetWander));
            AiTaskRegistry.Register("petmeleeattack", typeof(AiTaskPetMeleeAttack));
            AiTaskRegistry.Register("petseekentity", typeof(AiTaskPetSeekEntity));
            AiTaskRegistry.Register("stayclosetomaster", typeof(AiTaskStayCloseToMaster));

            harmony = new Harmony("com.jakecool19.farmlife.animalhunger");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            try
            {
                FarmerConfig FromDisk;
                if ((FromDisk = api.LoadModConfig<FarmerConfig>("FarmLifeConfig.json")) == null)
                {
                    api.StoreModConfig<FarmerConfig>(FarmerConfig.Loaded, "FarmLifeConfig.json");
                }
                else FarmerConfig.Loaded = FromDisk;
            }
            catch
            {
                api.StoreModConfig<FarmerConfig>(FarmerConfig.Loaded, "FarmLifeConfig.json");
            }
        }

        public override void Dispose()
        {
            harmony.UnpatchAll(harmony.Id);
            base.Dispose();
        }
    }

    public class FarmerConfig
    {
        public static FarmerConfig Loaded { get; set; } = new FarmerConfig();
        public bool RestrictPathfinding { get; set; } = false;

        public float PathRange { get; set; } = 48f;

        public float PathItemRange { get; set; } = 10f;

        public float BreedingCapRange { get; set; } = 20;

        public int BreedingCap { get; set; } = 3;

        public int BreedingCapMinPop { get; set; } = 10;

        public bool PoopEnabled { get; set; } = true;

        public bool UnloadedHungerEnabled { get; set; } = true;

        public bool ShearingEnabled { get; set; } = true;

        public bool LichenEnabled { get; set; } = true;

        public bool MedievalExpansionEnabled { get; set; } = true;

        public bool PrimitiveSurvivalEnabled { get; set; } = true;

        public bool WildFarmingEnabled { get; set; } = true;

        public bool PetsEnabled { get; set; } = false;

        public bool GluttonyEnabled { get; set; } = true;

        public bool WildBirthsEnabled { get; set; } = false;

        public bool GrazingEnabled { get; set; } = true;
    }
}
