using UnityEngine;

namespace _Game.Core
{
    /// <summary>
    /// Centralized dimensional/spatial constants for the entire game.
    /// All hardcoded magic numbers are defined here once and referenced everywhere.
    /// </summary>
    public static class GameConstants
    {
        // ============================================================
        // GRID & WORLD DIMENSIONS
        // ============================================================

        /// <summary> Base grid cell size in meters (misc game grids). </summary>
        public const float GRID_CELL_SIZE = 1f;

        /// <summary> Map generation world region extent (m). City is 800x800m. </summary>
        public const float WORLD_REGION_SIZE = 800f;

        /// <summary> Chunk side length in world units (m). Used by WorldData. </summary>
        public const float CHUNK_SIZE = 32f;

        /// <summary> Chunk resolution (vertices per axis). </summary>
        public const int CHUNK_RESOLUTION = 16;

        // ── 一级模块网格 (40m 基础单元, 20×20 = 800m) ──

        /// <summary> 一级模块基础单元尺寸 (m)。所有模块尺寸的 GCD。 </summary>
        public const float MODULE_BASE_UNIT = 40f;

        /// <summary> 一级模块基础网格分辨率 (800m / 40m = 20 格)。 </summary>
        public const int MODULE_BASE_GRID_SIZE = 20;

        /// <summary> 旧模块网格尺寸（v1 均匀网格，已废弃，保留参考）。 </summary>
        internal static readonly int[] MODULE_GRID_SIZES_LEGACY = { 5, 6, 8 };

        /// <summary> 最小模块尺寸(格) — 3×3=120m (商业/住宅A)。 </summary>
        public const int MODULE_MIN_SIZE = 3;

        /// <summary> 最大模块尺寸(格) — 5×5=200m (工业/郊区)。 </summary>
        public const int MODULE_MAX_SIZE = 5;

        // ============================================================
        // FLOOR / BUILDING DIMENSIONS
        // ============================================================

        /// <summary> Standard building floor height in meters (aligned to grid). </summary>
        public const float FLOOR_HEIGHT = 3f;

        // ============================================================
        // PLAYER BODY DIMENSIONS
        // ============================================================

        /// <summary> Standard player height in meters (eyes / camera ref). </summary>
        public const float PLAYER_HEIGHT = 1.8f;

        /// <summary> Player hip/hand reference height (m). Used for weapon offsets. </summary>
        public const float PLAYER_HAND_HEIGHT = 1.45f;

        /// <summary> Player chest height offset for aiming (m). </summary>
        public const float PLAYER_CHEST_HEIGHT = 1.2f;

        /// <summary> Player chest height for melee attack origin (m). </summary>
        public const float PLAYER_MELEE_ORIGIN_Y = 0.8f;

        /// <summary> Look-at offset for camera (target chest level). </summary>
        public const float PLAYER_LOOK_AT_CHEST = 1f;

        /// <summary> Player torso center for Gizmo debug (m). </summary>
        public const float PLAYER_GIZMO_TORSO_Y = 1.1f;

        // ============================================================
        // CAMERA PARAMETERS
        // ============================================================

        /// <summary> Default camera follow distance behind target (m). </summary>
        public const float CAMERA_DISTANCE = 15f;

        /// <summary> Default camera height above target (m). </summary>
        public const float CAMERA_HEIGHT = 10f;

        /// <summary> Default camera pitch angle (degrees, 0=horiz, 90=top-down). </summary>
        public const float CAMERA_ANGLE = 60f;

        /// <summary> Camera smooth follow interpolation speed. </summary>
        public const float CAMERA_SMOOTH_SPEED = 5f;

        // ============================================================
        // MOVEMENT PARAMETERS
        // ============================================================

        /// <summary> Default player movement speed (m/s). </summary>
        public const float PLAYER_MOVE_SPEED = 5f;

        /// <summary> Maximum slowdown ratio when overloaded (0.5 = 50%). </summary>
        public const float PLAYER_MAX_OVERLOAD_SLOW = 0.5f;

        /// <summary> Minimum move speed modifier allowed for status effects. </summary>
        public const float PLAYER_MIN_MOVE_MODIFIER = 0.1f;

