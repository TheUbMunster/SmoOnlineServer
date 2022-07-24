#define MEGA_VERBOSE

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using Shared;

namespace Server
{
    /// <summary>
    /// <b>Author: TheUbMunster</b><br></br><br></br>
    /// 
    /// This class recieves changes in in-game user data, and recalculates volumes if dirty.
    /// 
    /// It also recieves walkie-talkie packets and manages volumes (overridden) by walkie-talkie.
    /// </summary>
    class VolumeCalculation
    {
        #region Typedefs
        public enum Team
        {
            None = 0,
            Hiders,
            Seekers
        }

        private class UserVolInfo
        {
            /// <summary>
            /// Since the volume between users is (usually) the same e.g. player1 <-> player2 distance
            /// is the same as the distance player2 <-> player1, this is a reference to the other volume object
            /// </summary>
            public UserVolInfo Pair { get; set; } = null!;
            public ulong Ticker { get; set; }
            /// <summary>
            /// The running calculated volume (if null, has not been calculated yet)
            /// </summary>
            public float? RunningVolume { get; set; } = 0;
            /// <summary>
            /// The volume to send to the user the next time we need to send (if null, nothing to send)
            /// </summary>
            public float? ToSendVolume { get; set; } = null;
            /// <summary>
            /// The volume value that was last sent to the user.
            /// </summary>
            public float? LastSetVolume { get; set; } = null; //if null, this client has not yet had a request to be sent a volume
        }

        private class WalkieTicker
        {
            public PVCWalkieTalkiePacket Packet { get; set; } = null!;
            public long? SystemTickOnLastCalculation { get; set; } = null; //null means it has not been calculated once yet.
        }
        #endregion

        const float soundEpsilon = 0.01f; //what percent volume change results in an update in the client's volumes.
        private object lockKey = new object();

        private Logger volCalcLogger = new Logger("VolCalculation");
        private HashSet<string> igs = new HashSet<string>();
        /// <summary>
        /// ig -> that ig players volumes for each other user, if vol == null, that volume isn't dirty
        /// </summary>
        private Dictionary<string, Dictionary<string, UserVolInfo>> igToIgsToVols = new Dictionary<string, Dictionary<string, UserVolInfo>>();
        private Dictionary<string, Vector3> igToPos = new Dictionary<string, Vector3>();
        private Dictionary<string, string?> igToStage = new Dictionary<string, string?>();
        private Dictionary<string, Team> igToTeam = new Dictionary<string, Team>();

        private Dictionary<string, WalkieTicker> discordSourceToActiveWalkie = new Dictionary<string, WalkieTicker>();

        private Dictionary<string, string> igToDiscord = new Dictionary<string, string>();
        private Dictionary<string, string> discordToIg = new Dictionary<string, string>();

        private bool voiceProxEnabled = false;

        public VolumeCalculation() { }

        #region Data input
        public void SetIGDiscordAssoc(string igPlayer, string discord)
        {
            lock (lockKey)
            {
#if DEBUG
                Console.WriteLine($"IG-Discord assoc set: {igPlayer}, {discord}.");
#endif
                igToDiscord[igPlayer] = discord;
                discordToIg[discord] = igPlayer;
            }
        }

        public bool RemoveIGDiscordAssocIfExists(string discord)
        {
            lock (lockKey)
            {
                if (discordToIg.ContainsKey(discord))
                {
                    string ig = discordToIg[discord];
#if DEBUG
                    Console.WriteLine($"IG-Discord assoc removed: {ig}, {discord}.");
#endif
                    return (igToDiscord.Remove(ig) && discordToIg.Remove(discord));
                }
                return false;
            }
        }

