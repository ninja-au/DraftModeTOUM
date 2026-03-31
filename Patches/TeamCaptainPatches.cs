using DraftModeTOUM.DraftTypes;
using HarmonyLib;
using UnityEngine;
using System.Linq;

namespace DraftModeTOUM.Patches
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
    public static class TeamCaptainKillPatch
    {
        public static bool Prefix(PlayerControl __instance, PlayerControl target)
        {
            if (!TeamCaptainDraftType.IsTeamModeActive) return true;
            if (TeamCaptainDraftType.FriendlyFire) return true;
            if (__instance == null || target == null) return true;

            TryForceImpostor(__instance);

            if (TeamCaptainDraftType.TryGetTeam(__instance.PlayerId, out var t1) &&
                TeamCaptainDraftType.TryGetTeam(target.PlayerId, out var t2) &&
                t1 == t2)
            {
                return false;
            }

            return true;
        }

        public static void Postfix(PlayerControl __instance, PlayerControl target)
        {
            if (!TeamCaptainDraftType.IsTeamModeActive) return;
            if (!AmongUsClient.Instance.AmHost) return;
            if (__instance == null || target == null) return;
            if (target.Data == null) return;
            if (!target.Data.IsDead) return;

            TeamCaptainDraftType.RegisterKillHost(__instance.PlayerId, target.PlayerId);
        }

        private static void TryForceImpostor(PlayerControl player)
        {
            try
            {
                if (player?.Data == null) return;
                var data = player.Data;
                var prop = AccessTools.Property(data.GetType(), "IsImpostor");
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(data, true, null);
                    return;
                }
                var field = AccessTools.Field(data.GetType(), "IsImpostor");
                if (field != null && field.FieldType == typeof(bool))
                {
                    field.SetValue(data, true);
                }
            }
            catch { }
        }
    }

    [HarmonyPatch]
public static class TeamCaptainKillButtonPatch
{
    private static bool Prepare()
    {
        return AccessTools.Method(typeof(PlayerControl), "CanUseKillButton") != null
            || AccessTools.Method(typeof(PlayerControl), "CanKill") != null
            || AccessTools.Method(typeof(PlayerControl), "CanUseKill") != null;
    }

