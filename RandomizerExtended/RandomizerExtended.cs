using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using MonoMod;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RandomizerExtended
{
    [BepInDependency("evaisa.MonSancAPI")]
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class RandomizerExtended : BaseUnityPlugin
    {
        public const string ModGUID = "evaisa.randomizerextended";
        public const string ModName = "Randomizer Extended";
        public const string ModVersion = "0.1.0";

        static bool isFirstSpawn = true;

        private ConfigEntry<bool> randomizeChampionSkills;
        private ConfigEntry<bool> randomizeShifts;
        private ConfigEntry<bool> shiftPairs;
        private ConfigEntry<bool> randomizeResistanceWeakness;
        private ConfigEntry<bool> randomizeUltimates;
        private ConfigEntry<bool> randomizeSkillTrees;

        private ConfigEntry<bool> monstersBuildSkillsSeperately;

        public static Dictionary<string, List<GameObject>> referenceables = new Dictionary<string, List<GameObject>>();

        public static List<SkillTree> skillTrees = new List<SkillTree>();


        public RandomizerExtended()
        {
            randomizeSkillTrees = Config.Bind("General", "RandomizeSkillTrees", true, "Randomize skill trees of monsters.");
            randomizeChampionSkills = Config.Bind("General", "RandomizeChampionSkills", true, "Randomize the special champion skills.");
            monstersBuildSkillsSeperately = Config.Bind("General", "MonstersChooseSkillsSeperately", false, "Let every monster of the same type choose its own skills from their skill tree.");
            shiftPairs = Config.Bind("General", "ShiftPairs", false, "Keep the shift skills in the same set. (if this is false the dark and light shift skills are unrelated to eachother)");
            randomizeUltimates = Config.Bind("General", "RandomizeUltimates", true, "Randomize the ultimates of monsters.");
            randomizeShifts = Config.Bind("General", "RandomizeShifts", true, "Randomize the light/dark shifts of monsters.");
            randomizeResistanceWeakness = Config.Bind("General", "RandomizeResistanceWeakness", true, "Randomize resistances and weaknesses of monsters.");


            MonSancAPI.MonSancAPI.RegisterOption(MonSancAPI.MonSancAPI.optionType.gameplay, "RandomizeSkillTrees", delegate (OptionsMenu self) { return Utils.LOCA("Randomize Skill Trees", ELoca.UI); }, delegate (OptionsMenu self) { return self.GetBoolString(randomizeSkillTrees.Value); }, false, delegate (OptionsMenu self) { return false; });
            MonSancAPI.MonSancAPI.RegisterOption(MonSancAPI.MonSancAPI.optionType.gameplay, "RandomizeChampionSkills", delegate (OptionsMenu self) { return Utils.LOCA("Randomize Champion Skills", ELoca.UI); }, delegate (OptionsMenu self) { return self.GetBoolString(randomizeChampionSkills.Value); }, false, delegate (OptionsMenu self) { return false; });
            MonSancAPI.MonSancAPI.RegisterOption(MonSancAPI.MonSancAPI.optionType.gameplay, "MonstersChooseSkillsSeperately", delegate (OptionsMenu self) { return Utils.LOCA("Monsters Choose Their Own Skills", ELoca.UI); }, delegate (OptionsMenu self) { return self.GetBoolString(monstersBuildSkillsSeperately.Value); }, false, delegate (OptionsMenu self) { return false; });
            MonSancAPI.MonSancAPI.RegisterOption(MonSancAPI.MonSancAPI.optionType.gameplay, "ShiftPairs", delegate (OptionsMenu self) { return Utils.LOCA("Same Set Shift Skills", ELoca.UI); }, delegate (OptionsMenu self) { return self.GetBoolString(shiftPairs.Value); }, false, delegate (OptionsMenu self) { return false; });
            MonSancAPI.MonSancAPI.RegisterOption(MonSancAPI.MonSancAPI.optionType.gameplay, "RandomizeUltimates", delegate (OptionsMenu self) { return Utils.LOCA("Randomize Ultimates", ELoca.UI); }, delegate (OptionsMenu self) { return self.GetBoolString(randomizeUltimates.Value); }, false, delegate (OptionsMenu self) { return false; });
            MonSancAPI.MonSancAPI.RegisterOption(MonSancAPI.MonSancAPI.optionType.gameplay, "RandomizeShifts", delegate (OptionsMenu self) { return Utils.LOCA("Randomize Shifts", ELoca.UI); }, delegate (OptionsMenu self) { return self.GetBoolString(randomizeShifts.Value); }, false, delegate (OptionsMenu self) { return false; });
            MonSancAPI.MonSancAPI.RegisterOption(MonSancAPI.MonSancAPI.optionType.gameplay, "RandomizeResistanceWeakness", delegate (OptionsMenu self) { return Utils.LOCA("Randomize Resistance & Weakness", ELoca.UI); }, delegate (OptionsMenu self) { return self.GetBoolString(randomizeResistanceWeakness.Value); }, false, delegate (OptionsMenu self) { return false; });

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

            On.PlayerController.LoadGame += PlayerController_LoadGame;
            On.GameController.InitPlayerStartSetup += GameController_InitPlayerStartSetup;
            On.OptionsMenu.OnOptionsSelected += OptionsMenu_OnOptionsSelected;
          //  On.SaveGameMenu.InitSavegames += SaveGameMenu_InitSavegames;
        }

        private void OptionsMenu_OnOptionsSelected(On.OptionsMenu.orig_OnOptionsSelected orig, OptionsMenu self, MenuListItem menuItem)
        {
            string text = self.optionNames[self.GetCurrentOptionIndex()];
            // Debug.Log(text);
            if (text == "RandomizeSkillTrees")
            {
                if (randomizeSkillTrees.Value == true)
                {
                    randomizeSkillTrees.Value = false;
                }
                else
                {
                    randomizeSkillTrees.Value = true;
                }
                randomizeSkillTrees.ConfigFile.Save();
            }
            else if (text == "RandomizeChampionSkills")
            {
                if (randomizeChampionSkills.Value == true)
                {
                    randomizeChampionSkills.Value = false;
                }
                else
                {
                    randomizeChampionSkills.Value = true;
                }
                randomizeChampionSkills.ConfigFile.Save();
            }
            else if (text == "MonstersChooseSkillsSeperately")
            {
                if (monstersBuildSkillsSeperately.Value == true)
                {
                    monstersBuildSkillsSeperately.Value = false;
                }
                else
                {
                    monstersBuildSkillsSeperately.Value = true;
                }
                monstersBuildSkillsSeperately.ConfigFile.Save();
            }
            else if (text == "ShiftPairs")
            {
                if (shiftPairs.Value == true)
                {
                    shiftPairs.Value = false;
                }
                else
                {
                    shiftPairs.Value = true;
                }
                shiftPairs.ConfigFile.Save();
            }
            else if (text == "RandomizeUltimates")
            {
                if (randomizeUltimates.Value == true)
                {
                    randomizeUltimates.Value = false;
                }
                else
                {
                    randomizeUltimates.Value = true;
                }
                randomizeUltimates.ConfigFile.Save();
            }
            else if (text == "RandomizeShifts")
            {
                if (randomizeShifts.Value == true)
                {
                    randomizeShifts.Value = false;
                }
                else
                {
                    randomizeShifts.Value = true;
                }
                randomizeShifts.ConfigFile.Save();
            }
            else if (text == "RandomizeResistanceWeakness")
            {
                if (randomizeResistanceWeakness.Value == true)
                {
                    randomizeResistanceWeakness.Value = false;
                }
                else
                {
                    randomizeResistanceWeakness.Value = true;
                }
                randomizeResistanceWeakness.ConfigFile.Save();
            }
            orig(self, menuItem);
        }

        private void SaveGameMenu_InitSavegames(On.SaveGameMenu.orig_InitSavegames orig, SaveGameMenu self)
        {
            orig(self);
            self.NewGamePlusAvailable = true;
            Debug.Log("ok?");
        }


        private void PlayerController_LoadGame(On.PlayerController.orig_LoadGame orig, PlayerController self, SaveGameData saveGameData, bool newGamePlusSetup)
        {
            if (GameController.Instance.GameModes.RandomizerMode)
            {
                HandleReferenceModification(GameController.Instance.GameModes.Seed);
            }
            orig(self, saveGameData, newGamePlusSetup);
        }

        private void GameController_InitPlayerStartSetup(On.GameController.orig_InitPlayerStartSetup orig, GameController self)
        {
            if (GameController.Instance.GameModes.RandomizerMode)
            {
                HandleReferenceModification(GameController.Instance.GameModes.Seed);
            }
            orig(self);
        }

        public void HandleReferenceModification(int seed)
        {
            Random.InitState(seed);

            if (isFirstSpawn)
            {
                GameController.Instance.WorldData.Referenceables.ForEach(referenceable_class =>
                {
                    if (referenceable_class != null)
                    {
                        if (referenceable_class.gameObject != null)
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
                                            if (skill.GetComponent<ActionDamage>() != null)
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
                                            else if (skill.GetComponent<ActionSFX>() == null)
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
                                        if (skill.GetComponent<PassiveElementModifier>() != null)
                                        {
                                            if (skill.GetComponent<PassiveElementModifier>().Modifier > 0)
                                            {
                                                referenceables["skills.weakness"].Add(skill);
                                            }
                                            else if (skill.GetComponent<PassiveElementModifier>().Modifier < 0)
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

            Debug.Log("Running randomizer with seed: " + seed);
            Debug.Log("Random number: " + Random.Range(0, 100000));



            // buildMonsterSceneDictionary();
        }

        public void addLootIfMissing(GameObject monster)
        {
            var monsterComponent = monster.GetComponent<Monster>();

            // If not common rewards in loot table
            if (!monsterComponent.RewardsCommon.Any())
            {
                var smoke_bomb = referenceables["loot"].FirstOrDefault(item => item.name == "SmokeBomb");
                var potion = referenceables["loot"].FirstOrDefault(item => item.name == "SmallPotion");
                var food = referenceables["loot"].FindAll(item => item.GetComponent<Food>() != null)[Random.Range(0, referenceables["loot"].FindAll(item => item.GetComponent<Food>() != null).Count)];

                monsterComponent.RewardsCommon.Add(smoke_bomb);
                monsterComponent.RewardsCommon.Add(potion);
                monsterComponent.RewardsCommon.Add(food);
            }

            if (!monsterComponent.RewardsRare.Any())
            {
                var monster_egg = referenceables["loot"].FirstOrDefault(item => {
                    if (item.GetComponent<Egg>() != null)
                    {
                        if (item.GetComponent<Egg>().Monster.GetComponent<Monster>().OriginalMonsterName == monsterComponent.OriginalMonsterName)
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

                if (evolution_material != null)
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

                for (int i = 0; i < champion_skill_count; i++)
                {
                    if (i == 0)
                    {
                        skill_manager.ChampionSkills.Add(referenceables["skills.champion_passive"][Random.Range(0, referenceables["skills.champion_passive"].Count)]);
                    }
                    else
                    {
                        skill_manager.ChampionSkills.Add(referenceables["skills.champion"][Random.Range(0, referenceables["skills.champion"].Count)]);
                    }
                }
            }
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
                if (monster.GetComponent<SkillTree>() != null)
                {
                    monster.GetComponents<SkillTree>().ToList().ForEach(skilltree =>
                    {
                        Object.DestroyImmediate(skilltree);
                    });

                    var tree_count = 3;

                    if (Random.Range(1, 101) <= 75)
                    {
                        tree_count = 4;
                    }

                    Debug.Log("Monster \"" + monster.name + "\" got " + tree_count + " skill trees.");

                    var skill_manager = monster.GetComponent<SkillManager>();

                    skill_manager.BaseSkills = new List<GameObject>();

                    for (int i = 0; i < tree_count; i++)
                    {

                        var filtered_tree = skillTrees.FindAll(tree =>
                        {
                            return !((tree.Tier1Skills.Any(skill => skill_manager.BaseSkills.Contains(skill))) || (!tree.Tier1Skills.Any() && tree.Tier2Skills.Any(skill => skill_manager.BaseSkills.Contains(skill))));
                        });

                        var picked_tree = filtered_tree[Random.Range(0, filtered_tree.Count)];

                        var skill_tree = duplicateType(picked_tree);

                        var skill_count = 1;
                        if (monster.GetComponent<Monster>().IsSpectralFamiliar)
                        {
                            skill_count = 2;
                        }

                        if (skill_tree.Tier1Skills.Any())
                        {
                            var skill = skill_tree.Tier1Skills[Random.Range(0, skill_tree.Tier1Skills.Count)];
                            if (skill_manager.BaseSkills.Count <= skill_count)
                            {
                                skill_manager.BaseSkills.Add(skill);
                            }
                        }
                        else if (skill_tree.Tier2Skills.Any())
                        {
                            var skill = skill_tree.Tier2Skills[Random.Range(0, skill_tree.Tier2Skills.Count)];
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

                var resistance = referenceables["skills.resistance"][Random.Range(0, referenceables["skills.resistance"].Count)];

                var filtered_weaknesses = new List<GameObject>();

                referenceables["skills.weakness"].ForEach(skill =>
                {
                    if (skill.GetComponent<PassiveElementModifier>().Element != resistance.GetComponent<PassiveElementModifier>().Element)
                    {
                        filtered_weaknesses.Add(skill);
                    }
                });

                var weakness = filtered_weaknesses[Random.Range(0, filtered_weaknesses.Count)];

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
                    var random_pick = Random.Range(0, referenceables["skills.dark"].Count);

                    skill_manager.DarkSkill = referenceables["skills.dark"][random_pick];
                    skill_manager.LightSkill = referenceables["skills.light"][random_pick];
                }
                else
                {
                    skill_manager.DarkSkill = referenceables["skills.dark"][Random.Range(0, referenceables["skills.dark"].Count)];
                    skill_manager.LightSkill = referenceables["skills.light"][Random.Range(0, referenceables["skills.light"].Count)];
                }
            }
        }


        public static int skewedRandom(bool side, int min, int max)
        {
            float unif = (float)Random.Range(0.0f,1.0f);

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
            var picked_gameobject = possible_picks[Random.Range(0, possible_picks.Count)];
                
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