        public void OnRecieveUserData(string igPlayer, Vector3 pos)
        {
            lock (lockKey)
            {
#if DEBUG && MEGA_VERBOSE
                Console.WriteLine($"Recieved positional data of {igPlayer}.");
#endif
                igToPos[igPlayer] = pos;
                if (!igToStage.ContainsKey(igPlayer))
                    igToStage[igPlayer] = null;
                string? stage = igToStage[igPlayer];
                igs.Add(igPlayer);
                if (!igToIgsToVols.ContainsKey(igPlayer))
                    igToIgsToVols[igPlayer] = new Dictionary<string, UserVolInfo>();
                //only update based on distance.
                foreach (string ig in igs)
                {
                    if (ig == igPlayer)
                        continue;
                    //ig <-> igPlayer
                    UserVolInfo volInfo1 = null!, volInfo2 = null!;
                    if (!igToIgsToVols.ContainsKey(ig))
                        igToIgsToVols[ig] = new Dictionary<string, UserVolInfo>();
                    if (!igToIgsToVols[ig].ContainsKey(igPlayer))
                    {
                        volInfo1 = igToIgsToVols[igPlayer][ig] = new UserVolInfo();
                        volInfo2 = igToIgsToVols[ig][igPlayer] = new UserVolInfo();
                        volInfo1.Pair = volInfo2;
                        volInfo2.Pair = volInfo1;
                    }
                    volInfo1 ??= igToIgsToVols[igPlayer][ig];
                    volInfo2 ??= igToIgsToVols[ig][igPlayer];

                    //actual calculation
                    if (!igToStage.ContainsKey(ig))
                        igToStage[ig] = null;
                    string? igStage = igToStage[ig];
                    if (!igToPos.ContainsKey(ig) || !igToPos.ContainsKey(igPlayer))
                        continue;
                    float dist = Vector3.Distance(igToPos[ig], igToPos[igPlayer]);
                    float setVol;
                    if (igStage == null || stage == null || igStage != stage)
                    {
                        //not the same stage (0%)
                        setVol = 0f;
                    }
                    else if (dist > Settings.Instance.Discord.BeginHearingThreshold)
                    {
                        //too quiet because they are far away (0%)
                        setVol = 0f;
                    }
                    else if (dist < Settings.Instance.Discord.FullHearingThreshold)
                    {
                        //full vol because they are very close (100%)
                        setVol = 1f;
                    }
                    else
                    {
                        setVol = 1f - ClampedInvLerp(Settings.Instance.Discord.FullHearingThreshold,
                            Settings.Instance.Discord.BeginHearingThreshold, dist);
                        //semi-linearize from 1/((dist^2)*log(dist))
                        setVol *= setVol; //may sound better without this.
                    }
                    if (volInfo1.LastSetVolume == null || Math.Abs(volInfo1.LastSetVolume.Value - setVol) > soundEpsilon ||
                        (volInfo1.LastSetVolume != 0 && setVol == 0) || (volInfo1.LastSetVolume != 1 && setVol == 1))
                    {
                        volInfo1.RunningVolume = setVol;
                        if (voiceProxEnabled)
                        {
                            volInfo1.ToSendVolume = setVol;
                        }
                        volInfo1.Ticker++;
                    }
                    if (volInfo2.LastSetVolume == null || Math.Abs(volInfo2.LastSetVolume.Value - setVol) > soundEpsilon ||
                        (volInfo2.LastSetVolume != 0 && setVol == 0) || (volInfo2.LastSetVolume != 1 && setVol == 1))
                    {
                        volInfo2.RunningVolume = setVol;
                        if (voiceProxEnabled)
                        {
                            volInfo2.ToSendVolume = setVol;
                        }
                        volInfo2.Ticker++;
                    }
                }
            }
        }

