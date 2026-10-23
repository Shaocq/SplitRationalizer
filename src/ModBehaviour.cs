using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SplitRationalizer
{
    /// <summary>
    /// 拆分合理化。
    ///
    /// 功能：
    ///   1. 拆分界面在「分割」按钮下方加一个「物品防吞」按钮（绿色底色、白色文字、长度一致）；
    ///      点击后把拆分数量设为该物品的原版堆叠上限。
    ///   2. 启动时把游戏本体所有物品的默认叠加上限导出到 mod 目录的 vanilla_stack_limits.csv。
    ///
    /// 前置：必须启用「HarmonyLib」（工坊 3589088839 / HarmonyLoadMod）。
    ///
    /// 稳定性：所有入口（生命周期、Harmony 回调、按钮点击）都做了异常隔离，
    /// 任何错误只记录日志 + 屏幕提示 5 秒，不会影响游戏或其他 mod。
    /// </summary>
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private const string HarmonyId = "com.splitrationalizer";
        private const float RetryInterval = 2f;

        /// <summary>前置已就绪但补丁应用失败时的重试间隔（避免每 2 秒刷日志）。</summary>
        private const float PatchFailureBackoff = 10f;

        private static bool _patched;
        private static bool _patchAttempted;
        private static string _modDirectory = ".";

        private Coroutine _retryCoroutine;
        private bool _warnedMissingHarmony;

        protected override void OnAfterSetup()
        {
            try
            {
                string directory = info.path;
                if (string.IsNullOrEmpty(directory))
                    directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
                _modDirectory = directory;

                int count = StackLimitProvider.DumpVanillaStackLimits(directory);
                Debug.Log($"[拆分合理化] 已导出 {count} 条本体物品的叠加上限");
            }
            catch (Exception e)
            {
                SafeGuard.Report("导出本体物品叠加上限失败", e);
            }
        }

        // Unity 生命周期消息：ModBehaviour 继承 MonoBehaviour，必须保留为实例方法
#pragma warning disable CA1822
        private void OnEnable()
        {
            try
            {
                TryPatch();
            }
            catch (Exception e)
            {
                SafeGuard.Report("mod 启用失败", e);
            }
        }

        private void OnDisable()
        {
            try
            {
                if (_retryCoroutine != null)
                {
                    StopCoroutine(_retryCoroutine);
                    _retryCoroutine = null;
                }

                _warnedMissingHarmony = false;
                AntiSwallowButton.Destroy();
                Unpatch();
            }
            catch (Exception e)
            {
                SafeGuard.Report("mod 禁用时清理失败", e);
            }
        }
#pragma warning restore CA1822

        /// <summary>0Harmony 就绪就立刻打补丁；没就绪就开重试协程。</summary>
        private void TryPatch()
        {
            if (_patched)
                return;

            if (HarmonyLoad.Load0Harmony() != null)
            {
                ApplyPatches();
                if (_patched)
                    return;

                // 补丁应用失败时不能就此放弃，继续走重试协程
            }

            // 前置可能排在后面才加载，或者玩家刚启用还没轮到它
            if (_retryCoroutine == null)
                _retryCoroutine = StartCoroutine(WaitForHarmony());
        }

        /// <summary>
        /// 定时检查前置是否已经把 0Harmony 载入进程。
        /// 本方法刻意不引用 HarmonyLib 的类型：找到之后再调用 ApplyPatches，
        /// 避免程序集尚未存在时 JIT 解析失败。
        /// </summary>
        private IEnumerator WaitForHarmony()
        {
            float wait = RetryInterval;

            while (!_patched)
            {
                yield return new WaitForSecondsRealtime(wait);
                wait = RetryInterval;

                try
                {
                    if (HarmonyLoad.Load0Harmony() != null)
                    {
                        ApplyPatches();
                        if (_patched)
                            break;

                        // 补丁失败时 ApplyPatches 已经报告过原因，继续重试即可，
                        // 不能误报成“找不到前置”；拉长间隔避免日志刷屏
                        wait = PatchFailureBackoff;
                    }
                    else if (!_warnedMissingHarmony)
                    {
                        _warnedMissingHarmony = true;
                        SafeGuard.Report("尚未找到 0Harmony，请启用前置「HarmonyLib」（工坊 3589088839）并置于本 mod 之前");
                    }
                }
                catch (Exception e)
                {
                    SafeGuard.Report("等待 Harmony 前置失败", e);
                }
            }

            _retryCoroutine = null;
        }

        /// <summary>引用 HarmonyLib 的代码集中在这里，必须在 0Harmony 加载后调用。</summary>
        private static void ApplyPatches()
        {
            try
            {
                // 从这一刻起可能已经动过补丁，禁用时必须尝试清理
                _patchAttempted = true;

                Harmony harmony = new Harmony(HarmonyId);

                // 先清掉上一次可能残留或只应用了一半的补丁，避免重复叠加
                harmony.UnpatchAll(HarmonyId);
                harmony.PatchAll(typeof(ModBehaviour).Assembly);

                _patched = true;
                Debug.Log("[拆分合理化] Harmony 补丁已应用");
            }
            catch (Exception e)
            {
                SafeGuard.Report("应用 Harmony 补丁失败", e);
            }
        }

        private static void Unpatch()
        {
            // 只要尝试过打补丁就执行清理：
            // PatchAll 可能只应用了一半就抛异常，此时 _patched 仍是 false
            if (!_patchAttempted)
                return;

            try
            {
                new Harmony(HarmonyId).UnpatchAll(HarmonyId);
                _patchAttempted = false;
            }
            catch (Exception e)
            {
                SafeGuard.Report("移除 Harmony 补丁失败", e);
            }
            finally
            {
                _patched = false;
            }
        }

        public static string ModDirectory => _modDirectory;
    }
}
