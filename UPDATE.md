# Unpackaged (Installer) 自动更新适配计划

## 背景

主程序 (`Snap.Hutao.Remastered`) 已经通过 `RuntimeEnvironment.IsPackaged` 检测并适配了 unpackaged 模式（路径、版本检测等）。但自动更新流程仍然只支持 MSIX 包：服务器只返回 MSIX 包信息，部署工具也只通过 `PackageManager.AddPackageByUriAsync()` 安装 MSIX。

对于通过 Inno Setup 安装器安装的程序（unpackaged），需要改造整个更新流水线，使其能返回并安装安装器 EXE。

## 涉及的仓库

1. **Snap.Hutao.Server** — 支持同时提供 MSIX 和安装器包信息
2. **Snap.Hutao.Deployment** — 支持下载并运行安装器 EXE 来更新
3. **Snap.Hutao.Remastered** (主程序) — 向更新器传递安装类型
4. **installer.iss** — 添加更新模式处理（关闭运行中的应用，更新后自动启动）

---

## 1. Server: Snap.Hutao.Server

### 1.1 DB 实体: 添加 `PackageType` 列

**文件:** `D:\SnapHutaoRemasteringProject\Snap.Hutao.Server\src\Snap.Hutao.Server\Snap.Hutao.Server.Model\Entity\HutaoPackageInformation.cs`

添加新列 `PackageType` (string, max 20):

```csharp
[Required]
[StringLength(20)]
public string PackageType { get; set; } = "MSIX";  // "MSIX" 或 "Installer"
```

这样同一个表可以同时存放同一版本的 MSIX 和安装器包记录。

### 1.2 Controller: 接受 `type` 查询参数

**文件:** `D:\SnapHutaoRemasteringProject\Snap.Hutao.Server\src\Snap.Hutao.Server\Snap.Hutao.Server.API\Controller\PatchController.cs`

修改 `GetPatchInfo()` 接受可选 `type` 查询参数:

- 默认（省略 `type` 或 `type=msix`）: 保持现有 MSIX 行为（向后兼容）
- `type=installer`: 过滤 `PackageType == "Installer"`
- 将 `.Where(x => x.IsActive && x.PackageType == packageType)` 添加到查询

### 1.3 DTO: 无需修改

现有的 `HutaoPackageInformation` 响应 DTO（`version`, `validation`, `mirrors`）对 MSIX 和安装器包都适用——它们都有版本号、SHA256 哈希和下载 URL。

---

## 2. 部署工具: Snap.Hutao.Deployment

### 2.1 新 CLI 选项: `--installer-kind`

**文件:** `D:\SnapHutaoRemasteringProject\Snap.Hutao.Deployment\src\Snap.Hutao.Remastered.Deployment\InvocationOptions.cs`

添加:

```csharp
public static readonly Option<string> InstallerKind = new(
    "--installer-kind",
    () => "Msix",
    "The kind of installer: Msix or Installer.");
```

在 `Program.cs` 中注册。

### 2.2 API 服务: 传递 `type` 查询参数

**文件:** `D:\SnapHutaoRemasteringProject\Snap.Hutao.Deployment\src\Snap.Hutao.Remastered.Deployment\ApiService.cs`

修改 `GetVersionAndDownloadUrlAsync()` 接受可选的 `installerKind` 参数。当为 `"Installer"` 时，在端点 URL 后追加 `?type=installer`。

### 2.3 Invocation: 根据安装类型分支

**文件:** `D:\SnapHutaoRemasteringProject\Snap.Hutao.Deployment\src\Snap.Hutao.Remastered.Deployment\Invocation.cs`

#### `--installer-kind Installer` 的新行为:

**a) 下载阶段:**
- 下载安装器 EXE 而非 MSIX
- 使用 `HttpShardCopyWorker`（相同引擎，只是扩展名不同）
- 下载文件路径以 `.exe` 结尾而非 `.msix`

**b) Unpackaged 版本检测:**
- 不能使用 `PackageManager.FindPackages()`（那是 MSIX 专用的）
- 改为从注册表读取已安装版本:
  - `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{E8B6E2B3-D2A0-4435-A81D-2A16AAF405C8}_is1\DisplayVersion`
  - 或通过 `FileVersionInfo.GetVersionInfo()` 读取已安装 EXE 的文件版本

**c) 更新执行:**
- 不再使用 `PackageManager.AddPackageByUriAsync()`，改为运行安装器 EXE:
  ```
  installer.exe /VERYSILENT /SUPPRESSMSGBOXES /UPDATE
  ```
- 等待进程退出
- 检查退出代码