        public void OnRecieveUserData(string igPlayer, string? stage)
        {
            lock (lockKey)
            {
#if DEBUG
                Console.WriteLine($"Recieved stage data of {igPlayer}.");
#endif
                igToStage[igPlayer] = stage;
                igs.Add(igPlayer);
                if (!igToIgsToVols.ContainsKey(igPlayer))
                    igToIgsToVols[igPlayer] = new Dictionary<string, UserVolInfo>();
                //only update based on stage, if two who were not sharing a stage suddenly are
                //this frame 1 change might be before the position is updated, so just let it 
                //be handled when the position is updated

                //i.e. if two people no longer share a stage, mututally mute them
                foreach (string ig in igs)
                {
                    if (ig == igPlayer)
                        continue;
                    //ig <-> igPlayer
                    UserVolInfo volInfo1 = null!, volInfo2 = null!;
                    if (!igToIgsToVols.ContainsKey(ig))
                        igToIgsToVols[ig] = new Dictionary<string, UserVolInfo>();
                    if (!igToIgsToVols[ig].ContainsKey(igPlayer))
                    {
                        UserVolInfo one = new UserVolInfo(), two = new UserVolInfo();
                        volInfo1 = igToIgsToVols[igPlayer][ig] = one;
                        volInfo2 = igToIgsToVols[ig][igPlayer] = two;
                        volInfo1.Pair = volInfo2;
                        volInfo2.Pair = volInfo1;
                    }
                    volInfo1 ??= igToIgsToVols[igPlayer][ig];
                    volInfo2 ??= igToIgsToVols[ig][igPlayer];

                    //actual calculation
                    string? igStage = igToStage[ig];
                    if ((igStage == null || stage == null || igStage != stage) && (volInfo1.LastSetVolume != 0 || volInfo2.LastSetVolume != 0))
                    {
                        volInfo1.RunningVolume = 0;
                        volInfo2.RunningVolume = 0;
                        if (voiceProxEnabled)
                        {
                            volInfo1.ToSendVolume = 0;
                            volInfo2.ToSendVolume = 0;
                        }
                        volInfo1.Ticker++;
                        volInfo2.Ticker++;
                    }
                }
            }
        }

        public void OnRecieveUserData(string igPlayer, Team team)
        {
            lock (lockKey)
            {
#if DEBUG
                Console.WriteLine($"Recieved team data of {igPlayer}.");
#endif
                igToTeam[igPlayer] = team;
                igs.Add(igPlayer);
                if (!igToIgsToVols.ContainsKey(igPlayer))
                    igToIgsToVols[igPlayer] = new Dictionary<string, UserVolInfo>();
                //check if anyone is broadcasting in the walkie talkie dict
                //foreach (string ig in igs)
                //{
                //    if (ig == igPlayer)
                //        continue;
                //    //ig <-> igPlayer
                //    UserVolInfo volInfo = null!;
                //    if (!igToIgsToVols.ContainsKey(ig))
                //        igToIgsToVols[ig] = new Dictionary<string, UserVolInfo>();
                //    if (!igToIgsToVols[ig].ContainsKey(igPlayer))
                //        volInfo = igToIgsToVols[igPlayer][ig] = igToIgsToVols[ig][igPlayer] = new UserVolInfo();
                //    volInfo ??= igToIgsToVols[igPlayer][ig];

                //    //actual calculation
                //}
            }
        }