        /// <summary> Maximum move speed modifier allowed for status effects. </summary>
        public const float PLAYER_MAX_MOVE_MODIFIER = 2f;

        /// <summary> Player body rotation smooth speed (degrees/sec). </summary>
        public const float PLAYER_ROTATION_SMOOTH_SPEED = 540f;

        // ============================================================
        // CHUNK MANAGEMENT (L4)
        // ============================================================

        /// <summary> Runtime chunk size in meters (80m × 80m). </summary>
        public const float RUNTIME_CHUNK_SIZE = 80f;

        /// <summary> Chunk grid dimension (10×10 = 100 chunks, 800m×800m world). </summary>
        public const int CHUNK_GRID_SIZE = 10;

        /// <summary> Player must move this far before re-checking chunks (m). </summary>
        public const float CHUNK_CHECK_DISTANCE = 20f;

        /// <summary> Minimum interval between chunk checks (seconds). </summary>
        public const float CHUNK_CHECK_INTERVAL = 2f;

        /// <summary> Max preload steps processed per frame. </summary>
        public const int PRELOAD_STEPS_PER_FRAME = 2;

        /// <summary> Max player-dropped WorldItems per chunk before FIFO eviction. </summary>
        public const int PLAYER_DROP_PER_CHUNK_CAP = 400;

        /// <summary> Speed threshold for normal movement (m/s). </summary>
        public const float SPEED_THRESHOLD_NORMAL = 10f;

        /// <summary> Speed threshold for fast movement (m/s). </summary>
        public const float SPEED_THRESHOLD_FAST = 20f;

        /// <summary> Speed threshold for extreme movement (m/s). </summary>
        public const float SPEED_THRESHOLD_EXTREME = 40f;

        // ============================================================
        // INTERACTION RANGES
        // ============================================================

        /// <summary> Player interaction detection radius (m). </summary>
        public const float INTERACT_DETECT_RADIUS = 2.5f;

        /// <summary> Default interaction time for world containers (seconds). </summary>
        public const float INTERACT_CONTAINER_TIME = 1f;

        // ============================================================
        // WEAPON / COMBAT RANGES
        // ============================================================

        /// <summary> Weapon aim assist radius (m) for snapping to zombies. </summary>
        public const float AIM_ASSIST_RADIUS = 2f;

        /// <summary> Maximum raycast distance for aim detection. </summary>
        public const float AIM_MAX_RAY_DISTANCE = 200f;

        /// <summary> Fallback aim distance when no ground-plane hit (m). </summary>
        public const float AIM_FALLBACK_DISTANCE = 10f;

        /// <summary> Weapon model rotation smoothing speed. </summary>
        public const float WEAPON_ROTATION_SMOOTH_SPEED = 20f;

        /// <summary> Debug ray visible duration in seconds. </summary>
        public const float DEBUG_RAY_DURATION = 1.0f;

        /// <summary>
        /// Hand model offset for Capsule mode — X=right, Y=height, Z=forward.
        /// NOTE: This is a static readonly Vector3. Never mutate its components.
        /// Unity's Vector3 is a value type, so field assignments create copies.
        /// </summary>
        public static readonly Vector3 CAPSULE_HAND_OFFSET =
            new Vector3(0.45f, 1.45f, 0.25f);

        /// <summary>
        /// Hand model Euler rotation for Capsule mode.
        /// NOTE: Never mutate this Vector3.
        /// </summary>
        public static readonly Vector3 CAPSULE_HAND_EULER =
            new Vector3(0f, 0f, -10f);

        /// <summary> Fallback muzzle forward offset when no WeaponHolder. </summary>
        public const float MUZZLE_FORWARD_OFFSET = 0.5f;

        /// <summary> Fallback muzzle upward offset when no WeaponHolder. </summary>
        public const float MUZZLE_UP_OFFSET = 1.45f;

        /// <summary> Melee attack reach distance (m). </summary>
        public const float MELEE_ATTACK_RANGE = 2f;

        /// <summary> Melee attack sphere radius (m). </summary>
        public const float MELEE_ATTACK_RADIUS = 0.5f;

        /// <summary> Melee attack cooldown (seconds). </summary>
        public const float MELEE_ATTACK_COOLDOWN = 0.5f;

