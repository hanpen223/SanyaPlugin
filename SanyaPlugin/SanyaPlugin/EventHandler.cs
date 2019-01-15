﻿using System;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Net.Sockets;
using Smod2;
using Smod2.API;
using Smod2.Events;
using Smod2.EventHandlers;
using Smod2.EventSystem.Events;
using Newtonsoft.Json;

namespace SanyaPlugin
{
    class Playerinfo
    {
        public Playerinfo() { }

        public string name { get; set; }

        public string steamid { get; set; }

        public string role { get; set; }
    }

    class Serverinfo
    {
        public Serverinfo()
        {
            players = new List<Playerinfo>();
        }
        public string time { get; set; }

        public string gameversion { get; set; }

        public string smodversion { get; set; }

        public string sanyaversion { get; set; }

        public string name { get; set; }

        public string ip { get; set; }

        public int port { get; set; }

        public int playing { get; set; }

        public int maxplayer { get; set; }

        public int duration { get; set; }

        public List<Playerinfo> players { get; private set; }
    }

    class EventHandler :
        IEventHandlerWaitingForPlayers,
        IEventHandlerRoundStart,
        IEventHandlerRoundEnd,
        IEventHandlerRoundRestart,
        IEventHandlerPlayerJoin,
        IEventHandlerLCZDecontaminate,
        IEventHandlerWarheadStartCountdown,
        IEventHandlerWarheadStopCountdown,
        IEventHandlerWarheadDetonate,
        IEventHandlerSetNTFUnitName,
        IEventHandlerCheckEscape,
        IEventHandlerSetRole,
        IEventHandlerPlayerHurt,
        IEventHandlerPlayerDie,
        IEventHandlerPocketDimensionDie,
        IEventHandlerInfected,
        IEventHandlerRecallZombie,
        IEventHandlerDoorAccess,
        IEventHandlerRadioSwitch,
        IEventHandlerElevatorUse,
        IEventHandlerSCP914Activate,
        IEventHandlerCallCommand,
        IEventHandlerUpdate
    {
        private Plugin plugin;
        private Thread infosender;
        private bool running = false;
        private bool sender_live = true;
        UdpClient udpclient = new UdpClient();

        //-----------------config------------------
        private bool config_loaded = false;
        private string sanya_info_sender_to = "hatsunemiku24.ddo.jp";
        private int sanya_spectator_slot = 0;
        private bool sanya_title_timer = true;
        private bool sanya_cassie_subtitle = false;
        private bool sanya_friendly_warn = false;
        private bool sanya_scp914_changing = false;
        private int sanya_classd_startitem_percent = 20;
        private int sanya_classd_startitem_ok_itemid = 0;
        private int sanya_classd_startitem_no_itemid = -1;
        private bool sanya_door_lockable = false;
        private int sanya_door_lockable_second = 10;
        private int sanya_door_lockable_interval = 60;
        private int sanya_fallen_limit = 10;
        private float sanya_usp_damage_multiplier_human = 2.0f;
        private float sanya_usp_damage_multiplier_scp = 5.0f;
        private bool sanya_handcuffed_cantopen = true;
        private bool sanya_radio_enhance = false;
        private bool sanya_intercom_information = true;
        private bool sanya_escape_spawn = true;
        private bool sanya_pocket_cleanup = false;
        private bool sanya_infect_by_scp049_2 = true;
        private int sanya_infect_limit_time = 4;
        private bool sanya_traitor_enabled = false;
        private int sanya_traitor_limitter = 4;
        private int sanya_traitor_chance_percent = 50;
        private Dictionary<string, string> sanya_scp_actrecovery_amounts;

        //-----------------var---------------------
        //GlobalStatus
        private bool roundduring = false;

        //EscapeCheck
        private bool isEscaper = false;
        private Vector escape_pos;
        private int escape_player_id;

        //DoorLocker
        private int doorlock_interval = 0;

        //AutoNuker
        //private bool nuke_lock = false;

        //Update
        private int updatecounter = 0;
        private Vector traitor_pos = new Vector(170, 984, 28);

        //Spectator
        private List<Player> playingList = new List<Player>();
        private List<Player> spectatorList = new List<Player>();

        //-----------------------Event---------------------
        public EventHandler(Plugin plugin)
        {
            this.plugin = plugin;
            infosender = new Thread(new ThreadStart(Sender));
            infosender.Start();
            running = true;
        }

        private void Sender()
        {
            while (running)
            {
                if (config_loaded)
                {
                    try
                    {
                        string ip = sanya_info_sender_to;
                        int port = 37813;

                        if (ip == "none")
                        {
                            plugin.Info("InfoSender to Disabled(config:" + plugin.GetConfigString("sanya_info_sender_to") + ")");
                            running = false;
                            break;
                        }
                        Serverinfo cinfo = new Serverinfo();
                        Server server = this.plugin.Server;

                        DateTime dt = DateTime.Now;
                        cinfo.time = dt.ToString("yyyy-MM-ddTHH:mm:sszzzz");
                        cinfo.gameversion = CustomNetworkManager.CompatibleVersions[0];
                        cinfo.smodversion = PluginManager.GetSmodVersion() + "-" + PluginManager.GetSmodBuild();
                        cinfo.sanyaversion = this.plugin.Details.version;
                        string test = CustomNetworkManager.CompatibleVersions[0];
                        cinfo.name = ConfigManager.Manager.Config.GetStringValue("server_name", plugin.Server.Name);
                        cinfo.ip = server.IpAddress;
                        cinfo.port = server.Port;
                        cinfo.playing = server.NumPlayers - 1;
                        cinfo.maxplayer = server.MaxPlayers;
                        cinfo.duration = server.Round.Duration;

                        cinfo.name = cinfo.name.Replace("$number", (cinfo.port - 7776).ToString());

                        if (cinfo.playing > 0)
                        {
                            foreach (Player player in server.GetPlayers())
                            {
                                Playerinfo ply = new Playerinfo();

                                ply.name = player.Name;
                                ply.steamid = player.SteamId;
                                ply.role = player.TeamRole.Role.ToString();

                                cinfo.players.Add(ply);
                            }
                        }
                        string json = JsonConvert.SerializeObject(cinfo);

                        byte[] sendBytes = Encoding.UTF8.GetBytes(json);

                        udpclient.Send(sendBytes, sendBytes.Length, ip, port);

                        plugin.Debug("[Infosender] " + ip + ":" + port);

                        Thread.Sleep(15000);
                    }
                    catch (Exception e)
                    {
                        plugin.Debug(e.ToString());
                        sender_live = false;
                    }
                }
            }
        }

        public void OnWaitingForPlayers(WaitingForPlayersEvent ev)
        {
            sanya_info_sender_to = plugin.GetConfigString("sanya_info_sender_to");
            sanya_spectator_slot = plugin.GetConfigInt("sanya_spectator_slot");
            sanya_title_timer = plugin.GetConfigBool("sanya_title_timer");
            sanya_friendly_warn = plugin.GetConfigBool("sanya_friendly_warn");
            sanya_scp914_changing = plugin.GetConfigBool("sanya_scp914_changing");
            sanya_cassie_subtitle = plugin.GetConfigBool("sanya_cassie_subtitle");
            sanya_classd_startitem_percent = plugin.GetConfigInt("sanya_classd_startitem_percent");
            sanya_classd_startitem_ok_itemid = plugin.GetConfigInt("sanya_classd_startitem_ok_itemid");
            sanya_classd_startitem_no_itemid = plugin.GetConfigInt("sanya_classd_startitem_no_itemid");
            sanya_door_lockable = plugin.GetConfigBool("sanya_door_lockable");
            sanya_door_lockable_second = plugin.GetConfigInt("sanya_door_lockable_second");
            sanya_door_lockable_interval = plugin.GetConfigInt("sanya_door_lockable_interval");
            sanya_fallen_limit = plugin.GetConfigInt("sanya_fallen_limit");
            sanya_usp_damage_multiplier_human = plugin.GetConfigFloat("sanya_usp_damage_multiplier_human");
            sanya_usp_damage_multiplier_scp = plugin.GetConfigFloat("sanya_usp_damage_multiplier_scp");
            sanya_handcuffed_cantopen = plugin.GetConfigBool("sanya_handcuffed_cantopen");
            sanya_radio_enhance = plugin.GetConfigBool("sanya_radio_enhance");
            sanya_intercom_information = plugin.GetConfigBool("sanya_intercom_information");
            sanya_escape_spawn = plugin.GetConfigBool("sanya_escape_spawn");
            sanya_pocket_cleanup = plugin.GetConfigBool("sanya_pocket_cleanup");
            sanya_infect_by_scp049_2 = plugin.GetConfigBool("sanya_infect_by_scp049_2");
            sanya_infect_limit_time = plugin.GetConfigInt("sanya_infect_limit_time");
            sanya_traitor_enabled = plugin.GetConfigBool("sanya_traitor_enabled");
            sanya_traitor_limitter = plugin.GetConfigInt("sanya_traitor_limitter");
            sanya_traitor_chance_percent = plugin.GetConfigInt("sanya_traitor_chance_percent");
            sanya_scp_actrecovery_amounts = plugin.GetConfigDict("sanya_scp_actrecovery_amounts");

            plugin.Info("[ConfigLoader]Config Loaded");
            config_loaded = true;
        }

        public void OnRoundStart(RoundStartEvent ev)
        {
            updatecounter = 0;
            roundduring = true;

            plugin.Info("RoundStart!");

            //foreach(UnityEngine.GameObject item in UnityEngine.GameObject.FindGameObjectsWithTag("RoomID"))
            //{
            //    plugin.Info(item.GetComponent<Rid>().id);
            //}

            //defaultsize=45
            //plugin.Server.Map.Broadcast(10, "<size=9><color=#ff0000>オブジェクトの収容違反が確認されました。\n職員は直ちに脱出を開始してください。</color></size>", false);

            if (sanya_classd_startitem_percent > 0)
            {
                int success_count = 0;
                Random rnd = new Random();
                foreach (Vector spawnpos in plugin.Server.Map.GetSpawnPoints(Role.CLASSD))
                {
                    int ritem = rnd.Next(0, 100);

                    if (ritem >= 0 && ritem <= sanya_classd_startitem_percent)
                    {
                        success_count++;
                        ritem = sanya_classd_startitem_ok_itemid;
                    }
                    else
                    {
                        ritem = sanya_classd_startitem_no_itemid;
                    }

                    if (ritem >= 0)
                    {
                        plugin.Server.Map.SpawnItem((ItemType)ritem, spawnpos, new Vector(0, 0, 0));
                    }
                }
                plugin.Info("Class-D-Contaiment Item Droped! (Success:" + success_count + ")");
            }
        }

        public void OnRoundEnd(RoundEndEvent ev)
        {
            if (roundduring)
            {
                plugin.Info("Round Ended [" + ev.Status + "]");
                plugin.Info("Class-D:" + ev.Round.Stats.ClassDAlive + " Scientist:" + ev.Round.Stats.ScientistsAlive + " NTF:" + ev.Round.Stats.NTFAlive + " SCP:" + ev.Round.Stats.SCPAlive + " CI:" + ev.Round.Stats.CiAlive);
            }
            roundduring = false;
        }

        public void OnRoundRestart(RoundRestartEvent ev)
        {
            roundduring = false;
        }

        public void OnPlayerJoin(PlayerJoinEvent ev)
        {
            plugin.Info("[PlayerJoin] " + ev.Player.Name + "(" + ev.Player.SteamId + ")[" + ev.Player.IpAddress + "]");

            if (sanya_spectator_slot > 0)
            {
                if (playingList.Count >= plugin.Server.MaxPlayers - sanya_spectator_slot)
                {
                    plugin.Info("[Spectator]Join to Spectator : " + ev.Player.Name + "(" + ev.Player.SteamId + ")");
                    ev.Player.OverwatchMode = true;
                    ev.Player.SetRank("nickel", "SPECTATOR", "");
                    spectatorList.Add(ev.Player);
                }
                else
                {
                    plugin.Info("[Spectator]Join to Player : " + ev.Player.Name + "(" + ev.Player.SteamId + ")");
                    ev.Player.OverwatchMode = false;
                    playingList.Add(ev.Player);
                }
            }
        }

        public void OnDecontaminate()
        {
            plugin.Info("LCZ Decontaminated");

            if (sanya_cassie_subtitle)
            {
                plugin.Server.Map.ClearBroadcasts();
                plugin.Server.Map.Broadcast(13, "<size=25>《下層がロックされ、「再収容プロトコル」の準備が出来ました。全ての有機物は破壊されます。》\n </size><size=15>《Light Containment Zone is locked down and ready for decontamination. The removal of organic substances has now begun.》\n</size>", false);
            }
        }

        public void OnDetonate()
        {
            plugin.Info("AlphaWarhead Denotated");
        }

        public void OnStartCountdown(WarheadStartEvent ev)
        {
            if (sanya_cassie_subtitle)
            {
                plugin.Server.Map.ClearBroadcasts();
                if (!ev.IsResumed)
                {
                    plugin.Server.Map.Broadcast(15, "<color=#ff0000><size=23>《「AlphaWarhead」の緊急起爆シーケンスが開始されました。施設の地下区画は、約90秒後に爆破されます。》\n</size><size=15>《Alpha Warhead emergency detonation sequence engaged.The underground section of the facility will be detonated in t-minus 90 seconds.》\n</size></color>", false);
                }
                else
                {
                    double left = ev.TimeLeft;
                    double count = Math.Truncate(left / 10.0) * 10.0;
                    plugin.Server.Map.ClearBroadcasts();
                    plugin.Server.Map.Broadcast(10, "<color=#ff0000><size=25>《緊急起爆シーケンスが再開されました。約" + count.ToString() + "秒後に爆破されます。》\n</size><size=15>《Detonation sequence resumed. t-minus " + count.ToString() + " seconds.》\n</size></color>", false);
                }
            }
        }

        public void OnStopCountdown(WarheadStopEvent ev)
        {
            if (sanya_cassie_subtitle)
            {
                plugin.Server.Map.ClearBroadcasts();
                plugin.Server.Map.Broadcast(7, "<color=#ff0000><size=25>《起爆が取り消されました。システムを再起動します。》\n</size><size=15>《Detonation cancelled. Restarting systems.》\n</size></color>", false);
            }
        }

        public void OnSetNTFUnitName(SetNTFUnitNameEvent ev)
        {
            if (roundduring)
            {
                if (sanya_cassie_subtitle)
                {
                    plugin.Server.Map.ClearBroadcasts();
                    if (plugin.Server.Round.Stats.SCPAlive > 0)
                    {
                        plugin.Server.Map.Broadcast(30, "<color=#6c80ff><size=25>《機動部隊イプシロン-11「" + ev.Unit + "」が施設に到着しました。\n残りの全職員は、機動部隊が貴方の場所へ到着するまで「標準避難プロトコル」の続行を推奨します。\n「" + plugin.Server.Round.Stats.SCPAlive.ToString() + "」オブジェクトが再収容されていません。》\n</size><size=15>《Mobile Task Force Unit, Epsilon-11, designated, '" + ev.Unit + "', has entered the facility.\nAll remaining personnel are advised to proceed with standard evacuation protocols until an MTF squad reaches your destination.\nAwaiting recontainment of: " + plugin.Server.Round.Stats.SCPAlive.ToString() + " SCP subject.》\n</size></color>", false);
                    }
                    else
                    {
                        plugin.Server.Map.Broadcast(30, "<color=#6c80ff><size=25>《機動部隊イプシロン-11「" + ev.Unit + "」が施設に到着しました。\n残りの全職員は、機動部隊が貴方の場所へ到着するまで「標準避難プロトコル」の続行を推奨します。\n重大な脅威が施設内に存在します。注意してください。》\n </size><size=15>《Mobile Task Force Unit, Epsilon-11, designated, '" + ev.Unit + "', has entered the facility.\nAll remaining personnel are advised to proceed with standard evacuation protocols, until MTF squad has reached your destination.\nSubstantial threat to safety is within the facility -- Exercise caution.》\n</size></color>", false);
                    }
                }
            }
        }

        public void OnCheckEscape(PlayerCheckEscapeEvent ev)
        {
            plugin.Debug("[OnCheckEscape] " + ev.Player.Name + ":" + ev.Player.TeamRole.Role);

            isEscaper = true;
            escape_player_id = ev.Player.PlayerId;
            escape_pos = ev.Player.GetPosition();

            plugin.Debug("[OnCheckEscape] escaper x:" + escape_pos.x + " y:" + escape_pos.y + " z:" + escape_pos.z);
        }

        public void OnSetRole(PlayerSetRoleEvent ev)
        {
            plugin.Debug("[OnSetRole] " + ev.Player.Name + ":" + ev.Role);

            //---------EscapeChecker---------
            if (isEscaper)
            {
                if (escape_player_id == ev.Player.PlayerId)
                {
                    if (sanya_escape_spawn && ev.Player.TeamRole.Role != Role.CHAOS_INSURGENCY)
                    {
                        plugin.Debug("[OnSetRole] escaper_id:" + escape_player_id + " / spawn_id:" + ev.Player.PlayerId);
                        plugin.Info("[EscapeChecker] Escape Successfully [" + ev.Player.Name + ":" + ev.Player.TeamRole.Role.ToString() + "]");
                        ev.Player.Teleport(escape_pos, false);
                        isEscaper = false;
                    }
                    else
                    {
                        plugin.Info("[EscapeChecker] Disabled (config or CHAOS) [" + ev.Player.Name + ":" + ev.Player.TeamRole.Role.ToString() + "]");
                        isEscaper = false;
                    }
                }
            }
        }

        public void OnPlayerHurt(PlayerHurtEvent ev)
        {
            plugin.Debug("[OnPlayerHurt] " + ev.Attacker.Name + "<" + ev.Attacker.TeamRole.Team.ToString() + ">(" + ev.DamageType + ":" + ev.Damage + ") -> " + ev.Player.Name + "<" + ev.Player.TeamRole.Team.ToString() + ">");

            if (sanya_friendly_warn)
            {
                if (ev.Attacker != null && 
                    ev.Attacker.TeamRole.Team == ev.Player.TeamRole.Team &&
                    ev.Attacker.PlayerId != ev.Player.PlayerId)
                {
                    plugin.Info("[FriendlyFire] " + ev.Attacker.Name + "(" + ev.DamageType.ToString() + ":" + ev.Damage + ") -> " + ev.Player.Name);
                    ev.Attacker.PersonalClearBroadcasts();
                    ev.Attacker.PersonalBroadcast(5, "<color=#ff0000><size=30>《誤射に注意。味方へのダメージは容認されません。(<" + ev.Player.Name + ">への攻撃。)》\n</size><size=20>《Check your fire! Damage to ally forces not be tolerated.(Damaged to <" + ev.Player.Name +">)》\n</size></color>", false);
                }
            }

            if(ev.DamageType == DamageType.USP)
            {
                if(ev.Player.TeamRole.Team == Smod2.API.Team.SCP)
                {
                    ev.Damage *= sanya_usp_damage_multiplier_scp;
                }else
                {
                    ev.Damage *= sanya_usp_damage_multiplier_human;
                }              
            }

            if(ev.DamageType == DamageType.FALLDOWN)
            {
                if(ev.Damage <= sanya_fallen_limit)
                {
                    ev.Damage = 0;
                }
            }
        }

        public void OnPlayerDie(PlayerDeathEvent ev)
        {
            plugin.Debug("[OnPlayerDie] " + ev.Killer + ":" + ev.DamageTypeVar + " -> " + ev.Player.Name);

            //----------------------------------------------------キル回復------------------------------------------------  
            //############## SCP-173 ###############
            if (ev.DamageTypeVar == DamageType.SCP_173 && 
                ev.Killer.TeamRole.Role == Role.SCP_173 && 
                int.Parse(sanya_scp_actrecovery_amounts["SCP_173"]) > 0)
            {
                if(ev.Killer.GetHealth() + int.Parse(sanya_scp_actrecovery_amounts["SCP_173"]) < ev.Killer.TeamRole.MaxHP)
                {
                    ev.Killer.AddHealth(int.Parse(sanya_scp_actrecovery_amounts["SCP_173"]));
                }
                else
                {
                    ev.Killer.SetHealth(ev.Killer.TeamRole.MaxHP);
                }
            }

            //############## SCP-096 ###############
            if (ev.DamageTypeVar == DamageType.SCP_096 &&
                ev.Killer.TeamRole.Role == Role.SCP_096 &&
                int.Parse(sanya_scp_actrecovery_amounts["SCP_096"]) > 0)
            {
                if (ev.Killer.GetHealth() + int.Parse(sanya_scp_actrecovery_amounts["SCP_096"]) < ev.Killer.TeamRole.MaxHP)
                {
                    ev.Killer.AddHealth(int.Parse(sanya_scp_actrecovery_amounts["SCP_096"]));
                }
                else
                {
                    ev.Killer.SetHealth(ev.Killer.TeamRole.MaxHP);
                }
            }

            //############## SCP-939 ###############
            if (ev.DamageTypeVar == DamageType.SCP_939 &&
                (ev.Killer.TeamRole.Role == Role.SCP_939_53) || (ev.Killer.TeamRole.Role == Role.SCP_939_89) &&
                int.Parse(sanya_scp_actrecovery_amounts["SCP_939"]) > 0)
            {
                if (ev.Killer.GetHealth() + int.Parse(sanya_scp_actrecovery_amounts["SCP_939"]) < ev.Killer.TeamRole.MaxHP)
                {
                    ev.Killer.AddHealth(int.Parse(sanya_scp_actrecovery_amounts["SCP_939"]));
                }
                else
                {
                    ev.Killer.SetHealth(ev.Killer.TeamRole.MaxHP);
                }
            }

            //############## SCP-049-2 ###############
            if (ev.DamageTypeVar == DamageType.SCP_049_2 &&
                ev.Killer.TeamRole.Role == Role.SCP_049_2 &&
                int.Parse(sanya_scp_actrecovery_amounts["SCP_049_2"]) > 0)
            {
                if (ev.Killer.GetHealth() + int.Parse(sanya_scp_actrecovery_amounts["SCP_049_2"]) < ev.Killer.TeamRole.MaxHP)
                {
                    ev.Killer.AddHealth(int.Parse(sanya_scp_actrecovery_amounts["SCP_049_2"]));
                }
                else
                {
                    ev.Killer.SetHealth(ev.Killer.TeamRole.MaxHP);
                }
            }
            //----------------------------------------------------キル回復------------------------------------------------

            //----------------------------------------------------ペスト治療------------------------------------------------
            if (ev.DamageTypeVar == DamageType.SCP_049_2 && sanya_infect_by_scp049_2)
            {
                plugin.Info("[Infector] 049-2 Infected!(Limit:" + sanya_infect_limit_time + "s) [" + ev.Killer.Name + "->" + ev.Player.Name + "]");
                ev.DamageTypeVar = DamageType.SCP_049;
                ev.Player.Infect(sanya_infect_limit_time);
            }
            //----------------------------------------------------ペスト治療------------------------------------------------

            //----------------------------------------------------PocketCleaner---------------------------------------------
            if (ev.DamageTypeVar == DamageType.POCKET && sanya_pocket_cleanup)
            {
                plugin.Info("[PocketCleaner] Cleaned (" + ev.Player.Name + ")");
                ev.Player.Teleport(new Vector(0, 0, 0), false);
                ev.SpawnRagdoll = false;
            }
            //----------------------------------------------------PocketCleaner---------------------------------------------
        }

        public void OnPlayerInfected(PlayerInfectedEvent ev)
        {
            plugin.Info("[Infector] 049 Infected!(Limit:" + sanya_infect_limit_time + "s) [" + ev.Attacker.Name + "->" + ev.Player.Name + "]");
            ev.InfectTime = sanya_infect_limit_time;
        }

        public void OnPocketDimensionDie(PlayerPocketDimensionDieEvent ev)
        {
            plugin.Debug("[OnPocketDie] " + ev.Player.Name);

            //------------------------------ディメンション死亡回復(SCP-106)-----------------------------------------
            if (int.Parse(sanya_scp_actrecovery_amounts["SCP_106"]) > 0)
            {
                try
                {
                    foreach (Player ply in plugin.Server.GetPlayers())
                    {
                        if (ply.TeamRole.Role == Role.SCP_106)
                        {
                            if (ply.GetHealth() + int.Parse(sanya_scp_actrecovery_amounts["SCP_106"]) < ply.TeamRole.MaxHP)
                            {
                                ply.AddHealth(int.Parse(sanya_scp_actrecovery_amounts["SCP_106"]));
                            }
                            else
                            {
                                ply.SetHealth(ply.TeamRole.MaxHP);
                            }
                        }
                    }
                }
                catch (Exception)
                {

                }
            }
        }

        public void OnRecallZombie(PlayerRecallZombieEvent ev)
        {
            plugin.Debug("[OnRecallZombie] " + ev.Player.Name + " -> " + ev.Target.Name);
            //----------------------治療回復SCP-049---------------------------------
            if (ev.Player.TeamRole.Role == Role.SCP_049 &&
                int.Parse(sanya_scp_actrecovery_amounts["SCP_049"]) > 0)
            {
                if (ev.Player.GetHealth() + int.Parse(sanya_scp_actrecovery_amounts["SCP_049"]) < ev.Player.TeamRole.MaxHP)
                {
                    ev.Player.AddHealth(int.Parse(sanya_scp_actrecovery_amounts["SCP_049"]));
                }
                else
                {
                    ev.Player.SetHealth(ev.Player.TeamRole.MaxHP);
                }
            }
        }

        public void OnDoorAccess(PlayerDoorAccessEvent ev)
        {
            plugin.Debug("[OnDoorAccess] " + ev.Door.Name + "(" + ev.Door.Open + "):" + ev.Door.Permission + "=" + ev.Allow);

            if (sanya_door_lockable)
            {
                if (doorlock_interval >= sanya_door_lockable_interval)
                {
                    if (ev.Player.TeamRole.Role == Role.NTF_COMMANDER)
                    {
                        if (ev.Player.GetCurrentItemIndex() > -1)
                        {
                            if (ev.Player.GetCurrentItem().ItemType == ItemType.DISARMER)
                            {
                                if (!ev.Door.Name.Contains("CHECKPOINT") && !ev.Door.Name.Contains("GATE_"))
                                {
                                    if (ev.Door.Locked == false)
                                    {
                                        plugin.Info("[DoorLock] " + ev.Player.Name + " lock " + ev.Door.Name);
                                        doorlock_interval = 0;
                                        ev.Door.Locked = true;
                                        System.Timers.Timer t = new System.Timers.Timer
                                        {
                                            Interval = sanya_door_lockable_second * 1000,
                                            AutoReset = false,
                                            Enabled = true
                                        };
                                        t.Elapsed += delegate
                                        {
                                            ev.Allow = true;
                                            ev.Door.Locked = false;
                                            t.Enabled = false;
                                        };

                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (sanya_intercom_information)
            {
                if (ev.Door.Name == "INTERCOM")
                {
                    ev.Allow = true;
                }
            }

            if (sanya_handcuffed_cantopen)
            {
                if (ev.Player.IsHandcuffed())
                {
                    ev.Allow = false;
                }
            }
        }

        public void OnPlayerRadioSwitch(PlayerRadioSwitchEvent ev)
        {
            plugin.Debug("[OnPlayerRadioSwitch] " + ev.Player.Name + ":" + ev.ChangeTo);

            if (sanya_radio_enhance)
            {
                if (ev.ChangeTo == RadioStatus.ULTRA_RANGE)
                {
                    if (null == plugin.Server.Map.GetIntercomSpeaker())
                    {
                        plugin.Server.Map.SetIntercomSpeaker(ev.Player);
                    }
                }
                else
                {
                    if (plugin.Server.Map.GetIntercomSpeaker() != null)
                    {
                        if (ev.Player.Name == plugin.Server.Map.GetIntercomSpeaker().Name)
                        {
                            plugin.Server.Map.SetIntercomSpeaker(null);
                        }
                    }
                }
            }

        }

        public void OnElevatorUse(PlayerElevatorUseEvent ev)
        {
            plugin.Debug("[OnElevatorUse] " + ev.Elevator.ElevatorType + "[" + ev.Elevator.ElevatorStatus + "] => " + ev.Elevator.MovingSpeed);

            if (sanya_handcuffed_cantopen)
            {
                if (ev.Player.IsHandcuffed())
                {
                    ev.AllowUse = false;
                }
            }
        }

        public void OnSCP914Activate(SCP914ActivateEvent ev)
        {
            bool isAfterChange = false;
            Vector newpos = new Vector(ev.OutputPos.x, ev.OutputPos.y + 1.0f, ev.OutputPos.z);

            plugin.Debug("[OnSCP914Activate] " + ev.User.Name);

            if (sanya_scp914_changing)
            {

                foreach (UnityEngine.Collider item in ev.Inputs)
                {
                    Pickup comp1 = item.GetComponent<Pickup>();
                    if (comp1 == null)
                    {
                        NicknameSync comp2 = item.GetComponent<NicknameSync>();
                        CharacterClassManager comp3 = item.GetComponent<CharacterClassManager>();
                        PlyMovementSync comp4 = item.GetComponent<PlyMovementSync>();
                        PlayerStats comp5 = item.GetComponent<PlayerStats>();

                        if (comp2 != null && comp3 != null && comp4 != null && comp5 != null && item.gameObject != null)
                        {
                            UnityEngine.GameObject gameObject = item.gameObject;
                            ServerMod2.API.SmodPlayer player = new ServerMod2.API.SmodPlayer(gameObject);

                            if (ev.KnobSetting == KnobSetting.COARSE)
                            {
                                plugin.Info("[SCP914] COARSE:" + player.Name);

                                foreach (Smod2.API.Item hasitem in player.GetInventory())
                                {
                                    hasitem.Remove();
                                }
                                player.SetAmmo(AmmoType.DROPPED_5, 0);
                                player.SetAmmo(AmmoType.DROPPED_7, 0);
                                player.SetAmmo(AmmoType.DROPPED_9, 0);

                                player.ThrowGrenade(ItemType.FRAG_GRENADE, true, new Vector(0.25f, 1, 0), true, newpos, true, 1.0f);
                                player.ThrowGrenade(ItemType.FRAG_GRENADE, true, new Vector(0, 1, 0), true, newpos, true, 1.0f);
                                player.ThrowGrenade(ItemType.FRAG_GRENADE, true, new Vector(-0.25f, 1, 0), true, newpos, true, 1.0f);

                                player.Kill(DamageType.RAGDOLLLESS);
                            }
                            else if (ev.KnobSetting == KnobSetting.ONE_TO_ONE && !isAfterChange)
                            {
                                plugin.Info("[SCP914] 1to1:" + player.Name);

                                Random rnd = new Random();
                                List<Player> speclist = new List<Player>();
                                foreach (Player spec in plugin.Server.GetPlayers())
                                {
                                    if (spec.TeamRole.Role == Role.SPECTATOR)
                                    {
                                        speclist.Add(spec);
                                    }
                                }

                                plugin.Info(speclist.Count.ToString());

                                if (speclist.Count > 0)
                                {
                                    int targetspec = rnd.Next(0, speclist.Count - 1);

                                    speclist[targetspec].ChangeRole(player.TeamRole.Role, true, false, true, true);
                                    speclist[targetspec].Teleport(newpos, false);
                                    player.ChangeRole(Role.SPECTATOR, true, false, false, false);
                                    plugin.Info("[SCP914] 1to1_Changed_to:" + speclist[targetspec].Name);
                                }
                                isAfterChange = true;
                            }
                            else if (ev.KnobSetting == KnobSetting.FINE)
                            {
                                plugin.Info("[SCP914] FINE:" + player.Name);

                                Random rnd = new Random();

                                foreach (Smod2.API.Item hasitem in player.GetInventory())
                                {
                                    hasitem.Remove();
                                }
                                player.SetAmmo(AmmoType.DROPPED_5, 0);
                                player.SetAmmo(AmmoType.DROPPED_7, 0);
                                player.SetAmmo(AmmoType.DROPPED_9, 0);

                                player.ThrowGrenade(ItemType.FLASHBANG, true, new Vector(0.25f, 1, 0), true, newpos, true, 1.0f);
                                player.ThrowGrenade(ItemType.FLASHBANG, true, new Vector(0, 1, 0), true, newpos, true, 1.0f);
                                player.ThrowGrenade(ItemType.FLASHBANG, true, new Vector(-0.25f, 1, 0), true, newpos, true, 1.0f);
                                player.Kill(DamageType.RAGDOLLLESS);

                                plugin.Server.Map.SpawnItem((ItemType)rnd.Next(0, 30), newpos, new Vector(0, 0, 0));
                            }
                            else if (ev.KnobSetting == KnobSetting.VERY_FINE)
                            {
                                plugin.Info("[SCP914] VERY_FINE:" + player.Name);

                                player.ThrowGrenade(ItemType.FLASHBANG, true, new Vector(0.25f, 1, 0), true, newpos, true, 1.0f);
                                player.ThrowGrenade(ItemType.FLASHBANG, true, new Vector(0, 1, 0), true, newpos, true, 1.0f);
                                player.ThrowGrenade(ItemType.FLASHBANG, true, new Vector(-0.25f, 1, 0), true, newpos, true, 1.0f);
                                player.ChangeRole(Role.SCP_049_2, true, false, true, false);

                                System.Timers.Timer t = new System.Timers.Timer
                                {
                                    Interval = 500,
                                    AutoReset = true,
                                    Enabled = true
                                };
                                t.Elapsed += delegate
                                {
                                    player.SetHealth(player.GetHealth() - 10, DamageType.DECONT);
                                    if (player.GetHealth() - 10 <= 0)
                                    {
                                        player.SetHealth(player.GetHealth() - 10, DamageType.DECONT);
                                        t.AutoReset = false;
                                        t.Enabled = false;
                                    }
                                };
                            }

                        }
                    }
                }
                isAfterChange = true;
            }
        }

        public void OnCallCommand(PlayerCallCommandEvent ev)
        {
            plugin.Debug("[OnCallCommand] [Before] Called:" + ev.Player.Name + " Command:" + ev.Command + " Return:" + ev.ReturnMessage);

            if(ev.ReturnMessage == "Command not found.")
            {
                if (ev.Command.StartsWith("kill"))
                {
                    plugin.Info("[SelfKiller] " + ev.Player.Name);
                    ev.ReturnMessage = "Success.";
                    ev.Player.SetGodmode(false);
                    ev.Player.Kill(DamageType.DECONT);
                }
            }

            plugin.Info("[OnCallCommand] Called:" + ev.Player.Name + " Command:" + ev.Command + " Return:" + ev.ReturnMessage);
        }

        public void OnUpdate(UpdateEvent ev)
        {
            updatecounter += 1;

            /*
            if (updatecounter % 60 == 0 && roundduring)
            {
                //45 = decontainment attention
                if(plugin.Server.Round.Duration == 45)
                {
                    plugin.Server.Map.Broadcast(20, "<size=25>《全ての職員へ。「再収容プロトコル」が下層にて約15分後に実行されます。\n下層にいる全ての生物学的物質は、さらなる収容違反を防ぐため破壊されます。》</size>\n<size=15>《Attention, all personnel, the Light Containment Zone decontamination process will occur in t-minus 15 minutes. All biological substances must be removed in order to avoid destruction.》</size>", false);
                }

                //240 = decontainment 10minutes
                if (plugin.Server.Round.Duration == 240)
                {
                    plugin.Server.Map.Broadcast(10, "<size=25>《警告。「再収容プロトコル」が下層にて約10分後に実行されます。》</size>\n<size=15>《Danger, Light Containment Zone overall decontamination in t-minus 10 minutes.》</size>", false);
                }

                //440 = decontainment 5minutes
                if (plugin.Server.Round.Duration == 440)
                {
                    plugin.Server.Map.Broadcast(10, "<size=25>《警告。「再収容プロトコル」が下層にて約5分後に実行されます。》</size>\n<size=15>《Danger, Light Containment Zone overall decontamination in t-minus 5 minutes.》</size>", false);
                }
                    
                //638 = decontainment 1minutes
                if (plugin.Server.Round.Duration == 638)
                {
                    plugin.Server.Map.Broadcast(10, "<size=25>《警告。「再収容プロトコル」が下層にて約1分後に実行されます。》</size>\n<size=15>《Danger, Light Containment Zone overall decontamination in t-minus 1 minutes.》</size>", false);
                }

                //668 = decontainment countdown
                if (plugin.Server.Round.Duration == 668)
                {
                    plugin.Server.Map.Broadcast(10, "<size=25>《警告。「再収容プロトコル」が下層にて約30秒後に実行されます。全てのチェックポイントは開放されます。すぐに避難してください。》</size>\n<size=15>《Danger, Light Containment Zone overall decontamination in t-minus 30 seconds. All checkpoint doors have been permanently opened. Please evacuate immediately.》</size>", false);
                }
            }
            */

            if (updatecounter % 60 == 0 && config_loaded)
            {
                updatecounter = 0;

                if (sanya_spectator_slot > 0)
                {
                    plugin.Debug("[SpectatorCheck] playing:" + playingList.Count + " spectator:" + spectatorList.Count);

                    List<Player> players = plugin.Server.GetPlayers();

                    foreach (Player ply in playingList.ToArray())
                    {
                        if (players.FindIndex(item => item.SteamId == ply.SteamId) == -1)
                        {
                            plugin.Debug("[SpectatorCheck] delete player:" + ply.SteamId);
                            playingList.Remove(ply);
                        }
                    }

                    foreach (Player ply in spectatorList.ToArray())
                    {
                        if (players.FindIndex(item => item.SteamId == ply.SteamId) == -1)
                        {
                            plugin.Debug("[SpectatorCheck] delete spectator:" + ply.SteamId);
                            spectatorList.Remove(ply);
                        }
                    }

                    if (playingList.Count < plugin.Server.MaxPlayers - sanya_spectator_slot)
                    {
                        if (spectatorList.Count > 0)
                        {
                            plugin.Info("[SpectatorCheck] Spectator to Player:" + spectatorList[0].Name + "(" + spectatorList[0].SteamId + ")");
                            spectatorList[0].OverwatchMode = false;
                            spectatorList[0].SetRank();
                            playingList.Add(spectatorList[0]);
                            spectatorList.RemoveAt(0);
                        }
                    }

                }

                if (sanya_door_lockable)
                {
                    if (doorlock_interval < sanya_door_lockable_interval)
                    {
                        doorlock_interval++;
                    }
                }

                if (sanya_intercom_information)
                {
                    plugin.pluginManager.Server.Map.SetIntercomContent(IntercomStatus.Ready, "READY\nSCP LEFT:" + plugin.pluginManager.Server.Round.Stats.SCPAlive + "\nCLASS-D LEFT:" + plugin.pluginManager.Server.Round.Stats.ClassDAlive + "\nSCIENTIST LEFT:" + plugin.pluginManager.Server.Round.Stats.ScientistsAlive);
                }

                if (sanya_title_timer)
                {
                    string title = ConfigManager.Manager.Config.GetStringValue("player_list_title", "Unnamed Server") + " RoundTime: " + plugin.pluginManager.Server.Round.Duration / 60 + ":" + plugin.pluginManager.Server.Round.Duration % 60;
                    plugin.pluginManager.Server.PlayerListTitle = title;
                }                   

                if (sanya_traitor_enabled)
                {
                    try
                    {
                        foreach (Player ply in plugin.pluginManager.Server.GetPlayers())
                        {
                            if ((ply.TeamRole.Role == Role.NTF_CADET ||
                                ply.TeamRole.Role == Role.NTF_LIEUTENANT ||
                                ply.TeamRole.Role == Role.NTF_SCIENTIST ||
                                ply.TeamRole.Role == Role.NTF_COMMANDER ||
                                ply.TeamRole.Role == Role.FACILITY_GUARD ||
                                ply.TeamRole.Role == Role.CHAOS_INSURGENCY) && ply.IsHandcuffed())
                            {
                                Vector pos = ply.GetPosition();
                                int ntfcount = plugin.pluginManager.Server.Round.Stats.NTFAlive;
                                int cicount = plugin.pluginManager.Server.Round.Stats.CiAlive;

                                plugin.Debug("[TraitorCheck] NTF:" + ntfcount + " CI:" + cicount + " LIMIT:" + sanya_traitor_limitter);
                                plugin.Debug("[TraitorCheck] " + ply.Name + "(" + ply.TeamRole.Role.ToString() + ") x:" + pos.x + " y:" + pos.y + " z:" + pos.z + " cuff:" + ply.IsHandcuffed());

                                if ((pos.x >= 172 && pos.x <= 182) &&
                                    (pos.y >= 980 && pos.y <= 990) &&
                                    (pos.z >= 25 && pos.z <= 34))
                                {
                                    if ((ply.TeamRole.Role == Role.CHAOS_INSURGENCY && cicount <= sanya_traitor_limitter) ||
                                        (ply.TeamRole.Role != Role.CHAOS_INSURGENCY && ntfcount <= sanya_traitor_limitter))
                                    {
                                        Random rnd = new Random();
                                        int rndresult = rnd.Next(0, 100);
                                        plugin.Info("[TraitorCheck] Traitoring... [" + ply.Name + ":" + ply.TeamRole.Role + ":" + rndresult + "<=" + sanya_traitor_chance_percent + "]");
                                        if (rndresult <= sanya_traitor_chance_percent)
                                        {
                                            if (ply.TeamRole.Role == Role.CHAOS_INSURGENCY)
                                            {
                                                ply.ChangeRole(Role.NTF_CADET, true, false, true, true);
                                                ply.Teleport(traitor_pos, true);
                                            }
                                            else
                                            {
                                                ply.ChangeRole(Role.CHAOS_INSURGENCY, true, false, true, true);
                                                ply.Teleport(traitor_pos, true);
                                            }
                                            plugin.Info("[TraitorCheck] Success [" + ply.Name + ":" + ply.TeamRole.Role + ":" + rndresult + "<=" + sanya_traitor_chance_percent + "]");
                                        }
                                        else
                                        {
                                            ply.Teleport(traitor_pos, true);
                                            ply.Kill(DamageType.TESLA);
                                            plugin.Info("[TraitorCheck] Failed [" + ply.Name + ":" + ply.TeamRole.Role + ":" + rndresult + ">=" + sanya_traitor_chance_percent + "]");
                                        }
                                    };
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        plugin.Error(e.Message);
                    }
                }
            }

            if (!sender_live)
            {
                plugin.Debug("[InfoSender] Rebooting...");
                infosender.Abort();
                running = false;
                infosender = null;

                infosender = new Thread(new ThreadStart(Sender));
                infosender.Start();
                running = true;
                sender_live = true;
                plugin.Debug("[InfoSender] Rebooting Completed");
            }
        }
    }
}