        public void OnRecieveWalkieTalkie(PVCWalkieTalkiePacket packet)
        {
            lock (lockKey)
            {
#if DEBUG && MEGA_VERBOSE
                Console.WriteLine($"Recieved walkie talkie from {packet.DiscordSource}, type: {packet.GetWalkieMode()}");
#endif
                if (!voiceProxEnabled || !discordToIg.ContainsKey(packet.DiscordSource))
                    return; //no point of walkie-talkie when voiceprox is off, or if that discord user isn't recognized
                switch (packet.GetWalkieMode())
                {
                    case PVCWalkieTalkiePacket.WalkieMode.Individual:
                    case PVCWalkieTalkiePacket.WalkieMode.Team:
                    case PVCWalkieTalkiePacket.WalkieMode.Global:
                        {
                            if (discordSourceToActiveWalkie.ContainsKey(packet.DiscordSource))
                            {
                                if (discordSourceToActiveWalkie[packet.DiscordSource].Packet.WalkieTick < packet.WalkieTick)
                                {
                                    var oldInfo = discordSourceToActiveWalkie[packet.DiscordSource].SystemTickOnLastCalculation;
                                    discordSourceToActiveWalkie[packet.DiscordSource] = new WalkieTicker()
                                    {
                                        Packet = packet,
                                        SystemTickOnLastCalculation = oldInfo
                                    };
                                }
                                //else that one is outdated
                            }
                            else
                            {
                                discordSourceToActiveWalkie[packet.DiscordSource] = new WalkieTicker() { Packet = packet };
                            }
                        }
                        break;
                    #region Old
                    //in individual
                    //{
                    //    string igs = discordToIg[packet.DiscordSource];
                    //    if (!discordToIg.ContainsKey(packet.SpecificDiscordRecipient!))
                    //    {
                    //        volCalcLogger.Warn($"{packet.DiscordSource} tried to directly talk to {packet.SpecificDiscordRecipient!}, but they aren't in the user tables.");
                    //        return; //can't do it because target player isn't present in the dictionary
                    //    }
                    //    string igd = discordToIg[packet.SpecificDiscordRecipient!];
                    //    igToIgsToVols[igd][igs].Ticker++
                    //}
                    #endregion
                    default:
                        volCalcLogger.Warn("A walkie-talkie packet was recieved with an invalid type, should be impossible for this to happen");
                        break;
                }
            }
        }

        public void SetVoiceProxEnabled(bool enabled)
        {
            lock (lockKey)
            {
                voiceProxEnabled = enabled;
                foreach (string ig1 in igs)
                {
                    foreach (string ig2 in igs)
                    {
                        if (ig1 == ig2)
                            continue;
                        var info = igToIgsToVols[ig1][ig2];
                        if (!enabled)
                        {
                            //set all to send volumes to 1
                            info.ToSendVolume = 1f;
                        }
                        //else normal behavior will resume with voiceproxenabled == false
                    }
                }
            }
        }
        #endregion

