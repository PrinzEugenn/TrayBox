using System;
using System.Globalization;

internal enum TextKey
{
    StartupFailed,
    DragUnavailable,
    DragUnknown,
    TrayIcons,
    Done,
    Organize,
    DoneOrganizing,
    OrganizeIcons,
    OrganizeHint,
    TileHint,
    Settings,
    AppearanceAndStartup,
    AppearanceLinkHint,
    TrayIconsLinkHint,
    TrayIconsHint,
    SearchIcons,
    Refresh,
    Refreshing,
    AppIcon,
    PartialRegistration,
    Collected,
    Available,
    Inspecting,
    InspectFailed,
    RegistrationMissing,
    RegistrationNeeded,
    NoMatchingIcons,
    Unavailable,
    PendingSave,
    UnableToRecheck,
    WaitingForRegistration,
    RegistrationPartlyDenied,
    RegistrationDenied,
    RegistrationNotDelivered,
    RegistrationStillIncomplete,
    TrayTools,
    OpenTrayBox,
    RefreshTrayIcons,
    ExitAndRestore,
    SettingsSaved,
    SettingsSaveFailed,
    AppearanceHint,
    Appearance,
    SystemDefault,
    Light,
    Dark,
    Theme,
    ShowNames,
    SystemAcrylic,
    AcrylicHint,
    Tint,
    TintHint,
    Startup,
    StartupChangeFailed,
    StartAtSignIn,
    StartAtSignInHint,
    SettingsApplyNow,
    IconSize,
    IconSizeHint,
    GroupOrderSaveFailed,
    GroupOrderAccessDenied,
    GroupMenuHint,
    MoveGroupUp,
    MoveGroupDown,
    Rename,
    DissolveGroup,
    GroupDropHint,
    OrganizingHint,
    GroupDragHint,
    DragIntoBox,
    DragOutOfBox,
    Application,
    MoveEarlier,
    MoveLater,
    MoveToGroup,
    GroupNameExample,
    GroupName,
    NewGroup,
    RenameGroup,
    Save,
    Cancel,
    GroupSaveFailed,
    GroupDissolved,
    LayoutSaveFailed,
    LayoutAccessDenied,
    ReleaseToCollect,
    ReleaseToRestore,
    ReleaseToCancel,
    MoveCancelled,
    ReleaseToKeep,
    ReleaseToMoveGroup,
    Ungrouped,
    SelectCustomGroup,
    GroupNameLength,
    GroupNameTaken,
    GroupOrIconUnavailable,
    NoOwnerRecords,
    OwnerQueryRestricted,
    AmbiguousIconOwner,
    NoMatchingApp,
    OwnerRecordsDenied,
    OwnerRecordsFailed,
    GroupMoved,
    PersistGroupOrderFailed,
    PersistPositionFailed,
    Restored,
    Moved,
    Undone,
    Icon,
    UndoIconChanged,
    UndoGroupChanged,
    UndoPositionFailed,
    UndoSavePending,
    TaskbarNotFound,
    RecoveryRecordFailed,
    WindowsVisibilityFailed,
    IconWindowGone,
    CallbackMissing,
    IdentityDenied,
    IdentityUnknown,
    TrayBoxIcon,
    CantCollectMissingCallback,
    CollectWindowGone,
    IdentityChanged,
    SelectionSaveFailed,
    CallbackOrWindowGone,
    ClickDenied,
    ClickFailed,
    IconPositionMissing,
    TryRestoreIcon,
    Back,
    EmptyBox,
    EmptyBoxHint,
    Undo,
    NewGroupButton,
    Language,
    GroupActions,
    IconCount,
    OneIcon,
    DropInGroup,
    ExplainIcon,
    DefaultSize,
}

internal static class TrayText
{
    private static readonly bool SystemEnglish = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "zh";
    private static volatile bool english = SystemEnglish;

    internal static string LanguageTag { get { return english ? "en-US" : "zh-CN"; } }

    internal static void Use(string choice)
    {
        english = choice == "en" || (choice != "zh-Hans" && SystemEnglish);
    }

