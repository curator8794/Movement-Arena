using DeadworksManaged.Api;
using System.Drawing;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using static CMsgSteamLearn_InferenceMetadata_Response.Types.CompactTable.Types;

namespace ArenaRework
{

    public class AutoRestartConfig : IConfig
    {
        /// <summary>Restart interval in minutes.</summary>
        public int IntervalMinutes { get; set; } = 90;

        public void Validate()
        {
            if (IntervalMinutes < 1) IntervalMinutes = 1;
        }
    }
    public class GameData
    {
        public int RunnerStreak { get; set; } = 0;
        public int MovementStreak { get; set; } = 0;
        public bool InGame { get; set; } = false;
        public bool Taggable { get; set; } = true;
        public bool Teleportable { get; set; } = false;
        public bool RecentMelee { get; set; } = false;
        public int? WinTextIndex { get; set; } = null;
        public int? NameTextIndex { get; set; } = null;
        public int? SpeedTextIndex { get; set; } = null;
        public double HP { get; set; } = 2;
    }
    public class SteamAccount //files named with steamid
    {
        public string? Username { get; set; }
        public int AbilityPoints { get; set; } = 0;
        public bool InGame { get; set; } = false;
        public double Souls { get; set; } = 0;
        public int ArenaWins { get; set; } = 0;
        public Heroes Hero { get; set; } = Heroes.Lash;
        public GameData Data { get; set; } = new();
        

    }
    public class HeroBalance
    {
        public int HP { get; set; } = 2;
        public double Mult { get; set; } = 1;
    }
    public class AbilityBalance
    {
        public Int32 Tier { get; set; } = 0b11111;
        public bool OnChaser { get; set; } = true;
        public bool OnRunner { get; set; } = true;
        //public int Cooldown { get; set; } = 20; Not Sure if i wanna do this, kinda nice to have default cd's
    }


    public class Main : DeadworksPluginBase
    {

        public override string Name => "Movement Arena";

        [PluginConfig]
        public AutoRestartConfig Config { get; set; } = new();

        private IHandle? _restartSequence;

        public override void OnStartupServer()
        {
            EnsureConVars();
            StartRestartSequence();
            safe = true;
            Timer.Once(5.Seconds(), () =>
            {
                MadeLeaderboard = MakeLeaderboard();
            });
            Timer.Once(10.Seconds(), () =>
                {
                    InitializeText();
                });

        }

        public override void OnConfigReloaded()
        {
            _restartSequence?.Cancel();
            StartRestartSequence();
        }


        public override void OnLoad(bool isReload)
        {
            foreach (CCitadelPlayerController controller in Players.GetAllControllers())
            {
                var SteamID = controller.PlayerSteamId;
                TryAddAccount(SteamID);
                ReadAccount(SteamID);
                AccountDict[SteamID].Username = controller.PlayerName;
                Timer.Once(5.Seconds(), () => safe = true);
                var pawn = controller.GetHeroPawn();
                if (pawn == null) Console.WriteLine("null pawn on load");
                else
                {
                    ApplyAccount(pawn);
                    WriteAccount(SteamID);
                }
            }
            IHandle? heartbeat = Timer.Every(1.Seconds(), () =>
            {
                EnsureConVars();
            });
            IHandle? balance = Timer.Every(30.Seconds(), () =>
            {
                if (match) TryBalanceTeams();
            });
            
            IHandle? autokick = Timer.Every(120.Seconds(), () =>
            {
                Server.ExecuteCommand("sv_cheats 1");
                Server.ExecuteCommand("citadel_kick_disconnected_players");
                safe = true;
                Server.ExecuteCommand("sv_cheats 0");
            });
            

            Console.WriteLine($"[{Name}] Loaded! (reload={isReload})");
        }
        public override void OnUnload()
        {
            WriteAllAccounts();
            _restartSequence?.Cancel();
            Console.WriteLine($"[{Name}] Unloaded!");
        }



        private readonly UInt64[] AdminIDList = { 76561198109630389, };

        public bool match = true;
        public bool competitive = false;
        public bool arena = true;
        public bool apevent = false;
        public string apeventname = "";
        public bool modifier = false;
        public string modifiername = "";
        public bool safe { get; set; } = false;
        public string MadeLeaderboard = "";

        public Dictionary<ulong, SteamAccount> AccountDict = new();

        Vector3[] Spawns = [new(5000, 1400, 1200), new(6720, 2170, 1025), new(6545, 5100, 630), new(0, 4700, 512), new(-2230, 3600, 376), new(-7660, 3770, 648), new(-5700, 1730, 929), new(-2865, 5050, 1056), new(-2130, 2060, 609), new(3670, 3070, 416), new(3840, 1840, 536), new(0, 0, 576), new(-8300, 0, 416), new(-6600, 0, 132), new(-3930, 140, 912), new(3930, -140, 912), new(0, -7950, 646), new(0, -5800, 1232), new(1000, -3420, 1045), new(2600, -5060, 1024), new(5680, -1720, 928), new(1415, -975, 384), new(-4000, -2090, 968), new(-8525, -3320, 960), new(-5353, -5526, 1380), new(-2685, -2640, 640), new(-2360, -7440, 512), new(5680, -3600, 864)];


        public bool recruiting = false;



        public Dictionary<Heroes, HeroBalance> BalanceDict = new()
        {
            //Bucket 1
            //4 Stam
            {Heroes.Tengu ,new(){HP=1,Mult=0.60 } }, //Lowgrav
            {Heroes.Unicorn,new(){HP=1,Mult=0.5 } }, //OFF
            {Heroes.Orion ,new(){HP=2,Mult=0.75 }  }, //Flight
            //3 Stam
            {Heroes.Haze ,new(){HP=1,Mult=0.75 } }, //Smoke Bomb
            {Heroes.Doorman ,new(){HP=2,Mult=0.70 } }, //Door
            {Heroes.Nano ,new(){HP=1,Mult=0.75 } },
            {Heroes.Chrono ,new(){HP=2,Mult=0.75 } }, //Carbine
            //2 Stam
            {Heroes.VampireBat ,new(){HP=2,Mult=0.75 } }, //Broken, Has Umbrella
            {Heroes.Astro ,new(){HP=1,Mult=0.75 } }, // Jump Pad
            
            //Bucket 2
            //3 Stam
            {Heroes.Lash, new(){HP=2,Mult=0.75 } }, //Ground Strike 
            {Heroes.Viper ,new(){HP=2,Mult=0.75 } }, //Slither Slide
            {Heroes.Viscous, new(){HP=2,Mult=0.75 } }, //Cube
            {Heroes.Fencer ,new(){HP=2,Mult=0.75 }}, // Riposte (broken)
            {Heroes.PunkGoat ,new(){HP=2,Mult=1.1 } }, //Broken
            {Heroes.Drifter ,new(){HP=2,Mult=1 } }, //
            {Heroes.Inferno ,new(){HP=2,Mult=1 } },
            {Heroes.Mirage ,new(){HP=2,Mult=1 } }, //add dust devil?
            {Heroes.Synth ,new(){HP=2,Mult=0.75 } }, //Barrage
            {Heroes.Familiar ,new(){HP=2,Mult=1 } },
            {Heroes.Gigawatt ,new(){HP=2,Mult=1.1 } },
            {Heroes.Magician ,new(){HP=2,Mult=1 } },
            {Heroes.Warden ,new(){HP=2,Mult=1.1 } }, //Willpower
            {Heroes.Wraith ,new(){HP=2,Mult=1 } },
            {Heroes.Yamato ,new(){HP=2,Mult=0.9 } }, //Power Slash
            //2 Stam
            {Heroes.Shiv,new(){HP=1,Mult=0.5 } }, //Alt-fire
            {Heroes.Necro ,new(){HP=2,Mult=1.1 } },
            {Heroes.Forge ,new(){HP=2,Mult=1.2 } },
            {Heroes.Bookworm ,new(){HP=2,Mult=1 } },
            {Heroes.Werewolf ,new(){HP=2,Mult=1.2 } }, //Bootkick t5
            {Heroes.Hornet ,new(){HP=2,Mult=0.75 } }, //Flight

            //Bucket 3
            //3 Stam
            {Heroes.Atlas ,new(){HP=3,Mult=1.35 } },
            {Heroes.Dynamo ,new(){HP=2,Mult=0.9 } }, //Quantum
            {Heroes.Ghost ,new(){HP=3,Mult=1.1 } },
            {Heroes.Krill ,new(){HP=3,Mult=1 } },
            {Heroes.Priest ,new(){HP=2,Mult=0.85 } }, //Trap
            {Heroes.Frank ,new(){HP=2,Mult=1.5 } }, //Jumpstart
            {Heroes.Kelvin ,new(){HP=2,Mult=2 }},
            //2 Stam
            {Heroes.Bebop ,new(){HP=2,Mult=1.75 } },
        };



