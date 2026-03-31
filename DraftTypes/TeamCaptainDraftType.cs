using System;
using System.Collections.Generic;
using System.Linq;
using DraftModeTOUM.Managers;
using DraftModeTOUM.Patches;
using MiraAPI.GameOptions;
using MiraAPI.Roles;
using DraftModeTOUM.Roles;
using AmongUs.GameOptions;
using UnityEngine;

namespace DraftModeTOUM.DraftTypes
{
    public enum TeamRoundState
    {
        None = 0,
        Prep = 1,
        Active = 2,
        RoundEnd = 3,
        MatchOver = 4
    }

    public static class TeamCaptainDraftType
    {
        public static bool IsEnabled =>
            OptionGroupSingleton<DraftTypeOptions>.Instance.DraftType == DraftTypeMode.TeamCaptainBR;

        public static bool IsTeamModeActive { get; private set; }

        public static int CaptainsCount { get; private set; } = 2;
        public static CaptainSelectionMode SelectionMode { get; private set; } = CaptainSelectionMode.Random;
        public static TeamWinCondition WinCondition { get; private set; } = TeamWinCondition.LastTeamStanding;
        public static int RoundMinutes { get; private set; } = 5;
        public static int Rounds { get; private set; } = 3;
        public static int PrepTimeSeconds { get; private set; } = 30;
        public static bool FriendlyFire { get; private set; } = false;
        public static bool TasksCountIfDead { get; private set; } = true;
        public static int RoundWinPoints { get; private set; } = 3;
        public static int KillPoints { get; private set; } = 1;
        public static int TaskPoints { get; private set; } = 2;

        private static readonly List<byte> _captains = new();
        private static readonly List<byte> _pickOrder = new();
        private static readonly Dictionary<byte, byte> _playerToTeam = new();
        private static readonly Dictionary<byte, List<byte>> _teamMembers = new();
        private static readonly List<byte> _availablePlayers = new();
        private static int _currentPickIndex = 0;
        private static int _snakeIndex = 0;
        private static int _snakeDir = 1;

        private static TeamRoundState _roundState = TeamRoundState.None;
        private static int _currentRound = 0;
        private static float _roundTimer = 0f;
        private static float _prepTimer = 0f;
        private static float _roundEndTimer = 0f;
        private const float RoundEndDelaySeconds = 6f;
        private static bool _pendingMatchStart = false;
        private static bool _rolesAssigned = false;
        private static float _colorRefreshTimer = 0f;

        private static readonly Dictionary<byte, int> _teamScores = new();
        private static readonly Dictionary<byte, int> _teamRoundScores = new();
        private static readonly Dictionary<byte, int> _lastTaskCompleteCounts = new();
        private static readonly List<byte> _captainCandidates = new();
        private static bool _selectingCaptains = false;

        public static IReadOnlyList<byte> Captains => _captains;
        public static IReadOnlyDictionary<byte, byte> PlayerTeams => _playerToTeam;
        public static IReadOnlyDictionary<byte, List<byte>> TeamMembers => _teamMembers;
        public static TeamRoundState RoundState => _roundState;

        public static void ApplySettings()
        {
            var opts = OptionGroupSingleton<TeamCaptainOptions>.Instance;
            CaptainsCount = Mathf.Clamp(Mathf.RoundToInt(opts.TeamCaptains), 2, 5);
            SelectionMode = opts.SelectionMode;
            WinCondition = opts.WinCondition;
            RoundMinutes = Mathf.Clamp(Mathf.RoundToInt(opts.RoundTimeMinutes), 1, 60);
            Rounds = Mathf.Clamp(Mathf.RoundToInt(opts.Rounds), 2, 10);
            PrepTimeSeconds = Mathf.Clamp(Mathf.RoundToInt(opts.PrepTimeSeconds), 0, 60);
            FriendlyFire = opts.FriendlyFire;
            TasksCountIfDead = opts.TasksCountIfDead;
            RoundWinPoints = Mathf.Clamp(Mathf.RoundToInt(opts.RoundWinPoints), 0, 10);
            KillPoints = Mathf.Clamp(Mathf.RoundToInt(opts.KillPoints), 0, 10);
            TaskPoints = Mathf.Clamp(Mathf.RoundToInt(opts.TaskPoints), 0, 10);
        }