        /// <summary> Melee attack base damage (empty-hand). </summary>
        public const float MELEE_ATTACK_DAMAGE = 10f;

        /// <summary> 游戏中最大声音半径（步枪枪声），用于归一化噪声等级。 </summary>
        public const float MAX_SOUND_RADIUS = 80f;

        /// <summary> Gizmo debug wire sphere radius for aim target. </summary>
        public const float GIZMO_AIM_SPHERE_RADIUS = 0.15f;

        /// <summary> Gizmo aim direction ray length. </summary>
        public const float GIZMO_AIM_RAY_LENGTH = 5f;

        // ============================================================
        // INVENTORY LIMITS
        // ============================================================

        /// <summary> Default maximum weight capacity (kg). </summary>
        public const float INVENTORY_MAX_WEIGHT = 30f;

        /// <summary> Hard overload weight cap (kg) — cannot add beyond this. </summary>
        public const float INVENTORY_OVERLOAD_WEIGHT = 50f;

        /// <summary> Default container grid columns (world containers). </summary>
        public const int DEFAULT_CONTAINER_WIDTH = 4;

        /// <summary> Default container grid rows (world containers). </summary>
        public const int DEFAULT_CONTAINER_HEIGHT = 3;

        /// <summary> Drop item forward offset from player position (m). </summary>
        public const float DROP_FORWARD_OFFSET = 1.2f;

        /// <summary> Drop item upward offset from ground (m). </summary>
        public const float DROP_UP_OFFSET = 0.1f;

        /// <summary> Drop forward offset for manually dropped items (m, slightly farther). </summary>
        public const float DROP_MANUAL_FORWARD = 1.5f;

        /// <summary> Body armor container default width (grid cells). </summary>
        public const int BODY_ARMOR_GRID_WIDTH = 2;

        /// <summary> Body armor container default height (grid cells). </summary>
        public const int BODY_ARMOR_GRID_HEIGHT = 2;

        // ============================================================
        // SURVIVAL THRESHOLDS
        // ============================================================

        /// <summary> Initial health on spawn. </summary>
        public const float SURVIVAL_HEALTH_MAX = 100f;

        /// <summary> Initial body temperature (Celsius). </summary>
        public const float SURVIVAL_TEMP_INITIAL = 36.5f;

        /// <summary> Default environment temperature (Celsius). </summary>
        public const float SURVIVAL_ENV_TEMP_DEFAULT = 15f;

        /// <summary> Survival tick interval in game-seconds. </summary>
        public const float SURVIVAL_TICK_INTERVAL = 60f;

        /// <summary> Hunger max value. </summary>
        public const float SURVIVAL_HUNGER_MAX = 100f;

        /// <summary> Thirst max value. </summary>
        public const float SURVIVAL_THIRST_MAX = 100f;

        /// <summary> Maximum allowed temperature (Celsius). </summary>
        public const float SURVIVAL_TEMP_MAX = 50f;

        /// <summary> Minimum allowed temperature (Celsius). </summary>
        public const float SURVIVAL_TEMP_MIN = 0f;

        // ============================================================
        // MAP GENERATION — MESH / VISUALIZATION
        // ============================================================

        /// <summary> WorldGen spawn point X (center of world region). </summary>
        public const float WORLD_SPAWN_X = 400f;

        /// <summary> WorldGen spawn point Z (center of world region). </summary>
        public const float WORLD_SPAWN_Z = 400f;

        /// <summary> Ground plane Y coordinate. </summary>
        public const float GROUND_PLANE_Y = 0f;

        /// <summary> Plane visual Y offset above ground. </summary>
        public const float MESH_PLANE_Y_OFFSET = 0.02f;

        /// <summary> Plane scale shrink factor (to show grid borders). </summary>
        public const float MESH_PLANE_SCALE_FACTOR = 0.95f;

        /// <summary> Road visual width relative to cell size. </summary>
        public const float MESH_ROAD_WIDTH_FACTOR = 0.4f;

        /// <summary> Road visual Y position above ground. </summary>
        public const float MESH_ROAD_Y_POS = 0.1f;

        /// <summary> Road visual thickness (Cube height). </summary>
        public const float MESH_ROAD_THICKNESS = 0.15f;

