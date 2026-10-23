using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SplitRationalizer
{
    /// <summary>
    /// 全局异常兜底。
    ///
    /// 原则：本 mod 的任何异常都不允许抛回游戏或其他 mod 的调用栈。
    /// 记录完整日志，同时在屏幕顶部显示一句提示，5 秒后自动消失。
    /// 提示本身也全部包在 try/catch 里，最坏情况只留下日志。
    /// </summary>
    internal static class SafeGuard
    {
        /// <summary>同类提示的冷却时间，避免刷屏。</summary>
        private const float NoticeCooldown = 10f;

        /// <summary>同类日志的冷却时间，避免重试场景下每 2 秒写一条完整堆栈。</summary>
        private const float LogCooldown = 30f;

        private static float _lastNoticeTime = -999f;

        private static readonly Dictionary<string, float> LastLogTime =
            new Dictionary<string, float>();

        public static void Report(string context, Exception exception)
        {
            WriteLog(context, $"[拆分合理化] {context}：{exception}");
            ShowNotice();
        }

        public static void Report(string context)
        {
            WriteLog(context, $"[拆分合理化] {context}");
            ShowNotice();
        }

        /// <summary>按上下文限流写日志；日志失败也不允许抛异常。</summary>
        private static void WriteLog(string context, string message)
        {
            try
            {
                float now = Time.realtimeSinceStartup;
                if (LastLogTime.TryGetValue(context, out float last) && now - last < LogCooldown)
                    return;

                LastLogTime[context] = now;
                Debug.LogError(message);
            }
            catch
            {
                // 连日志都写不了就没办法了
            }
        }

        private static void ShowNotice()
        {
            try
            {
                float now = Time.realtimeSinceStartup;
                if (now - _lastNoticeTime < NoticeCooldown)
                    return;
                _lastNoticeTime = now;

                ModNoticeBehaviour.Show("拆分合理化 mod 出现问题，详见日志");
            }
            catch (Exception e)
            {
                try
                {
                    Debug.LogError($"[拆分合理化] 显示错误提示失败：{e}");
                }
                catch
                {
                }
            }
        }
    }

    /// <summary>屏幕顶部的错误提示条，显示 5 秒后自毁。</summary>
    internal sealed class ModNoticeBehaviour : MonoBehaviour
    {
        private const float ShowSeconds = 5f;

        public static void Show(string message)
        {
            GameObject go = null;
            try
            {
                go = new GameObject("SplitRationalizerNotice");
                UnityEngine.Object.DontDestroyOnLoad(go);
                ModNoticeBehaviour behaviour = go.AddComponent<ModNoticeBehaviour>();
                behaviour.Build(message);
            }
            catch (Exception e)
            {
                try
                {
                    if (go != null)
                        UnityEngine.Object.Destroy(go);
                }
                catch
                {
                }

                try
                {
                    Debug.LogError($"[拆分合理化] 创建错误提示失败：{e}");
                }
                catch
                {
                }
            }
        }

        private void Build(string message)
        {
            try
            {
                Canvas canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 30000;

                GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(transform, false);
                RectTransform panelRect = (RectTransform)panel.transform;
                panelRect.anchorMin = new Vector2(0.5f, 1f);
                panelRect.anchorMax = new Vector2(0.5f, 1f);
                panelRect.pivot = new Vector2(0.5f, 1f);
                panelRect.anchoredPosition = new Vector2(0f, -24f);
                panelRect.sizeDelta = new Vector2(760f, 64f);

                Image background = panel.GetComponent<Image>();
                background.color = new Color(0.15f, 0.15f, 0.15f, 0.92f);
                background.raycastTarget = false;

                GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
                textObject.transform.SetParent(panel.transform, false);
                RectTransform textRect = (RectTransform)textObject.transform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(12f, 4f);
                textRect.offsetMax = new Vector2(-12f, -4f);

                Text text = textObject.GetComponent<Text>();
                text.font = GetFont();
                text.fontSize = 22;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.raycastTarget = false;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.text = message;

                StartCoroutine(HideAfterDelay());
            }
            catch (Exception e)
            {
                try
                {
                    Debug.LogError($"[拆分合理化] 构建错误提示失败：{e}");
                }
                catch
                {
                }

                try
                {
                    UnityEngine.Object.Destroy(gameObject);
                }
                catch
                {
                }
            }
        }

        private static Font _cachedFont;

        private static Font GetFont()
        {
            // 字体创建一次后复用，避免每次提示都新建并泄漏一份字体资源
            if (_cachedFont != null)
                return _cachedFont;

            // 动态系统字体，保证中文可显示（Windows 自带微软雅黑/黑体）
            try
            {
                _cachedFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei", "微软雅黑", "SimHei", "黑体", "Arial" }, 22);
                if (_cachedFont != null)
                    return _cachedFont;
            }
            catch
            {
            }

            try
            {
                _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_cachedFont != null)
                    return _cachedFont;
            }
            catch
            {
            }

            try
            {
                _cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch
            {
                _cachedFont = null;
            }

            return _cachedFont;
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSecondsRealtime(ShowSeconds);

            try
            {
                UnityEngine.Object.Destroy(gameObject);
            }
            catch
            {
            }
        }
    }
}