        #region Get Packets
        /// <summary>
        /// Call this to get users who need to have volume data sent to them.
        /// </summary>
        public List<(string discordRecipient, PVCMultiDataPacket packet)> GetVolumePacketsForThisFrameAndClearCache()
        {
            lock (lockKey)
            {
                var walkiesToRemove = new List<string>();
                foreach (var kvp in discordSourceToActiveWalkie)
                {
                    var elm = kvp.Value;
                    string discordSource = kvp.Key;
                    elm.SystemTickOnLastCalculation ??= Environment.TickCount64;
                    long delta = Environment.TickCount64 - elm.SystemTickOnLastCalculation!.Value;
                    elm.SystemTickOnLastCalculation = Environment.TickCount64;
                    switch (elm.Packet.GetWalkieMode())
                    {
                        case PVCWalkieTalkiePacket.WalkieMode.Individual:
                            {
                                string discordRecip = elm.Packet.SpecificDiscordRecipient!;
                                if (discordToIg.ContainsKey(discordSource))
                                {
                                    string igs = discordToIg[discordSource];
                                    if (discordToIg.ContainsKey(discordRecip))
                                    {
                                        string igd = discordToIg[discordRecip];
                                        if (igs != igd) //expression *should* always be true
                                        {
                                            igToIgsToVols[igd][igs].ToSendVolume = 1f;
                                            //if incremented 10000 times a second, would take 58000 milennia to overflow (overflow wont be an issue)
                                            igToIgsToVols[igd][igs].Ticker++; //may or may not have been updated in OnRecievedUserData, but double increment isn't an issue.
                                        }
                                    }
                                    else
                                    {
                                        volCalcLogger.Warn($"{discordSource} tried to directly talk to {discordRecip}, but they aren't in the user tables.");
                                    }
                                }
                            }
                            break;
                        case PVCWalkieTalkiePacket.WalkieMode.Team:
                            {
                                if (discordToIg.ContainsKey(discordSource))
                                {
                                    string igsrc = discordToIg[discordSource];
                                    foreach (string ig in igs)
                                    {
                                        if (ig != igsrc && igToTeam.ContainsKey(ig) && igToTeam.ContainsKey(igsrc)
                                            && igToTeam[ig] == igToTeam[igsrc])
                                        {
                                            igToIgsToVols[ig][igsrc].ToSendVolume = 1f;
                                            igToIgsToVols[ig][igsrc].Ticker++;
                                        }
                                    }
                                }
                                else
                                {
                                    volCalcLogger.Warn($"{discordSource} tried to team talk, but they aren't in the user tables");
                                }
                            }
                            break;
                        case PVCWalkieTalkiePacket.WalkieMode.Global:
                            {
                                if (discordToIg.ContainsKey(discordSource))
                                {
                                    string igsrc = discordToIg[discordSource];
                                    foreach (string ig in igs)
                                    {
                                        if (ig != igsrc)
                                        {
                                            igToIgsToVols[ig][igsrc].ToSendVolume = 1f;
                                            igToIgsToVols[ig][igsrc].Ticker++;
                                        }
                                    }
                                }
                                else
                                {
                                    volCalcLogger.Warn($"{discordSource} tried to globally talk, but they aren't in the user tables");
                                }
                            }
                            break;
                    }
                    elm.Packet.KeepAliveMS -= delta;
                    if (elm.Packet.KeepAliveMS < 0)
                    {
                        walkiesToRemove.Add(discordSource);
                    }
                }
                walkiesToRemove.ForEach(x => discordSourceToActiveWalkie.Remove(x));
                var result = new List<(string discordRecipient, PVCMultiDataPacket packet)>();
                foreach (string ig in igs)
                {
                    if (igToDiscord.ContainsKey(ig))
                    {
                        Dictionary<string, PVCMultiDataPacket.VolTick> vols = new Dictionary<string, PVCMultiDataPacket.VolTick>();
                        PVCMultiDataPacket packet = new PVCMultiDataPacket() { Volumes = vols };
                        foreach (var kvp in igToIgsToVols[ig]) //ToDictionary doesn't work because it doesn't conditionally exclude entires
                        {
                            if (igToDiscord.ContainsKey(kvp.Key) && kvp.Value.ToSendVolume != null)
                            {
                                vols.Add(igToDiscord[kvp.Key], new PVCMultiDataPacket.VolTick()
                                {
                                    Ticker = kvp.Value.Ticker,
                                    Volume = kvp.Value.ToSendVolume //unless walkie override
                                });
                            }
                            //else that discord user isn't connected, shouldn't include their volume
                        }
                        result.Add((igToDiscord[ig], packet));
                    }
                    //else that discord user isn't connected, can't send a packet to them.
                }
                //reset all volumeinfo entries
                foreach (string ig1 in igs)
                {
                    foreach (string ig2 in igs)
                    {
                        if (ig1 != ig2 && igToIgsToVols[ig1].ContainsKey(ig2))
                        {
                            //only first key was found, second dictionary was empty for some reason
                            igToIgsToVols[ig1][ig2].LastSetVolume = igToIgsToVols[ig1][ig2].ToSendVolume; //unless walkie override
                            igToIgsToVols[ig1][ig2].ToSendVolume = null;
                        }
                    }
                }
#if DEBUG && MEGA_VERBOSE
                if (result.Count > 0)
                    Console.WriteLine($"Sending new volume data to: {string.Join(", ", result.Select(x => x.discordRecipient))}");
#endif
                return result;
            }
        }