        public static void StartTeamModeHost()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Joined) return;

            ApplySettings();
            ResetState();
            DraftTicker.EnsureExists();

            var players = GetLobbyPlayers();
            if (players.Count < 2)
            {
                DraftModePlugin.Logger.LogWarning("[TeamCaptain] Not enough players.");
                return;
            }

            if (players.Count == CaptainsCount)
            {
                _captains.Clear();
                foreach (var p in players) _captains.Add(p.PlayerId);
                StartTeamPickAfterCaptains(players);
                return;
            }

            if (SelectionMode == CaptainSelectionMode.HostChooses)
            {
                _selectingCaptains = true;
                _captainCandidates.Clear();
                _captainCandidates.AddRange(players.Select(p => p.PlayerId));
                _captains.Clear();
                TeamCaptainPickOverlay.ShowCaptainSelect(_captains, _captainCandidates, CaptainsCount);
                return;
            }

            var captainIds = ResolveCaptains(players);
            foreach (var cap in captainIds)
                AddToTeam(cap, (byte)_captains.Count);

            StartTeamPickAfterCaptains(players);
        }

        private static List<byte> ResolveCaptains(List<PlayerControl> players)
        {
            var result = new List<byte>();
            var remaining = players.Select(p => p.PlayerId).Where(id => !result.Contains(id)).ToList();
            remaining = remaining.OrderBy(_ => UnityEngine.Random.value).ToList();
            while (result.Count < CaptainsCount && remaining.Count > 0)
            {
                result.Add(remaining[0]);
                remaining.RemoveAt(0);
            }

            _captains.Clear();
            _captains.AddRange(result);
            return result;
        }

        private static void AddToTeam(byte playerId, byte teamId)
        {
            _playerToTeam[playerId] = teamId;
            if (!_teamMembers.ContainsKey(teamId))
                _teamMembers[teamId] = new List<byte>();
            if (!_teamMembers[teamId].Contains(playerId))
                _teamMembers[teamId].Add(playerId);
        }

        private static void StartNextPickHost()
        {
            if (!IsTeamModeActive) return;
            if (_availablePlayers.Count == 0)
            {
                EndTeamPickHost();
                return;
            }

            byte currentCaptain = _captains[_snakeIndex];
            DraftModePlugin.Logger.LogInfo($"[TeamCaptain] Pick turn captain={currentCaptain} remaining={_availablePlayers.Count}");
            TeamCaptainPickOverlay.SetCurrentCaptain(currentCaptain);
            DraftNetworkHelper.BroadcastTeamPickTurn(currentCaptain, _availablePlayers);
        }

        public static void HandleTeamPickHost(byte captainId, byte pickedPlayerId)
        {
            if (!AmongUsClient.Instance.AmHost || !IsTeamModeActive) return;
            if (_availablePlayers.Count == 0) return;
            if (!_availablePlayers.Contains(pickedPlayerId)) return;

            int teamIndex = _captains.IndexOf(captainId);
            if (teamIndex < 0) return;

            AddToTeam(pickedPlayerId, (byte)teamIndex);
            _availablePlayers.Remove(pickedPlayerId);

            DraftNetworkHelper.BroadcastTeamPickMade(captainId, pickedPlayerId, (byte)teamIndex);
            TeamCaptainPickOverlay.UpdateTeams(_teamMembers, _availablePlayers);

            AdvanceSnake();
            StartNextPickHost();
        }

        public static void HandleTeamPickLocal(byte captainId, byte pickedPlayerId, byte teamId)
        {
            AddToTeam(pickedPlayerId, teamId);
            _availablePlayers.Remove(pickedPlayerId);
            TeamCaptainPickOverlay.UpdateTeams(_teamMembers, _availablePlayers);
            ApplyTeamColorsLocal();
        }

        public static void HandleTeamPickStartLocal(List<byte> captains, Dictionary<byte, List<byte>> teams, List<byte> available)
        {
            ResetState();
            DraftTicker.EnsureExists();
            _captains.AddRange(captains);
            foreach (var kvp in teams)
            {
                foreach (var pid in kvp.Value)
                    AddToTeam(pid, kvp.Key);
            }
            _availablePlayers.AddRange(available);
            IsTeamModeActive = true;
            TeamCaptainPickOverlay.Show(_captains, _teamMembers, _availablePlayers);
            ApplyTeamColorsLocal();
        }

        private static void AdvanceSnake()
        {
            if (_captains.Count <= 1) return;
            if (_snakeIndex == 0 && _snakeDir == -1) _snakeDir = 1;
            else if (_snakeIndex == _captains.Count - 1 && _snakeDir == 1) _snakeDir = -1;
            _snakeIndex += _snakeDir;
        }

        private static void EndTeamPickHost()
        {
            DraftModePlugin.Logger.LogInfo("[TeamCaptain] Team picking complete.");
            TeamCaptainPickOverlay.Hide();
            DraftNetworkHelper.BroadcastTeamPickEnd();
            _pendingMatchStart = true;

            if (AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Joined &&
                GameStartManager.Instance != null)
            {
                GameStartManager.Instance.BeginGame();
            }
            else
            {
                BeginMatchHost();
            }
        }

        public static void EndTeamPickLocal()
        {
            TeamCaptainPickOverlay.Hide();
        }

        public static void HandleCaptainSelectHost(byte pickedPlayerId)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (!_selectingCaptains) return;
            if (!_captainCandidates.Contains(pickedPlayerId)) return;

            _captains.Add(pickedPlayerId);
            _captainCandidates.Remove(pickedPlayerId);
            TeamCaptainPickOverlay.UpdateCaptainSelect(_captains, _captainCandidates, CaptainsCount);

            if (_captains.Count >= CaptainsCount)
            {
                _selectingCaptains = false;
                var players = GetLobbyPlayers();
                StartTeamPickAfterCaptains(players);
            }
        }

        public static void ResetState()
        {
            IsTeamModeActive = false;
            _captains.Clear();
            _pickOrder.Clear();
            _playerToTeam.Clear();
            _teamMembers.Clear();
            _availablePlayers.Clear();
            _currentPickIndex = 0;
            _snakeIndex = 0;
            _snakeDir = 1;
            _roundState = TeamRoundState.None;
            _currentRound = 0;
            _roundTimer = 0f;
            _prepTimer = 0f;
            _roundEndTimer = 0f;
            _pendingMatchStart = false;
            _rolesAssigned = false;
            _colorRefreshTimer = 0f;
            _teamScores.Clear();
            _teamRoundScores.Clear();
            _lastTaskCompleteCounts.Clear();
            _captainCandidates.Clear();
            _selectingCaptains = false;
        }

        public static Color GetTeamColor(byte teamId)
        {
            return teamId switch
            {
                0 => Color.white,
                1 => new Color(0.2f, 0.5f, 1f),
                2 => new Color(0.2f, 1f, 0.4f),
                3 => new Color(1f, 0.2f, 0.2f),
                4 => new Color(0.7f, 0.3f, 1f),
                _ => Color.white
            };
        }

        public static string GetTeamName(byte teamId)
        {
            return teamId switch
            {
                0 => "White",
                1 => "Blue",
                2 => "Green",
                3 => "Red",
                4 => "Purple",
                _ => "White"
            };
        }

        public static string GetTeamLabel(byte teamId)
        {
            return $"Team {GetTeamName(teamId)}";
        }

        public static void Tick(float deltaTime)
        {
            if (!IsTeamModeActive) return;

            if (_roundState == TeamRoundState.Prep)
            {
                _prepTimer = Mathf.Max(0f, _prepTimer - deltaTime);
                TeamCaptainRoundOverlay.SetTimerLabel($"PREP: {FormatSeconds(_prepTimer)}");
                if (AmongUsClient.Instance.AmHost && _prepTimer <= 0f)
                {
                    StartRoundActiveHost();
                }
            }
            else if (_roundState == TeamRoundState.Active)
            {
                _roundTimer = Mathf.Max(0f, _roundTimer - deltaTime);
                TeamCaptainRoundOverlay.SetTimerLabel($"TIME: {FormatSeconds(_roundTimer)}");

                if (AmongUsClient.Instance.AmHost)
                {
                    UpdateTaskPointsHost();

                    if (WinCondition == TeamWinCondition.TimeLimit || WinCondition == TeamWinCondition.Both)
                    {
                        if (_roundTimer <= 0f)
                        {
                            EndRoundHost(isTimeLimit: true);
                        }
                    }

                    if (WinCondition == TeamWinCondition.LastTeamStanding || WinCondition == TeamWinCondition.Both)
                    {
                        var aliveTeams = GetAliveTeams();
                        if (aliveTeams.Count <= 1)
                        {
                            EndRoundHost(isTimeLimit: false);
                        }
                    }
                }
            }
            else if (_roundState == TeamRoundState.RoundEnd)
            {
                _roundEndTimer = Mathf.Max(0f, _roundEndTimer - deltaTime);
                TeamCaptainRoundOverlay.SetTimerLabel("ROUND OVER");
                if (AmongUsClient.Instance.AmHost && _roundEndTimer <= 0f)
                {
                    if (_currentRound >= Rounds)
                    {
                        EndMatchHost();
                    }
                    else
                    {
                        StartRoundHost();
                    }
                }
            }
        }

        private static void BeginMatchHost()
        {
            if (!_pendingMatchStart) return;
            _pendingMatchStart = false;
            IsTeamModeActive = true;
            _teamScores.Clear();
            _teamRoundScores.Clear();
            foreach (var cap in _captains)
            {
                byte teamId = (byte)_captains.IndexOf(cap);
                _teamScores[teamId] = 0;
                _teamRoundScores[teamId] = 0;
            }

            DraftNetworkHelper.BroadcastTeamModeStart(_playerToTeam, _teamScores, _teamRoundScores, 0, Rounds, PrepTimeSeconds, RoundMinutes);
            TeamCaptainRoundOverlay.Show();
            ApplyTeamColorsLocal();
            StartRoundHost();
        }

        public static void OnShipStart()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            AssignTeamRolesHost();
            if (_pendingMatchStart)
                BeginMatchHost();
        }

        private static void AssignTeamRolesHost()
        {
            if (_rolesAssigned) return;
            if (!IsTeamModeActive && !_pendingMatchStart) return;

            ushort roleId;
            try
            {
                roleId = RoleId.Get<GunslingerRole>();
            }
            catch
            {
                return;
            }

            var players = PlayerControl.AllPlayerControls.ToArray();
            foreach (var p in players)
            {
                if (p == null || p.Data == null || p.Data.Disconnected) continue;
                try
                {
                    p.RpcSetRole((RoleTypes)roleId, false);
                }
                catch
                {
                    // ignore
                }
            }

            _rolesAssigned = true;
        }

        private static void StartRoundHost()
        {
            _currentRound++;
            ResetRoundScores();
            ResetTasksForAll();
            ReviveAndTeleportAll();
            ApplyTeamColorsLocal();

            _prepTimer = PrepTimeSeconds;
            _roundTimer = RoundMinutes * 60f;
            _roundState = TeamRoundState.Prep;

            TeamCaptainRoundOverlay.Show();
            TeamCaptainRoundOverlay.SetRoundInfo(_currentRound, Rounds);
            TeamCaptainRoundOverlay.SetStatus("PREPARE");
            TeamCaptainRoundOverlay.SetScores(_teamScores, _teamRoundScores);

            DraftNetworkHelper.BroadcastTeamRoundState(_roundState, _currentRound, _roundTimer, _prepTimer, _teamScores, _teamRoundScores);
        }

        private static void StartRoundActiveHost()
        {
            _roundState = TeamRoundState.Active;
            TeamCaptainRoundOverlay.SetStatus("");
            DraftNetworkHelper.BroadcastTeamRoundState(_roundState, _currentRound, _roundTimer, _prepTimer, _teamScores, _teamRoundScores);
        }

        private static void EndRoundHost(bool isTimeLimit)
        {
            if (_roundState != TeamRoundState.Active) return;

            byte winnerTeam = 255;
            if (isTimeLimit)
            {
                int best = int.MinValue;
                foreach (var kvp in _teamRoundScores)
                {
                    if (kvp.Value > best)
                    {
                        best = kvp.Value;
                        winnerTeam = kvp.Key;
                    }
                    else if (kvp.Value == best)
                    {
                        winnerTeam = 255;
                    }
                }
            }
            else
            {
                var aliveTeams = GetAliveTeams();
                if (aliveTeams.Count == 1)
                    winnerTeam = aliveTeams[0];
            }

            if (winnerTeam != 255)
            {
                _teamScores[winnerTeam] = _teamScores.TryGetValue(winnerTeam, out var cur) ? cur + RoundWinPoints : RoundWinPoints;
            }

            _roundState = TeamRoundState.RoundEnd;
            _roundEndTimer = RoundEndDelaySeconds;

            DraftNetworkHelper.BroadcastTeamRoundEnd(winnerTeam, _currentRound, _teamScores, _teamRoundScores);
        }

        private static void EndMatchHost()
        {
            _roundState = TeamRoundState.MatchOver;

            byte winnerTeam = 255;
            int best = int.MinValue;
            foreach (var kvp in _teamScores)
            {
                if (kvp.Value > best)
                {
                    best = kvp.Value;
                    winnerTeam = kvp.Key;
                }
                else if (kvp.Value == best)
                {
                    winnerTeam = 255;
                }
            }

            DraftNetworkHelper.BroadcastTeamMatchEnd(winnerTeam, _teamScores);
        }

        public static void HandleTeamModeStartLocal(Dictionary<byte, byte> playerTeams, Dictionary<byte, int> totalScores, Dictionary<byte, int> roundScores, int round, int totalRounds, int prepSeconds, int roundMinutes)
        {
            ResetState();
            DraftTicker.EnsureExists();
            foreach (var kvp in playerTeams)
                AddToTeam(kvp.Key, kvp.Value);
            foreach (var kvp in totalScores)
                _teamScores[kvp.Key] = kvp.Value;
            foreach (var kvp in roundScores)
                _teamRoundScores[kvp.Key] = kvp.Value;

            IsTeamModeActive = true;
            _currentRound = round;
            Rounds = totalRounds;
            PrepTimeSeconds = prepSeconds;
            RoundMinutes = roundMinutes;

            TeamCaptainRoundOverlay.Show();
            TeamCaptainRoundOverlay.SetRoundInfo(Mathf.Max(1, round), Rounds);
            TeamCaptainRoundOverlay.SetScores(_teamScores, _teamRoundScores);
            ApplyTeamColorsLocal();
        }

        public static void HandleTeamRoundStateLocal(TeamRoundState state, int round, float roundTimer, float prepTimer, Dictionary<byte, int> totalScores, Dictionary<byte, int> roundScores)
        {
            _roundState = state;
            _currentRound = round;
            _roundTimer = roundTimer;
            _prepTimer = prepTimer;
            foreach (var kvp in totalScores) _teamScores[kvp.Key] = kvp.Value;
            foreach (var kvp in roundScores) _teamRoundScores[kvp.Key] = kvp.Value;

            TeamCaptainRoundOverlay.Show();
            TeamCaptainRoundOverlay.SetRoundInfo(_currentRound, Rounds);
            TeamCaptainRoundOverlay.SetScores(_teamScores, _teamRoundScores);
            TeamCaptainRoundOverlay.SetStatus(state == TeamRoundState.Prep ? "PREPARE" : "");
        }

        public static void HandleTeamRoundEndLocal(byte winnerTeam, int round, Dictionary<byte, int> totalScores, Dictionary<byte, int> roundScores)
        {
            _roundState = TeamRoundState.RoundEnd;
            _currentRound = round;
            foreach (var kvp in totalScores) _teamScores[kvp.Key] = kvp.Value;
            foreach (var kvp in roundScores) _teamRoundScores[kvp.Key] = kvp.Value;

            TeamCaptainRoundOverlay.Show();
            TeamCaptainRoundOverlay.SetRoundInfo(_currentRound, Rounds);
            TeamCaptainRoundOverlay.SetScores(_teamScores, _teamRoundScores);
            TeamCaptainRoundOverlay.SetStatus(winnerTeam == 255 ? "ROUND TIED" : $"TEAM {winnerTeam + 1} WINS ROUND");
        }

        public static void HandleTeamMatchEndLocal(byte winnerTeam, Dictionary<byte, int> totalScores)
        {
            _roundState = TeamRoundState.MatchOver;
            foreach (var kvp in totalScores) _teamScores[kvp.Key] = kvp.Value;
            TeamCaptainRoundOverlay.Show();
            TeamCaptainRoundOverlay.SetScores(_teamScores, _teamRoundScores);
            TeamCaptainRoundOverlay.SetStatus(winnerTeam == 255 ? "MATCH TIED" : $"TEAM {winnerTeam + 1} WINS MATCH");
        }

        public static void HandleTeamScoreUpdateLocal(Dictionary<byte, int> totalScores, Dictionary<byte, int> roundScores)
        {
            foreach (var kvp in totalScores) _teamScores[kvp.Key] = kvp.Value;
            foreach (var kvp in roundScores) _teamRoundScores[kvp.Key] = kvp.Value;
            TeamCaptainRoundOverlay.Show();
            TeamCaptainRoundOverlay.SetScores(_teamScores, _teamRoundScores);
        }

        public static void RegisterKillHost(byte killerId, byte targetId)
        {
            if (!AmongUsClient.Instance.AmHost || !_playerToTeam.ContainsKey(killerId)) return;
            if (_roundState != TeamRoundState.Active) return;
            if (!_playerToTeam.TryGetValue(killerId, out var teamId)) return;

            _teamScores[teamId] = _teamScores.TryGetValue(teamId, out var cur) ? cur + KillPoints : KillPoints;
            _teamRoundScores[teamId] = _teamRoundScores.TryGetValue(teamId, out var curRound) ? curRound + KillPoints : KillPoints;

            DraftNetworkHelper.BroadcastTeamScoreUpdate(_teamScores, _teamRoundScores);
        }

        private static void UpdateTaskPointsHost()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            bool changed = false;
            var players = PlayerControl.AllPlayerControls.ToArray();
            foreach (var p in players)
            {
                if (p == null || p.Data == null || p.Data.Disconnected) continue;
                if (!TasksCountIfDead && p.Data.IsDead) continue;
                if (!_playerToTeam.TryGetValue(p.PlayerId, out var teamId)) continue;

                int completed = CountCompletedTasks(p);
                if (!_lastTaskCompleteCounts.TryGetValue(p.PlayerId, out var last)) last = 0;
                if (completed > last)
                {
                    int delta = completed - last;
                    int add = delta * TaskPoints;
                    _teamScores[teamId] = _teamScores.TryGetValue(teamId, out var cur) ? cur + add : add;
                    _teamRoundScores[teamId] = _teamRoundScores.TryGetValue(teamId, out var curRound) ? curRound + add : add;
                    _lastTaskCompleteCounts[p.PlayerId] = completed;
                    changed = true;
                }
            }

            if (changed)
                DraftNetworkHelper.BroadcastTeamScoreUpdate(_teamScores, _teamRoundScores);
        }

        private static void ResetRoundScores()
        {
            _teamRoundScores.Clear();
            foreach (var teamId in _teamMembers.Keys)
                _teamRoundScores[teamId] = 0;
            _lastTaskCompleteCounts.Clear();
        }

        private static void ResetTasksForAll()
        {
            var players = PlayerControl.AllPlayerControls.ToArray();
            foreach (var p in players)
            {
                if (p == null || p.Data == null || p.Data.Disconnected) continue;
                ResetTasksForPlayer(p);
            }
        }

        private static void ResetTasksForPlayer(PlayerControl player)
        {
            try
            {
                var tasks = player.myTasks;
                if (tasks == null) return;
                foreach (var t in tasks)
                {
                    SetTaskComplete(t, false);
                }
            }
            catch { }
        }

        private static void SetTaskComplete(object task, bool value)
        {
            if (task == null) return;
            var type = task.GetType();
            var field = type.GetField("Complete") ?? type.GetField("complete");
            if (field != null)
            {
                field.SetValue(task, value);
                return;
            }
            var prop = type.GetProperty("IsComplete") ?? type.GetProperty("Complete");
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(task, value, null);
            }
        }

        private static int CountCompletedTasks(PlayerControl player)
        {
            int count = 0;
            var tasks = player.myTasks;
            if (tasks == null) return 0;
            foreach (var t in tasks)
            {
                if (IsTaskComplete(t)) count++;
            }
            return count;
        }

        private static bool IsTaskComplete(object task)
        {
            if (task == null) return false;
            var type = task.GetType();
            var field = type.GetField("Complete") ?? type.GetField("complete");
            if (field != null) return field.GetValue(task) is bool b && b;
            var prop = type.GetProperty("IsComplete") ?? type.GetProperty("Complete");
            if (prop != null) return prop.GetValue(task, null) is bool b2 && b2;
            return false;
        }

        private static void ReviveAndTeleportAll()
        {
            var players = PlayerControl.AllPlayerControls.ToArray();
            Vector2 meetingPos = GetMeetingSpawn();
            foreach (var p in players)
            {
                if (p == null || p.Data == null || p.Data.Disconnected) continue;
                try
                {
                    p.Revive();
                }
                catch { }
                try
                {
                    p.NetTransform?.RpcSnapTo(meetingPos);
                }
                catch { }
            }
        }

        private static Vector2 GetMeetingSpawn()
        {
            try
            {
                var ship = ShipStatus.Instance;
                if (ship == null) return Vector2.zero;

                var prop = ship.GetType().GetProperty("MeetingSpawnCenter");
                if (prop != null)
                {
                    var val = prop.GetValue(ship, null);
                    if (val is Vector2 v2) return v2;
                    if (val is Vector3 v3) return v3;
                }

                var rooms = ship.AllRooms;
                if (rooms != null)
                {
                    foreach (var room in rooms)
                    {
                        if (room == null) continue;
                        var roomTypeProp = room.GetType().GetProperty("RoomId") ?? room.GetType().GetProperty("RoomType");
                        if (roomTypeProp != null)
                        {
                            var id = roomTypeProp.GetValue(room, null);
                            if (id != null && id.ToString().Contains("Meeting"))
                            {
                                return ((Component)room).transform.position;
                            }
                        }
                    }
                }

                return ship.transform.position;
            }
            catch
            {
                return Vector2.zero;
            }
        }

        private static List<byte> GetAliveTeams()
        {
            var alive = new HashSet<byte>();
            var players = PlayerControl.AllPlayerControls.ToArray();
            foreach (var p in players)
            {
                if (p == null || p.Data == null || p.Data.Disconnected) continue;
                if (p.Data.IsDead) continue;
                if (_playerToTeam.TryGetValue(p.PlayerId, out var team))
                    alive.Add(team);
            }
            return alive.ToList();
        }

        public static bool TryGetTeam(byte playerId, out byte teamId)
        {
            return _playerToTeam.TryGetValue(playerId, out teamId);
        }

        private static void ApplyTeamColorsLocal()
        {
            var players = PlayerControl.AllPlayerControls.ToArray();
            foreach (var p in players)
            {
                if (p == null || p.cosmetics == null) continue;
                if (!_playerToTeam.TryGetValue(p.PlayerId, out var teamId)) continue;
                try
                {
                    var color = GetTeamColor(teamId);
                    p.cosmetics.nameText.color = color;
                }
                catch { }
            }
        }

        private static string FormatSeconds(float seconds)
        {
            int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
            int m = s / 60;
            int r = s % 60;
            return $"{m:00}:{r:00}";
        }

        private static List<PlayerControl> GetLobbyPlayers()
        {
            return PlayerControl.AllPlayerControls.ToArray()
                .Where(p => p != null && p.Data != null && !p.Data.Disconnected)
                .OrderBy(p => p.PlayerId)
                .ToList();
        }

        private static void StartTeamPickAfterCaptains(List<PlayerControl> players)
        {
            _playerToTeam.Clear();
            _teamMembers.Clear();
            foreach (var cap in _captains)
                AddToTeam(cap, (byte)_captains.IndexOf(cap));

            _availablePlayers.Clear();
            _availablePlayers.AddRange(players.Select(p => p.PlayerId).Where(id => !_playerToTeam.ContainsKey(id)));

            IsTeamModeActive = true;
            _snakeIndex = 0;
            _snakeDir = 1;
            _currentPickIndex = 0;

            if (_availablePlayers.Count == 0)
            {
                EndTeamPickHost();
                return;
            }

            TeamCaptainPickOverlay.Show(_captains, _teamMembers, _availablePlayers);
            TeamCaptainPickOverlay.SetCurrentCaptain(_captains[0]);
            DraftNetworkHelper.BroadcastTeamPickStart(_captains, _teamMembers, _availablePlayers);
            StartNextPickHost();
        }
    }
}
