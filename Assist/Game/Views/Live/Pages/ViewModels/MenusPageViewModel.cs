﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Assist.Game.Controls.Live;
using Assist.Game.Models;
using Assist.Game.Models.Recent;
using Assist.Game.Services;
using Assist.Game.Views.Live.ViewModels;
using Assist.Game.Views.Profile.ViewModels;
using Assist.Objects.Helpers;
using Assist.Objects.RiotSocket;
using Assist.ViewModels;
using AssistUser.Lib.Profiles.Models;
using AsyncImageLoader;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DiscordRPC;
using ReactiveUI;
using Serilog;
using ValNet.Objects.Exceptions;
using ValNet.Objects.Local;
using ValNet.Objects.Player;

namespace Assist.Game.Views.Live.Pages.ViewModels
{
    internal class MenusPageViewModel : ViewModelBase
    {
        public string CurrentPartyId;

        private List<LiveMenuPartyUser> _currentUsers = new List<LiveMenuPartyUser>();

        private static DateTime LastRefreshTime = DateTime.MinValue;
        private static MatchHistoryObj matchHistoryObj;
        public List<LiveMenuPartyUser> CurrentUsers
        {
            get => _currentUsers;
            set => this.RaiseAndSetIfChanged(ref _currentUsers, value);
        }

        private string? queueName;

        public string QueueName
        {
            get => queueName;
            set => this.RaiseAndSetIfChanged(ref queueName, value);
        }

        private string? partySize;

        public string PartySize
        {
            get => partySize;
            set => this.RaiseAndSetIfChanged(ref partySize, value);
        }

        private bool _endorseEnabled = false;

        public bool EndorseEnabled
        {
            get => _endorseEnabled;
            set => this.RaiseAndSetIfChanged(ref _endorseEnabled, value);
        }
        public async Task Setup(PresenceV4Message start = null)
        {
            AssistApplication.Current.RiotWebsocketService.UserPresenceMessageEvent += RiotWebsocketServiceOnUserPresenceMessageEvent;

            if (start != null)
            {
                RiotWebsocketServiceOnUserPresenceMessageEvent(start);
            }
            CheckAndHandleRecentMatchTracking();

            if (ProfilePageViewModel.ProfileData is null)
            {
                await ProfilePageViewModel.UpdateProfileData();
            }
            
            if (ProfilePageViewModel.ProfileData.LinkedRiotAccounts.Count == 0 || ProfilePageViewModel.ProfileData.LinkedRiotAccounts[0].Id != AssistApplication.Current.CurrentUser.UserData.sub)
            {
                return;
            }
            
            if (matchHistoryObj is null || LastRefreshTime.Equals(DateTime.MinValue) || DateTime.UtcNow > LastRefreshTime.AddMinutes(10) ) // wait 10 minutes to refresh matchhistory
            {
                matchHistoryObj = await AssistApplication.Current.CurrentUser.Player.GetPlayerMatchHistory(0, 3);
                LastRefreshTime = DateTime.UtcNow;
            }
            if (matchHistoryObj.History.Count <= 0 || (string.IsNullOrEmpty(matchHistoryObj.History[0].QueueID) || matchHistoryObj.History[0].QueueID.Equals("deathmatch", StringComparison.OrdinalIgnoreCase )))
            {
                return;
            }
            
            //EndorseEnabled = true;
        }
        