**d) 更新后启动应用:**
- 不再使用 `shell:AppsFolder\{FamilyName}!App`，改为直接启动已安装的 EXE:
  - 从注册表检测安装路径: `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{E8B6E2B3-D2A0-4435-A81D-2A16AAF405C8}_is1\InstallLocation`
  - 拼接 `Snap.Hutao.Remastered.exe`

**e) 证书安装:**
- 安装器模式下跳过 `CertificateService.CheckAndInstallCertificateAsync()`（安装器已通过 Inno Setup `[Code]` 处理证书安装）

### 2.4 修复 `args[0]` autoInstall 检查（可选但有必要的修复）

当前 `args[0].ToLower() == "update"` 永远不可能匹配，因为 `args[0]` 始终是程序路径。`--update` 参数在 `args[1]` 里。这是已有 bug。

最简单的修复: 将检查改为 `args.Contains("--update")`，或者在 `InvocationOptions` 中正确定义 `--update` 为 `Option<bool>`。

---

## 3. 主程序: Snap.Hutao.Remastered

### 3.1 UpdateService: 传递安装器类型

**文件:** `D:\SnapHutaoRemasteringProject\Snap.Hutao.Remastered\src\Snap.Hutao.Remastered\Snap.Hutao.Remastered\Service\Update\UpdateService.cs`

在 `LaunchUpdaterAsync()` 中，根据运行时检测追加 `--installer-kind`:

```csharp
commandLineBuilder.Append("--installer-kind", RuntimeEnvironment.IsUnpackaged ? "Installer" : "Msix");
```

### 3.2 无需其他修改

现有的 `CopyFileFromApplicationUri()` 已经处理了 unpackaged 模式（将 `ms-appx:///` 解析为基本目录路径），因此部署 EXE 可以正确找到。

---

## 4. Inno Setup 安装器: installer.iss

### 4.1 添加 `/UPDATE` 自定义参数支持

**文件:** `D:\SnapHutaoRemasteringProject\Snap.Hutao.Remastered\Installer\installer.iss`

通过 `[Code]` 添加自定义 `/UPDATE` 命令行参数:

```pascal
var
  IsUpdateMode: Boolean;

function InitializeSetup: Boolean;
begin
  IsUpdateMode := ExpandConstant('{param:UPDATE|false}') = 'true';
  
  if IsUpdateMode then
  begin
    // 如果应用正在运行则关闭它
  end;
  
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssDone) and IsUpdateMode then
  begin
    // 安装完成后启动更新后的应用
    Exec(ExpandConstant('{app}\Snap.Hutao.Remastered.exe'), '',
      '', SW_SHOW, ewNoWait, ResultCode);
  end;
end;
```

### 4.2 处理运行中的应用检测

在 `InitializeSetup` 函数中（当 `/UPDATE` 激活时）:
- 使用 `FindWindowByClassName` 或通过 `[Code]` 的进程操作方法关闭正在运行的应用
- 或使用 Inno Setup 内置的 `SetupMutex` 来检测和处理运行中的实例
- 在继续文件覆盖之前短暂等待进程退出

---

## 修改文件汇总

| 文件 | 修改内容 |
|------|---------|
| `Snap.Hutao.Server/.../Entity/HutaoPackageInformation.cs` | 添加 `PackageType` 列 |
| `Snap.Hutao.Server/.../API/Controller/PatchController.cs` | 添加 `type` 查询参数过滤 |
| `Snap.Hutao.Deployment/.../InvocationOptions.cs` | 添加 `--installer-kind` 选项 |
| `Snap.Hutao.Deployment/.../Program.cs` | 注册新选项 |
| `Snap.Hutao.Deployment/.../ApiService.cs` | 向 API 传递 `type` 参数 |
| `Snap.Hutao.Deployment/.../Invocation.cs` | 安装器更新流程分支 |
| `Snap.Hutao.Remastered/.../UpdateService.cs` | 向更新器传递 `--installer-kind` |
| `Snap.Hutao.Remastered/Installer/installer.iss` | 添加 `/UPDATE` 模式支持 |

## 验证方法

1. **Server**: 启动服务器，调用 `GET /patch/hutao?type=installer`，验证返回安装器包信息
2. **Deployment tool**: 以 `--installer-kind Installer --update` 运行，验证:
   - 使用 `?type=installer` 调用 API
   - 下载安装器 EXE
   - 以 `/VERYSILENT /SUPPRESSMSGBOXES /UPDATE` 参数运行安装器
3. **主程序**: 验证 unpackaged 模式下启动更新器时传递 `--installer-kind Installer`
4. **安装器**: 验证以 `/UPDATE /VERYSILENT` 参数运行时，关闭正在运行的应用，安装新版本，并重新启动应用
