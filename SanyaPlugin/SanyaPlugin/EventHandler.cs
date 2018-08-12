﻿using System;
using Smod2;
using Smod2.API;
using Smod2.Attributes;
using Smod2.EventHandlers;
using Smod2.Events;
using Smod2.Config;

namespace SanyaPlugin
{
    class EventHandler : 
        IEventHandlerRoundStart,
        IEventHandlerCheckEscape,
        IEventHandlerSpawn,
        IEventHandlerPocketDimensionDie,
        IEventHandlerInfected
    {
        private Plugin plugin;

        //-----------------var---------------------

        //HurtChanger
        private Vector lastpos;
        private Player targetplayer;

        //EscapeCheck
        private static bool isEscaper = false;
        private static bool isDoubleSpawn = false;
        private static Vector escape_pos;
        private static int escape_player_id;

        //PocketCleaner
        private int temphealth;


        //-----------------------Event---------------------
        public EventHandler(Plugin plugin)
        {
            this.plugin = plugin;
        }

        public void OnRoundStart(RoundStartEvent ev)
        {
            plugin.Debug("RoundStart!");
            plugin.Debug("sanya_escape_spawn :" + plugin.GetConfigBool("sanya_escape_spawn"));
            plugin.Debug("sanya_infect_by_scp049_2 :" + plugin.GetConfigBool("sanya_infect_by_scp049_2"));
            plugin.Debug("sanya_infect_limit_time :" + plugin.GetConfigInt("sanya_infect_limit_time"));
            plugin.Debug("sanya_warhead_dontlock :" + plugin.GetConfigBool("sanya_warhead_dontlock"));
            //plugin.Debug("sanya_warhead_forceopen :" + plugin.GetConfigBool("sanya_warhead_forceopen"));

            plugin.Debug("sanya_scp173_duplicate :" + plugin.GetConfigBool("sanya_scp173_duplicate"));
            plugin.Debug("sanya_scp173_duplicate_hp :" + plugin.GetConfigInt("sanya_scp173_duplicate_hp"));
            plugin.Debug("sanya_scp049_duplicate :" + plugin.GetConfigBool("sanya_scp049_duplicate"));
            plugin.Debug("sanya_scp049_duplicate_hp :" + plugin.GetConfigInt("sanya_scp049_duplicate_hp"));
            plugin.Debug("sanya_scp939_duplicate :" + plugin.GetConfigBool("sanya_scp939_duplicate"));
            plugin.Debug("sanya_scp939_duplicate_hp :" + plugin.GetConfigInt("sanya_scp939_duplicate_hp"));
            plugin.Debug("sanya_scp049_2_duplicate :" + plugin.GetConfigBool("sanya_scp049_2_duplicate"));
            plugin.Debug("sanya_scp049_2_duplicate_hp :" + plugin.GetConfigInt("sanya_scp049_2_duplicate_hp"));
            plugin.Debug("sanya_scp106_duplicate :" + plugin.GetConfigBool("sanya_scp106_duplicate"));
            plugin.Debug("sanya_scp106_duplicate_hp :" + plugin.GetConfigInt("sanya_scp106_duplicate_hp"));

            if (this.plugin.GetConfigBool("sanya_warhead_dontlock"))
            {
                foreach (Door door in ev.Server.Map.GetDoors())
                {
                    door.DontOpenOnWarhead = true;
                }
                plugin.Info("NukeOpenDoors Stopped");
            }
        }

        public void OnCheckEscape(PlayerCheckEscapeEvent ev)
        {
            plugin.Debug("OnCheckEscape " + ev.Player.Name + ":" + ev.Player.TeamRole.Role);

            isEscaper = true;
            isDoubleSpawn = true;
            escape_player_id = ev.Player.PlayerId;
            escape_pos = ev.Player.GetPosition();

            plugin.Debug("escaper x:" + escape_pos.x + " y:" + escape_pos.y + " z:" + escape_pos.z);
        }

        public void OnSpawn(PlayerSpawnEvent ev)
        {
            plugin.Debug("OnSpawn " + ev.Player.Name + ":" + ev.Player.TeamRole.Name + " x:" + ev.SpawnPos.x + " y:" + ev.SpawnPos.y + " z:" + ev.SpawnPos.z);

            if (isEscaper)
            {
                if (isDoubleSpawn)
                {
                    plugin.Debug("isDoubleSpawn : " + isDoubleSpawn);
                    isDoubleSpawn = false;
                }
                else
                {
                    plugin.Debug("isEscaper:" + isEscaper);
                    if (escape_player_id == ev.Player.PlayerId)
                    {
                        if (this.plugin.GetConfigBool("sanya_escape_spawn") && ev.Player.TeamRole.Role != Role.CHAOS_INSUGENCY)
                        {
                            plugin.Debug("escaper_id:" + escape_player_id + " / spawn_id:" + ev.Player.PlayerId);
                            plugin.Info("[EscapeChecker] Escape Successfully [" + ev.Player.Name + ":" + ev.Player.TeamRole.Role.ToString() + "]");
                            ev.SpawnPos = escape_pos;
                            isEscaper = false;
                        }
                        else
                        {
                            plugin.Info("[EscapeChecker] Disabled (config or CHAOS) [" + ev.Player.Name + ":" + ev.Player.TeamRole.Role.ToString() + "]");
                            isEscaper = false;
                            isDoubleSpawn = false;
                        }
                    }
                }

            }


        }

        public void OnPlayerHurt(PlayerHurtEvent ev)
        {
            targetplayer = ev.Attacker;

            //----------------------------------------------------オールドマン複製------------------------------------------------
            if (ev.DamageType == DamageType.SCP_106 && this.plugin.GetConfigBool("sanya_scp106_duplicate"))
            {
                plugin.Info("[Duplicator] 106 Duplicated! (" + targetplayer.Name + " -> " + ev.Player.Name + ")");
                ev.Damage = 0.0f;
                lastpos = ev.Player.GetPosition();
                ev.Player.ChangeRole(Role.SCP_106, true, false);
                ev.Player.SetHealth(this.plugin.GetConfigInt("sanya_scp106_duplicate_hp"));
                ev.Player.Teleport(lastpos);
            }
            //----------------------------------------------------オールドマン複製------------------------------------------------

            if ((ev.Player.GetHealth() - ev.Damage) < 0) //HP0になる被弾時
            {
                //----------------------------------------------------ペスト治療------------------------------------------------
                if (ev.DamageType == DamageType.SCP_049_2 && this.plugin.GetConfigBool("sanya_infect_by_scp049_2"))
                {
                    plugin.Info("[Infector] 049-2 Infected!(Limit:" + this.plugin.GetConfigInt("sanya_infect_limit_time") + "s) [" + targetplayer.Name + "->" + ev.Player.Name + "]");
                    ev.DamageType = DamageType.SCP_049;
                    ev.Player.Infect(this.plugin.GetConfigInt("sanya_infect_limit_time"));
                }
                //----------------------------------------------------ペスト治療------------------------------------------------

                //----------------------------------------------------複製-------------------------------------------------
                if (ev.DamageType == DamageType.SCP_049_2 && this.plugin.GetConfigBool("sanya_scp049_2_duplicate"))
                {
                    plugin.Info("[Duplicator] 049-2 Duplicated! (" + targetplayer.Name + " -> " + ev.Player.Name + ")");
                    ev.Damage = 0.0f;
                    lastpos = ev.Player.GetPosition();
                    ev.Player.ChangeRole(Role.SCP_049_2, true, false);
                    ev.Player.SetHealth(this.plugin.GetConfigInt("sanya_scp049_2_duplicate_hp"));
                    ev.Player.Teleport(lastpos);
                }

                if ((ev.DamageType == DamageType.SCP_939) && this.plugin.GetConfigBool("sanya_scp939_duplicate"))
                {
                    plugin.Info("[Duplicator] 939 Duplicated! (" + targetplayer.Name + " -> " + ev.Player.Name + ")");
                    ev.Damage = 0.0f;
                    lastpos = ev.Player.GetPosition();
                    ev.Player.ChangeRole(Role.SCP_939_89, true, false);
                    ev.Player.SetHealth(this.plugin.GetConfigInt("sanya_scp939_duplicate_hp"));
                    ev.Player.Teleport(lastpos);
                }

                if (ev.DamageType == DamageType.SCP_049 && this.plugin.GetConfigBool("sanya_scp049_duplicate"))
                {
                    plugin.Info("[Duplicator] 049 Duplicated! (" + targetplayer.Name + " -> " + ev.Player.Name + ")");
                    ev.Damage = 0.0f;
                    lastpos = ev.Player.GetPosition();
                    ev.Player.ChangeRole(Role.SCP_049, true, false);
                    ev.Player.SetHealth(this.plugin.GetConfigInt("sanya_scp049_duplicate_hp"));
                    ev.Player.Teleport(lastpos);
                }

                if (ev.DamageType == DamageType.SCP_173 && this.plugin.GetConfigBool("sanya_scp173_duplicate"))
                {
                    plugin.Info("[Duplicator] 173 Duplicated! (" + targetplayer.Name + " -> " + ev.Player.Name + ")");
                    ev.Damage = 0.0f;
                    lastpos = ev.Player.GetPosition();
                    ev.Player.ChangeRole(Role.SCP_173, true, false);
                    ev.Player.SetHealth(this.plugin.GetConfigInt("sanya_scp173_duplicate_hp"));
                    ev.Player.Teleport(lastpos);
                }
                //----------------------------------------------------複製-------------------------------------------------
            }
        }

        public void OnPocketDimensionDie(PlayerPocketDimensionDieEvent ev)
        {
            if (ev.Die && this.plugin.GetConfigBool("sanya_pocket_cleanup"))
            {
                plugin.Info("[PocketCleaner] Cleaning Start... (" + ev.Player.Name + ")");

                temphealth = ev.Player.GetHealth();
                ev.Player.Damage(1, DamageType.POCKET);
                if (temphealth == ev.Player.GetHealth())
                {
                    plugin.Info("[PocketCleaner] Protection (" + ev.Player.Name + ")");
                }
                else
                {
                    plugin.Info("[PocketCleaner] Cleaning Complete (" + ev.Player.Name + ")");
                    ev.Player.Teleport(new Vector(0, 0, 0));
                    ev.Player.Kill(DamageType.POCKET);
                }

            }

        }

        public void OnPlayerInfected(PlayerInfectedEvent ev)
        {
            plugin.Info("[InfectChecker] 049 Infected!(Limit:" + this.plugin.GetConfigInt("sanya_infect_limit_time") + "s) [" + ev.Attacker.Name + "->" + ev.Player.Name + "]");
            ev.InfectTime = this.plugin.GetConfigInt("sanya_infect_limit_time");
        }
    }
}