        public async Task SetupWithLocalPresence(ChatV4PresenceObj.Presence obj = null)
        {
            var data = await GetPresenceData(obj);
            if (data != null)
            {
                if(data.sessionLoopState != "MENUS")
                    return;

                

                CurrentPartyId = data.partyId;
                UpdateGeneralPartyInformation(data,obj);

                if (data.partySize > 1)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => { HandleMoreThanOneParty(data); });
                }
                else
                {
                    /*var alreadyHere = LiveViewViewModel.ReputationUserV2s.ContainsKey(obj.puuid);
                    if (!alreadyHere)
                    {
                        await LiveViewViewModel.GetUserReputations(new List<string>() { obj.puuid });
                    }
                    var profileAlreadyHere = LiveViewViewModel.AssistProfiles.ContainsKey(obj.puuid);
                    if (!profileAlreadyHere)
                    {
                        await LiveViewViewModel.GetUserProfile(obj.puuid);
                    }*/
                    
                    if (CurrentUsers.Count == 0)
                    {
                        var pData = await GetPresenceData(obj);
                        
                        if (LiveViewViewModel.AssistProfiles.TryGetValue(obj.puuid, out var profileData))
                        {
                            var t = await GetUserBadges(profileData);
                            AddUserToList(
                                new LiveMenuPartyUser()
                                {
                                    PlayerId = obj.puuid,
                                    PlayerName = string.IsNullOrEmpty(profileData.DisplayName) ? obj.game_name : profileData.DisplayName ,
                                    ValorantName = !string.IsNullOrEmpty(profileData.DisplayName) ?  $"{obj.game_name}#{obj.game_tag}" : "", 
                                    Playercard = $"https://content.assistapp.dev/playercards/{data.playerCardId}_LargeArt.png",
                                    PlayerReady = true,
                                    BadgeObjects = t,
                                    PlayerRankIcon = $"https://content.assistapp.dev/ranks/TX_CompetitiveTier_Large_{data.competitiveTier}.png"
                                }
                            );
                        }
                        else
                        {
                            AddUserToList(
                                new LiveMenuPartyUser()
                                {
                                    PlayerId = obj.puuid,
                                    PlayerName = $"{obj.game_name}",
                                    Playercard = $"https://content.assistapp.dev/playercards/{data.playerCardId}_LargeArt.png",
                                    PlayerReady = true,
                                    PlayerRankIcon = $"https://content.assistapp.dev/ranks/TX_CompetitiveTier_Large_{data.competitiveTier}.png"
                                }
                            );    
                        }
                    }

                    if (data.partySize == 1)
                    {
                        for (int i = 0; i < CurrentUsers.Count; i++)
                        {
                            if (CurrentUsers[i].PlayerId != AssistApplication.Current.CurrentUser.UserData.sub)
                                RemoveUserToList(CurrentUsers[i]);
                        }
                    }
                }
            }