        /// <summary> Label Y position above ground. </summary>
        public const float MESH_LABEL_Y_OFFSET = 0.25f;

        /// <summary> Label font size multiplier (relative to cell size). </summary>
        public const float MESH_LABEL_FONT_MULTIPLIER = 0.12f;

        // ============================================================
        // WORLD ITEM DISPLAY
        // ============================================================

        /// <summary> Default world item billboard scale. </summary>
        public const float WORLD_ITEM_SCALE = 0.5f;

        /// <summary> World item billboard Y offset above ground. </summary>
        public const float WORLD_ITEM_Y_OFFSET = 0.3f;

        /// <summary>
        /// World item trigger collider half-extents.
        /// NOTE: Never mutate this Vector3.
        /// </summary>
        public static readonly Vector3 WORLD_ITEM_COLLIDER_SIZE =
            new Vector3(0.5f, 0.5f, 0.5f);

        /// <summary> World container default grid width. </summary>
        public const int WORLD_CONTAINER_DEFAULT_WIDTH = 4;

        /// <summary> World container default grid height. </summary>
        public const int WORLD_CONTAINER_DEFAULT_HEIGHT = 3;

        // ============================================================
        // ZOMBIE PARAMETERS
        // ============================================================

        /// <summary> Zombie player detection range (m). </summary>
        public const float ZOMBIE_DETECT_RANGE = 18f;

        /// <summary> Zombie movement speed (m/s). </summary>
        public const float ZOMBIE_MOVE_SPEED = 3f;

        /// <summary> Zombie melee attack range (m). </summary>
        public const float ZOMBIE_ATTACK_RANGE = 1.5f;

        /// <summary> Zombie melee attack damage. </summary>
        public const int ZOMBIE_ATTACK_DAMAGE = 10;

        /// <summary> Zombie melee attack cooldown (seconds). </summary>
        public const float ZOMBIE_ATTACK_COOLDOWN = 1.5f;

        /// <summary> Zombie max health. </summary>
        public const float ZOMBIE_MAX_HEALTH = 100f;

        /// <summary> Damage flash duration for zombies (seconds). </summary>
        public const float ZOMBIE_FLASH_DURATION = 0.15f;

        /// <summary> Delay before destroying dead zombie body (seconds). </summary>
        public const float ZOMBIE_DESTROY_DELAY = 1f;

        // ============================================================
        // TIME SYSTEM
        // ============================================================

        /// <summary> Seconds in a game hour (used for time scaling calculation). </summary>
        public const float SECONDS_PER_GAME_HOUR = 3600f;

        /// <summary> Hours in a full day cycle. </summary>
        public const float HOURS_PER_DAY = 24f;

        // ============================================================
        // ITEM DEFAULTS
        // ============================================================

        /// <summary> Default item grid width (1x1). </summary>
        public const int DEFAULT_ITEM_GRID_WIDTH = 1;

        /// <summary> Default item grid height (1x1). </summary>
        public const int DEFAULT_ITEM_GRID_HEIGHT = 1;

        /// <summary> Default item weight (kg). </summary>
        public const float DEFAULT_ITEM_WEIGHT = 1f;

        /// <summary> Default weapon range in meters. </summary>
        public const float DEFAULT_WEAPON_RANGE = 50f;

        /// <summary> Default magazine capacity (0 = unlimited / melee). </summary>
        public const int DEFAULT_MAGAZINE_SIZE = 0;

        /// <summary> Default item max stack size. </summary>
        public const int DEFAULT_MAX_STACK = 1;

        /// <summary> Default item use time in seconds. </summary>
        public const float DEFAULT_USE_TIME = 1f;

        // ============================================================
        // VEHICLE PARAMETERS
        // ============================================================

        /// <summary> Camera extra distance when driving (m, added to CAMERA_DISTANCE). </summary>
        public const float VEHICLE_CAMERA_EXTRA_DISTANCE = 5f;

        /// <summary> Maximum vehicle interaction detection range (m). </summary>
        public const float VEHICLE_INTERACT_RANGE = 3f;

        /// <summary> Default vehicle fuel (L). </summary>
        public const float VEHICLE_DEFAULT_FUEL = 40f;

