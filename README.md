# WBCGE-Different-Wallpapers-on-Every-Desktop-for-Wallpaper-Engine
Assign a different Wallpaper Engine wallpaper to every Windows virtual desktop.  允许在每个 Windows 虚拟桌面使用不同的 Wallpaper Engine 壁纸。
# WBCGE

### Wallpaper by Virtual Desktop

中文

WBCGE 是一个轻量级 Windows 工具，让每个 Windows 虚拟桌面都可以使用不同的 Wallpaper Engine 壁纸。

主要功能：

 🖥️ 为每个虚拟桌面保存独立壁纸
 🔄 切换桌面时自动切换壁纸
 🔍 自动寻找 Wallpaper Engine
 💾 自动保存壁纸配置
 📌 后台托盘运行

English

WBCGE is a lightweight Windows utility that lets you use a different Wallpaper Engine wallpaper for each Windows virtual desktop.

Features:

 🖥️ Different wallpaper for each virtual desktop
 🔄 Automatic wallpaper switching
 🔍 Automatic Wallpaper Engine detection
 💾 Automatic configuration saving
 📌 Runs in the system tray


##下载 / Download
在Releases页面下载wgcbe 2.0.0.zip 解压即可使用
Go to Releases and download the .zip file
## 使用方法 / Usage

中文：

1. 启动 WBCGE。
2. WBCGE 会自动寻找 Wallpaper Engine。
3. 切换到一个虚拟桌面，并选择你想使用的壁纸。
4. 切换到其他虚拟桌面并设置壁纸。
5. 之后再次切换桌面时，WBCGE 会自动恢复对应的壁纸。
6. 记得首次启动前将.exe文件放在一个文件夹内。

首次使用某个虚拟桌面时，WBCGE 不会主动修改壁纸，直接正常选择壁纸即可。

English:

1. Start WBCGE.
2. WBCGE will automatically detect Wallpaper Engine.
3. Switch to a virtual desktop and choose your wallpaper.
4. Repeat for other virtual desktops.
5. WBCGE will automatically restore the corresponding wallpaper when you switch desktops.
6. Remember to put this .exe in a folder before using.

When using a virtual desktop for the first time, WBCGE will not change its wallpaper automatically. Simply choose a wallpaper normally.

## 注意事项 / Notes

中文：

 ⚠️ 使用前请确保 Wallpaper Engine 已安装并正常运行。
 ⚠️ WBCGE 依赖 Windows 虚拟桌面功能。
 ⚠️ 不建议同时使用其他虚拟桌面壁纸管理软件。
 ⚠️ 删除 `wbcge.json` 会清除已保存的壁纸记录。
 ⚠️ 如果移动或删除 Wallpaper Engine 中的壁纸文件，WBCGE 可能无法恢复该壁纸。
 ⚠️ 首次使用某个虚拟桌面时，需要先手动选择一次壁纸。

English:

 ⚠️ Make sure Wallpaper Engine is installed and running.
 ⚠️ WBCGE relies on Windows Virtual Desktops.
 ⚠️ Using other virtual desktop wallpaper managers at the same time is not recommended.
 ⚠️ Deleting `wbcge.json` will remove all saved wallpaper records.
 ⚠️ Moving or deleting a Wallpaper Engine wallpaper may prevent WBCGE from restoring it.
 ⚠️ A wallpaper must be selected manually the first time a virtual desktop is used.

## Requirements / 环境

 Windows 10 / 11
 Wallpaper Engine
 .NET 8 (not required for the self-contained release)

## License / 许可证

[MIT License](LICENSE)

---

Make every virtual desktop feel different.