            await Setup(null);
        }

        private async void RiotWebsocketServiceOnUserPresenceMessageEvent(PresenceV4Message obj)
        {
            // On User Update 
            var data = await GetPresenceData(obj.data.presences[0]);

            if (data != null)
            {
                if(data.sessionLoopState != "MENUS")
                    return;
                
                CurrentPartyId = data.partyId;
                UpdateGeneralPartyInformation(data,obj);

                if (data.partySize > 1)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => { HandleMoreThanOneParty(data); });
                }
                else
                {
                    /*var alreadyHere = LiveViewViewModel.ReputationUserV2s.ContainsKey(obj.data.presences[0].puuid);
                    if (!alreadyHere)
                    {
                        await LiveViewViewModel.GetUserReputations(new List<string>() { obj.data.presences[0].puuid });
                    }
                    
                    var profileAlreadyHere = LiveViewViewModel.AssistProfiles.ContainsKey(obj.data.presences[0].puuid);
                    if (!profileAlreadyHere)
                    {
                        await LiveViewViewModel.GetUserProfile(obj.data.presences[0].puuid);
                    }*/
                    
                    
                    if (CurrentUsers.Count == 0)
                    {
                        if (LiveViewViewModel.AssistProfiles.TryGetValue(obj.data.presences[0].puuid, out var profileData))
                        {
                            var t = await GetUserBadges(profileData);
                            AddUserToList(
                                new LiveMenuPartyUser()
                                {
                                    PlayerId = obj.data.presences[0].puuid,
                                    PlayerName = string.IsNullOrEmpty(profileData.DisplayName) ? obj.data.presences[0].game_name : profileData.DisplayName ,
                                    ValorantName = !string.IsNullOrEmpty(profileData.DisplayName) ?  $"{obj.data.presences[0].game_name}#{obj.data.presences[0].game_tag}" : "", 
                                    Playercard = $"https://content.assistapp.dev/playercards/{data.playerCardId}_LargeArt.png",
                                    PlayerReady = true,
                                    BadgeObjects = t,
                                    PlayerRankIcon = $"https://content.assistapp.dev/ranks/TX_CompetitiveTier_Large_{data.competitiveTier}.png"
                                }
                            );
                        }
                        else
                        {
                            AddUserToList(
                                new LiveMenuPartyUser()
                                {
                                    PlayerId = obj.data.presences[0].puuid,
                                    PlayerName = $"{obj.data.presences[0].game_name}",
                                    Playercard = $"https://content.assistapp.dev/playercards/{data.playerCardId}_LargeArt.png",
                                    PlayerReady = true,
                                    //PlayerReputationLevel = SetupReputation(obj.data.presences[0].puuid),
                                    PlayerRankIcon = $"https://content.assistapp.dev/ranks/TX_CompetitiveTier_Large_{data.competitiveTier}.png"
                                }
                            );    
                        }
                    }

                    if (data.partySize == 1)
                    {
                        for (int i = 0; i < CurrentUsers.Count; i++)
                        {
                            if (CurrentUsers[i].PlayerId != AssistApplication.Current.CurrentUser.UserData.sub)
                                RemoveUserToList(CurrentUsers[i]);
                        }
                    }
                }
            }
            
        }

        public async void UpdateGeneralPartyInformation(PlayerPresence data, PresenceV4Message obj)
        {
            var currentUserBtn = CurrentUsers.ToList().Find(member => member.PlayerId == obj.data.presences[0].puuid);
            Log.Information($"QUEUE ID {data.queueId}");
            QueueName = QueueNames.DetermineQueueKey(data.queueId).ToUpper();
            PartySize = $"{data.partySize}/{data.maxPartySize}";

            if (data.maxPartySize > 5)
            {
                QueueName = "CUSTOM GAME (Not currently supported within Assist)";
            }

            if (currentUserBtn != null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    currentUserBtn.Playercard = $"https://content.assistapp.dev/playercards/{data.playerCardId}_LargeArt.png";
                    
                });
            }

            // Update UI Elements
            
        }
        
        public async void UpdateGeneralPartyInformation(PlayerPresence data, ChatV4PresenceObj.Presence obj)
        {
            var currentUserBtn = CurrentUsers.ToList().Find(member => member.PlayerId == obj.puuid);
            Log.Information($"QUEUE ID {data.queueId}");
            QueueName = QueueNames.DetermineQueueKey(data.queueId).ToUpper();
            PartySize = $"{data.partySize}/{data.maxPartySize}";

            if (data.maxPartySize > 5)
            {
                QueueName = "CUSTOM GAME (Not currently supported within Assist)";
            }

            if (currentUserBtn != null)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    currentUserBtn.Playercard = $"https://content.assistapp.dev/playercards/{data.playerCardId}_LargeArt.png";
                    
                });
            }

            // Update UI Elements
            
        }
        public async void HandleMoreThanOneParty(PlayerPresence data)
        {
            try
            {
                var partyData = await AssistApplication.Current.CurrentUser.Party.FetchParty();
                var pres = await AssistApplication.Current.CurrentUser.Presence.GetPresences();
                if (partyData != null)
                {
                    if(partyData.Members.Count <= 1)
                        return;

                    var allIds = partyData.Members.Select(x => x.Subject).ToList();

                   // await LiveViewViewModel.GetUserReputations(allIds);

                    for (int i = 0; i < partyData.Members.Count; i++)
                    {
                        
                        var member = partyData.Members[i];
                        var currentUserBtn = CurrentUsers.ToList().Find(member => member.PlayerId == partyData.Members[i].Subject);
                        var gameName = pres.presences.Find(pres => pres.puuid == member.Subject);
                        var p = await GetPresenceData(gameName);
                        
                        var profileAlreadyHere = LiveViewViewModel.AssistProfiles.ContainsKey(member.Subject);
                        if (!profileAlreadyHere)
                        {
                            await LiveViewViewModel.GetUserProfile(member.Subject);
                        }
                        if (currentUserBtn == null)
                        {
                            Dispatcher.UIThread.InvokeAsync(async () =>
                            {

                                if (LiveViewViewModel.AssistProfiles.TryGetValue(member.Subject, out var profileData))
                                {

                                    var t = await GetUserBadges(profileData);
                                    AddUserToList(new LiveMenuPartyUser()
                                    {
                                        PlayerName = string.IsNullOrEmpty(profileData.DisplayName) ? gameName.game_name : profileData.DisplayName ,
                                        ValorantName = !string.IsNullOrEmpty(profileData.DisplayName) ?  $"{gameName.game_name}#{gameName.game_tag}" : "",
                                        PlayerId = member.Subject,
                                        Playercard =
                                            $"https://content.assistapp.dev/playercards/{p.playerCardId}_LargeArt.png",
                                        PlayerReady = true,
                                        BadgeObjects = t,
                                        //PlayerReputationLevel = SetupReputation(member.Subject),
                                        PlayerRankIcon = $"https://content.assistapp.dev/ranks/TX_CompetitiveTier_Large_{p.competitiveTier}.png"
                                    });
                                }
                                else
                                {
                                    AddUserToList(new LiveMenuPartyUser()
                                    {
                                        PlayerName = $"{gameName.game_name}",
                                        PlayerId = member.Subject,
                                        Playercard = $"https://content.assistapp.dev/playercards/{p.playerCardId}_LargeArt.png",
                                        PlayerReady = true,
                                        //PlayerReputationLevel = SetupReputation(member.Subject),
                                        PlayerRankIcon = $"https://content.assistapp.dev/ranks/TX_CompetitiveTier_Large_{p.competitiveTier}.png"
                                    });
                                }
                                
                                
                                
                            });
                            // This means this is a new Party Member
                            
                        }


                    }

                    for (int i = 0; i < CurrentUsers.Count; i++)
                    {
                        var d = partyData.Members.Find(member => member.Subject == CurrentUsers[i].PlayerId);
                        if (d == null)
                        {
                            RemoveUserToList(CurrentUsers[i]);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Fatal("Failed to get Party");
                Log.Fatal(e.Message);

                if (e is RequestException)
                {
                    var test = e as RequestException;

                    if (test != null)
                    {
                        Log.Fatal(test.StatusCode.ToString());
                        Log.Fatal(test.Content);
                    }
                }
            }
        }
        
        public async void AddUserToList(LiveMenuPartyUser u)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                var newTempList = new List<LiveMenuPartyUser>();
                newTempList.Add(u);
                var l = CurrentUsers.Concat(newTempList).ToList();
                CurrentUsers = l;
            });
        }

        public async void RemoveUserToList(LiveMenuPartyUser u)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                var newTempList = new List<LiveMenuPartyUser>();
                CurrentUsers.ForEach(user =>
                {
                    if(user.PlayerId != u.PlayerId)
                        newTempList.Add(user);

                });

                CurrentUsers = newTempList;
            });
        }
        
        public async Task<PlayerPresence> GetPresenceData(ChatV4PresenceObj.Presence data)
        {
            if (string.IsNullOrEmpty(data.Private))
                return new PlayerPresence();
            byte[] stringData = Convert.FromBase64String(data.Private);
            string decodedString = Encoding.UTF8.GetString(stringData);
            return JsonSerializer.Deserialize<PlayerPresence>(decodedString);
        }
        
        public async Task<List<AdvancedImage>> GetUserBadges(AssistProfile data)
        {
            if (data.FeaturedBadges.Count == 0)
                return null;

            List<AdvancedImage> t = new();
            foreach (var badge in data.FeaturedBadges)
            {
                var imgObj = new AdvancedImage(new Uri($"https://content.assistapp.dev/badges/{badge.Id}.png"))
                {
                    Source = $"https://content.assistapp.dev/badges/{badge.Id}.png"
                };
                RenderOptions.SetBitmapInterpolationMode(imgObj, BitmapInterpolationMode.MediumQuality);
                
                t.Add(imgObj);
            }

            return t;

        }


        public void UnsubscribeFromEvents()
        {
            Log.Information("Page is Unloaded, Unsubbing from Events from MenusPageView");
            AssistApplication.Current.RiotWebsocketService.UserPresenceMessageEvent -= RiotWebsocketServiceOnUserPresenceMessageEvent;
        }
        
        private Bitmap? SetupReputation(string _playerId)
        {
            LiveViewViewModel.ReputationUserV2s.TryGetValue(_playerId, out var reputationUserV2);

            if (reputationUserV2 is null)
            {
                Log.Information("User requested Reputation data does not exist.");
            }

            reputationUserV2.SeasonalReputation.TryGetValue(AssistApplication.EpisodeId, out var reputation);
            if (reputation != null)
            {
                return new Bitmap(AssetLoader.Open(new Uri($@"avares://Assist/Resources/Game/Assist_EndorseLevel{reputation.Level}.png")));
            }
            
            return new Bitmap(AssetLoader.Open(new Uri($@"avares://Assist/Resources/Game/Assist_EndorseLevel{reputation.Level}.png")));
        }
        
        
        private async void CheckAndHandleRecentMatchTracking()
        {
            var allUnfinished = RecentService.Current.RecentMatches.FindAll(x => !x.IsCompleted);

            foreach (var unfinishedMatch in allUnfinished)
            {
                // This is ran in the menus, meaning this is assuming no game is currently going on within the current CLient.

                if (unfinishedMatch.Result != RecentMatch.MatchResult.REMAKE)
                {
                    /*if (!unfinishedMatch.OwningPlayer.Equals(AssistApplication.Current.CurrentUser.UserData.sub))
                    {
                        if (DateTime.Now.ToUniversalTime() > unfinishedMatch.DateOfMatch.ToUniversalTime().AddMinutes(5)) RecentService.Current.RemoveMatch(unfinishedMatch.MatchId);
                        return;    
                    }*/
                    
                    await RecentService.Current.UpdateMatch(unfinishedMatch.MatchId);

                    var updatedMatch =
                        RecentService.Current.RecentMatches.Find(x => x.MatchId.Equals(unfinishedMatch.MatchId));

                    if (!updatedMatch.IsCompleted && updatedMatch.MatchTrack_LastState.Equals("PREGAME", StringComparison.OrdinalIgnoreCase))  // This means that the match is still not finished while the player is in the match.
                    {
                        Log.Information("Found match that is not valid. Marking as Remake");
                        updatedMatch.MatchTrack_LastState = "MENUS";
                        updatedMatch.Result = RecentMatch.MatchResult.REMAKE;
                        RecentService.Current.UpdateMatch(updatedMatch);
                    }
                }
            }
        }

    }
}