        public override void OnClientFullConnect(ClientFullConnectEvent args)
        {

                var controller = args.Controller;
            if (controller == null) ErrorCode("421");
            else
            {
                var SteamID = controller.PlayerSteamId;

                TryAddAccount(SteamID);
                ReadAccount(SteamID);
                AccountDict[SteamID].Username = controller.PlayerName;

                IntoLobby(controller);
                WriteAccount(SteamID);

                var pawn = controller.GetHeroPawn();
                if (pawn == null) ErrorCode("679");
                else
                {
                    ApplyAccount(pawn);
                }



            }
        }

        
        public override bool OnClientConnect(ClientConnectEvent args)
        {
            try { Server.ExecuteCommand("citadel_kick_disconnected_players"); } catch { }
            return true;
        }


        public override void OnClientDisconnect(ClientDisconnectedEvent args)
        {
            var controller = args.Controller;
            if (controller == null) Console.WriteLine("6544215");
            else
            {
                var id = controller.PlayerSteamId;
                var slot = controller.Slot;
                WriteAccount(id);
                TryRemoveAccount(id);
                controller.GetHeroPawn()?.Remove();
                controller.Remove();
                //Server.ExecuteCommand($"kick {slot}");
            }
            foreach (var delay in new[] { 1, 3, 5, 8 })
            {
                Timer.Once(delay.Seconds(), () =>
                    Server.ExecuteCommand("citadel_kick_disconnected_players"));
            }
            Timer.Once(10.Seconds(), () => {
                if (controller == null || !controller.IsValid) return;
                var pawn = controller.GetHeroPawn();
                if (pawn != null && pawn.IsValid) pawn.Remove();
                controller.Remove();
            });
        }


        
        /*
        [GameEventHandler("player_disconnect")]
        public HookResult OnDC(PlayerDisconnectEvent args)
        {
            Server.ExecuteCommand($"kick {args.Userid.Slot}");
            return HookResult.Continue;
        }
        */

        public override void OnEntitySpawned(EntitySpawnedEvent args)
        {
            if (garbageEntities.Contains(args.Entity.DesignerName))
                args.Entity.Remove();

            if (garbageEntityNames.Contains(args.Entity.Name))
                args.Entity.Remove();
        }
        string[] garbageEntities = {
            "citadel_trigger_teleport",
            "npc_trooper_boss",
            "npc_boss_tier2",
            "npc_boss_tier3",
            "baseanimgraph",
            "destroyable_building",
            "npc_base_defense_sentry",
            "citadel_herotest_orbspawner",
            "npc_barrack_boss",
            "info_neutral_trooper_camp",
            "npc_trooper",
            "npc_super_neutral",
            "npc_trooper_neutral",
            "npc_neutral_sinners_sacrifice",
            "trigger_item_shop",
            "citadel_trigger_shop_tunnel",
            "trigger_item_shop_safe_zone",
            "func_regenerate",
            "citadel_item_pickup_idol",
            "citadel_item_powerup_spawner",
            "citadel_punchable_powerup",
            "citadel_breakable_prop",
            "item_crate_spawn",
            "citadel_zap_trigger",
            "npc_neutral_bug",
        };
        string[] garbageEntityNames = {
            "amber_shrine_east",
            "amber_shrine_west",
            "amber_effigy_brush",
            "sapphire_shrine_east",
            "sapphire_shrine_west",
            "sapphire_effigy_brush",
            "sapphire_watcher_broadway_left",
            "sapphire_watcher_broadway_right",
            "sapphire_watcher_york_right",
            "sapphire_watcher_york_left",
            "sapphire_watcher_park_left",
            "sapphire_watcher_park_right",
            "amber_watcher_broadway_left",
            "amber_watcher_broadway_right",
            "amber_watcher_york_right",
            "amber_watcher_york_left",
            "amber_watcher_park_left",
            "amber_watcher_park_right",
        };





        public override void OnGameFrame(bool simulating, bool firstTick, bool lastTick)
        {
            UpdatePlayerText();

            if (match)
            {
                if (competitive)
                {
                    if (arena)
                        foreach (CCitadelPlayerPawn pawn in Players.GetAllPawns())
                        {

                        }

                    if (apevent)
                    {
                        if (apeventname == "")
                        {

                        }
                    }
                }

                if (!competitive)
                {
                    if (arena)
                    {
                        foreach (CCitadelPlayerPawn pawn in Players.GetAllPawns())
                        {
                            if (pawn.Controller != null)
                            {
                                if (AccountDict[pawn.Controller.PlayerSteamId].InGame)
                                {
                                    TickCurrency(pawn);
                                    TickStreak(pawn);
                                    if (pawn.GetCurrency(ECurrencyType.EGold) >= 100000) WinPlayer(pawn);
                                    if (pawn.TeamNum == 3)
                                    {
                                        if (recruiting && InMidPit(pawn))
                                        {
                                            pawn.AddModifier("citadel_change_team");
                                            recruiting = false;
                                        }
                                        if (AccountDict[pawn.Controller.PlayerSteamId].Data.Teleportable)
                                        {
                                            TryPitTp(pawn);
                                        }
                                    }
                                    pawn.ModifierProp?.SetModifierState(EModifierState.VisibleToEnemy, true);
                                }
                                if (!AccountDict[pawn.Controller.PlayerSteamId].InGame)
                                {
                                    KeepInLobby(pawn);
                                    if (InTextTp(pawn)) IntoArena(pawn.Controller);
                                }
                            }
                        }
                    }

                    if (apevent)
                    {
                        /*
                        foreach (CCitadelPlayerPawn pawn in Players.GetAllPawns())
                        {
                            if (AccountDict[pawn.Controller.PlayerSteamId].InGame)
                            {

                            }
                            if (!AccountDict[pawn.Controller.PlayerSteamId].InGame)
                            {
                                KeepInLobby(pawn);
                            }
                        }
                        */
                    }
                }
            }

        }


