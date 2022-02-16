
namespace HKHardModeMod;

class HKHardMode : ModBase
{
    public static GameObject vesselFrag;
    public static GameObject heartPiece;
    public static GameObject expPrefab;
    public override List<(string, string)> GetPreloadNames()
    {
        return new()
        {
            ("Ruins2_09", "Vessel Fragment"),
            ("Mines_32", "Heart Piece")
        };
    }
    public static bool GGScene()
    {
        return (GameManager.instance.sm?.mapZone ?? GlobalEnums.MapZone.NONE) == GlobalEnums.MapZone.GODS_GLORY;
            //|| PlayerData.instance?.permadeathMode != 1;
    }
    [FsmPatcher(false, "Room_Jinn", "Jinn NPC", "Conversation Control")]
    private static void PatchJinn(FSMPatch patch)
    {
        Modding.Logger.Log("Modify Jinn NPC");
        patch.EditState("Transaction")
            .ForEachFsmStateActions<CallMethodProper>(x => null)
            .AppendAction(FSMHelper.CreateMethodAction((fsm) => {
                UnityEngine.Object.Instantiate(heartPiece, HeroController.instance.transform.position, 
                    Quaternion.identity)
                        .SetActive(true);
            }));
    }
    public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects)
    {
        expPrefab = UnityEngine.Object.Instantiate(Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(
                x => x.name == "Gas Explosion Recycle L"
                ));
        expPrefab.transform.parent = null;
        expPrefab.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(expPrefab);
        UnityEngine.Object.Destroy(expPrefab.LocateMyFSM("damages_enemy"));
        var dh = expPrefab.GetComponent<DamageHero>();
        dh.damageDealt = 1;
        dh.hazardType = 1;
        dh.shadowDashHazard = false;

        vesselFrag = preloadedObjects["Ruins2_09"]["Vessel Fragment"];
        heartPiece = preloadedObjects["Mines_32"]["Heart Piece"];
        UnityEngine.Object.Destroy(vesselFrag.GetComponent<PersistentBoolItem>());
        UnityEngine.Object.Destroy(heartPiece.GetComponent<PersistentBoolItem>());
        ModHooks.OnEnableEnemyHook += (enemy, isDead) =>
        {
            if(GGScene()) return isDead;
            if(isDead) return isDead;
            if(PlayerData.instance.equippedCharm_20 || PlayerData.instance.equippedCharm_21) return isDead;
            if(enemy.scene.name == "Crossroads_ShamanTemple")
            {
                if(enemy.name.StartsWith("Blocker"))
                {
                    return true;
                }
            }
            return isDead;
        };
        ModHooks.GetPlayerBoolHook += (name, orig) =>
        {
            if(GGScene()) return orig;
            if(name == "crossroadsInfected") return true;
            return orig;
        };
        ModHooks.SetPlayerBoolHook += (name, value) =>
        {
            if(GGScene()) return value;
            if(name == "gotCharm_20")
            {
                PlayerData.instance.shaman = 5;
                PlayMakerFSM.BroadcastEvent("UPDATE SHAMAN");
            }
            return value;
        };
        On.HealthManager.Hit += (orig, self, hit) =>
        {
            if(hit.AttackType == AttackTypes.Spell)
            {
                hit.DamageDealt = Mathf.RoundToInt(hit.DamageDealt * 2.5f);
            }
            orig(self, hit);
        };
        ModHooks.TakeHealthHook += (damage) =>
        {
            if(GGScene()) return damage;
            if(PlayerData.instance.maxHealthBase == 1 || PlayerData.instance.healthBlue > 0) return damage;
            PlayerData.instance.maxHealthBase = Mathf.Max(1, PlayerData.instance.maxHealthBase - damage);
            PlayerData.instance.maxHealth = PlayerData.instance.maxHealthBase;
            var healthGroup = UnityEngine.Object.FindObjectOfType<HUDCamera>()
                .transform.Find("Hud Canvas")
                .Find("Health");
            Log("Take Damage");
            for(int i = 1; i < 12 ; i++)
            {
                var health = healthGroup.Find($"Health {i}");
                if(health != null)
                {
                    if(i > PlayerData.instance.maxHealth)
                    {
                        health.gameObject.LocateMyFSM("health_display").SetState("Charm Pause");
                        foreach(var v in health.GetComponentsInChildren<MeshRenderer>()) v.enabled = false;
                    }
                }
            }
            return damage;
        };
        ModHooks.SoulGainHook += (orig) =>
        {
            if(GGScene()) return orig;
            if(PlayerData.instance.equippedCharm_20 && PlayerData.instance.equippedCharm_21) return 12;
            if(PlayerData.instance.equippedCharm_20) return 4;
            if(PlayerData.instance.equippedCharm_21) return 8;
            return 0;
        };
        On.HealthManager.Start += (orig, self) =>
        {
            orig(self);
            if(GGScene() || self.hp > 800) return;
            self.hp = Mathf.RoundToInt(self.hp * 2.25f * ((PlayerData.instance?.crossroadsInfected ?? false) ? 0.75f : 0.5f)
                 * Mathf.Max(1 , PlayerData.instance?.nailSmithUpgrades ?? 1));
            if(self.name.StartsWith("Bursting Zombie"))
            {
                using(var patch = self.gameObject.LocateMyFSM("Attack").Fsm.CreatePatch())
                {
                    FsmFloat xScale = new();
                    FsmFloat speed = new FsmFloat()
                    {
                        Value = 15
                    };
                    patch.EditState("Running")
                        .AppendAction(new FaceObject()
                        {
                            objectA = new FsmGameObject()
                            {
                                Value = self.gameObject
                            },
                            objectB = new FsmGameObject()
                            {
                                Value = HeroController.instance.gameObject
                            },
                            spriteFacesRight = new()
                            {
                                Value = false
                            },
                            playNewAnimation = false,
                            resetFrame = false,
                            everyFrame = true
                        })
                        .AppendAction(new GetScale()
                        {
                            gameObject = new FsmOwnerDefault()
                            {
                                OwnerOption = OwnerDefaultOption.UseOwner
                            },
                            vector = new(),
                            yScale = new(),
                            zScale = new(),
                            xScale = xScale,
                            everyFrame = true
                        })
                        .AppendAction(new FloatMultiply()
                        {
                            floatVariable = xScale,
                            multiplyBy = new FsmFloat()
                            {
                                Value = -1
                            },
                            everyFrame = true
                        })
                        .AppendAction(new SetFloatValue()
                        {
                            floatVariable = patch.TargetFSM.Variables.FindFsmFloat("Speed"),
                            floatValue = speed,
                            everyFrame = true
                        })
                        .AppendAction(new FloatMultiply()
                        {
                            floatVariable = patch.TargetFSM.Variables.FindFsmFloat("Speed"),
                            multiplyBy = xScale,
                            everyFrame = true
                        })
                        .AppendAction(new SetVelocity2d()
                        {
                            gameObject = new()
                            {
                                OwnerOption = OwnerDefaultOption.UseOwner
                            },
                            x = patch.TargetFSM.Variables.FindFsmFloat("Speed"),
                            y = new(),
                            vector = new(),
                            everyFrame = true
                        })
                        .AppendAction(new Wait()
                        {
                            time = 5,
                            finishEvent = FsmEvent.GetFsmEvent("EXPLODE")
                        })
                        ;
                }
            }
        };
        On.EnemyBullet.Collision += (orig, self, a0, a1) => 
        {
            UnityEngine.Object.Instantiate(expPrefab, self.transform.position, Quaternion.identity)
                .SetActive(true);
            return orig(self, a0, a1);
        };
        On.HeroController.CanFocus += (orig, self) => GGScene() ? orig(self) : false;
        On.HealthManager.Die += (orig, self, a1, attackTypes, a2) => {
            orig(self, a1, attackTypes, a2);
            if(GGScene()) return;
            if(attackTypes == AttackTypes.Nail)
            {
                int weight = 8;
                if(ReflectionHelper.GetField<HealthManager, int>(self, "enemyType") == 3)
                {
                    weight += 8;
                }
                if(UnityEngine.Random.Range(0, 80) <= weight)
                {
                    UnityEngine.Object.Instantiate(heartPiece, self.transform.position, Quaternion.identity)
                        .SetActive(true);
                }
            }
        };
    }
}
