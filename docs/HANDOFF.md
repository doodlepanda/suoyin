# Windows 开发交接

交接日期：2026-11-09。当前 Windows 源码版本：4.0.6。

## 开发入口

- 远端：`https://github.com/doodlepanda/suoyin`，分支 `main`。
- `windows-native/Program.cs`：WinForms 界面、照片导入、排版、预览和导出。
- `windows-native/PhotoLayoutTool.Windows.csproj`：.NET 8 Windows x64 单文件、自包含发布配置。
- `windows-native/fiona-logo.png`、`app-icon.ico`：现有品牌资源，未改动。
- 本次只更新 Windows 原生版本，网页和 macOS 版本未同步修改。

## 已实现的需求

1. 优先通过 Windows Shell 获取照片缩略图，失败后再读取原图生成预览。
2. 使用 Parallel.For 多核加载，按导入顺序汇总结果。
3. 1800 × 1200 逻辑画布，排版区域为 x=50、y=40、宽1700、高1120，上下排版区域边距均为40。不将整个网格垂直居中。
4. LOGO 固定在右下角，宽195；右边距50、底部编号区域边距40，编号与 LOGO 右对齐。
5. 单元格独立按可用宽高计算；照片等比例完整显示，不拉伸、不裁剪。照片比例与单元格不同时会留白，不能把单元格边界等同于照片内容边界。
6. 最后一行可与 LOGO 共用底部空间。

## 构建

需要 Windows 与 .NET 8 SDK。在仓库根目录执行：

```powershell
dotnet restore .\windows-native\PhotoLayoutTool.Windows.csproj
dotnet publish .\windows-native\PhotoLayoutTool.Windows.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\windows-v4.0.6
```

输出：`artifacts/windows-v4.0.6/fiona索引制作工具.exe`。SDK、bin、obj 和大型 exe 不提交到 Git。

## 本地交接位置

本次工作区根目录：`C:/Users/Administrator/Documents/Codex/2026-07-01/doodlepanda-suoyin-https-github-com-doodlepanda`。

- `work/repository/`：带 Git 历史的仓库，后续开发应以此目录为准。
- `work/source/windows-native/`：先前构建时的源码副本，本次已同步至仓库；不要在两处交替修改。
- `work/dotnet/dotnet.exe`：本地 .NET SDK。
- `outputs/FionaIndexTool-Windows-v4.0.6.exe`：已有自包含构建，大小161963599字节。
- 上述 exe SHA256：`CAC7D8E3F133DA7515AC1D7626A8E5285245B3037C875B5F53F0BD6F926EB06D`。

## 验证状态与继续开发重点

- v4.0.6 先前已成功编译并核对版本及文件哈希；本次交接再次从 Git 仓库目录成功执行自包含发布，并通过 git diff --check；未完成真实照片的完整 GUI 回归。
- 需用横图、竖图、方图、旋转图以及40张混合照片验证预览和 JPG/PNG 导出，确认无裁剪、变形及 LOGO 遮挡。
- 当前 LOGO 没有独立的避让算法；手动行列或其他照片数量可能与照片区域重叠，不能宣称所有布局均已验证。
- Windows Shell 的 ThumbnailOnly 未设置缓存专用标志，可能触发 Shell 缩略图提供器生成图像；HEIC 等格式依赖系统扩展和提供器支持。
- 当前导入虽然并行，但仍由界面线程等待完成，大批量导入可能暂时阻塞界面。
- 当前导出复用预览缩略图，不重新读取高分辨率原图；后续如需要高质量导出，应单独设计并验证。
- 未为本次交接擅自增加新功能；明确下一项需求后继续开发。
