## v1.19.5 版本更新说明：

- **【🎉新增】** 支持 Unpackaged 模式 — 程序脱离 MSIX 可直接通过 .exe 运行  
- **【🎉新增】** 支持非 MSIX 安装版的自动更新（Inno Setup 安装器）  
- **【🎉新增】** 颂愿记录（千星奇域）云存储同步完善  
- **【🎉新增】** 切换账号后活动日历联动切换显示 #107  
- **【🎉新增】** 兼容所有 UIGF 版本导入/导出  

- **【✨优化】** 启动速度优化 — Release 跳过预构建校验、分阶段初始化  
- **【✨优化】** Cake 构建系统重构 — 统一 MSIX + Installer 构建，自动移除 onnxruntime.dll  
- **【✨优化】** 移除祈愿页面 Code-Behind 代码  
- **【✨优化】** 无插件时修改使用的图标  
- **【✨优化】** 移除不需要的静态资源（LoadingPic 等）  

- **【🔨修复】** Loopback 解锁实现错误  
- **【🔨修复】** 切换账号后抽数合并显示异常 #150  
- **【🔨修复】** Unpackaged 模式下无法以管理员身份重启  
- **【🔨修复】** 某些情况下第二个实例未正常退出  
- **【🔨修复】** 胡桃通行证 Token 刷新不重试  
- **【🔨修复】** 启动失败时提示报错信息  
- **【🔨修复】** 本地化字段命名错误  

---

## 1.19.5 Version Update

- **【🎉New】** Unpackaged mode support — Run directly via .exe without MSIX packaging  
- **【🎉New】** Auto-update for non-MSIX installations (Inno Setup installer)  
- **【🎉New】** Beyond Gacha Log (Thousand-Star Realm) cloud storage sync improvements  
- **【🎉New】** Activity calendar display switches with account selection #107  
- **【🎉New】** Full UIGF version compatibility for import/export  

- **【✨Optimization】** Startup speed optimization — Skip pre-build validation in Release, phased initialization  
- **【✨Optimization】** Cake build system refactoring — Unified MSIX + Installer build, auto-remove onnxruntime.dll  
- **【✨Optimization】** Remove code-behind from gacha wish page  
- **【✨Optimization】** Update icon when no plugins are loaded  
- **【✨Optimization】** Remove unused static resources (LoadingPic, etc.)  

- **【🔨Fix】** Loopback unlock implementation error  
- **【🔨Fix】** Gacha total pulls display anomaly after switching accounts #150  
- **【🔨Fix】** Unable to restart as administrator in Unpackaged mode  
- **【🔨Fix】** Second instance not exiting properly in certain cases  
- **【🔨Fix】** Hutao Passport token refresh not retrying  
- **【🔨Fix】** Error message now shown when startup fails  
- **【🔨Fix】** Incorrect localization field names  

---

> 建议使用 Snap.Hutao.Remastered.Deployment 自动更新工具进行安装  
> We recommend using Snap.Hutao.Remastered.Deployment for automatic installation  
