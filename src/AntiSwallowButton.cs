using System;
using System.Reflection;
using Duckov.UI.Animations;
using HarmonyLib;
using ItemStatsSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SplitRationalizer
{
    /// <summary>拆分界面打开时（SplitDialogue.Setup 之后）注入「物品防吞」按钮。</summary>
    [HarmonyPatch(typeof(SplitDialogue), "Setup")]
    internal static class SplitDialogueSetupPatch
    {
        private static void Postfix(SplitDialogue __instance)
        {
            // 无论这里发生什么都不能把异常抛回游戏
            try
            {
                AntiSwallowButton.Ensure(__instance);
            }
            catch (Exception e)
            {
                SafeGuard.Report("注入「物品防吞」按钮失败", e);
            }
        }
    }

    internal static class AntiSwallowButton
    {
        private const string RowName = "AntiSwallowRow";
        private const string ButtonName = "AntiSwallowButton";
        private const string SpacerName = "AntiSwallowSpacer";
        private const string ButtonLabel = "物品防吞";

        /// <summary>与上方按钮之间的间距。</summary>
        private const float SpacerHeight = 8f;

        private const float FallbackWidth = 334f;
        private const float FallbackHeight = 80f;

        // 绿色底色（#2EA043）
        private static readonly Color Green = new Color32(46, 160, 67, 255);

        private static readonly FieldInfo ConfirmField =
            typeof(SplitDialogue).GetField("confirmButton", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SliderField =
            typeof(SplitDialogue).GetField("slider", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo TargetField =
            typeof(SplitDialogue).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>原按钮预制体里按下/抬起的相对偏移（与 SizeDeltaToggle 的 active 值一致）。</summary>
        private static readonly Vector2 PressOffset = new Vector2(-8f, -8f);

        private static SplitDialogue _dialogue;
        private static Slider _slider;
        private static Button _button;
        private static GameObject _row;
        private static GameObject _spacer;
        private static bool _structureReported;

        /// <summary>原按钮「正常状态」的 sizeDelta，首次打开拆分界面时记录。</summary>
        private static bool _capturedIdleSize;
        private static Vector2 _idleSizeDelta;

        public static void Ensure(SplitDialogue dialogue)
        {
            try
            {
                if (dialogue == null)
                    return;

                Button confirm = ConfirmField?.GetValue(dialogue) as Button;
                Slider slider = SliderField?.GetValue(dialogue) as Slider;
                if (confirm == null || slider == null)
                {
                    // 结构真的变了才报一次，避免每次打开对话框都刷日志
                    if (!_structureReported)
                    {
                        _structureReported = true;
                        SafeGuard.Report("拆分界面结构变化：未找到确认按钮或滑条");
                    }
                    return;
                }
                _structureReported = false;

                _dialogue = dialogue;
                _slider = slider;

                RectTransform confirmRect = confirm.transform as RectTransform;
                RectTransform rowRect = confirm.transform.parent as RectTransform;                    // BtnContainer
                RectTransform contentRect = rowRect != null ? rowRect.parent as RectTransform : null; // Content

                // 原按钮「正常状态」的 sizeDelta 只在首次打开时记录：
                // 此时界面刚 Setup 完、还没有发生任何点击，拿到的一定是预制体原值
                if (!_capturedIdleSize && confirmRect != null)
                {
                    _capturedIdleSize = true;
                    _idleSizeDelta = confirmRect.sizeDelta;
                    Debug.Log($"[拆分合理化] 已记录原按钮基准尺寸：{_idleSizeDelta}");
                }

                // 修正原按钮的按下动画并复位上次点击残留的尺寸
                PrepareSizeAnimation(confirm.gameObject, false);

                // 已创建且挂载点没变，直接复用
                if (_button != null && _row != null && contentRect != null && _row.transform.parent == contentRect)
                {
                    _row.SetActive(true);
                    if (_spacer != null)
                        _spacer.SetActive(true);
                    RefreshVisuals(_button.gameObject);
                    return;
                }

                DestroyCurrent();
                Create(confirm, confirmRect, rowRect, contentRect);
            }
            catch (Exception e)
            {
                SafeGuard.Report("创建「物品防吞」按钮失败", e);
            }
        }

        private static void Create(Button confirm, RectTransform confirmRect, RectTransform rowRect, RectTransform contentRect)
        {
            // 原按钮的有效尺寸（父容器宽度 + sizeDelta）
            float buttonWidth = EffectiveSize(confirmRect, rowRect, true);
            float buttonHeight = EffectiveSize(confirmRect, rowRect, false);
            if (buttonWidth <= 1f)
                buttonWidth = FallbackWidth;
            if (buttonHeight <= 1f)
                buttonHeight = FallbackHeight;

            float rowWidth = rowRect != null && rowRect.rect.width > 1f ? rowRect.rect.width : buttonWidth;

            VerticalLayoutGroup layout = contentRect != null
                ? contentRect.GetComponent<VerticalLayoutGroup>()
                : null;

            GameObject row = null;
            GameObject clone = null;
            GameObject spacer = null;

            try
            {
                // 行容器：模拟原按钮所在的 BtnContainer，
                // 让按钮内部的拉伸锚点保持和原版一致的行为
                row = new GameObject(RowName, typeof(RectTransform), typeof(LayoutElement));
                row.SetActive(false);
                RectTransform rowTransform = (RectTransform)row.transform;
                rowTransform.sizeDelta = new Vector2(rowWidth, buttonHeight);

                // 布局模式下完全照抄原 BtnContainer 的 LayoutElement：
                // preferredWidth = -1 + flexibleWidth = 1，由 VerticalLayoutGroup
                // 拉伸到与 SliderContainer / BtnContainer 相同的宽度，
                // 按钮（stretch 锚点 + sizeDelta.x = -400）因此与原「分割」按钮等宽。
                // 这样不依赖读取 rect.width 的时机（界面未完成布局时可能还是预制体值）。
                LayoutElement rowLayout = row.GetComponent<LayoutElement>();
                rowLayout.preferredWidth = layout != null ? -1f : rowWidth;
                rowLayout.preferredHeight = buttonHeight;
                rowLayout.minWidth = -1f;
                rowLayout.minHeight = -1f;
                rowLayout.flexibleWidth = layout != null ? 1f : -1f;
                rowLayout.flexibleHeight = -1f;

                // 克隆确认按钮，配置完成后再启用
                clone = Object.Instantiate(confirm.gameObject);
                clone.name = ButtonName;
                clone.SetActive(false);

                Button button = clone.GetComponent<Button>();
                if (button == null)
                    button = clone.AddComponent<Button>();
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnClick);

                clone.transform.SetParent(row.transform, false);
                RectTransform cloneRect = (RectTransform)clone.transform;
                if (confirmRect != null)
                {
                    cloneRect.anchorMin = confirmRect.anchorMin;
                    cloneRect.anchorMax = confirmRect.anchorMax;
                    cloneRect.pivot = confirmRect.pivot;
                    cloneRect.sizeDelta = confirmRect.sizeDelta;
                    cloneRect.anchoredPosition = confirmRect.anchoredPosition;
                    cloneRect.localScale = confirmRect.localScale;
                }

                if (layout != null && contentRect != null)
                {
                    int baseIndex = rowRect != null ? rowRect.GetSiblingIndex() : contentRect.childCount - 1;

                    // 间距占位行
                    spacer = new GameObject(SpacerName, typeof(RectTransform), typeof(LayoutElement));
                    spacer.SetActive(false);
                    RectTransform spacerTransform = (RectTransform)spacer.transform;
                    spacerTransform.SetParent(contentRect, false);
                    spacerTransform.sizeDelta = new Vector2(0f, SpacerHeight);

                    LayoutElement spacerLayout = spacer.GetComponent<LayoutElement>();
                    spacerLayout.preferredWidth = -1f;
                    spacerLayout.preferredHeight = SpacerHeight;
                    spacerLayout.minWidth = -1f;
                    spacerLayout.minHeight = -1f;
                    spacerLayout.flexibleWidth = -1f;
                    spacerLayout.flexibleHeight = -1f;
                    spacerTransform.SetSiblingIndex(baseIndex + 1);

                    // 按钮行紧跟在占位行后面 → 排在原「分割」按钮下方
                    row.transform.SetParent(contentRect, false);
                    row.transform.SetSiblingIndex(baseIndex + 2);
                }
                else
                {
                    // 兜底：界面没有 VerticalLayoutGroup 时手动定位。
                    // row 必须和 BtnContainer 同级（都挂在 Content 下），
                    // 才能直接沿用 BtnContainer 的 anchoredPosition。
                    if (contentRect == null || rowRect == null)
                        throw new InvalidOperationException("未找到 Content / BtnContainer，无法确定按钮位置");

                    row.transform.SetParent(contentRect, false);
                    row.transform.SetAsLastSibling();

                    rowTransform.anchorMin = new Vector2(0f, 1f);
                    rowTransform.anchorMax = new Vector2(0f, 1f);
                    rowTransform.pivot = new Vector2(0.5f, 0.5f);
                    rowTransform.sizeDelta = new Vector2(rowWidth, buttonHeight);
                    rowTransform.anchoredPosition =
                        rowRect.anchoredPosition + new Vector2(0f, -(buttonHeight + SpacerHeight));

                    // 只加高一次（row 在 BtnContainer 外，不能重复加）
                    contentRect.sizeDelta = new Vector2(
                        contentRect.sizeDelta.x,
                        contentRect.sizeDelta.y + buttonHeight + SpacerHeight);
                }

                RefreshVisuals(clone);

                row.SetActive(true);
                if (spacer != null)
                    spacer.SetActive(true);
                clone.SetActive(true);

                _row = row;
                _button = button;
                _spacer = spacer;

                // Content 上有 VerticalLayoutGroup + ContentSizeFitter，
                // 新增一行后高度会自动增加；主动标记一次让它立刻生效
                if (contentRect != null)
                    LayoutRebuilder.MarkLayoutForRebuild(contentRect);
            }
            catch
            {
                // 创建中途失败：回收已经生成的对象，避免残留在界面里
                if (row != null)
                    Object.Destroy(row);
                if (spacer != null)
                    Object.Destroy(spacer);
                if (clone != null)
                    Object.Destroy(clone);

                _row = null;
                _button = null;
                _spacer = null;
                throw;
            }
        }

        /// <summary>
        /// 修正按下动画的尺寸参数。
        ///
        /// 预制体上的 SizeDeltaToggle 保存的是绝对尺寸：idle=(0,0)、active=(-8,-8)，
        /// 而按钮实际的 sizeDelta 是 (-400,0)。按下放大后，抬起会被补间到 (0,0)，
        /// 按钮变成父容器整行宽且永远回不去。这里把两个值改成基于基准尺寸的相对值，
        /// 并把上次点击残留的尺寸复位。
        ///
        /// addBridge = true 时用于克隆按钮：它原生的 PointerDownUpEvents 已被销毁，
        /// 改由 PressToggleBridge 驱动动画，保证不会误操作原按钮。
        /// </summary>
        private static void PrepareSizeAnimation(GameObject go, bool addBridge)
        {
            try
            {
                if (go == null || !_capturedIdleSize)
                    return;

                RectTransform rect = go.transform as RectTransform;
                if (rect != null)
                    rect.sizeDelta = _idleSizeDelta;

                SizeDeltaToggle toggle = go.GetComponent<SizeDeltaToggle>();
                if (toggle == null)
                    return;

                toggle.idleSizeDelta = _idleSizeDelta;
                toggle.activeSizeDelta = _idleSizeDelta + PressOffset;

                // 让状态回到 idle：SetToggle 内部会先 DOKill，顺便清掉可能残留的补间
                toggle.SetToggle(false);

                if (addBridge)
                {
                    PressToggleBridge bridge = go.GetComponent<PressToggleBridge>();
                    if (bridge == null)
                        bridge = go.AddComponent<PressToggleBridge>();
                    bridge.Configure(toggle);
                }
            }
            catch (Exception e)
            {
                SafeGuard.Report("修正按钮的按下动画失败", e);
            }
        }

        /// <summary>计算 RectTransform 在父容器中的实际尺寸（考虑拉伸锚点）。</summary>
        private static float EffectiveSize(RectTransform rect, RectTransform parent, bool horizontal)
        {
            if (rect == null)
                return 0f;

            float size = horizontal ? rect.sizeDelta.x : rect.sizeDelta.y;
            if (parent == null)
                return size;

            float min = horizontal ? rect.anchorMin.x : rect.anchorMin.y;
            float max = horizontal ? rect.anchorMax.x : rect.anchorMax.y;
            float parentSize = horizontal ? parent.rect.width : parent.rect.height;
            return (max - min) * parentSize + size;
        }

        private static void RefreshVisuals(GameObject go)
        {
            // 克隆体上这两个组件必须去掉：
            //   TextLocalizor       —— 会把文字改回「分割」，且切换语言时即使禁用也会刷新；
            //   PointerDownUpEvents —— 改由 PressToggleBridge 驱动，确保动画目标一定是
            //                          克隆按钮自己的 SizeDeltaToggle，不会误操作原按钮。
            // SizeDeltaToggle 保留：动画仍由游戏原组件播放（DOTween + 原版缓动曲线），
            // 只需在 PrepareSizeAnimation 里把尺寸参数修正为相对值。
            // 先禁用再销毁（Destroy 到帧末才生效，禁用可立即阻止事件）。
            foreach (MonoBehaviour behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                    continue;

                string typeName = behaviour.GetType().Name;
                if (typeName == "TextLocalizor" || typeName == "PointerDownUpEvents")
                {
                    behaviour.enabled = false;
                    Object.Destroy(behaviour);
                }
            }

            PrepareSizeAnimation(go, true);

            Image image = go.GetComponent<Image>();
            if (image == null)
                image = go.GetComponentInChildren<Image>(true);
            if (image != null)
                image.color = Green;

            Button button = go.GetComponent<Button>();
            if (button != null)
            {
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.92f, 1f, 0.92f, 1f);
                colors.pressedColor = new Color(0.8f, 0.92f, 0.8f, 1f);
                colors.selectedColor = Color.white;
                button.colors = colors;
            }

            // 白色文字
            TMP_Text text = go.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = ButtonLabel;
                text.color = Color.white;
            }
        }

        private static void OnClick()
        {
            try
            {
                if (_dialogue == null || _slider == null)
                    return;

                Item target = TargetField?.GetValue(_dialogue) as Item;
                if (target == null)
                    return;

                int vanillaMax = StackLimitProvider.GetVanillaMaxStack(target);
                if (vanillaMax <= 0)
                {
                    SafeGuard.Report($"未能获取「{target.DisplayName}」的原版堆叠上限");
                    return;
                }

                // 当前堆叠数小于原版上限时，最多只能拖到当前堆叠数
                _slider.value = Mathf.Clamp(vanillaMax, _slider.minValue, _slider.maxValue);
            }
            catch (Exception e)
            {
                SafeGuard.Report("「物品防吞」按钮执行失败", e);
            }
        }

        /// <summary>mod 被禁用时清掉注入的按钮。</summary>
        public static void Destroy()
        {
            try
            {
                DestroyCurrent();
            }
            catch (Exception e)
            {
                SafeGuard.Report("清理「物品防吞」按钮失败", e);
            }

            _slider = null;
            _dialogue = null;
        }

        private static void DestroyCurrent()
        {
            // 先隐藏再销毁，避免 Destroy 延迟到帧末期间布局里短暂出现两份
            if (_row != null)
            {
                _row.SetActive(false);
                Object.Destroy(_row);
            }
            else if (_button != null)
            {
                _button.gameObject.SetActive(false);
                Object.Destroy(_button.gameObject);
            }

            if (_spacer != null)
            {
                _spacer.SetActive(false);
                Object.Destroy(_spacer);
            }

            _row = null;
            _spacer = null;
            _button = null;
        }
    }
}