        public PVCMultiDataPacket? GetZeroedVolumePacketForUser(string discord)
        {
            lock (lockKey)
            {
                if (discordToIg.ContainsKey(discord))
                {
                    Dictionary<string, PVCMultiDataPacket.VolTick> vols = new Dictionary<string, PVCMultiDataPacket.VolTick>();
                    PVCMultiDataPacket packet = new PVCMultiDataPacket() { Volumes = vols };
                    //igToIgsToVols can be empty here
                    string igPlayer = discordToIg[discord];
                    if (!igToIgsToVols.ContainsKey(igPlayer))
                        igToIgsToVols[igPlayer] = new Dictionary<string, UserVolInfo>();
                    foreach (string ig in igs) //ToDictionary doesn't work because it doesn't conditionally exclude entires
                    {
                        if (ig == igPlayer)
                            continue;
                        //ig <-> igPlayer
                        UserVolInfo volInfo = null!;
                        if (!igToIgsToVols.ContainsKey(ig))
                            igToIgsToVols[ig] = new Dictionary<string, UserVolInfo>();
                        if (!igToIgsToVols[ig].ContainsKey(igPlayer))
                        {
                            UserVolInfo one = new UserVolInfo(), two = new UserVolInfo();
                            volInfo = igToIgsToVols[igPlayer][ig] = one;
                            igToIgsToVols[ig][igPlayer] = two;
                            one.Pair = two;
                            two.Pair = one;
                        }
                        volInfo ??= igToIgsToVols[igPlayer][ig];

                        if (igToDiscord.ContainsKey(ig))
                        {
                            vols.Add(igToDiscord[ig], new PVCMultiDataPacket.VolTick()
                            {
                                Ticker = volInfo.Ticker++,
                                Volume = 0
                            });
                            volInfo.LastSetVolume = 0;
                        }
                        //else that discord user isn't connected, shouldn't include their volume
                    }
#if DEBUG
                    if (packet.Volumes.Count > 0)
                        Console.WriteLine($"Sending zeroed volume data to {discord}, for the users: {string.Join(", ", packet.Volumes.Select(x => x.Value))}");
#endif
                    return packet;
                }
                else return null;
            }
        }

        //TODO: is this necessary?
        /// <summary>
        /// Returns a packet with the most up-to-date volume information for a user.
        /// </summary>
        /// <returns>Null if the requisite data isn't present in the internal info tables, the packet otherwise</returns>
        //public PVCMultiDataPacket? GetCorrectVolumePacketForUser(string discord)
        //{
        //    lock (lockKey)
        //    {
        //        if (discordToIg.ContainsKey(discord))
        //        {
        //            Dictionary<string, PVCMultiDataPacket.VolTick> vols = new Dictionary<string, PVCMultiDataPacket.VolTick>();
        //            PVCMultiDataPacket packet = new PVCMultiDataPacket() { Volumes = vols };
        //            foreach (var kvp in igToIgsToVols[discordToIg[discord]]) //ToDictionary doesn't work because it doesn't conditionally exclude entires
        //            {
        //                if (igToDiscord.ContainsKey(kvp.Key))
        //                {
        //                    if (kvp.Value.ToSendVolume != null)
        //                    {
        //                        vols.Add(igToDiscord[kvp.Key], new PVCMultiDataPacket.VolTick()
        //                        {
        //                            Ticker = kvp.Value.Ticker,
        //                            Volume = kvp.Value.ToSendVolume
        //                        });
        //                    }
        //                    else
        //                    {
        //                        //since tosend is null, just use the running
        //                        vols.Add(igToDiscord[kvp.Key], new PVCMultiDataPacket.VolTick()
        //                        {
        //                            Ticker = kvp.Value.Ticker++,
        //                            Volume = kvp.Value.RunningVolume
        //                        });
        //                    }
        //                }
        //                //else that discord user isn't connected, shouldn't include their volume
        //            }
        //            foreach (string ig1 in igs)
        //            {
        //                foreach (string ig2 in igs)
        //                {
        //                    igToIgsToVols[ig1][ig2].LastSetVolume = igToIgsToVols[ig1][ig2].ToSendVolume;
        //                    igToIgsToVols[ig1][ig2].ToSendVolume = null;
        //                }
        //            }
        //            return packet;
        //        }
        //        else return null;
        //    }
        //}
        #endregion

        #region Helpers
        private static float ClampedInvLerp(float a, float b, float v)
        {
            return v < a ? 0 : (v > b ? 1 : (v - a) / (b - a)); //see "linear interpolation"
        }
        #endregion
    }
}
