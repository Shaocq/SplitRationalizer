using System;
using Duckov.UI.Animations;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SplitRationalizer
{
    /// <summary>
    /// 把按下/抬起事件转给克隆按钮自己的 SizeDeltaToggle。
    ///
    /// 克隆按钮原生的 PointerDownUpEvents 是 UnityEvent 持久化绑定，
    /// 销毁后改用这个组件驱动，确保动画目标一定是克隆按钮自己的
    /// SizeDeltaToggle，不会误操作原版「分割」按钮。
    ///
    /// 动画本身仍由游戏自带的 SizeDeltaToggle 播放（DOTween + 原版缓动曲线），
    /// 因此效果与原版按钮完全一致。
    /// </summary>
    internal sealed class PressToggleBridge : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private SizeDeltaToggle _toggle;

        public void Configure(SizeDeltaToggle toggle)
        {
            _toggle = toggle;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            try
            {
                if (_toggle != null)
                    _toggle.SetToggle(true);
            }
            catch (Exception e)
            {
                SafeGuard.Report("播放按钮按下动画失败", e);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            try
            {
                if (_toggle != null)
                    _toggle.SetToggle(false);
            }
            catch (Exception e)
            {
                SafeGuard.Report("播放按钮抬起动画失败", e);
            }
        }
    }
}