    internal static string TextFor(TextKey key)
    {
        bool useEnglish = english;
        switch (key)
        {
            case TextKey.StartupFailed: return useEnglish ? "TrayBox couldn't start" : "TrayBox 无法启动：";
            case TextKey.DragUnavailable: return useEnglish ? "Drag and drop couldn't start. Use Settings > Tray icons to collect icons." : "无法启用拖动监听，请使用设置中的图标收纳。";
            case TextKey.DragUnknown: return useEnglish ? "Couldn't identify the dragged icon. Check Settings > Tray icons." : "未能确认拖动的图标，可在「设置 › 图标收纳」查看识别结果";
            case TextKey.TrayIcons: return useEnglish ? "Tray icons" : "图标收纳";
            case TextKey.Done: return useEnglish ? "Done" : "完成";
            case TextKey.Organize: return useEnglish ? "Organize" : "整理";
            case TextKey.DoneOrganizing: return useEnglish ? "Done organizing" : "完成整理";
            case TextKey.OrganizeIcons: return useEnglish ? "Organize icons" : "整理图标";
            case TextKey.OrganizeHint: return useEnglish ? "Reorder and group icons" : "调整顺序与分组";
            case TextKey.TileHint: return useEnglish ? "Drag to reorder or right-click to move to a group" : "拖动可排序；右键可移到分组";
            case TextKey.Settings: return useEnglish ? "Settings" : "设置";
            case TextKey.AppearanceAndStartup: return useEnglish ? "Appearance & startup" : "外观与启动";
            case TextKey.AppearanceLinkHint: return useEnglish ? "Theme, Acrylic, and launch at sign-in" : "主题、系统材质、自动启动";
            case TextKey.TrayIconsLinkHint: return useEnglish ? "Choose which icons to collect and see their status" : "选择收纳图标，查看识别状态";
            case TextKey.TrayIconsHint: return useEnglish ? "Choose which notification-area icons to collect" : "选择要放进 TrayBox 的通知图标";
            case TextKey.SearchIcons: return useEnglish ? "Search icons" : "搜索图标名称";
            case TextKey.Refresh: return useEnglish ? "Refresh" : "刷新图标";
            case TextKey.Refreshing: return useEnglish ? "Refreshing tray icons…" : "正在刷新图标名单…";
            case TextKey.AppIcon: return useEnglish ? "App icon" : "应用图标";
            case TextKey.PartialRegistration: return useEnglish ? "TrayBox found only part of this icon's registration data." : "TrayBox 已取得此图标的部分注册信息。";
            case TextKey.Collected: return useEnglish ? "Collected" : "已收纳";
            case TextKey.Available: return useEnglish ? "Available" : "可收纳";
            case TextKey.Inspecting: return useEnglish ? "Checking the Windows notification area…" : "正在检查 Windows 通知区域…";
            case TextKey.InspectFailed: return useEnglish ? "Couldn't check the Windows notification area. Refresh to try again." : "无法检查 Windows 通知区域，可稍后刷新重试。";
            case TextKey.RegistrationMissing: return useEnglish ? "TrayBox hasn't received this icon's registration data" : "尚未捕获此图标的应用注册信息";
            case TextKey.RegistrationNeeded: return useEnglish ? "The icon is visible in the Windows notification area. TrayBox needs its app identity and click callback to collect it." : "Windows 通知区域可见该图标。收纳需要取得所属应用的身份和点击回调。";
            case TextKey.NoMatchingIcons: return useEnglish ? "No matching icons" : "没有匹配的图标";
            case TextKey.Unavailable: return useEnglish ? "Unavailable" : "暂不可收纳";
            case TextKey.PendingSave: return useEnglish ? "TrayBox is still trying to save your collected icons" : "收纳记录暂未保存，TrayBox 会继续重试";
            case TextKey.UnableToRecheck: return useEnglish ? "Couldn't recheck the notification area to identify the app" : "复查时无法读取通知区域，图标归属尚未核实";
            case TextKey.WaitingForRegistration: return useEnglish ? "Waiting for the app to register the icon again" : "已请求应用重新注册，正在等待图标信息";
            case TextKey.RegistrationPartlyDenied: return useEnglish ? "Some app windows refused the registration request." : "有应用窗口拒绝重新注册请求。";
            case TextKey.RegistrationDenied: return useEnglish ? "Windows blocked the request to register this icon again" : "Windows 拒绝向该应用发送重新注册请求";
            case TextKey.RegistrationNotDelivered: return useEnglish ? "Matched a running app, but couldn't reach it to request registration" : "已匹配运行中的应用，重新注册请求未能送达";
            case TextKey.RegistrationStillIncomplete: return useEnglish ? "The app received the request, but its icon registration is still incomplete" : "已送达重新注册请求，仍未取得完整图标注册";
            case TextKey.TrayTools: return useEnglish ? "Notification area tools" : "通知区域工具";
            case TextKey.OpenTrayBox: return useEnglish ? "Open TrayBox" : "打开 TrayBox";
            case TextKey.RefreshTrayIcons: return useEnglish ? "Refresh tray icons" : "刷新可用图标";
            case TextKey.ExitAndRestore: return useEnglish ? "Exit and restore icons" : "退出并恢复图标";
            case TextKey.SettingsSaved: return useEnglish ? "Settings saved" : "外观设置已保存";
            case TextKey.SettingsSaveFailed: return useEnglish ? "Couldn't save settings" : "无法保存外观设置";
            case TextKey.AppearanceHint: return useEnglish ? "Make TrayBox work the way you like" : "按你的习惯设置 TrayBox";
            case TextKey.Appearance: return useEnglish ? "Appearance" : "外观";
            case TextKey.SystemDefault: return useEnglish ? "System default" : "跟随系统";
            case TextKey.Light: return useEnglish ? "Light" : "浅色";
            case TextKey.Dark: return useEnglish ? "Dark" : "深色";
            case TextKey.Theme: return useEnglish ? "App theme" : "应用主题";
            case TextKey.ShowNames: return useEnglish ? "Show icon names" : "显示收纳图标名称";
            case TextKey.SystemAcrylic: return useEnglish ? "Acrylic" : "系统毛玻璃";
            case TextKey.AcrylicHint: return useEnglish ? "Use the Windows Acrylic backdrop" : "使用 Windows Acrylic 材质";
            case TextKey.Tint: return useEnglish ? "Background tint" : "背景色强度";
            case TextKey.TintHint: return useEnglish ? "Adjust the tint over the backdrop" : "调整材质上方的明暗遮罩";
            case TextKey.Startup: return useEnglish ? "Startup" : "启动";
            case TextKey.StartupChangeFailed: return useEnglish ? "Couldn't change the startup setting" : "无法更改启动设置";
            case TextKey.StartAtSignIn: return useEnglish ? "Launch at sign-in" : "登录时自动启动";
            case TextKey.StartAtSignInHint: return useEnglish ? "Start quietly in the notification area" : "启动后驻留通知区域";
            case TextKey.SettingsApplyNow: return useEnglish ? "Changes apply immediately" : "设置即时生效";
            case TextKey.IconSize: return useEnglish ? "Icon size" : "收纳图标尺寸";
            case TextKey.IconSizeHint: return useEnglish ? "Change the size of icons in the box" : "调整收纳盒里的图标边长";
            case TextKey.GroupOrderSaveFailed: return useEnglish ? "Couldn't save the icon order. Check your disk space." : "无法保存图标顺序，请检查磁盘空间";
            case TextKey.GroupOrderAccessDenied: return useEnglish ? "TrayBox doesn't have permission to save the icon order" : "没有权限保存图标顺序";
            case TextKey.GroupMenuHint: return useEnglish ? "Group actions" : "排序与分组操作";
            case TextKey.MoveGroupUp: return useEnglish ? "Move group up" : "上移分组";
            case TextKey.MoveGroupDown: return useEnglish ? "Move group down" : "下移分组";
            case TextKey.Rename: return useEnglish ? "Rename" : "重命名";
            case TextKey.DissolveGroup: return useEnglish ? "Ungroup" : "解散分组";
            case TextKey.GroupDropHint: return useEnglish ? "Drag icons here to add them to this group" : "将图标拖到此处分组";
            case TextKey.OrganizingHint: return useEnglish ? "Drag icons or group headers to reorder" : "拖动图标或分组标题调整顺序";
            case TextKey.GroupDragHint: return useEnglish ? "Drag group headers to reorder" : "拖动分组标题可排序";
            case TextKey.DragIntoBox: return useEnglish ? "Drag icons in from the notification area" : "从通知区域拖入图标";
            case TextKey.DragOutOfBox: return useEnglish ? "Drag icons out of the box to restore them" : "拖出窗口可移出图标";
            case TextKey.Application: return useEnglish ? "App" : "应用";
            case TextKey.MoveEarlier: return useEnglish ? "Move earlier" : "前移";
            case TextKey.MoveLater: return useEnglish ? "Move later" : "后移";
            case TextKey.MoveToGroup: return useEnglish ? "Move to group" : "移到分组";
            case TextKey.GroupNameExample: return useEnglish ? "For example, Work or Utilities" : "例如：工作、系统工具";
            case TextKey.GroupName: return useEnglish ? "Group name" : "分组名称";
            case TextKey.NewGroup: return useEnglish ? "New group" : "新建分组";
            case TextKey.RenameGroup: return useEnglish ? "Rename group" : "重命名分组";
            case TextKey.Save: return useEnglish ? "Save" : "保存";
            case TextKey.Cancel: return useEnglish ? "Cancel" : "取消";
            case TextKey.GroupSaveFailed: return useEnglish ? "Couldn't save the group. Check permissions and disk space." : "无法保存分组，请检查配置文件权限与磁盘空间";
            case TextKey.GroupDissolved: return useEnglish ? "Group removed" : "分组已解散";
            case TextKey.LayoutSaveFailed: return useEnglish ? "Couldn't save your changes. Check your disk space." : "无法保存整理结果，请检查磁盘空间";
            case TextKey.LayoutAccessDenied: return useEnglish ? "TrayBox doesn't have permission to save your changes" : "没有权限保存整理结果";
            case TextKey.ReleaseToCollect: return useEnglish ? "Release to collect" : "松开以收纳";
            case TextKey.ReleaseToRestore: return useEnglish ? "Release to restore" : "松开以移出";
            case TextKey.ReleaseToCancel: return useEnglish ? "Release to cancel" : "松开以取消";
            case TextKey.MoveCancelled: return useEnglish ? "Move cancelled. The icon is still in the box." : "已取消移动，图标仍在收纳盒内";
            case TextKey.ReleaseToKeep: return useEnglish ? "Release to keep the current position" : "松开以保留当前位置";
            case TextKey.ReleaseToMoveGroup: return useEnglish ? "Release to move the group" : "松开以移动分组";
            case TextKey.Ungrouped: return useEnglish ? "Ungrouped" : "未分组";
            case TextKey.SelectCustomGroup: return useEnglish ? "Choose a custom group" : "请选择自建分组";
            case TextKey.GroupNameLength: return useEnglish ? "Group names must be 1–32 characters" : "分组名称需为 1–32 个字符";
            case TextKey.GroupNameTaken: return useEnglish ? "A group already has this name" : "已有同名分组";
            case TextKey.GroupOrIconUnavailable: return useEnglish ? "The icon or group is unavailable" : "图标或分组不可用";
            case TextKey.NoOwnerRecords: return useEnglish ? "Windows did not provide a record that identifies the app" : "Windows 未提供可用于核对图标归属的记录";
            case TextKey.OwnerQueryRestricted: return useEnglish ? "Windows permissions restricted access to the app's process identity" : "相关进程的身份查询受 Windows 权限限制";
            case TextKey.AmbiguousIconOwner: return useEnglish ? "More than one running app uses this icon name" : "多个运行中的应用使用相同图标名称";
            case TextKey.NoMatchingApp: return useEnglish ? "Couldn't match the icon to a running app with a verified identity" : "未匹配到可验证身份的运行中应用";
            case TextKey.OwnerRecordsDenied: return useEnglish ? "Windows denied access to the icon ownership records" : "Windows 拒绝读取图标归属记录";
            case TextKey.OwnerRecordsFailed: return useEnglish ? "Couldn't read the Windows icon ownership records" : "读取 Windows 图标归属记录失败";
            case TextKey.GroupMoved: return useEnglish ? "Group moved" : "已移动分组";
            case TextKey.PersistGroupOrderFailed: return useEnglish ? "Couldn't save the group order. Check the settings folder." : "未能保存分组顺序，请检查配置目录";
            case TextKey.PersistPositionFailed: return useEnglish ? "Couldn't save the icon's position. Check the settings folder." : "未能保存图标位置，请检查配置目录";
            case TextKey.Restored: return useEnglish ? "Restored" : "已移出";
            case TextKey.Moved: return useEnglish ? "Moved" : "已移动";
            case TextKey.Undone: return useEnglish ? "Undone" : "已撤销";
            case TextKey.Icon: return useEnglish ? "Icon" : "图标";
            case TextKey.UndoIconChanged: return useEnglish ? "Can't undo this change. The app closed or registered its icon again." : "该应用的通知图标已退出或重新注册，无法撤销";
            case TextKey.UndoGroupChanged: return useEnglish ? "Can't restore the previous order because the groups changed" : "分组已变化，无法恢复之前的顺序";
            case TextKey.UndoPositionFailed: return useEnglish ? "Couldn't restore the icon's position. Select Undo to retry." : "未能恢复图标位置，可再次点击撤销重试";
            case TextKey.UndoSavePending: return useEnglish ? "Undone. TrayBox is retrying the save." : "已撤销，TrayBox 正在重试保存收纳记录";
            case TextKey.TaskbarNotFound: return useEnglish ? "The Windows taskbar couldn't be found." : "未找到 Windows 任务栏。";
            case TextKey.RecoveryRecordFailed: return useEnglish ? "Couldn't write the icon recovery record. Check the TrayBox settings folder." : "无法写入图标恢复记录，请检查 TrayBox 配置目录";
            case TextKey.WindowsVisibilityFailed: return useEnglish ? "Windows didn't accept the change to this icon's visibility" : "Windows 未接受该图标的显示状态变更";
            case TextKey.IconWindowGone: return useEnglish ? "The icon's app window closed or is no longer available" : "图标的应用窗口已退出或失效";
            case TextKey.CallbackMissing: return useEnglish ? "The icon registration has no click callback" : "已取得图标注册，但其中缺少点击回调";
            case TextKey.IdentityDenied: return useEnglish ? "Windows denied access to the identity of the icon's app" : "Windows 拒绝读取此图标所属进程的身份";
            case TextKey.IdentityUnknown: return useEnglish ? "Couldn't verify the app that owns this icon" : "无法验证图标所属应用的身份";
            case TextKey.TrayBoxIcon: return useEnglish ? "The TrayBox icon must stay in the notification area" : "TrayBox 自身的图标需要保留在通知区域";
            case TextKey.CantCollectMissingCallback: return useEnglish ? "Can't collect this icon without its click callback" : "未取得点击回调，暂时无法收纳";
            case TextKey.CollectWindowGone: return useEnglish ? "The app closed or its icon window is no longer available" : "应用已退出或图标窗口已失效";
            case TextKey.IdentityChanged: return useEnglish ? "The app identity changed. Refresh icons and try again." : "应用身份已变化，请刷新图标后再试";
            case TextKey.SelectionSaveFailed: return useEnglish ? "Couldn't save your collected icons. TrayBox tried to restore the icon." : "收纳记录无法保存，已尝试恢复图标";
            case TextKey.CallbackOrWindowGone: return useEnglish ? "The icon is missing its click callback or the app closed" : "图标回调缺失或应用已退出";
            case TextKey.ClickDenied: return useEnglish ? "Windows blocked the click. The app may be elevated or restrict messages from TrayBox." : "Windows 拒绝点击转发：应用权限高于 TrayBox 或消息访问受限";
            case TextKey.ClickFailed: return useEnglish ? "Couldn't forward the click. The app window may have changed." : "点击转发失败：应用窗口可能已变化";
            case TextKey.IconPositionMissing: return useEnglish ? "Windows couldn't provide the icon position required by the app" : "Windows 未能提供应用需要的图标位置";
            case TextKey.TryRestoreIcon: return useEnglish ? "The app needs the icon's position, but Windows hasn't returned it. Try again or restore the icon before opening it." : "应用需要系统图标的位置，Windows 暂未返回。请重试或移出后打开。";
            case TextKey.Back: return useEnglish ? "Back" : "返回";
            case TextKey.EmptyBox: return useEnglish ? "Your box is empty" : "收纳盒还是空的";
            case TextKey.EmptyBoxHint: return useEnglish ? "Drag in an icon or add one in Settings" : "拖入图标，或在设置中添加";
            case TextKey.Undo: return useEnglish ? "Undo" : "撤销";
            case TextKey.NewGroupButton: return useEnglish ? "+ New group" : "+ 新建分组";
            case TextKey.Language: return useEnglish ? "Language" : "语言";
            case TextKey.GroupActions: return useEnglish ? "Group actions for {0}" : "分组操作：{0}";
            case TextKey.IconCount: return useEnglish ? "{0} icons" : "{0} 个图标";
            case TextKey.OneIcon: return useEnglish ? "1 icon" : "1 个图标";
            case TextKey.DropInGroup: return useEnglish ? "Drop in “{0}”" : "放到「{0}」";
            case TextKey.ExplainIcon: return useEnglish ? "Why {0} is unavailable" : "查看 {0} 无法收纳的原因";
            case TextKey.DefaultSize: return useEnglish ? "{0} (default)" : "{0}（默认）";
            default: throw new ArgumentOutOfRangeException("key");
        }
    }

    internal static string TextFor(TextKey key, object argument)
    {
        return String.Format(CultureInfo.CurrentCulture, TextFor(key), argument);
    }
}