        /// <summary> Default vehicle motor torque. </summary>
        public const float VEHICLE_DEFAULT_TORQUE = 800f;

        /// <summary> Default vehicle braking force. </summary>
        public const float VEHICLE_DEFAULT_BRAKE = 3000f;

        /// <summary> Default vehicle max steer angle (degrees). </summary>
        public const float VEHICLE_DEFAULT_STEER = 35f;

        /// <summary> Default vehicle reverse torque. </summary>
        public const float VEHICLE_DEFAULT_REVERSE = 400f;

        // ============================================================
        // CONTAINER TAGS
        // ============================================================

        /// <summary> 容器标签：柜子/橱柜 </summary>
        public const string CONTAINER_TAG_CABINET = "CABINET";

        /// <summary> 容器标签：冰箱 </summary>
        public const string CONTAINER_TAG_FRIDGE = "FRIDGE";

        /// <summary> 容器标签：尸体 </summary>
        public const string CONTAINER_TAG_CORPSE = "CORPSE";

        /// <summary> 容器标签：木箱/板条箱 </summary>
        public const string CONTAINER_TAG_CRATE = "CRATE";

        // ============================================================
        // SURVIVAL XP SYSTEM
        // ============================================================

        /// <summary> XP required to earn 1 skill point. </summary>
        public const int XP_PER_SKILL_POINT = 100;

        /// <summary> XP granted per in-game day. </summary>
        public const int XP_PER_DAY = 200;

        /// <summary> XP granted per in-game hour (idle). </summary>
        public const int XP_PER_HOUR = 5;

        /// <summary> XP multiplier during night / harsh weather. </summary>
        public const float XP_NIGHT_MULTIPLIER = 2f;

        // — combat —
        public const int XP_KILL_NORMAL_ZOMBIE = 25;
        public const int XP_KILL_ELITE_ZOMBIE = 60;
        public const int XP_KILL_BEAST = 40;
        public const int XP_HEADSHOT_BONUS = 15;
        public const int XP_BLOCK_SUCCESS = 5;
        public const int XP_DODGE_SUCCESS = 8;

        // — movement & exploration —
        public const int XP_WALK_PER_100M = 2;
        public const int XP_RUN_PER_100M = 4;
        public const int XP_NEW_AREA_DISCOVERED = 30;
        public const int XP_SEARCH_ROOM = 10;

        // — labor —
        public const int XP_CHOP_TREE = 8;
        public const int XP_MINE_ORE = 10;
        public const int XP_HARVEST_HERB = 6;
        public const int XP_SKIN_ANIMAL = 12;
        public const int XP_CARRY_HEAVY_PER_10S = 3;

        // — crafting & building —
        public const int XP_CRAFT_ITEM = 15;
        public const int XP_CRAFT_WEAPON = 25;
        public const int XP_PLACE_BUILDING = 20;
        public const int XP_PLACE_TRAP = 15;
        public const int XP_DECONSTRUCT = 10;

        // — vehicles —
        public const int XP_DRIVE_PER_KM = 3;
        public const int XP_OFFROAD_PER_KM = 5;
        public const int XP_REPAIR_VEHICLE = 20;
        public const int XP_START_GENERATOR = 10;

        // — mental —
        public const int XP_READ_BOOK = 50;
        public const int XP_LEARN_RECIPE = 40;
        public const int XP_CRACK_LOCK = 30;

        // — survival —
        public const int XP_START_FIRE = 8;
        public const int XP_PURIFY_WATER = 10;
        public const int XP_COOK_FOOD = 12;
        public const int XP_BANDAGE_SELF = 5;
        public const int XP_HEAL_ALLY = 15;

        // ============================================================
        // STAMINA SYSTEM
        // ============================================================

        /// <summary> Base stamina (before endurance bonus). </summary>
        public const float STAMINA_BASE_MAX = 100f;

        /// <summary> Stamina per endurance point. </summary>
        public const float STAMINA_PER_ENDURANCE = 10f;

        /// <summary> Stamina drain while running (per second). </summary>
        public const float STAMINA_DRAIN_RUN = 8f;

        /// <summary> Stamina drain per melee attack. </summary>
        public const float STAMINA_DRAIN_MELEE = 15f;

