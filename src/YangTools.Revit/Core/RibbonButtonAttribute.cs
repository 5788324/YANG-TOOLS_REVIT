using System;

namespace YangTools.Revit.Core
{
    /// <summary>
    /// 自定义特性，用于标记需要自动注册为 Ribbon 按钮的 Revit 外部命令
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class RibbonButtonAttribute : Attribute
    {
        /// <summary>
        /// 面板名称（如：通用工具、几何工具）
        /// </summary>
        public string PanelName { get; }

        /// <summary>
        /// 按钮上显示的文本
        /// </summary>
        public string ButtonText { get; }

        /// <summary>
        /// 鼠标悬停时的提示文本
        /// </summary>
        public string Tooltip { get; }

        /// <summary>详细使用步骤与适用边界，显示在 Ribbon 扩展提示中。</summary>
        public string Usage { get; }

        /// <summary>
        /// 32x32 大图标路径（可以是程序集嵌入资源，也可以是本地绝对路径，支持以 "pack://application:,,," 开头的 URI）
        /// </summary>
        public string? LargeIcon { get; }

        /// <summary>
        /// 16x16 小图标路径
        /// </summary>
        public string? SmallIcon { get; }

        /// <summary>
        /// 按钮分组名。如果设置，同一分组的按钮会被放进同一个下拉菜单中。
        /// </summary>
        public string? GroupName { get; }

        /// <summary>
        /// 分组下拉按钮的 32x32 图标。为空时回退到组内第一个命令的大图标。
        /// </summary>
        public string? GroupLargeIcon { get; }

        /// <summary>
        /// 分组下拉按钮的 16x16 图标。为空时回退到组内第一个命令的小图标。
        /// </summary>
        public string? GroupSmallIcon { get; }

        /// <summary>
        /// 是否放在面板底部的扩展区域 (SlideOut)
        /// </summary>
        public bool IsSlideOut { get; }

        /// <summary>
        /// Ribbon 显示顺序。数值越小越靠前；同序号再按按钮文字排序。
        /// </summary>
        public int Order { get; }

        public RibbonButtonAttribute(
            string panelName, 
            string buttonText, 
            string tooltip = "", 
            string? largeIcon = null, 
            string? smallIcon = null,
            string? groupName = null,
            bool isSlideOut = false,
            int order = 1000,
            string? groupLargeIcon = null,
            string? groupSmallIcon = null,
            string usage = "")
        {
            PanelName = panelName;
            ButtonText = buttonText;
            Tooltip = tooltip;
            Usage = usage;
            LargeIcon = largeIcon;
            SmallIcon = smallIcon;
            GroupName = groupName;
            GroupLargeIcon = groupLargeIcon;
            GroupSmallIcon = groupSmallIcon;
            IsSlideOut = isSlideOut;
            Order = order;
        }
    }
}