        public override HookResult OnTakeDamage(TakeDamageEvent damageEvent)
        {
            //Main Tagging
            //Console.WriteLine(damageEvent.Info.Ability.Classname);
            var victimEntity = damageEvent.Entity;
            var attackerEntity = damageEvent.Info.Attacker;
            if (attackerEntity == null) { ErrorCode("1111"); return HookResult.Stop; }

            var attackerPawn = attackerEntity.As<CCitadelPlayerPawn>();
            var victimPawn = victimEntity.As<CCitadelPlayerPawn>();
            if (attackerPawn == null) { ErrorCode("1112"); return HookResult.Stop; }
            if (victimPawn == null) { ErrorCode("1113"); return HookResult.Stop; } // i've seen this a few times, probably to do with shooting gun?

            var attackerController = attackerPawn.Controller;
            var victimController = victimPawn.Controller;
            if (attackerController == null) { ErrorCode("1114"); return HookResult.Stop; }
            if (victimController == null) { ErrorCode("1115"); return HookResult.Stop; }

            var ability = damageEvent.Info.Ability;
            if (ability == null) { ErrorCode("1116"); return HookResult.Stop; }
            var realmelee = RealMelee(ability, victimController.PlayerSteamId);
            var taggable = AccountDict[victimController.PlayerSteamId].Data.Taggable;

            if (arena)
            {
                if (realmelee)
                {
                    if (attackerPawn.TeamNum == 2 && victimPawn.TeamNum == 3)
                    {
                        if ((damageEvent.Info.DamageFlags & TakeDamageFlags.LightMelee) != 0)
                        {
                            ModifySouls(attackerPawn, 750);
                        }
                        if ((damageEvent.Info.DamageFlags & TakeDamageFlags.HeavyMelee) != 0)
                        {
                            ModifySouls(attackerPawn, 1250);
                        }

                        return HookResult.Stop;
                    }
                    if (attackerPawn.TeamNum == 3 && victimPawn.TeamNum == 2)
                    {
                        if ((damageEvent.Info.DamageFlags & TakeDamageFlags.LightMelee) != 0 && taggable)
                        {
                            ModifyHP(attackerPawn, victimPawn, -1);
                            ModifySouls(attackerPawn, 750);
                        }
                        if ((damageEvent.Info.DamageFlags & TakeDamageFlags.HeavyMelee) != 0 && taggable)
                        {
                            ModifyHP(attackerPawn, victimPawn, -2);
                            ModifySouls(attackerPawn, 1500);
                        }

                        if (AccountDict[victimController.PlayerSteamId].Data.HP <= 0 && taggable)
                        {
                            ResetHP(victimController);
                            AccountDict[attackerController.PlayerSteamId].Data.Taggable = false;
                            Timer.Once(1.5.Seconds(), () => AccountDict[attackerController.PlayerSteamId].Data.Taggable = true);

                            damageEvent.Info.DamageFlags = TakeDamageFlags.ForceDeath & TakeDamageFlags.AllowSuicide;
                            damageEvent.Info.DamageType = 16777216;

                            ModifySouls(attackerPawn, AccountDict[victimController.PlayerSteamId].Data.RunnerStreak);
                            attackerPawn.SetStamina(3);
                            CCitadelUserMsg_HudGameAnnouncement tagmsg = new()
                            {
                                TitleLocstring = "RUN!!!",
                                DescriptionLocstring = $"You have taken {victimController.PlayerName}'s spot on the Hidden King!",
                            };
                            CCitadelUserMsg_HudGameAnnouncement taggedmsg = new()
                            {
                                TitleLocstring = "TAGGED",
                                DescriptionLocstring = $"You have been tagged by {attackerController.PlayerName}!",
                            };
                            NetMessages.Send<CCitadelUserMsg_HudGameAnnouncement>(tagmsg, RecipientFilter.Single(attackerController.Slot));
                            Timer.Once(1.1.Seconds(), () => NetMessages.Send<CCitadelUserMsg_HudGameAnnouncement>(taggedmsg, RecipientFilter.Single(victimController.Slot)));
                            attackerEntity.AddModifier("citadel_change_team");
                            victimEntity.AddModifier("citadel_change_team");
                            Timer.Once(1.1.Seconds(), () => IntoArena(victimController, true));
                            //maybe remove above line, see what happens, and then handle that case
                            return HookResult.Continue;
                        }
                    }
                }
            }
            return HookResult.Stop;
        }
        public int CheckTag()
        {
            return 1;
        }
        public void DoTag()
        {

        }
        public void DoKill()
        {

        }
        public bool RealMelee(CBaseEntity ability, ulong id)
        {
            if (ability.Classname == "CCitadel_Ability_HoldMelee")
                if (!AccountDict[id].Data.RecentMelee) return true;
            return false;
        }


        /*
        [GameEventHandler("player_respawned")]
        public HookResult OnPlayerRespawned(PlayerRespawnedEvent args)
        {
            var pawn = (CCitadelPlayerPawn?)args.Userid;
            if (pawn == null) { ErrorCode("493"); return HookResult.Continue; }

            var controller = pawn.Controller;
            if (match && controller != null)
            {
                IntoArena(controller);
            }
            return HookResult.Continue;
        }
        */






        public override HookResult OnModifyCurrency(ModifyCurrencyEvent args)
        {
            if (args.CurrencyType == ECurrencyType.EGold)
            {
                if (args.Source != ECurrencySource.ECheats)
                    return HookResult.Stop;
            }
            if (args.CurrencyType == ECurrencyType.EAbilityPoints)
            {
                if (args.Source != ECurrencySource.ECheats)
                    return HookResult.Stop;
            }
            return HookResult.Continue;
        }

