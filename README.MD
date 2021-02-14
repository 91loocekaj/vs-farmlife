-----Livestock Changes-----
These changes are made to simulate real animal life as well as possible while still allowing the game to be enjoyable

  -----Hare-----
  The new livestock addition of the mod, rabbits can be seen as a highly efficient protein source  and source of small hides due to their fast reproduction, growth, and small diet, but they are highly competitive and vulnerable to predators.

  Maximum saturation = 8
  Item Food = All vegetables, as well as hay(dry grass).
  Block food = All crops planted on farmland
  Trough feed = Hay in small trough
  Poop amount = Small

  Gestation time(in game days) = 5
  Pregnancy Recovery Time(in game days) = 1-3
  Litter size = 6-12
  Baby to Adult Time(in game days) = 7

  Additional notes = Males are extremely competitive and will attack other males and babies for 1 damage

  -----Chicken-----
  Chickens will provide a daily supply of eggs, given they have enough room and food for them. They have the smallest and most expensive diet though.

  Maximum saturation = 12
  Item Food = All grains.
  Block food = All grain crops planted on farmland
  Trough feed = All grain in small trough
  Poop amount = Small

  Gestation time(in game days) = 5
  Pregnancy Recovery Time(in game days) = 1-3
  Litter size = 1-2
  Baby to Adult Time(in game days) = 12

  Additional notes = Hens will attempt to lay eggs in nearby hen boxes and need to be at 75% saturation to lay eggs

  -----Pig-----
  These beasts have the largest litter size and diet of all the animals. Though they eat a lot more, and are a risk to bushes and beehives.

  Maximum saturation = 20
  Item Food = All grains, all vegetables, all meat, all dairy, all fruit as well as hay(dry grass), raw meats, and rot.
  Block food = All crops planted on farmland, ripe bushes, beehives
  Trough feed = All grain, hay, and rot in large trough
  Poop amount = Small to medium

  Gestation time(in game days) = 5
  Pregnancy Recovery Time(in game days) = 1-3
  Litter size = 7-14
  Baby to Adult Time(in game days) = 7

  -----Sheep-----
  These creatures are currently the only source of dairy in the game and produce the largest amount of poop. Though there diet is somewhat picky and they have the slowest reproduction of all the animals

  Maximum saturation = 16
  Item Food = All grains, as well as hay(dry grass).
  Block food = All grain crops planted on farmland
  Trough feed = All grain and hay in large trough
  Poop amount = Medium to Large

  Gestation time(in game days) = 20
  Pregnancy Recovery Time(in game days) = 14-21
  Litter size = 1
  Baby to Adult Time(in game days) = 16

  Additional notes = Do not continue hold right click after a failed milking attempt. This could lead to a loss of milk due to a bug. Also ewes need to be at 75% saturation to produce milk

-----Livestock Hunger System-----
This mod adds a new hunger system for animals this means:

- Livestock animals all have saturation like the player
- They lose saturation every 24 hours
- Animals will continue to eat until they are full or there is no more food
- Animal AI prioritizes getting food over being idle and wandering
- Females and males need at least 50% of their max saturation in order to conceive a child
- Females hunger rate is increased by 50% while pregnant
- Pregnant females will lose their baby if their health or saturation drops below 25%. Will get a warning at 30% or less of either
- After the grow time needed is met, babies will need full saturation in order to mature to adults(Will display "Malnourished..." when looking at it)
- Males lose 1 saturation after mating
- All livestock animals can now eat loose items and blocks like raccoons, making early game animal husbandry possible
- Animals poop every time they lose saturation
- Poop can be used as a fertilizer: Small 5% all nutrients, Medium 10% all nutrients, Large 15% all nutrients
- Babies always produce the smallest amount of poop, males the largest, and females poop a random size

-----Other Changes/Additions-----
- Hay(dry grass) can now be put in small troughs for rabbits
- Rot can now be put in large troughs for pigs
- Reduced trough input amounts to match loose item eating
- Significantly increased trough sizes Large = 64, Small = 32
- Compost Bin added that rots food super fast
- Increased rot gained from spoiled food
- Grain crops drop dry grass when harvested.
- Hen boxes can be crafted and chickens can place eggs in them from a 6 block radius
- Trough hitboxes were removed, and lamb hitboxes were reduced to allow for better pathfinding

-----Technical Info-----
Lay eggs behavior attributes(layegg)
- minHourDelay: Determines minimum amount of time to lay egg, default 24
- maxHourDelay: Determines maximum amount of time to lay egg, default 26
- eggSaturation: Saturation needed to lay eggs. Default 0.75
- nestRange: How far a hen will look for hen boxes, default 6. WARNING Performance heavy!
- eggCodes: Block codes of egg(s)

Food based reproduction behavior (foodmultiply)
- pregnancyDays: How many days pregnancy lasts
- spawnEntityCode: What entity to give birth to
- requiresNearbyEntityCode: What entity is required to concieve child
- requiresNearbyRange: How far the mate can be to concieve
- reproduceSaturation: Saturation required for mating to occur
- healthMiscarriage: When a miscarriage should happen due to low health
- foodMiscarriage: When a miscarriage should happen due to low saturation
- forTwoRate: How much hunger rate goes up during pregnancy.
- multiplyCooldownDaysMin: Minimum time until another pregnancy
- multiplyCooldownDaysMax: Maximum days until another pregnancy
- spawnQuantityMin: Minimum litter size
- spawnQuantityMax: Maximum litter size

Food based growing behavior(foodgrow)
- hoursToGrow: How many hours till it is possible for it to grow
- nutritionReq: How much saturation is needed to grow up
- adultEntityCodes: What entities it can grow into

Food Consumption Behavior(consume)
- poopCodes: What the entity poops

Entity attributes
- maxSaturation: What is the maximum this entity can eat, default 20
