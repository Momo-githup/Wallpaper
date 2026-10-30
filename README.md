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
- Supabase 邮箱/密码注册与登录
- 登录会话使用 Windows DPAPI 加密保存并自动恢复
- 云端社区数据库已配置用户资料、壁纸、点赞、收藏、下载、举报和标签模型

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

## Supabase

云端数据库迁移位于 `supabase/migrations`。客户端只包含可公开的 Supabase publishable key，所有业务表均启用 RLS，匿名用户不能访问社区数据；任何 service-role 或 secret key 都不能加入客户端。

如需连接其他 Supabase 项目，可以在启动应用前设置：

```powershell
$env:WALLPAPERSTUDIO_SUPABASE_URL = "https://your-project.supabase.co"
$env:WALLPAPERSTUDIO_SUPABASE_KEY = "sb_publishable_..."
```

托管项目默认要求注册用户验证邮箱。确认邮件后即可在客户端登录，本地图库和壁纸功能不要求登录。

## 动态壁纸说明

视频播放使用 LibVLC，推荐使用兼容性最好的 MP4（H.264）格式。主窗口退出后动态壁纸由后台宿主继续播放，并可在 Windows 登录后自动恢复。
