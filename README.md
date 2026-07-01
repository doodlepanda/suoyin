# fiona索引制作工具

用于制作 6 寸照片索引 / 缩略图排版的小工具，支持 macOS 客户端、Windows 客户端和网页版本。

## 功能

- 6 x 4 英寸画布输出
- 自动排版或手动设置行列
- 照片间距调节
- 单张照片旋转
- JPG / PNG 导出
- 每版最多 40 张，自动生成并批量导出多个版面
- 多版预览切换
- 文件夹拖入
- 支持 JPG、PNG、BMP、TIF/TIFF、HEIC/HEIF/HIF 等常见照片格式
- 文件夹名包含连续 5 位数字时自动填入编号

## 项目结构

- `index.html`：网页 / macOS WebView 客户端使用的主界面
- `PhotoLayoutTool.swift`：macOS WebKit 客户端壳
- `windows-native/`：Windows WinForms 原生客户端源码
- `assets/`：应用图标资源
- `build/releases/`：正式发布包和版本说明

## 最新发布

见 `build/releases/v4.0.2/`。

Windows 读取 iPhone HEIC / HEIF / HIF 静态照片需要安装 Microsoft 官方 HEIF 图像扩展，Windows 发布包中包含安装辅助脚本。