        /// <summary> Stamina drain per chop/mine action. </summary>
        public const float STAMINA_DRAIN_LABOR = 10f;

        /// <summary> Stamina drain multiplier when overloaded (>50%). </summary>
        public const float STAMINA_OVERLOAD_MULT = 1.5f;

        /// <summary> Stamina regen while standing still (per second). </summary>
        public const float STAMINA_REGEN_IDLE = 15f;

        /// <summary> Stamina regen while walking (per second). </summary>
        public const float STAMINA_REGEN_WALK = 10f;

        /// <summary> Stamina regen bonus per endurance point (per second). </summary>
        public const float STAMINA_REGEN_PER_ENDURANCE = 2f;

        /// <summary> Move speed multiplier when stamina exhausted. </summary>
        public const float STAMINA_EXHAUSTED_SPEED_MULT = 0.6f;

        // ============================================================
        // SKILL EFFECT FORMULAS
        // ============================================================

        /// <summary> Melee damage multiplier per strength point. </summary>
        public const float STRENGTH_DAMAGE_MULT = 0.05f;

        /// <summary> Melee damage multiplier per 近战专精 level. </summary>
        public const float MELEE_SKILL_DAMAGE_MULT = 0.08f;

        /// <summary> Aim spread reduction per 枪械专精 level. </summary>
        public const float GUN_SKILL_SPREAD_REDUCTION = 0.06f;

        /// <summary> Carry weight bonus per strength point (kg). </summary>
        public const float STRENGTH_CARRY_WEIGHT_BONUS = 5f;

        // ============================================================
        // WEATHER SYSTEM
        // ============================================================

        /// <summary> Game-hours between weather roll checks. </summary>
        public const float WEATHER_CHECK_INTERVAL = 2f;

        /// <summary> Cooldown periods after storm (no rain/storm). </summary>
        public const int WEATHER_STORM_COOLDOWN = 2;

        /// <summary> Max consecutive rain/storm periods. </summary>
        public const int WEATHER_MAX_CONSECUTIVE_RAIN = 3;

        /// <summary> 基础马尔可夫转移权重 [fromState, toState].
        /// from=0..3 (Sunny/Cloudy/Rain/Storm), to=0..3, weights are integers summed for probability. </summary>
        public static readonly int[,] WEATHER_TRANSITION_WEIGHTS = {
            // →   Sunny  Cloudy  Rain  Storm
            /* Sunny  */ { 70, 20, 10,  0 },
            /* Cloudy */ { 40, 30, 25,  5 },
            /* Rain   */ { 30, 30, 20, 20 },
            /* Storm  */ { 50, 30, 15,  5 }
        };

        /// <summary> Difficulty multipliers for rain/storm weights. </summary>
        public const float WEATHER_DIFF_EASY_RAIN_MULT   = 0.7f;
        public const float WEATHER_DIFF_EASY_STORM_MULT  = 0.5f;
        public const float WEATHER_DIFF_EASY_COOLDOWN    = 1.5f;
        public const float WEATHER_DIFF_HARD_RAIN_MULT   = 1.3f;
        public const float WEATHER_DIFF_HARD_STORM_MULT  = 1.5f;
        public const float WEATHER_DIFF_HARD_COOLDOWN    = 0.5f;

        /// <summary> Biome modifiers for rain/storm weights.
        /// Key: ModuleType index (Water=5, Forest=6). Row: [rainMult, stormMult, cloudyMult]. </summary>
        public static readonly System.Collections.Generic.Dictionary<int, float[]> WEATHER_BIOME_MODIFIERS =
            new System.Collections.Generic.Dictionary<int, float[]>
            {
                { 5, new[] { 1.4f, 1.2f, 1f } },   // Water
                { 6, new[] { 1.1f, 1f,   1.3f } },  // Forest
            };

        /// <summary> Weather ambient temp base + modifier per weather type index. </summary>
        public const float WEATHER_BASE_TEMP = 15f;
        public static readonly float[] WEATHER_TEMP_MODS = { 5f, 0f, -5f, -10f };

        /// <summary> Lighting intensity lerp speed. </summary>
        public const float WEATHER_LIGHT_LERP_SPEED = 1.5f;
    }
}