    private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        var list = new System.Collections.Generic.List<System.Reflection.MethodBase>();
        var m = AccessTools.Method(typeof(PlayerControl), "CanUseKillButton");
        if (m != null) list.Add(m);
        m = AccessTools.Method(typeof(PlayerControl), "CanKill");
        if (m != null) list.Add(m);
        m = AccessTools.Method(typeof(PlayerControl), "CanUseKill");
        if (m != null) list.Add(m);
        return list;
    }

    public static void Postfix(PlayerControl __instance, ref bool __result)
    {
        if (!TeamCaptainDraftType.IsTeamModeActive) return;
        __result = true;
    }
}
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    public static class TeamCaptainHudKillButtonPatch
    {
        public static void Postfix(HudManager __instance)
        {
            if (!TeamCaptainDraftType.IsTeamModeActive) return;
            if (__instance == null) return;
            try
            {
                var killButton = __instance.KillButton;
                if (killButton == null) return;
                killButton.gameObject.SetActive(true);
                var setEnabled = AccessTools.Method(killButton.GetType(), "SetEnabled");
                if (setEnabled != null)
                {
                    setEnabled.Invoke(killButton, new object[] { true });
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}


    [HarmonyPatch]
    public static class TeamCaptainRoleKillPermissionPatch
    {
        private static bool Prepare()
        {
            return AccessTools.Method(typeof(RoleBehaviour), "CanUseKillButton") != null
                || AccessTools.Method(typeof(RoleBehaviour), "CanKill") != null
                || AccessTools.Method(typeof(RoleBehaviour), "CanUseKill") != null;
        }

        private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            var list = new System.Collections.Generic.List<System.Reflection.MethodBase>();
            var m = AccessTools.Method(typeof(RoleBehaviour), "CanUseKillButton");
            if (m != null) list.Add(m);
            m = AccessTools.Method(typeof(RoleBehaviour), "CanKill");
            if (m != null) list.Add(m);
            m = AccessTools.Method(typeof(RoleBehaviour), "CanUseKill");
            if (m != null) list.Add(m);
            return list;
        }

        public static void Postfix(RoleBehaviour __instance, ref bool __result)
        {
            if (!TeamCaptainDraftType.IsTeamModeActive) return;
            __result = true;
        }
    }

    [HarmonyPatch]
    public static class TeamCaptainKillButtonClickPatch
    {
        private enum KillArgKind
        {
            PlayerControl,
            NetworkedPlayerInfo,
            PlayerIdByte,
            PlayerIdInt
        }

        private static System.Reflection.MethodInfo _killMethod;
        private static KillArgKind _killArgKind;
        private static System.Reflection.MethodInfo _rpcKillMethod;
        private static KillArgKind _rpcKillArgKind;
        private static System.Reflection.FieldInfo _impostorField;
        private static System.Reflection.PropertyInfo _impostorProp;
        private static bool _resolved;

        private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            var list = new System.Collections.Generic.List<System.Reflection.MethodBase>();
            var m = AccessTools.Method(typeof(KillButton), "DoClick");
            if (m != null) list.Add(m);
            m = AccessTools.Method(typeof(KillButton), "PerformKill");
            if (m != null) list.Add(m);
            m = AccessTools.Method(typeof(KillButton), "Click");
            if (m != null) list.Add(m);
            return list;
        }

        public static bool Prefix(KillButton __instance)
        {
            if (!TeamCaptainDraftType.IsTeamModeActive) return true;
            var local = PlayerControl.LocalPlayer;
            if (local == null || __instance == null) return true;

            var target = TryGetKillTarget(__instance);
            if (target == null)
            {
                target = FindNearestValidTarget(local);
            }
            if (target == null) return true;
            if (target.Data == null || target.Data.IsDead) return true;

            if (!TeamCaptainDraftType.FriendlyFire &&
                TeamCaptainDraftType.TryGetTeam(local.PlayerId, out var t1) &&
                TeamCaptainDraftType.TryGetTeam(target.PlayerId, out var t2) &&
                t1 == t2)
            {
                return false;
            }

            ResolveKillReflection(local);
            var wasImpostor = GetImpostor(local);
            ForceImpostor(local, true);
            TryKill(local, target);
            ForceImpostor(local, wasImpostor);
            return false;
        }

        private static PlayerControl TryGetKillTarget(KillButton btn)
        {
            try
            {
                var type = btn.GetType();
                var f = AccessTools.Field(type, "currentTarget") ?? AccessTools.Field(type, "CurrentTarget");
                if (f != null) return f.GetValue(btn) as PlayerControl;
                var p = AccessTools.Property(type, "CurrentTarget");
                if (p != null) return p.GetValue(btn, null) as PlayerControl;
                var m = AccessTools.Method(type, "GetTarget");
                if (m != null) return m.Invoke(btn, null) as PlayerControl;
            }
            catch { }
            return null;
        }

        private static PlayerControl FindNearestValidTarget(PlayerControl local)
        {
            try
            {
                var players = PlayerControl.AllPlayerControls.ToArray();
                PlayerControl best = null;
                float bestDist = float.MaxValue;
                Vector2 localPos = local.transform.position;
                foreach (var p in players)
                {
                    if (p == null || p.Data == null || p.Data.Disconnected) continue;
                    if (p.PlayerId == local.PlayerId) continue;
                    if (p.Data.IsDead) continue;
                    if (!TeamCaptainDraftType.FriendlyFire &&
                        TeamCaptainDraftType.TryGetTeam(local.PlayerId, out var t1) &&
                        TeamCaptainDraftType.TryGetTeam(p.PlayerId, out var t2) &&
                        t1 == t2)
                    {
                        continue;
                    }

                    Vector2 pos = p.transform.position;
                    float d = Vector2.Distance(localPos, pos);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = p;
                    }
                }

                // basic range clamp to avoid cross-map kills
                if (bestDist > 2.0f) return null;
                return best;
            }
            catch
            {
                return null;
            }
        }

        private static void TryKill(PlayerControl killer, PlayerControl target)
        {
            try
            {
                if (TryInvokeByName(killer, "CmdCheckMurder", target)) return;
                if (TryInvokeByName(killer, "CheckMurder", target)) return;
                if (TryInvokeByName(killer, "RpcCheckMurder", target)) return;
                if (TryInvokeByName(killer, "RpcMurderPlayer", target)) return;
                if (TryInvokeByName(killer, "MurderPlayer", target)) return;

                if (_rpcKillMethod == null)
                {
                    (_rpcKillMethod, _rpcKillArgKind) = FindKillMethod(killer.GetType(), preferRpc: true);
                }
                if (_rpcKillMethod != null)
                {
                    _rpcKillMethod.Invoke(killer, new object[] { BuildKillArg(_rpcKillArgKind, target) });
                    return;
                }
                if (_killMethod == null)
                {
                    (_killMethod, _killArgKind) = FindKillMethod(killer.GetType(), preferRpc: false);
                }
                if (_killMethod != null)
                {
                    _killMethod.Invoke(killer, new object[] { BuildKillArg(_killArgKind, target) });
                }
            }
            catch { }
        }

        private static bool TryInvokeByName(PlayerControl killer, string name, PlayerControl target)
        {
            try
            {
                var methods = killer.GetType().GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                    .Where(m => string.Equals(m.Name, name, System.StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                foreach (var m in methods)
                {
                    var args = BuildArgsForMethod(m, target);
                    if (args == null) continue;
                    m.Invoke(killer, args);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static void ForceImpostor(PlayerControl player, bool value)
        {
            try
            {
                if (player?.Data == null) return;
                var data = player.Data;
                ResolveImpostorReflection(data.GetType());
                if (_impostorProp != null && _impostorProp.CanWrite)
                {
                    _impostorProp.SetValue(data, value, null);
                    return;
                }
                if (_impostorField != null && _impostorField.FieldType == typeof(bool))
                {
                    _impostorField.SetValue(data, value);
                }
            }
            catch { }
        }

        private static bool GetImpostor(PlayerControl player)
        {
            try
            {
                if (player?.Data == null) return false;
                var data = player.Data;
                ResolveImpostorReflection(data.GetType());
                if (_impostorProp != null && _impostorProp.CanRead)
                {
                    return (bool)_impostorProp.GetValue(data, null);
                }
                if (_impostorField != null && _impostorField.FieldType == typeof(bool))
                {
                    return (bool)_impostorField.GetValue(data);
                }
            }
            catch { }
            return false;
        }

        private static void ResolveKillReflection(PlayerControl local)
        {
            if (_resolved) return;
            _resolved = true;
            try
            {
                (_rpcKillMethod, _rpcKillArgKind) = FindKillMethod(local.GetType(), preferRpc: true);
                (_killMethod, _killArgKind) = FindKillMethod(local.GetType(), preferRpc: false);
            }
            catch { }
        }

        private static (System.Reflection.MethodInfo method, KillArgKind argKind) FindKillMethod(System.Type type, bool preferRpc)
        {
            try
            {
                var methods = type.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                    .Where(m => m.ReturnType == typeof(void))
                    .Where(m =>
                        m.Name.IndexOf("Kill", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        m.Name.IndexOf("Murder", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                if (methods.Count == 0) return (null, KillArgKind.PlayerControl);

                if (preferRpc)
                {
                    var rpc = methods.FirstOrDefault(m => m.Name.IndexOf("Rpc", System.StringComparison.OrdinalIgnoreCase) >= 0);
                    if (rpc != null) return (rpc, GetArgKind(rpc));
                }

                return (methods[0], GetArgKind(methods[0]));
            }
            catch
            {
                return (null, KillArgKind.PlayerControl);
            }
        }

        private static KillArgKind GetArgKind(System.Reflection.MethodInfo method)
        {
            try
            {
                var parameters = method.GetParameters();
                if (parameters.Length < 1) return KillArgKind.PlayerControl;
                var p = parameters[0].ParameterType;
                if (typeof(PlayerControl).IsAssignableFrom(p)) return KillArgKind.PlayerControl;
                if (typeof(NetworkedPlayerInfo).IsAssignableFrom(p)) return KillArgKind.NetworkedPlayerInfo;
                if (p == typeof(byte) || p == typeof(sbyte)) return KillArgKind.PlayerIdByte;
                if (p == typeof(int) || p == typeof(uint) || p == typeof(short) || p == typeof(ushort)) return KillArgKind.PlayerIdInt;
            }
            catch { }
            return KillArgKind.PlayerControl;
        }

        private static object BuildKillArg(KillArgKind kind, PlayerControl target)
        {
            return kind switch
            {
                KillArgKind.NetworkedPlayerInfo => target.Data,
                KillArgKind.PlayerIdByte => target.PlayerId,
                KillArgKind.PlayerIdInt => (int)target.PlayerId,
                _ => target
            };
        }

        private static object[] BuildArgsForMethod(System.Reflection.MethodInfo method, PlayerControl target)
        {
            try
            {
                var ps = method.GetParameters();
                if (ps.Length == 1)
                {
                    var first = BuildArgForType(ps[0].ParameterType, target);
                    if (first == null) return null;
                    return new object[] { first };
                }
                if (ps.Length == 2)
                {
                    var first = BuildArgForType(ps[0].ParameterType, target);
                    if (first == null) return null;
                    var second = GetDefaultValue(ps[1].ParameterType);
                    return new object[] { first, second };
                }
            }
            catch { }
            return null;
        }

        private static object BuildArgForType(System.Type type, PlayerControl target)
        {
            if (type == null) return null;
            if (typeof(PlayerControl).IsAssignableFrom(type)) return target;
            if (typeof(NetworkedPlayerInfo).IsAssignableFrom(type)) return target.Data;
            if (type == typeof(byte) || type == typeof(sbyte)) return target.PlayerId;
            if (type == typeof(int) || type == typeof(uint) || type == typeof(short) || type == typeof(ushort)) return (int)target.PlayerId;
            return null;
        }

        private static object GetDefaultValue(System.Type type)
        {
            try
            {
                if (type == typeof(bool)) return true;
                if (type == typeof(byte)) return (byte)0;
                if (type == typeof(sbyte)) return (sbyte)0;
                if (type == typeof(short)) return (short)0;
                if (type == typeof(ushort)) return (ushort)0;
                if (type == typeof(int)) return 0;
                if (type == typeof(uint)) return 0u;
                if (type == typeof(float)) return 0f;
                if (type == typeof(double)) return 0d;
                if (type.IsEnum) return System.Enum.ToObject(type, 0);
                return System.Activator.CreateInstance(type);
            }
            catch
            {
                return null;
            }
        }

        private static void ResolveImpostorReflection(System.Type dataType)
        {
            if (_impostorField != null || _impostorProp != null) return;
            try
            {
                _impostorProp = dataType.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                    .FirstOrDefault(p => p.PropertyType == typeof(bool) && p.Name.IndexOf("Impostor", System.StringComparison.OrdinalIgnoreCase) >= 0);
                _impostorField = dataType.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                    .FirstOrDefault(f => f.FieldType == typeof(bool) && f.Name.IndexOf("Impostor", System.StringComparison.OrdinalIgnoreCase) >= 0);
            }
            catch { }
        }
    }


