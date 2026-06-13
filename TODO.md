# TODO

## 高优先级

### 探索进度页面
`WorldExploration` 模型已存在于 GameRecord API 客户端，但缺少对应的 ViewModel/Page 来展示各区域的探索度、供奉等级等数据。

- [ ] 创建 `ExplorationViewModel`
- [ ] 创建 `ExplorationPage.xaml`
- [ ] 注册到导航和 DI

### 伤害计算器
角色属性面板只展示面板数据，不做期望伤害计算。可基于 Enka.Network 数据 + Wiki 数据做期望伤害模拟。

- [ ] 设计伤害计算引擎（技能倍率 × 面板属性 × 反应加成 × 敌人抗性等）
- [ ] 创建 `DamageCalculatorViewModel` / `DamageCalculatorPage`
- [ ] 与现有 AvatarProperty 数据对接

### 圣遗物评分/优化器
对已有圣遗物做副属性评分，推荐最优搭配方案。

- [ ] 设计圣遗物评分算法（副属性权重、有效词条数）
- [ ] 创建圣遗物评分视图
- [ ] 搭配推荐逻辑

## 中优先级

### BGI 自动化任务集成
`IAutomationTaskService` 的 7 个方法全部是 `throw new NotImplementedException()`，管道通信层已就绪，需实现任务分发逻辑。

- [ ] 实现自动化任务调度/分发
- [ ] 实现具体自动化任务（树脂消耗、每日委托等）
- [ ] 与 BGI 命名管道对接测试

### 摆设/尘歌壶计算器
`CultivationService.cs:82` 已有 `// TODO: support furniture calc` 注释。

- [ ] 扩展养成服务支持摆设材料计算
- [ ] 创建家具计算相关 UI

### 游戏内实时覆盖层增强
Overlay 系统目前仅支持快捷键和 Game Island 切换，可扩展实时数据覆盖层。

- [ ] 设计实时覆盖层渲染方案（DPS 计量、战斗统计等）
- [ ] 与游戏进程的数据对接

## 低优先级

### 七圣召唤相关
- [ ] 卡牌收集追踪
- [ ] 卡组构建器
- [ ] 对战记录查看

### 树脂提醒增强
- [ ] 自定义提醒阈值
- [ ] 收益最大化时段预估

## 代码待重构

- [ ] `PackageConverter.cs` — 大量相似代码，逻辑完成后重构
- [ ] `GamePackageService.cs:86` — 窗口创建逻辑移出服务
- [ ] `CultivationResinStatisticsService.cs:45` — 减少枚举次数
- [ ] `DataTable`/`DataRow` 面板 — 多处布局/可见性/尺寸 TODO
- [ ] `ExceptionHandling.cs:87` — 异常时是否关闭当前 XAML 窗口

## 代码质量改进（2026-06-08 审查）

### 移除生产代码中的测试文件
- [ ] `ViewModel/TestViewModel.cs` (651行) — 纯测试代码，移入 `Snap.Hutao.Remastered.Test` 或删除

### CA1001 潜在内存泄漏（10处）
以下文件持有可释放字段但未实现 `IDisposable`（搜索 `[SuppressMessage("", "CA1001")]`）：
- [ ] `Core/LifeCycle/AppActivation.cs`
- [ ] `Service/Game/Package/Advanced/GamePackageService.cs`
- [ ] `UI/Xaml/Control/Image/CachedImage.cs`
- [ ] `UI/Xaml/Control/WebView2/CompactWebView2Window.xaml.cs`
- [ ] `UI/Windowing/XamlWindowController.cs`
- [ ] `ViewModel/Cultivation/CultivationViewModel.cs`

### 减少 Service Locator 反模式
- [ ] `Web/Bridge/MiHoYoJSBridge.cs` — 13处 `GetRequiredService`，改用构造器注入
- [ ] `ViewModel/Home/AnnouncementViewModel.cs` — 7处，改用构造器注入

### SH003 抑制积压（54处）
54个方法抑制 "Use ValueTask instead of Task"：
- [ ] 热路径方法迁移到 `ValueTask`，或更新规范接受 `Task`

### 测试覆盖
- [ ] 为 GachaLog UIGF 导入/导出添加测试
- [ ] 为游戏启动管线添加测试
- [ ] 为元数据初始化添加测试

### CI/CD
- [ ] 添加 PR 轻量校验（`dotnet build` + `dotnet test`）

### 数据库迁移
- [ ] 压缩 2024年以前的旧 EF Core 迁移为基线快照

### StyleCop 规则
- [ ] 选择性启用 SA 规则（先启 SA1503 大括号、SA1200 using 排序）
