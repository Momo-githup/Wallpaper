# Wallpaper Studio

一款面向 Windows 10/11 的本地壁纸管理工具，使用 .NET 8 与 WPF 构建。

## 当前功能

- 导入单张图片、批量图片或整个文件夹
- 支持 JPG、JPEG、PNG、BMP 和 WebP
- 支持 MP4、WMV、AVI、MOV、M4V 本地视频动态壁纸
- 本地 SQLite 图库、缩略图缓存和重复文件过滤
- 卡片式浏览、详情查看和全屏预览
- 将选中图片统一应用到所有显示器
- 自动使用适合大多数屏幕的填充方式
- 动态壁纸静音循环播放，切换回图片时自动停止
- 关闭窗口后驻留系统托盘
- 默认不随 Windows 启动
- 支持将图片或文件夹拖入窗口

“移出图库”只删除图库索引，不会删除原始图片。

## 开发运行

需要 Windows 10/11 与 .NET 8 SDK。

```powershell
dotnet restore --configfile NuGet.Config
dotnet build WallpaperStudio.sln --no-restore
dotnet run --project src/WallpaperStudio.App
```

运行测试：

```powershell
dotnet test WallpaperStudio.sln --no-restore
```

## 发布

生成无需预装 .NET 的 x64 版本：

```powershell
dotnet publish src/WallpaperStudio.App -c Release -r win-x64 --self-contained true -o publish/win-x64
```

应用数据保存在 `%LOCALAPPDATA%\WallpaperStudio`。正式分发前建议为可执行文件和安装包配置代码签名。

## 动态壁纸说明

视频播放使用 Windows 自带媒体组件，推荐使用兼容性最好的 MP4（H.264）格式。退出应用会停止动态壁纸；关闭主窗口到托盘则继续播放。
