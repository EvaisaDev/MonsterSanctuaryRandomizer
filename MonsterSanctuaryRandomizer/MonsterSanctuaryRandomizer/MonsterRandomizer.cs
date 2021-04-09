using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Random = System.Random;
using System.IO;
using System.Reflection.Emit;
using MonSancAPI;


namespace MonsterSanctuaryRandomizer
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class MonsterRandomizer : BaseUnityPlugin
    {
        public const string ModGUID = "evaisa.monsterrandomizer";
        public const string ModName = "Monster Randomizer";
        public const string ModVersion = "0.3.4";

        public static ConfigEntry<bool> randomizeMonsters;
        public static ConfigEntry<bool> randomizeChampions;
        public static ConfigEntry<bool> addChampionsToRegularPool;
        public static ConfigEntry<bool> randomizeChests;
        public static ConfigEntry<bool> randomizeSkillTrees;
        public static ConfigEntry<bool> randomizeUltimates;
        public static ConfigEntry<bool> randomizeShifts;
        public static ConfigEntry<bool> randomizeChampionSkills;

        public static ConfigEntry<bool> ChestsCanBeMimics;

        public static ConfigEntry<bool> monstersBuildSkillsSeperately;

        public static ConfigEntry<bool> shiftPairs;
        public static ConfigEntry<bool> randomizeResistanceWeakness;
        public static ConfigEntry<bool> skipCutscenes;

        public static Dictionary<string, List<GameObject>> referenceables = new Dictionary<string, List<GameObject>>();

        public static List<SkillTree> skillTrees = new List<SkillTree>();

        public static Dictionary<string, List<GameObject>> regularMonstersByScene = new Dictionary<string, List<GameObject>>();
        public static Dictionary<string, List<GameObject>> regularAndChampionMonstersByScene = new Dictionary<string, List<GameObject>>();
        public static Dictionary<string, List<GameObject>> championMonstersByScene = new Dictionary<string, List<GameObject>>();

        public static Dictionary<string, Monster> currentChampionLookupTable = new Dictionary<string, Monster>();

        public static Dictionary<string, GameObject> refightReplacement = new Dictionary<string, GameObject>();
 
        public static Random rand;

        public static List<int> mimicIDs = new List<int>();

        public static bool isFirstSpawn = true;

        public MonsterRandomizer()
        {
            randomizeMonsters = Config.Bind("General", "RandomizeMonsters", true, "Randomize monsters that spawn in the world.");
            randomizeChampions = Config.Bind("General", "RandomizeChampions", true, "Randomize champions that spawn in the world.");
            addChampionsToRegularPool = Config.Bind("General", "ChampionsInRegularPool", true, "Add champion monsters to the regular spawn pool.");
            randomizeChests = Config.Bind("General", "RandomizeChests", true, "Randomize the contents of chests.");
            randomizeSkillTrees = Config.Bind("General", "RandomizeSkillTrees", true, "Randomize skill trees of monsters.");
            randomizeChampionSkills = Config.Bind("General", "Randomize Champion Skills", true, "Randomize the special champion skills.");
            monstersBuildSkillsSeperately = Config.Bind("General", "MonstersChooseSkillsSeperately", false, "Let every monster of the same type choose its own skills from their skill tree.");
            shiftPairs = Config.Bind("General", "ShiftPairs", false, "Keep the shift skills in the same set. (if this is false the dark and light shift skills are unrelated to eachother)");
            randomizeUltimates = Config.Bind("General", "RandomizeUltimates", true, "Randomize the ultimates of monsters.");
            randomizeShifts = Config.Bind("General", "RandomizeShifts", true, "Randomize the light/dark shifts of monsters.");
            randomizeResistanceWeakness = Config.Bind("General", "RandomizeResistanceWeakness", true, "Randomize resistances and weaknesses of monsters.");
            ChestsCanBeMimics = Config.Bind("General", "ChestsCanBeMimics", false, "Chests can be replaced with mimics. (EXPERIMENTAL)");
            skipCutscenes = Config.Bind("General", "SkipCutscenes", false, "Automatically skip cutscenes.");

            // Add the dictionary keys for skills
            referenceables.Add("skills.ultimate", new List<GameObject>());
            referenceables.Add("skills.champion", new List<GameObject>());
            referenceables.Add("skills.champion_passive", new List<GameObject>());
            referenceables.Add("skills.dark", new List<GameObject>());
            referenceables.Add("skills.light", new List<GameObject>());
            referenceables.Add("skills.weakness", new List<GameObject>());
            referenceables.Add("skills.resistance", new List<GameObject>());
            referenceables.Add("skills.active.tier1", new List<GameObject>());
            referenceables.Add("skills.active.tier2", new List<GameObject>());
            referenceables.Add("skills.active.tier3", new List<GameObject>());
            referenceables.Add("skills.active.tier4", new List<GameObject>());
            referenceables.Add("skills.active.tier5", new List<GameObject>());
            referenceables.Add("skills.passive.tier1", new List<GameObject>());
            referenceables.Add("skills.passive.tier2", new List<GameObject>());
            referenceables.Add("skills.passive.tier3", new List<GameObject>());
            referenceables.Add("skills.passive.tier4", new List<GameObject>());
            referenceables.Add("skills.passive.tier5", new List<GameObject>());

            referenceables.Add("monsters.regular", new List<GameObject>());
            referenceables.Add("monsters.champion", new List<GameObject>());
            referenceables.Add("monsters.familiar", new List<GameObject>());

            referenceables.Add("loot", new List<GameObject>());

            //MonSancAPI.MonSancAPI.RegisterConfigCategory("Randomizer Options");

            On.PlayerController.LoadGame += PlayerController_LoadGame;
            On.GameController.InitPlayerStartSetup += GameController_InitPlayerStartSetup;
            On.MonsterEncounter.DetermineEnemy += MonsterEncounter_DetermineEnemy;
            //On.ProgressManager.ChampionKilled += ProgressManager_ChampionKilled;
            On.ProgressManager.ChampionKilled += ProgressManager_ChampionKilled; ;
            On.BaseCutscene.Update += BaseCutscene_Update;
            On.Chest.Start += Chest_Start;
          //  On.SkillManager.LoadSkillData += SkillManager_LoadSkillData;
            //IL.SkillManager.LearnActiveFromSavegame += SkillManager_LearnActiveFromSavegame;
            On.SkillManager.LearnActiveFromSavegame += SkillManager_LearnActiveFromSavegame;

            On.Chest.Interact += Chest_Interact; 
        }

        private void Chest_Interact(On.Chest.orig_Interact orig, Chest self)
        {
            if (self.gameObject.name.Contains("SpecialChest"))
            {
                orig(self);
                return;
            }
            if (rand.Next(1, 101) <= 5 && self.Item != null && self.Item.GetComponent<UniqueItem>() == null && self.Item.GetComponent<KeyItem>() == null && ChestsCanBeMimics.Value)
            {
                
                var chestPosition = self.gameObject.transform.position;

                chestPosition.x -= 5;

                chestPosition.y -= 8;

                var mimicPrefab = referenceables["monsters.regular"].FirstOrDefault(monster => monster.GetComponent<Monster>().OriginalMonsterName == "Mimic");

                if (mimicPrefab != null)
                {
                    var encounterObject = Instantiate(new GameObject());

                    var boxCollider = encounterObject.AddComponent<BoxCollider2D>();

                    boxCollider.size = new Vector2(16, 200);
                    boxCollider.isTrigger = true;

                    encounterObject.transform.position = chestPosition;

                    var mimicEncounter = encounterObject.AddComponent<MonsterEncounter>();

                    mimicEncounter.PredefinedMonsters = new MonsterEncounter.EncounterConfig();

                    mimicEncounter.PredefinedMonsters.Monster = new GameObject[1];

                    mimicEncounter.PredefinedMonsters.level = 1;

                    mimicEncounter.PredefinedMonsters.weight = 1;

                    mimicEncounter.PredefinedMonsters.Monster[0] = mimicPrefab;

                    mimicEncounter.EncounterType = EEncounterType.Normal;

                    mimicEncounter.VariableLevel = true;

                    mimicEncounter.AutoStart = true;

                    mimicEncounter.CanRetreat = true;

                    mimicEncounter.MonsterBoundsRange = 130;

                    mimicEncounter.ContrahentsDistance = 160;

                    mimicEncounter.SetupEnemies(chestPosition, false);

                    var monsters = mimicEncounter.DeterminedEnemies;

                    monsters[0].RewardsCommon.Add(self.Item);

                    //mimicEncounter.StartCombat(true, monsters[0]);

                    PlayerController.Instance.Follower.CancelExploreAction();
                    CombatController.Instance.Direction = mimicEncounter.GetDirection(monsters[0]);

                    Vector3 enemyPos = mimicEncounter.GetEnemyPos(null);
                    mimicEncounter.spawnedDirection = mimicEncounter.GetDirection(null);

                    Vector3 vector = mimicEncounter.gameObject.transform.position + Vector3.left * mimicEncounter.ContrahentsDistance / 2f * (float)CombatController.Instance.Direction;
                    RaycastHit2D raycastHit2D = Utils.CheckForGround(vector, 0f, 256f);
                    RaycastHit2D raycastHit2D2 = Utils.CheckForGround(mimicEncounter.GetEnemyPos(monsters[0]), 0f, 256f);

                    if (raycastHit2D.collider != null && raycastHit2D2.collider != null)
                    {
                        vector.y = raycastHit2D.point.y;
                        CombatController.Instance.StartCombat(vector, mimicEncounter.GetEnemyPos(monsters[0]), mimicEncounter.DeterminedEnemies, mimicEncounter, true);
                    }

                    Object.DestroyImmediate(self.gameObject);
                    // CombatController.Instance.StartCombat(PlayerController.Instance.PlayerPosition, chestPosition, monsters, mimicEncounter);
                    // mimicEncounter.SpawnEnemies();

                    //CombatController.Instance.StartCombat(PlayerController.Instance.PlayerPosition, chestPosition, monsters);

                    // 

                    // 

                    //GameObject.DestroyImmediate(self);

                }

            }
            else
            {
                orig(self);
            }
        }

        private void SkillManager_LearnActiveFromSavegame(On.SkillManager.orig_LearnActiveFromSavegame orig, SkillManager self, List<BaseAction> list, BaseAction action)
        {
            bool first_instance = true;
            foreach (SkillTree skillTree in self.SkillTrees)
            {
                for (int j = 0; j < SkillTree.TierCount; j++)
                {
                    foreach (SkillTreeEntry skillTreeEntry in skillTree.GetSkillsByTier(j))
                    {
                        if (skillTreeEntry.Skill == action && skillTreeEntry.Learned == false && first_instance == false)
                        {
                            skillTreeEntry.Learned = true;
                            list.Add(action);
                            return;
                        }
                        first_instance = false;
                    }
                }
            }

            orig(self, list, action);
        }

        /*
        private void SkillManager_LearnActiveFromSavegame(ILContext il)
        {
            var cursor = new ILCursor(il);
            // cursor.GotoNext(MoveType.After, x => x.MatchCallvirt("SkillTree", "GetSkillsByTier"));
            ILLabel target;
            if (cursor.TryGotoNext(
                MoveType.After,
                x => x.MatchCallOrCallvirt<SkillManager>("get_SkillTrees"),
                x => x.MatchStloc(out _),
                x => x.MatchCallOrCallvirt(typeof(SkillTree).GetProperty(nameof(SkillTree.GetSkillsByTier), BindingFlags.Instance | BindingFlags.Public).GetGetMethod(true)),
                x => x.MatchLdloc(6),
                x => x.MatchBrfalse(out target),
                x => x.
            ))
            {
                cursor.EmitDelegate<System.Func<bool>>(() =>
                {

                    return true;
                });
            }
           // cursor.GotoNext(MoveType.After, x => x.MatchCallvirt("RoR2.SceneDirector", "GenerateInteractableCardSelection"));
        }*/

        public void Update()
        {
            
            if (Input.GetKeyDown(KeyCode.F5))
            {
                GameController.Instance.targetScene = "MagmaChamber_Champion";
                GameController.Instance.ChangeScene();
            }
            
        }

        private void Chest_Start(On.Chest.orig_Start orig, Chest self)
        {
            orig(self);

            if (self.gameObject.name.Contains("SpecialChest"))
            {
                return;
            }

            var common_loot = new List<GameObject>();
            var uncommon_loot = new List<GameObject>();
            var rare_loot = new List<GameObject>();
            var egg_loot = new List<GameObject>();
           
            referenceables["loot"].ForEach(loot =>
            {
                if (loot.GetComponent<CraftMaterial>() == null && !loot.name.Contains("+1") && !loot.name.Contains("+2") && !loot.name.Contains("+3") && !loot.name.Contains("+4") && !loot.name.Contains("+5") && loot.name != "WoodenStick")
                {
                    if (loot.GetComponent<UniqueItem>() == null && loot.GetComponent<KeyItem>() == null)
                    {
                        if (loot.GetComponent<Equipment>() != null)
                        {
                            if (loot.GetComponent<Equipment>().Unique)
                            {
                                rare_loot.Add(loot);
                            }
                            else
                            {
                                if(loot.GetComponent<BaseItem>().Price == 0)
                                {
                                    rare_loot.Add(loot);
                                }
                                else if (loot.GetComponent<BaseItem>().Price < 1000)
                                {
                                    common_loot.Add(loot);
                                }
                                else if (loot.GetComponent<BaseItem>().Price >= 1000 && loot.GetComponent<BaseItem>().Price < 2000)
                                {
                                    uncommon_loot.Add(loot);
                                }
                                else
                                {
                                    rare_loot.Add(loot);
                                }
                            }
                        }
                        else
                        {
                            if (loot.GetComponent<LootBox>() != null)
                            {
                                if (loot.gameObject.name.Contains("Lvl1"))
                                {
                                    common_loot.Add(loot);
                                }
                                else if (loot.gameObject.name.Contains("Lvl2"))
                                {
                                    common_loot.Add(loot);
                                }
                                else if (loot.gameObject.name.Contains("Lvl3"))
                                {
                                    uncommon_loot.Add(loot);
                                }
                                else if (loot.gameObject.name.Contains("Lvl4"))
                                {
                                    rare_loot.Add(loot);
                                }
                                else if (loot.gameObject.name.Contains("Lvl5"))
                                {
                                    rare_loot.Add(loot);
                                }
                            }
                            if(loot.GetComponent<Catalyst>() != null)
                            {
                                rare_loot.Add(loot);
                            }
                            else if (loot.GetComponent<LevelBadge>() != null)
                            {
                                if(loot.GetComponent<LevelBadge>().MaxLevel > 0)
                                {
                                    rare_loot.Add(loot);
                                }
                                else
                                {
                                    uncommon_loot.Add(loot);
                                }
                            }
                            else if (loot.GetComponent<Egg>() == null)
                            {
                                if (loot.GetComponent<BaseItem>().Price == 0)
                                {
                                    rare_loot.Add(loot);
                                }
                                else if (loot.GetComponent<BaseItem>().Price < 1000)
                                {
                                    common_loot.Add(loot);
                                }
                                else if (loot.GetComponent<BaseItem>().Price >= 1000 && loot.GetComponent<BaseItem>().Price < 2000)
                                {
                                    uncommon_loot.Add(loot);
                                }
                                else
                                {
                                    rare_loot.Add(loot);
                                }
                            }
                            else
                            {
                                egg_loot.Add(loot);
                            }
                        }
                    }
                }
            });

            if (self.Item == null && self.Gold == 0 || self.Item.GetComponent<UniqueItem>() == null && self.Item.GetComponent<KeyItem>() == null)
            {
                var random_pick = rand.Next(1, 101);
                if(random_pick < 5)
                {
                    self.Item = egg_loot[rand.Next(0, egg_loot.Count)];
                }
                if (random_pick <= 20)
                {
                    self.Item = rare_loot[rand.Next(0, rare_loot.Count)];
                }
                else if (random_pick <= 50)
                {
                    self.Item = uncommon_loot[rand.Next(0, uncommon_loot.Count)];
                }
                else
                {
                    self.Item = common_loot[rand.Next(0, common_loot.Count)];
                }
            }

            if (rand.Next(1, 101) <= 4 && self.Item != null && self.Item.GetComponent<UniqueItem>() == null && self.Item.GetComponent<KeyItem>() == null || self.Item == null)
            {
                self.Item = null;

                self.Gold = skewedRandom(false, 1, 15) * 100;
            }

            if (self.Item != null && self.Item.GetComponent<UniqueItem>() == null && self.Item.GetComponent<KeyItem>() == null)
            {
                var new_random = MonsterRandomizer.rand.Next(1, 101);
                if (new_random <= 5)
                {
                    self.Quantity = 3;
                }
                else if (new_random < 20)
                {
                    self.Quantity = 2;
                }
                else
                {
                    self.Quantity = 1;
                }

            }

            /*
            if(rand.Next(1, 101) <= 100 && self.Item.GetComponent<UniqueItem>() == null && self.Item.GetComponent<KeyItem>() == null)
            {
                var chestPosition = self.gameObject.transform.position;
                var mimicPrefab = referenceables["monsters.regular"].FirstOrDefault(monster => monster.GetComponent<Monster>().OriginalMonsterName == "Mimic");

                if (mimicPrefab != null)
                {
                    var encounterObject = Instantiate(new GameObject());

                    var boxCollider = encounterObject.AddComponent<BoxCollider2D>();

                    boxCollider.size = new Vector2(16, 200);
                    boxCollider.isTrigger = true;


                    var mimicEncounter = encounterObject.AddComponent<MonsterEncounter>();

                    mimicEncounter.PredefinedMonsters = new MonsterEncounter.EncounterConfig();

                    mimicEncounter.PredefinedMonsters.Monster = new GameObject[1];

                    mimicEncounter.PredefinedMonsters.Monster[0] = mimicPrefab;

                    mimicEncounter.EncounterType = EEncounterType.Normal;

                    mimicEncounter.VariableLevel = true;

                    mimicEncounter.AutoStart = true;

                    mimicEncounter.CanRetreat = true;

                    mimicEncounter.MonsterBoundsRange = 130;

                    mimicEncounter.ContrahentsDistance = 160;

                    // CombatController.Instance.SetupEnemies(mimicEncounter, chestPosition, false);

                    mimicEncounter.SetupEnemies(chestPosition, false);

                    GameObject.DestroyImmediate(self);

                }
            
            }*/
        }

        private void BaseCutscene_Update(On.BaseCutscene.orig_Update orig, BaseCutscene self)
        {
            if (skipCutscenes.Value)
            {
                self.RequestSkip();
            }
            orig(self);
        }



        private void ProgressManager_ChampionKilled(On.ProgressManager.orig_ChampionKilled orig, ProgressManager self, Monster champion, int score, int points, EDifficulty difficulty)
        {
            var champion_name = champion.OriginalMonsterName;

            var original_champion = champion;

            //Debug.Log("Player killed champion: " + champion_name);
            Debug.Log("Finding champion lookup for monster: " + champion_name);
            if (currentChampionLookupTable.ContainsKey(champion_name))
            {
                champion = currentChampionLookupTable[champion_name];
                Debug.Log("Player just beat: " + champion.OriginalMonsterName);
            }






            orig(self, champion, score, points, difficulty);
        }

        private MonsterEncounter.EncounterConfig MonsterEncounter_DetermineEnemy(On.MonsterEncounter.orig_DetermineEnemy orig, MonsterEncounter self)
        {


            Debug.Log("Encounter manager entity = " + self.gameObject);

            var result = orig(self);

            var monster_count = result.Monster.Length;

            var current_area = GameController.Instance.CurrentSceneName;

            Debug.Log("Area name = " + current_area);

            Debug.Log("Current area identifier: " + rand.Next(1000000));

            Debug.Log("Monster types in current scene: " + regularAndChampionMonstersByScene[current_area].Count);

            Debug.Log("Regular monster pool: ");
            regularMonstersByScene[current_area].ForEach(monster =>
            {
                Debug.Log(monster.GetComponent<Monster>());
            });

            Debug.Log("Champion monster pool: ");
            championMonstersByScene[current_area].ForEach(monster =>
            {
                Debug.Log(monster.GetComponent<Monster>());
            });

            var champion_battle = self.IsChampion;

            if (champion_battle)
            {
                rand = new Random((PlayerController.Instance.PlayerName + current_area).GetHashCode());
            }

            List<GameObject> regularEnemies = new List<GameObject>();

            if (addChampionsToRegularPool.Value && !champion_battle)
            {
                regularAndChampionMonstersByScene[current_area].ForEach(monster =>
                {
                    regularEnemies.Add(monster);
                });

            }
            else
            {
                regularMonstersByScene[current_area].ForEach(monster =>
                {
                    regularEnemies.Add(monster);
                });
            }

            for (int i = 0; i < monster_count; i++)
            {

                

                var is_champion = result.Monster[i].GetComponent<SkillManager>().GetChampionPassive() != null && result.Monster[i].GetComponent<SkillManager>().ChampionSkills.Any() && champion_battle;
                var old_name = result.Monster[i].GetComponent<Monster>().OriginalMonsterName;

                Debug.Log("Is this a champion? " + is_champion);

                Debug.Log("Original monster: " + old_name);
                if (is_champion)
                {
                    



                    if (randomizeChampions.Value)
                    {


                        var old_monster = result.Monster[i].GetComponent<Monster>();

                        if (refightReplacement.ContainsKey(old_name))
                        {
                            result.Monster[i] = refightReplacement[old_name];
                        }
                        else
                        {
                            if (championMonstersByScene[current_area].Any())
                            {
                                result.Monster[i] = championMonstersByScene[current_area][rand.Next(0, championMonstersByScene[current_area].Count)];
                            }
                        }
                        


                        var new_monster_name = result.Monster[i].GetComponent<Monster>().OriginalMonsterName;


                        if (!currentChampionLookupTable.ContainsKey(new_monster_name))
                        {
                            Debug.Log("Added champion lookup for monster: " + new_monster_name);
                            currentChampionLookupTable.Add(new_monster_name, old_monster);
                        }
                        else
                        {
                            Debug.Log("Set champion lookup for monster: " + new_monster_name);
                            currentChampionLookupTable[new_monster_name] = old_monster;
                        }

                        randomizeMonsterAISkills(result.Monster[i], self.Level);
                    }
                }
                else
                {
                    if (randomizeMonsters.Value && regularEnemies.Any() && !mimicIDs.Contains(self.ID))
                    {



                        result.Monster[i] = regularEnemies[rand.Next(0, regularEnemies.Count)];
                        randomizeMonsterAISkills(result.Monster[i], self.Level);
                    }
                }

                Debug.Log("Replaced with monster: " + result.Monster[i].GetComponent<Monster>().OriginalMonsterName);
            }


            return result;
        }

        public void randomizeMonsterAISkills(GameObject monster, int level) 
        {
            if (!monstersBuildSkillsSeperately.Value)
            {
                rand = new Random((PlayerController.Instance.PlayerName + monster.GetComponent<Monster>().OriginalMonsterName).GetHashCode());
            }
            var skill_points = (int)Math.Max(Math.Floor(level / 3f), 1);
           // var mana = monster.GetComponent<Monster>().AI.GetAvailableMana();

            var skill_trees = monster.GetComponents<SkillTree>().ToList();

            var skill_manager = monster.GetComponent<SkillManager>();

            skill_manager.AISkills = new List<GameObject>();

            skill_manager.BaseSkills.ForEach(skill =>
            {
                if (skill.GetComponent<PassiveElementModifier>() == null) {
                    skill_manager.AISkills.Add(skill);
                }
            });

            IListExtensions.Shuffle(skill_trees);
            skill_trees.ForEach(tree =>
            {
                var skillAndPoints = getHighestUsableSkill(tree, level, skill_points, monster.GetComponent<Monster>());

                skill_points -= skillAndPoints.Value;

               // Debug.Log("Added AI skill to entity: "+monster+" || "+skillAndPoints.Key);



                if (skillAndPoints.Key != null)
                {
                    skill_manager.AISkills.Add(skillAndPoints.Key);
                }
            });
        }


        public KeyValuePair<GameObject, int> getHighestUsableSkill(SkillTree tree, int level, int skill_points, Monster monster)
        {
            GameObject currentSkill = null;
            int skill_points_used = 0;


            for (int i = 0; i < SkillTree.TierCount; i++)
            {
                if ((i - 1) * 10 <= level)
                {
                    if(skill_points >= i)
                    {
                        
                        switch (i)
                        {
                            case 1:
                                if (tree.Tier1Skills.Any())
                                {
                                    var newCurrentSkill = tree.Tier1Skills[rand.Next(0, tree.Tier1Skills.Count)];
                                    if (newCurrentSkill.gameObject.name != "Revive1" || newCurrentSkill.gameObject.name != "Revive2" || newCurrentSkill.gameObject.name != "Revive3")
                                    {
                                        skill_points_used = i;

                                        if (!monster.gameObject.GetComponent<SkillManager>().AISkills.Contains(newCurrentSkill))
                                        {
                                            currentSkill = newCurrentSkill;
                                        }
                                    }
                                }
                                break;
                            case 2:
                                if (tree.Tier2Skills.Any())
                                {
                                    var newCurrentSkill = tree.Tier2Skills[rand.Next(0, tree.Tier2Skills.Count)];
                                    if (newCurrentSkill.gameObject.name != "Revive1" || newCurrentSkill.gameObject.name != "Revive2" || newCurrentSkill.gameObject.name != "Revive3")
                                    {
                                        skill_points_used = i;
                                        
                                        if (!monster.gameObject.GetComponent<SkillManager>().AISkills.Contains(newCurrentSkill))
                                        {
                                            currentSkill = newCurrentSkill;
                                        }
                                    }
                                }
                                break;
                            case 3:
                                if (tree.Tier3Skills.Any())
                                {
                                    var newCurrentSkill = tree.Tier3Skills[rand.Next(0, tree.Tier3Skills.Count)];
                                    if (newCurrentSkill.gameObject.name != "Revive1" || newCurrentSkill.gameObject.name != "Revive2" || newCurrentSkill.gameObject.name != "Revive3")
                                    {
                                        skill_points_used = i;
                                        
                                        if (!monster.gameObject.GetComponent<SkillManager>().AISkills.Contains(newCurrentSkill))
                                        {
                                            currentSkill = newCurrentSkill;
                                        }
                                    }
                                }
                                break;
                            case 4:
                                if (tree.Tier4Skills.Any())
                                {
                                    var newCurrentSkill = tree.Tier4Skills[rand.Next(0, tree.Tier4Skills.Count)];
                                    if (newCurrentSkill.gameObject.name != "Revive1" || newCurrentSkill.gameObject.name != "Revive2" || newCurrentSkill.gameObject.name != "Revive3")
                                    {
                                        skill_points_used = i;
                                        
                                        if (!monster.gameObject.GetComponent<SkillManager>().AISkills.Contains(newCurrentSkill))
                                        {
                                            currentSkill = newCurrentSkill;
                                        }
                                    }
                                }
                                break;
                            case 5:
                                if (tree.Tier5Skills.Any())
                                {
                                    var newCurrentSkill = tree.Tier5Skills[rand.Next(0, tree.Tier5Skills.Count)];
                                    if (newCurrentSkill.gameObject.name != "Revive1" || newCurrentSkill.gameObject.name != "Revive2" || newCurrentSkill.gameObject.name != "Revive3")
                                    {
                                        skill_points_used = i;
                                        
                                        if (!monster.gameObject.GetComponent<SkillManager>().AISkills.Contains(newCurrentSkill))
                                        {
                                            currentSkill = newCurrentSkill;
                                        }
                                    }
                                }
                                break;
                        }
                    }
                }
            }

            return new KeyValuePair<GameObject, int>( currentSkill, skill_points_used);
        }

        private void GameController_InitPlayerStartSetup(On.GameController.orig_InitPlayerStartSetup orig, GameController self)
        {



            HandleReferenceModification(PlayerController.Instance.PlayerName);
            orig(self);
        }

        private void PlayerController_LoadGame(On.PlayerController.orig_LoadGame orig, PlayerController self, SaveGameData saveGameData, bool newGamePlusSetup)
        {
            self.name = saveGameData.PlayerName;


            HandleReferenceModification(saveGameData.PlayerName);
            orig(self, saveGameData, newGamePlusSetup);
        }


        public void HandleReferenceModification(string playerName)
        {
            rand = new Random(playerName.GetHashCode());
            Debug.Log("Player name: " + playerName);
            Debug.Log("Running randomizer with seed: " + playerName.GetHashCode());
            Debug.Log("Random number: " + rand.Next(0, 100000));


            if (isFirstSpawn)
            {
                GameController.Instance.WorldData.Referenceables.ForEach(referenceable_class =>
                {
                    if(referenceable_class != null)
                    {
                        if(referenceable_class.gameObject != null)
                        {
                            var referenceable = referenceable_class.gameObject;

                            if (referenceable.GetComponent<BaseItem>())
                            {
                                referenceables["loot"].Add(referenceable);
                            }

                            if (referenceable.GetComponent<Monster>())
                            {
                                if (referenceable.GetComponent<SkillTree>() != null)
                                {
                                    referenceable.GetComponents<SkillTree>().ToList().ForEach(skilltree =>
                                    {
                                        skillTrees.Add(duplicateType(skilltree));
                                    });
                                }

                                if (referenceable.GetComponent<SkillTree>() != null)
                                {
                                    referenceable.GetComponents<SkillTree>().ToList().ForEach(skilltree =>
                                    {
                                        skilltree.Tier1Skills.ForEach(skill =>
                                        {
                                            if(skill.GetComponent<ActionDamage>() != null)
                                            {
                                                referenceables["skills.active.tier1"].Add(skill);
                                            }
                                            else if (skill.GetComponent<ActionSFX>() == null)
                                            {
                                                referenceables["skills.passive.tier1"].Add(skill);
                                            }
                                        });
                                        skilltree.Tier2Skills.ForEach(skill =>
                                        {
                                            if (skill.GetComponent<ActionDamage>() != null)
                                            {
                                                referenceables["skills.active.tier2"].Add(skill);
                                            }
                                            else if(skill.GetComponent<ActionSFX>() == null)
                                            {
                                                referenceables["skills.passive.tier2"].Add(skill);
                                            }
                                        });
                                        skilltree.Tier3Skills.ForEach(skill =>
                                        {
                                            if (skill.GetComponent<ActionDamage>() != null)
                                            {
                                                referenceables["skills.active.tier3"].Add(skill);
                                            }
                                            else if (skill.GetComponent<ActionSFX>() == null)
                                            {
                                                referenceables["skills.passive.tier3"].Add(skill);
                                            }
                                        });
                                        skilltree.Tier4Skills.ForEach(skill =>
                                        {
                                            if (skill.GetComponent<ActionDamage>() != null)
                                            {
                                                referenceables["skills.active.tier4"].Add(skill);
                                            }
                                            else if (skill.GetComponent<ActionSFX>() == null)
                                            {
                                                referenceables["skills.passive.tier4"].Add(skill);
                                            }
                                        });
                                        skilltree.Tier5Skills.ForEach(skill =>
                                        {
                                            if (skill.GetComponent<ActionDamage>() != null)
                                            {
                                                referenceables["skills.active.tier5"].Add(skill);
                                            }
                                            else if (skill.GetComponent<ActionSFX>() == null)
                                            {
                                                referenceables["skills.passive.tier5"].Add(skill);
                                            }
                                        });
                                    });
                                }

                                if (referenceable.GetComponent<SkillManager>() != null)
                                {
                                    referenceable.GetComponent<SkillManager>().Ultimates.ForEach(skill =>
                                    {
                                        referenceables["skills.ultimate"].Add(skill);
                                    });

                                    referenceable.GetComponent<SkillManager>().ChampionSkills.ForEach(skill =>
                                    {
                                        if (skill.GetComponent<PassiveChampion>() != null)
                                        {
                                            referenceables["skills.champion_passive"].Add(skill);
                                        }
                                        else
                                        {
                                            referenceables["skills.champion"].Add(skill);
                                        }
                                    });

                                    referenceables["skills.dark"].Add(referenceable.GetComponent<SkillManager>().DarkSkill);
                                    referenceables["skills.light"].Add(referenceable.GetComponent<SkillManager>().LightSkill);

                                    referenceable.GetComponent<SkillManager>().BaseSkills.ForEach(skill =>
                                    {
                                        if(skill.GetComponent<PassiveElementModifier>() != null)
                                        {
                                            if(skill.GetComponent<PassiveElementModifier>().Modifier > 0)
                                            {
                                                referenceables["skills.weakness"].Add(skill);
                                            }
                                            else if(skill.GetComponent<PassiveElementModifier>().Modifier < 0)
                                            {
                                                referenceables["skills.resistance"].Add(skill);
                                            }
                                        }
                                    });

                                    if (referenceable.GetComponent<SkillManager>().ChampionSkills.Any() && referenceable.GetComponent<SkillManager>().GetChampionPassive() != null)
                                    {
                                        if (referenceable.GetComponent<Monster>().IsSpectralFamiliar)
                                        {
                                            referenceables["monsters.familiar"].Add(referenceable);
                                        }
                                        else
                                        {
                                            referenceables["monsters.champion"].Add(referenceable);
                                        }
                                    }
                                    else
                                    {
                                        referenceables["monsters.regular"].Add(referenceable);
                                    }
                                }
                            }
                        }
                    }
                });
                isFirstSpawn = false;
            }


            referenceables["monsters.regular"].ForEach(monster =>
            {
                randomizeMonsterUltimates(monster);
                randomizeMonsterSkillTrees(monster);
                randomizeMonsterElementMultiplier(monster);
                randomizeMonsterShiftSkills(monster);
                addLootIfMissing(monster);
            });
            referenceables["monsters.champion"].ForEach(monster =>
            {
                randomizeMonsterUltimates(monster);
                randomizeMonsterSkillTrees(monster);
                randomizeMonsterElementMultiplier(monster);
                randomizeMonsterShiftSkills(monster);
                randomizeMonsterChampionSkills(monster);
                addLootIfMissing(monster);

            });
            referenceables["monsters.familiar"].ForEach(monster =>
            {
                randomizeMonsterUltimates(monster);
                randomizeMonsterSkillTrees(monster);
                randomizeMonsterElementMultiplier(monster);
                randomizeMonsterShiftSkills(monster);
                randomizeMonsterChampionSkills(monster);
                addLootIfMissing(monster);
            });

            buildMonsterSceneDictionary();
        }

        public void addLootIfMissing(GameObject monster)
        {
            var monsterComponent = monster.GetComponent<Monster>();

            // If not common rewards in loot table
            if (!monsterComponent.RewardsCommon.Any())
            {
                var smoke_bomb = referenceables["loot"].FirstOrDefault(item => item.name == "SmokeBomb");
                var potion = referenceables["loot"].FirstOrDefault(item => item.name == "SmallPotion");
                var food = referenceables["loot"].FindAll(item => item.GetComponent<Food>() != null)[rand.Next(referenceables["loot"].FindAll(item => item.GetComponent<Food>() != null).Count)];

                monsterComponent.RewardsCommon.Add(smoke_bomb);
                monsterComponent.RewardsCommon.Add(potion);
                monsterComponent.RewardsCommon.Add(food);
            }

            if (!monsterComponent.RewardsRare.Any())
            {
                var monster_egg = referenceables["loot"].FirstOrDefault(item => { 
                    if(item.GetComponent<Egg>() != null)
                    {
                        if(item.GetComponent<Egg>().Monster.GetComponent<Monster>().OriginalMonsterName == monsterComponent.OriginalMonsterName)
                        {
                            return true;
                        }
                    }
                    return false;
                });

                var evolution_material = referenceables["loot"].FirstOrDefault(item => {
                    if (item.GetComponent<Catalyst>() != null)
                    {
                        if (item.GetComponent<Catalyst>().BaseMonster.GetComponent<Monster>().OriginalMonsterName == monsterComponent.OriginalMonsterName)
                        {
                            return true;
                        }
                    }
                    return false;
                });

                if (monster_egg != null)
                {
                    monsterComponent.RewardsRare.Add(monster_egg);
                }

                if(evolution_material != null)
                {
                    monsterComponent.RewardsRare.Add(evolution_material);
                }

                var level_badge = referenceables["loot"].FirstOrDefault(item => item.name == "LevelBadge");

                monsterComponent.RewardsRare.Add(level_badge);
            }
        }

        public void randomizeMonsterChampionSkills(GameObject monster)
        {
            if (randomizeChampionSkills.Value)
            {
                var skill_manager = monster.GetComponent<SkillManager>();

                var champion_skill_count = skill_manager.ChampionSkills.Count;


                skill_manager.ChampionSkills = new List<GameObject>();

                for(int i = 0; i < champion_skill_count; i++)
                {
                    if(i == 0)
                    {
                        skill_manager.ChampionSkills.Add(referenceables["skills.champion_passive"][rand.Next(0, referenceables["skills.champion_passive"].Count)]);
                    }
                    else
                    {
                        skill_manager.ChampionSkills.Add(referenceables["skills.champion"][rand.Next(0, referenceables["skills.champion"].Count)]);
                    }
                }
            }
        }

        public void buildMonsterSceneDictionary()
        {
            List<GameObject> availableRegularMonsters = new List<GameObject>();
            List<GameObject> availableChampionMonsters = new List<GameObject>();
            List<GameObject> oldRegularMonsters = new List<GameObject>();
            List<GameObject> oldChampionMonsters = new List<GameObject>();

            referenceables["monsters.regular"].ForEach(monster =>
            {
                availableRegularMonsters.Add(monster);
            });
            referenceables["monsters.champion"].ForEach(monster =>
            {
                availableChampionMonsters.Add(monster);
            });

            /*
            if (addChampionsToRegularPool.Value)
            {
                referenceables["monsters.champion"].ForEach(monster =>
                {
                    availableRegularMonsters.Add(monster);
                });
            }
            */

            GameController.Instance.WorldData.Maps.ForEach(mapData =>
            {
                var current_scene = mapData.SceneName;

                var regular_monsters_in_area = new List<GameObject>();
                var champion_monsters_in_area = new List<GameObject>();

                mapData.Encounters.ForEach(encounter =>
                {
                    encounter.Monsters.ForEach(monster =>
                    {
                        if (monster.gameObject.GetComponent<SkillManager>().ChampionSkills.Any() && monster.gameObject.GetComponent<SkillManager>().GetChampionPassive() != null && encounter.EncounterType == EEncounterType.Champion)
                        {
                            Debug.Log("In area: " + current_scene + ", found champion: " + monster.OriginalMonsterName);
                            if (!champion_monsters_in_area.Any(mon => mon.GetComponent<Monster>().OriginalMonsterName == monster.GetComponent<Monster>().OriginalMonsterName))
                            {
                                champion_monsters_in_area.Add(monster.gameObject);
                            }
                        }
                        else
                        {
                            if (!regular_monsters_in_area.Any(mon => mon.GetComponent<Monster>().OriginalMonsterName == monster.GetComponent<Monster>().OriginalMonsterName))
                            {
                                regular_monsters_in_area.Add(monster.gameObject);
                            }
                        }
                    });
                });

                if (!championMonstersByScene.ContainsKey(current_scene))
                {
                    championMonstersByScene.Add(current_scene, new List<GameObject>());
                }
                else
                {
                    championMonstersByScene[current_scene] = new List<GameObject>();
                }

                if (!regularMonstersByScene.ContainsKey(current_scene))
                {
                    regularMonstersByScene.Add(current_scene, new List<GameObject>());
                }
                else
                {
                    regularMonstersByScene[current_scene] = new List<GameObject>();
                }

                if (!regularAndChampionMonstersByScene.ContainsKey(current_scene))
                {
                    regularAndChampionMonstersByScene.Add(current_scene, new List<GameObject>());
                }
                else
                {
                    regularAndChampionMonstersByScene[current_scene] = new List<GameObject>();
                }

                for (int i = 0; i < regular_monsters_in_area.Count; i++)
                {
                    if (!availableRegularMonsters.Any())
                    {
                        oldRegularMonsters.ForEach(monster_old =>
                        {
                            availableRegularMonsters.Add(monster_old);
                        });
                    }
                    if (!availableChampionMonsters.Any())
                    {
                        oldChampionMonsters.ForEach(monster_old =>
                        {
                            availableChampionMonsters.Add(monster_old);
                        });
                    }

                    //Debug.Log("Monsters available: "+ availableRegularMonsters.Count);

                    if (availableRegularMonsters.Count > 0)
                    {

                        var allAvailableMonsters = availableRegularMonsters.Concat(availableChampionMonsters).ToList();

                        var picked_monster_index = rand.Next(0, availableRegularMonsters.Count);

                        var picked_monster_index2 = rand.Next(0, allAvailableMonsters.Count);

                        if (picked_monster_index2 < availableRegularMonsters.Count)
                        {
                            oldRegularMonsters.Add(availableRegularMonsters[picked_monster_index2]);
                        }

                        oldRegularMonsters.Add(availableRegularMonsters[picked_monster_index]);

                        regularAndChampionMonstersByScene[current_scene].Add(allAvailableMonsters[picked_monster_index2]);

                        var monster1 = allAvailableMonsters[picked_monster_index2];

                        regularMonstersByScene[current_scene].Add(availableRegularMonsters[picked_monster_index]);

                        var monster2 = availableRegularMonsters[picked_monster_index];

                        availableRegularMonsters.Remove(monster1);

                        availableRegularMonsters.Remove(monster2);
 
                    }
                }

                for (int i = 0; i < champion_monsters_in_area.Count; i++)
                {
                    if (!availableChampionMonsters.Any())
                    {
                        oldChampionMonsters.ForEach(monster_old =>
                        {
                            availableChampionMonsters.Add(monster_old);
                        });
                    }

                    var picked_monster_index = rand.Next(0, availableChampionMonsters.Count);

                    oldChampionMonsters.Add(availableChampionMonsters[picked_monster_index]);

                    championMonstersByScene[current_scene].Add(availableChampionMonsters[picked_monster_index]);

                    if (!refightReplacement.ContainsKey(champion_monsters_in_area[i].GetComponent<Monster>().OriginalMonsterName))
                    {
                        refightReplacement.Add(champion_monsters_in_area[i].GetComponent<Monster>().OriginalMonsterName, availableChampionMonsters[picked_monster_index]);
                    }
                    else
                    {
                        refightReplacement[champion_monsters_in_area[i].GetComponent<Monster>().OriginalMonsterName] = availableChampionMonsters[picked_monster_index];
                    }

                    availableChampionMonsters.RemoveAt(picked_monster_index);
                }
            });
        }

        public void randomizeMonsterUltimates(GameObject monster)
        {
            if (randomizeUltimates.Value)
            {
                var skill_manager = monster.GetComponent<SkillManager>();
                skill_manager.Ultimates = new List<GameObject>();

                for (int i = 0; i < 3; i++)
                {
                    skill_manager.Ultimates.Add(pickNewObject(referenceables["skills.ultimate"], skill_manager.Ultimates));
                }
            }
        }

        public void randomizeMonsterSkillTrees(GameObject monster)
        {
            if (randomizeSkillTrees.Value)
            {
                if(monster.GetComponent<SkillTree>() != null)
                {
                    monster.GetComponents<SkillTree>().ToList().ForEach(skilltree =>
                    {
                        Object.DestroyImmediate(skilltree);
                    });

                    var tree_count = 3;

                    if(rand.Next(1, 101) <= 75)
                    {
                        tree_count = 4;
                    }

                    Debug.Log("Monster \"" + monster.name + "\" got " + tree_count + " skill trees.");

                    var skill_manager = monster.GetComponent<SkillManager>();

                    skill_manager.BaseSkills = new List<GameObject>();
                    for(int i = 0; i < tree_count; i++)
                    {
                        var skill_tree = duplicateType(skillTrees[rand.Next(0, skillTrees.Count)]);

                        /*
                        List<KeyValuePair<GameObject, int>> tier1passives = new List<KeyValuePair<GameObject, int>>();
                        List<KeyValuePair<GameObject, int>> tier2passives = new List<KeyValuePair<GameObject, int>>();

                        for (int index = 0; index < skill_tree.Tier1Skills.Count; index++)
                        {
                            var skill = skill_tree.Tier1Skills[index];

                            if(skill.GetComponent<ActionSFX>() == null && skill.GetComponent<ActionDamage>() == null)
                            {
                                tier1passives.Add(new KeyValuePair<GameObject, int>(skill, index));
                            }
                        }

                        for (int index = 0; index < skill_tree.Tier2Skills.Count; index++)
                        {
                            var skill = skill_tree.Tier2Skills[index];

                            if (skill.GetComponent<ActionSFX>() == null && skill.GetComponent<ActionDamage>() == null)
                            {
                                tier2passives.Add(new KeyValuePair<GameObject, int>(skill, index));
                            }
                        }

                        for (int index = 0; index < skill_tree.Tier1Skills.Count; index++)
                        {
                            var skill = skill_tree.Tier1Skills[index];

                            if (skill.GetComponent<ActionSFX>() == null && skill.GetComponent<ActionDamage>() == null)
                            {
                                var skill_new = referenceables["skills.passive.tier1"][rand.Next(referenceables["skills.passive.tier1"].Count)];
                                skill_tree.Tier1Skills[index] = skill_new;
                            }
                        }

                        for (int index = 0; index < skill_tree.Tier2Skills.Count; index++)
                        {
                            var skill = skill_tree.Tier2Skills[index];

                            if (skill.GetComponent<ActionSFX>() == null && skill.GetComponent<ActionDamage>() == null)
                            {
                                var skill_old = skill_tree.Tier2Skills[index];
                                var skill_new = referenceables["skills.passive.tier2"][rand.Next(referenceables["skills.passive.tier2"].Count)];
                                skill_tree.Tier2Skills[index] = skill_new;
                                skill_tree.Tier2Skills[index].GetComponent<BaseAction>().ParentAction = skill_tree.Tier1Skills[tier1passives.FirstOrDefault(passive => passive.Key == skill_old).Value].GetComponent<BaseAction>();
                            }
                        }
                        */

                        var skill_count = 1;
                        if (monster.GetComponent<Monster>().IsSpectralFamiliar)
                        {
                            skill_count = 2;
                        }

                        if (skill_tree.Tier1Skills.Any())
                        {
                            var skill = skill_tree.Tier1Skills[MonsterRandomizer.rand.Next(0, skill_tree.Tier1Skills.Count)];
                            if (skill_manager.BaseSkills.Count <= skill_count)
                            {
                                skill_manager.BaseSkills.Add(skill);
                            }
                        }
                        else if(skill_tree.Tier2Skills.Any())
                        {
                            var skill = skill_tree.Tier2Skills[MonsterRandomizer.rand.Next(0, skill_tree.Tier2Skills.Count)];
                            if (skill_manager.BaseSkills.Count <= skill_count)
                            {
                                skill_manager.BaseSkills.Add(skill);
                            }
                        }

                        CopyComponent(skill_tree, monster);
                    }
                }
            }
        }

        public void randomizeMonsterElementMultiplier(GameObject monster)
        {
            if (randomizeResistanceWeakness.Value)
            {
                var skill_manager = monster.GetComponent<SkillManager>();

                var resistance = referenceables["skills.resistance"][rand.Next(0, referenceables["skills.resistance"].Count)];

                var filtered_weaknesses = new List<GameObject>();

                referenceables["skills.weakness"].ForEach(skill =>
                {
                    if(skill.GetComponent<PassiveElementModifier>().Element != resistance.GetComponent<PassiveElementModifier>().Element)
                    {
                        filtered_weaknesses.Add(skill);
                    }
                });

                var weakness = filtered_weaknesses[rand.Next(0, filtered_weaknesses.Count)];

                skill_manager.BaseSkills.Add(resistance);
                skill_manager.BaseSkills.Add(weakness);
            }
        }

        public void randomizeMonsterShiftSkills(GameObject monster)
        {
            if (randomizeShifts.Value)
            {
                var skill_manager = monster.GetComponent<SkillManager>();

                if (shiftPairs.Value)
                {
                    var random_pick = rand.Next(0, referenceables["skills.dark"].Count);

                    skill_manager.DarkSkill = referenceables["skills.dark"][random_pick];
                    skill_manager.LightSkill = referenceables["skills.light"][random_pick];
                }
                else
                {
                    skill_manager.DarkSkill = referenceables["skills.dark"][rand.Next(0, referenceables["skills.dark"].Count)];
                    skill_manager.LightSkill = referenceables["skills.light"][rand.Next(0, referenceables["skills.light"].Count)];
                }
            }
        }


        public static int skewedRandom(bool side, int min, int max)
        {
            float unif = (float)rand.NextDouble();

            float beta = (float)Math.Pow(Math.Sin(unif * Math.PI / 2f), 2f);

            if (side)
            {
                beta = (beta < 0.5f) ? 2 * beta : 2 * (1 - beta);
            }
            else
            {
                beta = (beta > 0.5f) ? 2 * beta - 1 : 2 * (1 - beta) - 1;
            }

            return (int)Math.Floor(beta * (max - min + 1)) + min;
        }


        public static T CopyComponent<T>(T original, GameObject destination) where T : Component
        {
            System.Type type = original.GetType();
            Component copy = destination.AddComponent(type);
            System.Reflection.FieldInfo[] fields = type.GetFields();
            foreach (System.Reflection.FieldInfo field in fields)
            {
                field.SetValue(copy, field.GetValue(original));
            }
            return copy as T;
        }

        public static T duplicateType<T>(T original) where T : Component
        {
            System.Type type = original.GetType();
            Component copy = (T)Activator.CreateInstance(type);
            System.Reflection.FieldInfo[] fields = type.GetFields();
            foreach (System.Reflection.FieldInfo field in fields)
            {
                field.SetValue(copy, field.GetValue(original));
            }
            return copy as T;
        }

        public static T pickNewObject<T>(List<T> possible_picks, List<T> existing_picks) where T : Object
        {
            if (!possible_picks.Any())
            {
                return null;
            }
            var picked_gameobject = possible_picks[MonsterRandomizer.rand.Next(0, possible_picks.Count)];

            if (existing_picks.Contains(picked_gameobject))
            {
                return pickNewObject(possible_picks, existing_picks);
            }

            return picked_gameobject;
        }
    }
    public static class IListExtensions
    {
        /// <summary>
        /// Shuffles the element order of the specified list.
        /// </summary>
        public static void Shuffle<T>(this IList<T> ts)
        {
            var count = ts.Count;
            var last = count - 1;
            for (var i = 0; i < last; ++i)
            {
                var r = UnityEngine.Random.Range(i, count);
                var tmp = ts[i];
                ts[i] = ts[r];
                ts[r] = tmp;
            }
        }
    }
}