        public override HookResult OnClientConCommand(ClientConCommandEvent ctx)
        {
            var command = ctx.Command;
            if ((command == "changeteam" || command == "jointeam"))
            {
                return HookResult.Continue;
            }
            var controller = ctx.Controller;
            if (command == "selecthero" && controller != null)
            {
                if (!AccountDict[controller.PlayerSteamId].InGame && !string.Equals(ctx.Args[1], "hero_unicorn", StringComparison.CurrentCultureIgnoreCase) && !string.Equals(ctx.Args[1], "hero_vampirebat", StringComparison.CurrentCultureIgnoreCase) && !string.Equals(ctx.Args[1], "hero_familiar", StringComparison.CurrentCultureIgnoreCase) && !string.Equals(ctx.Args[1], "hero_PunkGoat", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (HeroTypeExtensions.TryParse(ctx.Args[1], out Heroes hero))
                        AccountDict[controller.PlayerSteamId].Hero = hero;
                    return HookResult.Continue;
                }
                if (AccountDict[controller.PlayerSteamId].InGame)
                    return HookResult.Stop;
                return HookResult.Stop;
            }

            return HookResult.Continue;
        }

        /*
        [GameEventHandler("player_used_ability")]
        public HookResult OnAbilityUsed(PlayerUsedAbilityEvent __event)
        {
            Console.Write(__event.Player);
            Console.Write(__event.Caster);
            Console.Write(__event.Abilityname);
            Console.Write(__event.Annotation);
            return HookResult.Handled;
        }
        */




        public void UpdatePlayerText()
        {
            foreach (CCitadelPlayerPawn pawn in Players.GetAllPawns())
            {
                TryCreateNameText(pawn);
                TryCreateSpeedText(pawn);
                TryCreateWinText(pawn);
                UpdateNameText(pawn);
                UpdateSpeedText(pawn);
                UpdateWinText(pawn);
            }
        }

        public void TryCreateNameText(CCitadelPlayerPawn pawn)
        {
            var controller = pawn.Controller;
            if (controller == null) ErrorCode("438");
            else if (AccountDict[controller.PlayerSteamId].Data.NameTextIndex == null)
            {
                var text = CBaseEntity.CreateByDesignerName("point_worldtext");
                var ekv = new CEntityKeyValues();
                ekv.SetString("message", $"{controller.PlayerName}");
                ekv.SetInt("font_size", 120);
                ekv.SetString("font_name", "Comic Sans MS");
                ekv.SetInt("enabled", 1);
                ekv.SetFloat("world_units_per_pixel", 0.12f);
                ekv.SetInt("justify_horizontal", 1);
                ekv.SetInt("reorient_mode", 1);
                ekv.SetInt("fullbright", 1);
                ekv.SetColor("color", 255, 255, 255, 255);
                if (text == null) ErrorCode("840");
                else
                {
                    text.Spawn(ekv);
                    text.Teleport(pawn.Position + new Vector3(0, 0, 100), new Vector3(0, 180, 90));
                    text.SetParent(pawn);
                    AccountDict[controller.PlayerSteamId].Data.NameTextIndex = text.EntityIndex;
                }
            }
        }
        public void TryCreateSpeedText(CCitadelPlayerPawn pawn)
        {
            var controller = pawn.Controller;
            if (controller == null) ErrorCode("241");
            else if (AccountDict[controller.PlayerSteamId].Data.SpeedTextIndex == null)
            {
                var text = CBaseEntity.CreateByDesignerName("point_worldtext");
                var ekv = new CEntityKeyValues();
                ekv.SetString("message", "Speed");
                ekv.SetInt("font_size", 500);
                ekv.SetString("font_name", "Cambria Math");
                ekv.SetInt("enabled", 1);
                ekv.SetFloat("world_units_per_pixel", 0.12f);
                ekv.SetInt("justify_horizontal", 1);
                ekv.SetInt("reorient_mode", 1);
                ekv.SetInt("fullbright", 1);
                ekv.SetColor("color", 20, 200, 80, 255);
                if (text == null) ErrorCode("492");
                else
                {
                    text.Spawn(ekv);
                    text.Teleport(pawn.Position + new Vector3(0, 0, 84), new Vector3(0, 180, 90));
                    text.SetParent(pawn);
                    AccountDict[controller.PlayerSteamId].Data.SpeedTextIndex = text.EntityIndex;
                }
            }
        }
        public void TryCreateWinText(CCitadelPlayerPawn pawn)
        {
            var controller = pawn.Controller;
            if (controller == null) Console.WriteLine("948");
            else if (AccountDict[controller.PlayerSteamId].Data.WinTextIndex == null)
            {
                var text = CBaseEntity.CreateByDesignerName("point_worldtext");
                var ekv = new CEntityKeyValues();
                ekv.SetString("message", "Wins");
                ekv.SetInt("font_size", 15);
                ekv.SetString("font_name", "Comic Sans MS");
                ekv.SetInt("enabled", 1);
                ekv.SetFloat("world_units_per_pixel", 0.12f);
                ekv.SetInt("justify_horizontal", 1);
                ekv.SetInt("reorient_mode", 1);
                ekv.SetInt("fullbright", 1);
                ekv.SetColor("color", 255, 255, 255, 100);
                if (text == null) ErrorCode("127");
                else
                {
                    text.Spawn(ekv);
                    text.Teleport(pawn.Position + new Vector3(0, 0, 95), new Vector3(0, 180, 90));
                    text.SetParent(pawn);
                    AccountDict[controller.PlayerSteamId].Data.WinTextIndex = text.EntityIndex;
                }
            }
        }
        public void UpdateNameText(CCitadelPlayerPawn pawn)
        {
            var controller = pawn.Controller;
            if (controller == null) ErrorCode("421");
            else
            {
                var SteamID = controller.PlayerSteamId;
                var TextIndex = AccountDict[SteamID].Data.NameTextIndex;
                if (TextIndex != null)
                {
                    CPointWorldText? NameText = CBaseEntity.FromIndex<CPointWorldText>((int)TextIndex);
                    if (NameText == null)
                    {
                        TextIndex = null;
                    }
                    if (NameText != null)
                    {
                        //string message = NameText.GetField<string>("CPointWorldText"u8, "m_messageText"u8);
                        if (pawn.TeamNum == 2)
                        {
                            NameText.SetMessage($"[{AccountDict[SteamID].Data.HP}]\n{controller.PlayerName} ({Math.Min(AccountDict[SteamID].Data.RunnerStreak / 640, 10)})");
                            Timer.Once(1.Ticks(), () => NameText.SetColor(Color.Goldenrod)); //212, 135, 12);
                        }

                        if (pawn.TeamNum == 3)
                        {
                            NameText.SetMessage($"{controller.PlayerName}");
                            Timer.Once(1.Ticks(), () => NameText.SetColor(Color.CornflowerBlue)); //78, 118, 196
                        }
                    }
                }
            }

        }
        public void UpdateSpeedText(CCitadelPlayerPawn pawn)
        {
            var controller = pawn.Controller;
            if (controller == null) ErrorCode("514");
            else
            {
                var SteamID = controller.PlayerSteamId;
                var TextIndex = AccountDict[SteamID].Data.SpeedTextIndex;
                if (TextIndex != null)
                {
                    CPointWorldText? SpeedText = CBaseEntity.FromIndex<CPointWorldText>((int)TextIndex);
                    if (SpeedText == null)
                    {
                        TextIndex = null;
                    }
                    if (SpeedText != null)
                    {
                        //string message = NameText.GetField<string>("CPointWorldText"u8, "m_messageText"u8);
                        SpeedText.SetMessage($"{Math.Round(XYVelocity(pawn))} ({Math.Min(AccountDict[SteamID].Data.MovementStreak / 160, 10)})");
                    }
                }
            }
        }
        public void UpdateWinText(CCitadelPlayerPawn pawn)
        {
            var controller = pawn.Controller;
            if (controller == null) ErrorCode("614");
            else
            {
                var SteamID = controller.PlayerSteamId;
                var TextIndex = AccountDict[SteamID].Data.WinTextIndex;
                if (TextIndex != null)
                {
                    CPointWorldText? WinText = CBaseEntity.FromIndex<CPointWorldText>((int)TextIndex);
                    if (WinText == null)
                    {
                        TextIndex = null;
                    }
                    if (WinText != null)
                    {
                        //string message = NameText.GetField<string>("CPointWorldText"u8, "m_messageText"u8);
                        WinText.SetMessage($"Wins: {AccountDict[SteamID].ArenaWins}");
                    }
                }
            }
        }


        public string BalanceNeeded()
        {
            var HK = 0;
            var AM = 0;
            foreach (CCitadelPlayerPawn pawn in Players.GetAllPawns())
            {
                if (pawn.TeamNum == 2) HK++;
                if (pawn.TeamNum == 3) AM++;
            }
            if (HK >= 1 || AM >= 2)
            { // 0 1, 1 1, 1 2, 1 3, 1 4, 2 4, 2 5, 2 6, 2 7, 2 8, 3 8, 3 9, 3 10, 3 11, 3 12,
                if (HK <= (AM - 1) / 4) return "add";
                if (HK > (AM / 4) + 1) return "remove";
            }
            return "none";
        }
        public void TryBalanceTeams()
        {
            string needed = BalanceNeeded();
            if (needed == "add") AddHK();
            if (needed == "remove") RemoveHK();
        }

        public void AddHK()
        {
            CCitadelUserMsg_HudGameAnnouncement announcement = new()
            {
                TitleLocstring = "The Hidden King is Recruiting!",
                DescriptionLocstring = "Reach Midboss Pit first to be chosen",
            };
            foreach (CCitadelPlayerController controller in Players.GetAllControllers())
            {
                var slot = controller.Slot;
                if (controller.TeamNum == 3)
                {
                    NetMessages.Send<CCitadelUserMsg_HudGameAnnouncement>(announcement, RecipientFilter.Single(slot));
                    
                }
            }
            Timer.Once(4.Seconds(), () =>
            {
                foreach (CCitadelPlayerController controller in Players.GetAllControllers())
                {
                    var slot = controller.Slot;
                    if (controller.TeamNum == 3)
                    {
                        if (BalanceNeeded() == "add" && recruiting)
                            NetMessages.Send<CCitadelUserMsg_HudGameAnnouncement>(announcement, RecipientFilter.Single(slot));
                    }
                }
            });
            recruiting = true;
        }
        public void RemoveHK()
        {
            CCitadelUserMsg_HudGameAnnouncement announcement = new()
            {
                TitleLocstring = "Layoffs at the Hidden King",
                DescriptionLocstring = "Get to a lower altitude to avoid being fired!",
            };
            foreach (CCitadelPlayerController controller in Players.GetAllControllers())
            {
                var slot = controller.Slot;
                if (controller.TeamNum == 2)
                    NetMessages.Send<CCitadelUserMsg_HudGameAnnouncement>(announcement, RecipientFilter.Single(slot));
            }
            //NOTE: BalanceNeeded equaling remove guarantees non-null HighestHK
            Timer.Once(15.Seconds(), () => 
            {
                if (BalanceNeeded() == "remove")
                {
                    var fired = HighestHK();
                    announcement = new()
                    {
                        TitleLocstring = "!! WARNING !!",
                        DescriptionLocstring = "You are about to be fired!!",
                    };
                    NetMessages.Send<CCitadelUserMsg_HudGameAnnouncement>(announcement, RecipientFilter.Single(fired.Controller.Slot));
                }
            });
            Timer.Once(20.Seconds(), () =>
            {
                if (BalanceNeeded() == "remove")
                {
                    var fired = HighestHK();
                    CCitadelUserMsg_HudGameAnnouncement firedannouncement = new()
                    {
                        TitleLocstring = "!! FIRED !!",
                        DescriptionLocstring = $"The Hidden King says: 'Dissapointing, {fired.Controller.PlayerName}.",
                    };
                    CCitadelUserMsg_HudGameAnnouncement safeannouncement = new()
                    {
                        TitleLocstring = "!! SAFE !!",
                        DescriptionLocstring = $"{fired.Controller.PlayerName} has been fired.",
                    };
                    foreach (CCitadelPlayerController controller in Players.GetAllControllers())
                    {
                        if (fired.Controller == controller) NetMessages.Send<CCitadelUserMsg_HudGameAnnouncement>(firedannouncement, RecipientFilter.Single(controller.Slot));
                        if (fired.Controller != controller && fired.TeamNum == 2) NetMessages.Send<CCitadelUserMsg_HudGameAnnouncement>(safeannouncement, RecipientFilter.Single(controller.Slot));
                    }
                    fired.AddModifier("citadel_change_team");
                    ResetHP(fired.Controller);
                }
                else
                {
                    CCitadelUserMsg_HudGameAnnouncement announcement = new()
                    {
                        TitleLocstring = "Layoffs Cancelled",
                        DescriptionLocstring = "The Hidden King changes his mind.",
                    };
                    foreach (CCitadelPlayerController controller in Players.GetAllControllers())
                    {

                        var slot = controller.Slot;
                        if (controller.TeamNum == 2)
                            NetMessages.Send<CCitadelUserMsg_HudGameAnnouncement>(announcement, RecipientFilter.Single(slot));
                    }
                }
            });

        }


        public void ResetHP(CCitadelPlayerController controller)
        {
            AccountDict[controller.PlayerSteamId].Data.HP = BalanceDict[AccountDict[controller.PlayerSteamId].Hero].HP;
        }

        public CCitadelPlayerPawn? HighestHK()
        {
            CCitadelPlayerPawn? HighestRunner = null;
            foreach (CCitadelPlayerPawn pawn in Players.GetAllPawns())
            {
                if (pawn.TeamNum == 2)
                    if (HighestRunner == null || pawn.Position.Z > HighestRunner.Position.Z)
                        HighestRunner = pawn;
            }
            return HighestRunner;
        }


        public void TickCurrency(CCitadelPlayerPawn pawn)
        {
            if (pawn.TeamNum == 2) ModifySouls(pawn, 3);
            var controller = pawn.Controller;
            if (controller != null)
            {
                ModifySouls(pawn, Math.Min((double)AccountDict[controller.PlayerSteamId].Data.RunnerStreak / 6400, 3));
                ModifySouls(pawn, Math.Min((double)AccountDict[controller.PlayerSteamId].Data.MovementStreak / 800, 2));
            }
        }
        public void TickStreak(CCitadelPlayerPawn pawn)
        {
            var controller = pawn.Controller;
            if (controller != null) {
                if (pawn.TeamNum == 3) AccountDict[controller.PlayerSteamId].Data.RunnerStreak = 0;

                if (pawn.TeamNum == 2) 
                {
                    AccountDict[controller.PlayerSteamId].Data.RunnerStreak += NearbyPlayerCount(pawn);
                        };


                var xyvel = XYVelocity(pawn);
                if (xyvel >= 400)
                {
                    AccountDict[controller.PlayerSteamId].Data.MovementStreak = (int)Math.Min(AccountDict[controller.PlayerSteamId].Data.MovementStreak + Math.Round(xyvel / 300), 2000);
                }
                if (xyvel < 400)
                {
                    AccountDict[controller.PlayerSteamId].Data.MovementStreak = (int)Math.Max(AccountDict[controller.PlayerSteamId].Data.MovementStreak - 1, 0);
                    if (AccountDict[controller.PlayerSteamId].Data.MovementStreak > 800)
                    {
                        AccountDict[controller.PlayerSteamId].Data.MovementStreak = (int)Math.Max(AccountDict[controller.PlayerSteamId].Data.MovementStreak - 3, 0);
                    }
                } 
            }

        }
        public double XYVelocity(CCitadelPlayerPawn pawn)
        {
            var Velocity = pawn.AbsVelocity;
            var XYVelocitySq = (Velocity.X * Velocity.X) + (Velocity.Y * Velocity.Y);
            var XYVelocity = Math.Sqrt(XYVelocitySq);
            return XYVelocity;
        }
        public int NearbyPlayerCount(CCitadelPlayerPawn pawn)
        {
            var i = 0;
            foreach (CCitadelPlayerPawn otherpawn in Players.GetAllPawns())
            { 
                if (otherpawn != pawn)
                {
                    if (Math.Abs((pawn.Position-otherpawn.Position).Length()) < 1000)
                            i++;
                }
            }
            return i;
        }




        public void ModifySouls(CCitadelPlayerPawn pawn, double amt, bool setting = false)
        {
            var adjustedamt = amt * BalanceDict[(Heroes)pawn.HeroID].Mult;
            var controller = pawn.Controller;
            if (controller != null)
            {
                AccountDict[controller.PlayerSteamId].Souls += adjustedamt;
                if (setting) AccountDict[controller.PlayerSteamId].Souls = amt;

                pawn.SetCurrency(ECurrencyType.EGold, (int)Math.Floor(AccountDict[controller.PlayerSteamId].Souls));
            }
        }
        public void ModifyAP(CCitadelPlayerPawn pawn, int amt)
        {
            pawn.ModifyCurrency(ECurrencyType.EAbilityPoints, amt, ECurrencySource.ECheats);
            var controller = pawn.Controller;
            if (controller != null)
            {
                AccountDict[controller.PlayerSteamId].AbilityPoints = pawn.GetCurrency(ECurrencyType.EAbilityPoints);
            }
        }
        public void ModifyHP(CCitadelPlayerPawn attackerPawn, CCitadelPlayerPawn victimPawn, int amt)
        {
            var adjustedamt = amt;
            var victimController = victimPawn.Controller;
            if (victimController != null)
            {
                AccountDict[victimController.PlayerSteamId].Data.HP += adjustedamt;
            }
        }




        public void StartEvent(string EventName)
        {
            if (EventName == "Random" || EventName == "random")
            {

            }
        }




        public void TryPitTp(CCitadelPlayerPawn pawn)
        {
            var pos = pawn.Position;
            if ((pos.X * pos.X) + ((pos.Y - 7950) * (pos.Y - 7950)) < 250000 && pos.Z < 650)
            {
                var spawn = GetClosestSpawn(new Vector3(pos.X * 20, (pos.Y - 7950) * 20, pos.Z));
                pawn.Teleport(
                    position: spawn,
                    velocity: new System.Numerics.Vector3(0, 0, 0)
                );
                var controller = pawn.Controller;
                if (controller != null)
                {
                    AccountDict[controller.PlayerSteamId].Data.Teleportable = false;
                }
            }
        }

        private Vector3 GetClosestSpawn(Vector3 pos)
        {
            KeyValuePair<Vector3, float> ClosestSpawn = new(new(0, 0, 0), (pos.X * pos.X + pos.Y * pos.Y));
            foreach (Vector3 spawnpoint in Spawns)
            {
                float distSq = (spawnpoint.X - pos.X) * (spawnpoint.X - pos.X) + (spawnpoint.Y - pos.Y) * (spawnpoint.Y - pos.Y);
                if (distSq < ClosestSpawn.Value && distSq > 1000000)
                {
                    ClosestSpawn = new(spawnpoint, distSq);
                }
            }
            return ClosestSpawn.Key;
        }

        /*
        [Command("givewin")]
        public HookResult GiveWin(CCitadelPlayerController controller)
        {
            WinPlayer(controller.GetHeroPawn());
            return HookResult.Handled;
        }
        */
        public void WinPlayer(CCitadelPlayerPawn pawn)
        {
            ulong id = 
                pawn.Controller != null
                ? pawn.Controller.PlayerSteamId
                : 0;
            if (id!=0)
            {
                AccountDict[id].ArenaWins += 1;
                //ModifyAP(pawn, 1);
                ModifySouls(pawn, 0, true);
                WriteAccount(id);
                try 
                { 
                    CCitadelUserMsg_HudGameAnnouncement servermsg = new()
                    {
                        TitleLocstring = "",
                        DescriptionLocstring = $"{pawn.Controller.PlayerName} has achieved a win!",
                    };
                    CCitadelUserMsg_HudGameAnnouncement winmsg = new()
                    {
                        TitleLocstring = "WIN!!!",
                        DescriptionLocstring = $"You have reached 100k souls!",
                    };
                    foreach (CCitadelPlayerController controller in Players.GetAllControllers())
                    {
                        var msg = servermsg;
                        if (controller == pawn.Controller)
                            msg = winmsg;
                        NetMessages.Send<CCitadelUserMsg_HudGameAnnouncement>(msg, RecipientFilter.Single(controller.Slot));        
                    }

                    IntoLobby(pawn.Controller);
                }
                catch { Console.WriteLine("!err51679034"); }
            }
        }




        public void TryAddAccount(ulong id)
        {
            if (!AccountDict.ContainsKey(id))
            {
                AccountDict.Add(id, new());
            }
        }
        public void TryRemoveAccount(ulong account)
        {
            if (AccountDict.ContainsKey(account))
            {
                AccountDict.Remove(account);
            }
        }

        public void ReadAccount(ulong account)
        {
            if (Directory.GetFiles("Accounts").Contains($"Accounts\\{account}.json"))
            {
                var json = File.ReadAllText($"Accounts\\{account}.json");
                var deserialized = JsonSerializer.Deserialize<SteamAccount>(json);
                if (deserialized != null)
                {
                    AccountDict[account] = deserialized;
                    AccountDict[account].Data = new();
                }
            }
        }
        public void WriteAccount(ulong id)
        {
            string json = JsonSerializer.Serialize(AccountDict[id]);
            File.WriteAllText($"Accounts\\{id}.json", json);
        }
        public void WriteAllAccounts()
        {

            foreach (CCitadelPlayerController controller in Players.GetAllControllers())
            {
                var SteamID = controller.PlayerSteamId;
                WriteAccount(SteamID);
            }
        }

        public void ApplyAccount(CCitadelPlayerPawn pawn)
        {
            //TODO
            var controller = pawn.Controller;
            if (controller != null)
            {
                var SteamID = controller.PlayerSteamId;
                if (AccountDict.ContainsKey(SteamID))
                {
                    pawn.SetCurrency(ECurrencyType.EAbilityPoints, AccountDict[SteamID].AbilityPoints);
                    pawn.SetCurrency(ECurrencyType.EGold, (int)Math.Round(AccountDict[SteamID].Souls));
                }
            }
        }



        public void InitializeText()
        {
            var BaseTextEkv = new CEntityKeyValues();
            BaseTextEkv.SetInt("enabled", 1);
            BaseTextEkv.SetInt("fullbright", 1);
            BaseTextEkv.SetInt("reorient_mode", 0);
            BaseTextEkv.SetColor("color", 255, 255, 255, 255);
            BaseTextEkv.SetInt("font_size", 150);
            BaseTextEkv.SetString("font_name", "Ebrima");

            var HelloText = CBaseEntity.CreateByDesignerName("point_worldtext");
            var HelloTextMsg = "Welcome to the Movement Arena!\nHidden King is giving out souls to only those with the best movement.\nKill a member of the Hidden King's army with melee attacks to take their spot\nand get paid for being a movement god!\nChoose a character by pressing [esc] and walk into this message to get started!\n(More Info behind)";
            var HelloTextEkv = new CEntityKeyValues();
            HelloTextEkv.SetInt("enabled", 1);
            HelloTextEkv.SetInt("fullbright", 1);
            HelloTextEkv.SetInt("reorient_mode", 0);
            HelloTextEkv.SetColor("color", 255, 255, 255, 255);
            HelloTextEkv.SetInt("justify_horizontal", 1);
            HelloTextEkv.SetInt("font_size", 150);
            HelloTextEkv.SetString("font_name", "Ebrima");
            HelloTextEkv.SetString("message", HelloTextMsg);
            HelloTextEkv.SetFloat("world_units_per_pixel", 0.3f);
            HelloTextEkv.SetColor("color", 212, 135, 12);
            HelloText?.Spawn(HelloTextEkv);
            HelloText?.Teleport(new Vector3(0, -700, 1630), new Vector3(0, 180, 90));

            var CommandText = CBaseEntity.CreateByDesignerName("point_worldtext");
            var CommandTextMsg = "!!Commands!!\nEach command 'x' can be activated in console with dw_x, or typed in chat with /x or !x (must use console while here in skybox lobby)\nArena : Places you on archmother and teleports you to start.\nLobby : Removes you from the arena and places you in the skybox lobby (allows hero switching)\nFOV #: Sets your FOV to # (thanks amin_a for this plugin)";
            var CommandTextEkv = new CEntityKeyValues();
            CommandTextEkv.SetInt("enabled", 1);
            CommandTextEkv.SetInt("fullbright", 1);
            CommandTextEkv.SetInt("reorient_mode", 0);
            CommandTextEkv.SetColor("color", 255, 255, 255, 255);
            CommandTextEkv.SetInt("justify_horizontal", 1);
            CommandTextEkv.SetInt("font_size", 150);
            CommandTextEkv.SetString("font_name", "Ebrima");
            CommandTextEkv.SetString("message", CommandTextMsg);
            CommandTextEkv.SetFloat("world_units_per_pixel", 0.15f);
            CommandTextEkv.SetColor("color", 255, 255, 255, 255);
            CommandText?.Spawn(CommandTextEkv);
            CommandText?.Teleport(new Vector3(0, 700, 1900), new Vector3(0, 0, 90));
            

            var InfoText = CBaseEntity.CreateByDesignerName("point_worldtext");
            var InfoTextMsg = "!!Info!!\nSome characters have an ability unlocked, try them out!\nHidden King players have their HP to the left of their name in brackets. Light melees deal 1 damage and Heavy Melees deal 2!\nHidden King players have a number to the right of their name in parenthesis, this is their runner streak!\nThe higher the number, the more souls they gain and the more souls you get for killing them!\nAll players have their horizontal velocity displayed in green.";
            var InfoTextEkv = new CEntityKeyValues();
            InfoTextEkv.SetInt("enabled", 1);
            InfoTextEkv.SetInt("fullbright", 1);
            InfoTextEkv.SetInt("reorient_mode", 0);
            InfoTextEkv.SetColor("color", 255, 255, 255, 255);
            InfoTextEkv.SetInt("font_size", 150);
            InfoTextEkv.SetInt("justify_horizontal", 1);
            InfoTextEkv.SetString("font_name", "Ebrima");
            InfoTextEkv.SetString("message", InfoTextMsg);
            InfoTextEkv.SetFloat("world_units_per_pixel", 0.15f);
            InfoTextEkv.SetColor("color", 255, 255, 255, 255);
            InfoText?.Spawn(InfoTextEkv);
            InfoText?.Teleport(new Vector3(0, 700, 1700), new Vector3(0, 0, 90));

            var InfoText2 = CBaseEntity.CreateByDesignerName("point_worldtext");
            var InfoText2Msg = "The number next to velocity is your movement streak! Keep your speed high to keep this number up, and gain souls based on it!\nAll Heroes have a soul multiplier based on how good their movement is.\nSome heroes have a different starting HP.\nParry is enabled.";
            var InfoText2Ekv = new CEntityKeyValues();
            InfoText2Ekv.SetInt("enabled", 1);
            InfoText2Ekv.SetInt("fullbright", 1);
            InfoText2Ekv.SetInt("reorient_mode", 0);
            InfoText2Ekv.SetColor("color", 255, 255, 255, 255);
            InfoText2Ekv.SetInt("font_size", 150);
            InfoText2Ekv.SetInt("justify_horizontal", 1);
            InfoText2Ekv.SetString("font_name", "Ebrima");
            InfoText2Ekv.SetString("message", InfoText2Msg);
            InfoText2Ekv.SetFloat("world_units_per_pixel", 0.15f);
            InfoText2Ekv.SetColor("color", 255, 255, 255, 255);
            InfoText2?.Spawn(InfoText2Ekv);
            InfoText2?.Teleport(new Vector3(0, 700, 1600), new Vector3(0, 0, 90));
            
            //IN future make tip random on server start
            var TipsText = CBaseEntity.CreateByDesignerName("point_worldtext");
            var TipsTextMsg = "!!Tip!!\nTry to parry when you are moving\n";
            var TipsTextEkv = new CEntityKeyValues();
            TipsTextEkv.SetInt("enabled", 1);
            TipsTextEkv.SetInt("fullbright", 1);
            TipsTextEkv.SetInt("reorient_mode", 0);
            TipsTextEkv.SetColor("color", 255, 255, 255, 255);
            TipsTextEkv.SetInt("justify_horizontal", 1);
            TipsTextEkv.SetInt("font_size", 150);
            TipsTextEkv.SetString("font_name", "Ebrima");
            TipsTextEkv.SetString("message", TipsTextMsg);
            TipsTextEkv.SetFloat("world_units_per_pixel", 0.15f);
            TipsTextEkv.SetColor("color", 255, 255, 255, 255);
            TipsText?.Spawn(TipsTextEkv);
            TipsText?.Teleport(new Vector3(700, -200, 1630), new Vector3(0, 270, 90));

            //IN future make tip random on server start
            var LeaderboardText = CBaseEntity.CreateByDesignerName("point_worldtext");
            var LeaderboardTextMsg = MadeLeaderboard;
            var LeaderboardTextEkv = new CEntityKeyValues();
            LeaderboardTextEkv.SetInt("enabled", 1);
            LeaderboardTextEkv.SetInt("fullbright", 1);
            LeaderboardTextEkv.SetInt("reorient_mode", 0);
            LeaderboardTextEkv.SetColor("color", 255, 255, 255, 255);
            LeaderboardTextEkv.SetInt("justify_horizontal", 1);
            LeaderboardTextEkv.SetInt("font_size", 150);
            LeaderboardTextEkv.SetString("font_name", "Ebrima");
            LeaderboardTextEkv.SetString("message", LeaderboardTextMsg);
            LeaderboardTextEkv.SetFloat("world_units_per_pixel", 0.15f);
            LeaderboardTextEkv.SetColor("color", 255, 255, 255, 255);
            LeaderboardText?.Spawn(LeaderboardTextEkv);
            LeaderboardText?.Teleport(new Vector3(700, 200, 1630), new Vector3(0, 270, 90));


            var AboutText = CBaseEntity.CreateByDesignerName("point_worldtext");
            var AboutTextMsg = "!!About this server!!\nHi! My name is L, and I'm the creator of this server!\nThis mode is currently in development, so feedback is very welcome!\nCome join our discord to give feedback or discuss (discord.gg/WAaGcQsVAd)\nThis server was made using deadworks, check out their website! (deadworks.net)\nThis server is running in NA East, hopefully more server locations to come!";
            var AboutTextEkv = new CEntityKeyValues();
            AboutTextEkv.SetInt("enabled", 1);
            AboutTextEkv.SetInt("fullbright", 1);
            AboutTextEkv.SetInt("reorient_mode", 0);
            AboutTextEkv.SetColor("color", 255, 255, 255, 255);
            AboutTextEkv.SetInt("justify_horizontal", 1);
            AboutTextEkv.SetInt("font_size", 150);
            AboutTextEkv.SetString("font_name", "Ebrima");
            AboutTextEkv.SetString("message", AboutTextMsg);
            AboutTextEkv.SetFloat("world_units_per_pixel", 0.15f);
            AboutTextEkv.SetColor("color", 255, 255, 255, 255);
            AboutText?.Spawn(AboutTextEkv);
            AboutText?.Teleport(new Vector3(-700, -200, 1630), new Vector3(0, 90, 90));

            var TpText = CBaseEntity.CreateByDesignerName("point_worldtext");
            var TpTextMsg = "! Archmother TP !\nJump in the pit to get teleported!\nYour landing position determines where you will teleport!";
            var TpTextEkv = new CEntityKeyValues();
            TpTextEkv.SetInt("enabled", 1);
            TpTextEkv.SetInt("fullbright", 1);
            TpTextEkv.SetInt("reorient_mode", 0);
            TpTextEkv.SetColor("color", 255, 255, 255, 255);
            TpTextEkv.SetInt("font_size", 150);
            TpTextEkv.SetInt("justify_horizontal", 1);
            TpTextEkv.SetString("font_name", "Ebrima");
            TpTextEkv.SetString("message", TpTextMsg);
            TpTextEkv.SetFloat("world_units_per_pixel", 0.43f);
            TpTextEkv.SetColor("color", 255, 255, 255, 255);
            TpText?.Spawn(TpTextEkv);
            TpText?.Teleport(new Vector3(0, 8000, 1800), new Vector3(0, 180, 90));
        }

        public string MakeLeaderboard()
        {
            List<KeyValuePair<string, int>> Leaderboard = new();
            var first = true;

            foreach (var account in Directory.GetFiles("Accounts"))
            {
                var json = File.ReadAllText(account);
                var deserialized = JsonSerializer.Deserialize<SteamAccount>(json);
                var inleaderboard = false;
                var i = 0;
                if (first)
                {
                    Leaderboard.Add(new(deserialized.Username, deserialized.ArenaWins));
                    first = false;
                    inleaderboard = true;
                }

                else foreach (KeyValuePair<string,int> pair in Leaderboard)
                {

                    if (deserialized != null && deserialized.ArenaWins >= pair.Value)
                    {
                        Leaderboard.Insert(i, new KeyValuePair<string,int>(deserialized.Username, deserialized.ArenaWins));

                        inleaderboard = true;
                        
                        break;
                    }
                    i++;
                }
                if (!inleaderboard)
                {
                    Leaderboard.Add(new KeyValuePair<string, int>(deserialized.Username, deserialized.ArenaWins));
                
                }
            }


            string LeaderboardString = "!!Leaderboard!!\n";
            for (var i = 1; i <= 10 && i <= Leaderboard.Count; i++)
            {
                LeaderboardString = $"{LeaderboardString}{i}. {Leaderboard[i - 1].Key} ({Leaderboard[i - 1].Value})\n";
            }

            if (LeaderboardString != "")
                return LeaderboardString;
            return "ERROR";
        }

        public void KeepInLobby(CCitadelPlayerPawn pawn)
        {
            if (pawn.Position.Z < 1400) LobbyTP(pawn);
        }
        public void LobbyTP(CCitadelPlayerPawn pawn)
        {
            var randomx = new Random().Next(-500, 500);
            var randomy = new Random().Next(-500, 500);
            pawn.Teleport
            (
                position: new System.Numerics.Vector3(randomx, randomy, 1536),
                angles: new System.Numerics.Vector3(0, -90, 0),
                velocity: new System.Numerics.Vector3(0, 0, 0)
            );
        }

        [Command("lobby")]
        public void IntoLobby(CCitadelPlayerController controller)
        {

            controller.ChangeTeam(4);
            controller.SelectHero(AccountDict[controller.PlayerSteamId].Hero);
            AccountDict[controller.PlayerSteamId].InGame = false;
            WriteAccount(controller.PlayerSteamId);

            var pawn = controller.GetHeroPawn();
            Timer.Once(0.3.Seconds(), () => {
                if (pawn != null)
                {
                    LobbyTP(pawn);
                }
            });
        }


        [Command("arena")]
        public void IntoArena(CCitadelPlayerController controller, bool respawn = false)
        {

            controller.ChangeTeam(3);
            controller.SelectHero(AccountDict[controller.PlayerSteamId].Hero);
            AccountDict[controller.PlayerSteamId].InGame = true;
            var pawn = controller.GetHeroPawn();
            if (pawn!=null)
                ArchmotherTP(pawn);

            AccountDict[controller.PlayerSteamId].Data.Taggable = true;
            ResetHP(controller);
            if (!respawn)
            Timer.Once(1.Seconds(), () => {
                if (safe)
                    StripAbilities(controller);
                
            });
        }

        [Command("1db")]
        private void debuggin(CCitadelPlayerController c)
        {

            Console.WriteLine(c);
            foreach (var entity in Entities.All)
            {
                if (entity.DesignerName.Contains("teleport"))
                {
                    Console.WriteLine($"Found potential teleporter: {entity.DesignerName} (Class: {entity.Classname})");
                }
            }
        }


        private Dictionary<string, AbilityBalance> AllowedAbilities = new()
        {
            {"citadel_ability_mantle", new()},
            {"citadel_ability_jump", new()},
            {"citadel_ability_slide", new()},
            {"citadel_ability_climb_rope", new()},
            {"citadel_ability_dash", new()},
            {"citadel_ability_sprint", new()},
            {"citadel_ability_melee_parry", new()},
            {"ability_doorman_doorway", new()},
            {"ability_doorway_close", new()},
            {"ability_bounce_pad", new(){Tier=0b00001}},
            {"citadel_weapon_shiv_alt", new()},
            {"ability_viper_slide", new(){Tier=0b00001}},
            {"ability_viper_snakedash", new(){Tier=0b00001}},
            {"citadel_ability_lash_down_strike", new(){Tier=0b00001}},
            //{"ability_priest_knockback", new(){Tier=0b00001}},
            {"ability_priest_beartrap",new(){Tier=0b00001}},
            {"citadel_ability_void_sphere", new(){ Tier=0b00001}},
            //{"drifter_shadow_mark", new(){OnChaser=false}}, 
            //{"drifter_shadow_mark_teleport", new(){OnChaser=false}},
            {"viscous_restorative_goo", new(){}},
            //{"viscous_telepunch",new(){Tier=0b00001}}, MELEE
            {"ability_power_jump",new(){Tier=0b00001}},
            //{"ability_fencer_riposte",new(){Tier=0b00001}},
            {"ability_smoke_bomb",new(){Tier=0b00001}},
            {"ability_nano_dash",new(){}},
            {"citadel_ability_chrono_kinetic_carbine",new(){Tier=0b00001}},
            {"synth_barrage",new(){}},
            {"ability_werewolf_kickflip",new(){}},
            {"ability_warden_high_alert",new(){Tier=0b00011}},
            {"ability_frank_selfzap",new(){Tier=0b00001}},
            {"citadel_ability_hornet_leap", new(){Tier=0b00001}},
            {"citadel_ability_power_slash",new()},
        };
        
        public void StripAbilities(CCitadelPlayerController controller, int i = 0)
        {
 
            if (Players.GetAllControllers().Contains(controller) && safe && i < 5)
            {
                var pawn = controller.GetHeroPawn();
                if (pawn != null)
                {

                    try
                    {
                        foreach (var ability in pawn.AbilityComponent.Abilities)
                        {
                            ability.UpgradeBits = 0b11111;
                            if (!(ability.AbilitySlot == EAbilitySlot.WeaponMelee || ability.AbilitySlot == EAbilitySlot.WeaponPrimary || AllowedAbilities.ContainsKey(ability.AbilityName)))
                                pawn.RemoveAbility(ability.AbilityName);
                            if (AllowedAbilities.ContainsKey(ability.AbilityName))
                            {
                                ability.UpgradeBits = AllowedAbilities[ability.AbilityName].Tier;
                            }
                        }
                    }
                    catch
                    {
                        Timer.Once(1.Seconds(), () =>
                        {
                            i++;
                            StripAbilities(controller, i);
                        });
                    }

                }
            }

        }

        public bool InTextTp(CCitadelPlayerPawn pawn)
        {
            
            var Position = pawn.Position;
            if (-700 < Position.X && Position.X < 700)
                if (-800 < Position.Y && Position.Y < -700)
                    if (Position.Z > -1500)
                        return true;
            return false;
        }


        public bool InMidPit(CCitadelPlayerPawn pawn)
        {
            
            var Position = pawn.Position;
            if (-400 < Position.X && Position.X < 400)
                if (-700 < Position.Y && Position.Y < 700)
                    if (Position.Z < -700)
                        return true;
            return false;
        }




        private void TpAll()
        {
            Console.WriteLine("Tping");
            foreach (CCitadelPlayerPawn pawn in Players.GetAllPawns())
            {
                if (pawn.TeamNum == 3) ArchmotherTP(pawn);
                if (pawn.TeamNum == 2) HiddenkingTP(pawn);
            }
        }
        public void ArchmotherTP(CCitadelPlayerPawn pawn)
        {
            var randomx = new Random().Next(-350, 350);
            pawn.Teleport(
                    position: new System.Numerics.Vector3(randomx, 9610, 1963),
                    angles: new System.Numerics.Vector3(0, 270, 0),
                    velocity: new System.Numerics.Vector3(0, 0, 0)
                );
            var controller = pawn.Controller;
            if (controller != null)
            {
                AccountDict[controller.PlayerSteamId].Data.Teleportable = true;
                Timer.Once(20.Seconds(), () => { AccountDict[controller.PlayerSteamId].Data.Teleportable = false; });
            }
        }


        public void HiddenkingTP(CCitadelPlayerPawn pawn)
        {
            var randomx = new Random().Next(-400, 400);
            pawn.Teleport(
                    position: new System.Numerics.Vector3(randomx, 0, 681),
                    angles: new System.Numerics.Vector3(0, 90, 0),
                    velocity: new System.Numerics.Vector3(0, 0, 0)
                );
        }

        private void EnsureConVars()
        {
            ConVar.Find("citadel_voice_all_talk")?.SetInt(1); ///or .SetFloat();
            ConVar.Find("citadel_player_spawn_time_max_respawn_time")?.SetInt(1);
            ConVar.Find("citadel_npc_spawn_enabled")?.SetInt(0);
            ConVar.Find("citadel_trooper_spawn_enabled")?.SetInt(0);
            ConVar.Find("citadel_player_starting_gold")?.SetInt(0);
            ConVar.Find("citadel_active_lane")?.SetInt(255);
            ConVar.Find("citadel_allow_duplicate_heroes")?.SetInt(1);
            ///ConVar.Find("convar")?.SetInt(#); 

        }


        private void StartRestartSequence()
        {
            var intervalMin = Config.IntervalMinutes;
            Console.WriteLine($"[AutoRestart] Scheduling restart in {intervalMin} minutes");

            var notifications = new List<(int SecondsRemaining, string Message)>();

            if (intervalMin >= 11)
                notifications.Add((600, "Map restart in 10 minutes"));
            if (intervalMin >= 6)
                notifications.Add((300, "Map restart in 5 minutes"));
            if (intervalMin >= 2)
                notifications.Add((60, "Map restart in 1 minute"));

            for (int i = 10; i >= 1; i--)
            {
                notifications.Add((i, $"Map restart in {i} second{(i == 1 ? "" : "s")}"));
            }
            notifications.Sort((a, b) => b.SecondsRemaining.CompareTo(a.SecondsRemaining));

            var totalSeconds = intervalMin * 60;
            var notifIndex = 0;
            var elapsedSeconds = 0;

            _restartSequence = Timer.Sequence(step =>
            {
                if (notifIndex < notifications.Count)
                {
                    var (secondsRemaining, message) = notifications[notifIndex];
                    var targetElapsed = totalSeconds - secondsRemaining;

                    if (elapsedSeconds >= targetElapsed)
                    {
                        Chat.PrintToChatAll(message);
                        Console.WriteLine($"[AutoRestart] {message}");
                        notifIndex++;

                        if (notifIndex < notifications.Count)
                        {
                            var nextSecondsRemaining = notifications[notifIndex].SecondsRemaining;
                            var waitSeconds = secondsRemaining - nextSecondsRemaining;
                            elapsedSeconds += waitSeconds;
                            return step.Wait(waitSeconds.Seconds());
                        }
                        safe = false;
                        DoRestart();
                        return step.Done();
                    }

                    var waitUntilNext = targetElapsed - elapsedSeconds;
                    elapsedSeconds = targetElapsed;
                    return step.Wait(waitUntilNext.Seconds());
                }
                safe = false;
                DoRestart();
                return step.Done();
            }).CancelOnMapChange();
        }

        private void DoRestart()
        {
            safe = false;
            var map = Server.MapName;
            Console.WriteLine($"[AutoRestart] Restarting — changelevel {map}");
            Server.ExecuteCommand($"changelevel {map}");
        }


        /*
        private Heroes PickRandomHeroNotInDevelopment()
        {
            var availableHeroes = Enum.GetValues<Heroes>()
                .Where(h =>
                {
                    var data = h.GetHeroData();
                    return data != null && !data.InDevelopment;
                })
                .ToArray();

            if (availableHeroes.Length == 0)
                throw new InvalidOperationException("No heroes available");

            return availableHeroes[Random.Shared.Next(availableHeroes.Length)];
        }
        */


        private void ErrorCode(string code)
        {
            string suffixedcode = "err!" + code;
            Console.WriteLine(suffixedcode);
        }
        public void Test(string msg = "Hi")
        {
            Console.WriteLine(msg);
        }

    }
}
