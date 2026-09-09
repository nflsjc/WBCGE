
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Win32;

class Program
{
    static readonly string ConfigFile =
        Path.Combine(AppContext.BaseDirectory, "wbcge.json");

    static readonly string LogFile =
        Path.Combine(AppContext.BaseDirectory, "wbcge.log");


    // =========================================================
    // Wallpaper Engine 路径
    // =========================================================

    // 不再写死路径。
    // 程序启动时会自动寻找 Wallpaper Engine。

    static string WallpaperEngine = "";

    static string WallpaperConfig = "";


    // =========================================================
    // 每个虚拟桌面的壁纸
    // =========================================================

    //
    // Key:
    // 0 = Windows 桌面 1
    // 1 = Windows 桌面 2
    // 2 = Windows 桌面 3
    // ...
    //
    // Value:
    // Wallpaper Engine 当前壁纸文件路径

    static readonly Dictionary<int, string> DesktopWallpapers =
        new Dictionary<int, string>();

    static int LastDesktop = -1;

    static volatile bool Running = true;


    // Windows 虚拟桌面注册表
    static string RegistryPath =
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops";

    static NotifyIcon? TrayIcon;


    // =========================================================
    // 日志
    // =========================================================

    static void Log(string message)
    {
        try
        {
            string line =
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

            File.AppendAllText(
                LogFile,
                line + Environment.NewLine);
        }
        catch
        {
        }
    }


    // =========================================================
    // 自动寻找 Wallpaper Engine
    // =========================================================

    static bool FindWallpaperEngine()
    {
        Log("开始自动寻找 Wallpaper Engine...");


        // ---------------------------------------------------------
        // 第一阶段：
        // 从 Steam 注册表获取 Steam 安装目录
        // ---------------------------------------------------------

        List<string> steamPaths =
            new List<string>();

        try
        {
            string? steamPath =
                Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Valve\Steam",
                    "SteamPath",
                    null) as string;

            if (!string.IsNullOrWhiteSpace(steamPath))
            {
                steamPath =
                    steamPath.Trim();

                if (Directory.Exists(steamPath))
                {
                    steamPaths.Add(
                        steamPath);

                    Log(
                        $"从注册表找到 Steam: {steamPath}");
                }
            }
        }
        catch (Exception ex)
        {
            Log(
                $"读取 Steam 注册表失败: {ex.Message}");
        }


        // ---------------------------------------------------------
        // 第二阶段：
        // 尝试常见 Steam 安装位置
        // ---------------------------------------------------------

        string[] commonSteamPaths =
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam",
            @"D:\Steam",
            @"D:\steam",
            @"E:\Steam",
            @"E:\steam",
            @"F:\Steam",
            @"F:\steam",
            @"G:\Steam",
            @"G:\steam",
            @"H:\Steam",
            @"H:\steam",
            @"I:\Steam",
            @"I:\steam",
        };

        foreach (string path in commonSteamPaths)
        {
            if (Directory.Exists(path) &&
                !steamPaths.Contains(path))
            {
                steamPaths.Add(path);

                Log(
                    $"找到可能的 Steam 目录: {path}");
            }
        }


        // ---------------------------------------------------------
        // 第三阶段：
        // 从每个 Steam 目录寻找 Wallpaper Engine
        // ---------------------------------------------------------

        foreach (string steamPath in steamPaths)
        {
            // 先检查 Steam 本体 Library
            string candidate =
                Path.Combine(
                    steamPath,
                    "steamapps",
                    "common",
                    "wallpaper_engine",
                    "wallpaper64.exe");

            if (TryUseWallpaperEngine(candidate))
            {
                return true;
            }


            // -----------------------------------------------------
            // 读取 libraryfolders.vdf
            // 用于寻找 D/E/F 等其他硬盘上的 Steam Library
            // -----------------------------------------------------

            string libraryFile =
                Path.Combine(
                    steamPath,
                    "steamapps",
                    "libraryfolders.vdf");

            if (!File.Exists(libraryFile))
            {
                continue;
            }

            Log(
                $"正在读取 Steam Library: {libraryFile}");

            try
            {
                string vdf =
                    File.ReadAllText(libraryFile);

                List<string> libraryPaths =
                    ParseSteamLibraryPaths(vdf);

                foreach (string libraryPath in libraryPaths)
                {
                    string wallpaperEngine =
                        Path.Combine(
                            libraryPath,
                            "steamapps",
                            "common",
                            "wallpaper_engine",
                            "wallpaper64.exe");

                    if (TryUseWallpaperEngine(
                            wallpaperEngine))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log(
                    $"读取 libraryfolders.vdf 失败: {ex.Message}");
            }
        }


        // ---------------------------------------------------------
        // 第四阶段：
        // 最后再检查当前程序所在磁盘的常见路径
        // ---------------------------------------------------------

        try
        {
            string currentDrive =
                Path.GetPathRoot(
                    AppContext.BaseDirectory) ?? "";

            if (!string.IsNullOrWhiteSpace(currentDrive))
            {
                string candidate =
                    Path.Combine(
                        currentDrive,
                        "Steam",
                        "steamapps",
                        "common",
                        "wallpaper_engine",
                        "wallpaper64.exe");

                if (TryUseWallpaperEngine(candidate))
                {
                    return true;
                }

                candidate =
                    Path.Combine(
                        currentDrive,
                        "steam",
                        "steamapps",
                        "common",
                        "wallpaper_engine",
                        "wallpaper64.exe");

                if (TryUseWallpaperEngine(candidate))
                {
                    return true;
                }
            }
        }
        catch
        {
        }


        Log(
            "未能自动找到 Wallpaper Engine");

        return false;
    }


    // =========================================================
    // 检查 Wallpaper Engine 路径
    // =========================================================

    static bool TryUseWallpaperEngine(
        string executable)
    {
        try
        {
            if (!File.Exists(executable))
            {
                return false;
            }

            string? directory =
                Path.GetDirectoryName(executable);

            if (string.IsNullOrWhiteSpace(directory))
            {
                return false;
            }

            string config =
                Path.Combine(
                    directory,
                    "config.json");


            // Wallpaper Engine 程序存在即可。
            // config.json 不存在时后面读取时会给出日志。
            WallpaperEngine =
                executable;

            WallpaperConfig =
                config;

            Log(
                $"✓ 找到 Wallpaper Engine: {WallpaperEngine}");

            Log(
                $"✓ Wallpaper Engine 配置文件: {WallpaperConfig}");

            return true;
        }
        catch (Exception ex)
        {
            Log(
                $"检查 Wallpaper Engine 路径失败: {ex.Message}");

            return false;
        }
    }


    // =========================================================
    // 解析 Steam libraryfolders.vdf
    // =========================================================

    static List<string> ParseSteamLibraryPaths(
        string vdf)
    {
        List<string> result =
            new List<string>();

        try
        {
            using StringReader reader =
                new StringReader(vdf);

            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                line =
                    line.Trim();

                // Steam VDF 中通常类似：
                //
                // "path"		"C:\\SteamLibrary"
                //

                if (!line.StartsWith("\"path\"",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int firstQuote =
                    line.IndexOf(
                        '"',
                        6);

                if (firstQuote < 0)
                {
                    continue;
                }

                int secondQuote =
                    line.IndexOf(
                        '"',
                        firstQuote + 1);

                if (secondQuote < 0)
                {
                    continue;
                }

                string path =
                    line.Substring(
                        firstQuote + 1,
                        secondQuote - firstQuote - 1);

                path =
                    path.Replace(
                        "\\\\",
                        "\\");

                path =
                    path.Trim();

                if (Directory.Exists(path) &&
                    !result.Contains(path))
                {
                    result.Add(path);

                    Log(
                        $"找到 Steam Library: {path}");
                }
            }
        }
        catch (Exception ex)
        {
            Log(
                $"解析 Steam Library 失败: {ex.Message}");
        }

        return result;
    }


    // =========================================================
    // 读取 wbcge.json
    // =========================================================

    static bool LoadConfig()
    {
        try
        {
            DesktopWallpapers.Clear();

            if (!File.Exists(ConfigFile))
            {
                Log("没有找到 wbcge.json");

                // 第一次运行允许没有配置文件
                // 程序会自动创建

                return true;
            }

            string json =
                File.ReadAllText(ConfigFile);

            using JsonDocument doc =
                JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty(
                    "DesktopWallpapers",
                    out JsonElement savedWallpapers))
            {
                Log(
                    "wbcge.json 中没有 DesktopWallpapers，使用空配置");

                return true;
            }

            if (savedWallpapers.ValueKind !=
                JsonValueKind.Object)
            {
                Log(
                    "DesktopWallpapers 格式错误，使用空配置");

                return true;
            }

            foreach (JsonProperty property
                     in savedWallpapers.EnumerateObject())
            {
                if (!int.TryParse(
                        property.Name,
                        out int desktop))
                {
                    continue;
                }

                string wallpaper =
                    property.Value.GetString() ?? "";

                wallpaper =
                    wallpaper.Trim();

                if (!string.IsNullOrWhiteSpace(wallpaper))
                {
                    DesktopWallpapers[desktop] =
                        wallpaper;
                }
            }

            Log(
                $"配置加载成功，已加载 {DesktopWallpapers.Count} 个桌面的壁纸记忆");

            return true;
        }
        catch (JsonException ex)
        {
            Log(
                $"wbcge.json JSON 格式错误: {ex.Message}");

            return false;
        }
        catch (Exception ex)
        {
            Log(
                $"读取 wbcge.json 失败: {ex.Message}");

            return false;
        }
    }


    // =========================================================
    // 保存 wbcge.json
    // =========================================================

    static void SaveConfig()
    {
        try
        {
            var data =
                new Dictionary<string, object>();

            var wallpapers =
                new Dictionary<string, string>();

            foreach (var pair in DesktopWallpapers)
            {
                wallpapers[pair.Key.ToString()] =
                    pair.Value;
            }

            data["DesktopWallpapers"] =
                wallpapers;

            JsonSerializerOptions options =
                new JsonSerializerOptions
                {
                    WriteIndented = true
                };

            string json =
                JsonSerializer.Serialize(
                    data,
                    options);

            string tempFile =
                ConfigFile + ".tmp";

            File.WriteAllText(
                tempFile,
                json);

            File.Move(
                tempFile,
                ConfigFile,
                true);

            Log("wbcge.json 已保存");
        }
        catch (Exception ex)
        {
            Log(
                $"保存 wbcge.json 失败: {ex.Message}");
        }
    }


    // =========================================================
    // 获取当前虚拟桌面
    // =========================================================

    static int GetCurrentDesktop()
    {
        try
        {
            byte[]? current =
                Registry.GetValue(
                    RegistryPath,
                    "CurrentVirtualDesktop",
                    null) as byte[];

            byte[]? all =
                Registry.GetValue(
                    RegistryPath,
                    "VirtualDesktopIDs",
                    null) as byte[];

            if (current is null ||
                all is null)
            {
                return 0;
            }

            int count =
                all.Length / 16;

            for (int i = 0; i < count; i++)
            {
                bool same = true;

                for (int j = 0; j < 16; j++)
                {
                    if (all[i * 16 + j] != current[j])
                    {
                        same = false;
                        break;
                    }
                }

                if (same)
                {
                    return i;
                }
            }
        }
        catch (Exception ex)
        {
            Log(
                $"读取虚拟桌面失败: {ex.Message}");
        }

        return 0;
    }


    // =========================================================
    // 从 Wallpaper Engine config.json 中递归寻找属性
    // =========================================================

    static bool FindProperty(
        JsonElement element,
        string propertyName,
        out JsonElement result)
    {
        if (element.ValueKind ==
            JsonValueKind.Object)
        {
            foreach (JsonProperty property
                     in element.EnumerateObject())
            {
                if (property.Name.Equals(
                        propertyName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    result =
                        property.Value;

                    return true;
                }

                if (FindProperty(
                        property.Value,
                        propertyName,
                        out result))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind ==
                 JsonValueKind.Array)
        {
            foreach (JsonElement item
                     in element.EnumerateArray())
            {
                if (FindProperty(
                        item,
                        propertyName,
                        out result))
                {
                    return true;
                }
            }
        }

        result =
            default;

        return false;
    }


    // =========================================================
    // 获取 Wallpaper Engine 当前壁纸
    // =========================================================

    static string GetWallpaperFromConfig()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(
                    WallpaperConfig))
            {
                Log(
                    "WallpaperConfig 为空");

                return "";
            }

            if (!File.Exists(WallpaperConfig))
            {
                Log(
                    $"找不到 Wallpaper Engine config.json: {WallpaperConfig}");

                return "";
            }

            string json =
                File.ReadAllText(WallpaperConfig);

            using JsonDocument doc =
                JsonDocument.Parse(json);

            if (!FindProperty(
                    doc.RootElement,
                    "wallpaperconfig",
                    out JsonElement wallpaperConfig))
            {
                Log(
                    "config.json 中找不到 wallpaperconfig");

                return "";
            }

            if (!wallpaperConfig.TryGetProperty(
                    "selectedwallpapers",
                    out JsonElement selectedWallpapers))
            {
                Log(
                    "找不到 selectedwallpapers");

                return "";
            }

            if (!selectedWallpapers.TryGetProperty(
                    "Monitor0",
                    out JsonElement monitor0))
            {
                Log(
                    "找不到 Monitor0");

                return "";
            }

            if (!monitor0.TryGetProperty(
                    "file",
                    out JsonElement file))
            {
                Log(
                    "找不到 file");

                return "";
            }

            string path =
                file.GetString() ?? "";

            return path.Trim();
        }
        catch (Exception ex)
        {
            Log(
                $"读取 Wallpaper Engine config.json 失败: {ex.Message}");

            return "";
        }
    }


    // =========================================================
    // 重试读取当前壁纸
    // =========================================================

    static string GetWallpaperFromConfigWithRetry()
    {
        for (int i = 0; i < 6; i++)
        {
            string result =
                GetWallpaperFromConfig();

            if (!string.IsNullOrWhiteSpace(result))
            {
                return result;
            }

            Thread.Sleep(300);
        }

        return "";
    }


    // =========================================================
    // 设置 Wallpaper Engine 壁纸
    // =========================================================

    static bool SetWallpaper(string wallpaper)
    {
        if (string.IsNullOrWhiteSpace(wallpaper))
        {
            Log(
                "壁纸路径为空，跳过设置");

            return false;
        }

        Log(
            $"设置壁纸: {wallpaper}");

        try
        {
            if (!File.Exists(WallpaperEngine))
            {
                Log(
                    $"找不到 Wallpaper Engine: {WallpaperEngine}");

                return false;
            }

            ProcessStartInfo psi =
                new ProcessStartInfo();

            psi.FileName =
                WallpaperEngine;

            psi.Arguments =
                $"-control openWallpaper -file \"{wallpaper}\"";

            psi.UseShellExecute =
                false;

            psi.CreateNoWindow =
                true;

            psi.RedirectStandardOutput =
                true;

            psi.RedirectStandardError =
                true;

            using Process? process =
                Process.Start(psi);

            if (process == null)
            {
                Log(
                    "无法启动 Wallpaper Engine");

                return false;
            }

            process.WaitForExit(5000);

            string output =
                process.StandardOutput.ReadToEnd();

            string error =
                process.StandardError.ReadToEnd();

            if (!string.IsNullOrWhiteSpace(output))
            {
                Log(
                    $"Wallpaper Engine 输出: {output.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                Log(
                    $"Wallpaper Engine 错误: {error.Trim()}");
            }

            Log(
                $"Wallpaper Engine 返回代码: {process.ExitCode}");

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Log(
                $"设置壁纸失败: {ex.Message}");

            return false;
        }
    }


    // =========================================================
    // 保存某个桌面的当前壁纸
    // =========================================================

    static bool SaveCurrentDesktopWallpaper(
        int desktop)
    {
        Log(
            $"正在读取桌面 {desktop + 1} 当前壁纸");

        string wallpaper =
            GetWallpaperFromConfigWithRetry();

        if (string.IsNullOrWhiteSpace(wallpaper))
        {
            Log(
                $"桌面 {desktop + 1} 当前壁纸为空，未保存");

            return false;
        }

        DesktopWallpapers[desktop] =
            wallpaper;

        Log(
            $"✓ 已保存桌面 {desktop + 1} 壁纸: {wallpaper}");

        SaveConfig();

        return true;
    }


    // =========================================================
    // 状态窗口
    // =========================================================

    static string GetStatusText()
    {
        int desktop =
            GetCurrentDesktop();

        string text =
            $"当前虚拟桌面：桌面 {desktop + 1}" +
            Environment.NewLine +
            Environment.NewLine;

        if (DesktopWallpapers.TryGetValue(
                desktop,
                out string? wallpaper) &&
            !string.IsNullOrWhiteSpace(wallpaper))
        {
            text +=
                "当前记忆壁纸：" +
                Environment.NewLine +
                wallpaper;
        }
        else
        {
            text +=
                "这个桌面还没有保存壁纸。";
        }

        text +=
            Environment.NewLine +
            Environment.NewLine +
            $"已保存桌面数：{DesktopWallpapers.Count}" +
            Environment.NewLine +
            Environment.NewLine +
            "Wallpaper Engine：" +
            Environment.NewLine +
            (string.IsNullOrWhiteSpace(WallpaperEngine)
                ? "未找到"
                : WallpaperEngine);

        return text;
    }


    static void ShowStatus()
    {
        MessageBox.Show(
            GetStatusText(),
            "wbcge",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }


    // =========================================================
    // 托盘
    // =========================================================

    static void CreateTrayIcon()
    {
        TrayIcon =
            new NotifyIcon();

        TrayIcon.Icon =
            SystemIcons.Application;

        TrayIcon.Text =
            "wbcge - Wallpaper Engine Helper";

        TrayIcon.Visible =
            true;

        ContextMenuStrip menu =
            new ContextMenuStrip();

        ToolStripMenuItem statusItem =
            new ToolStripMenuItem(
                "显示状态");

        statusItem.Click +=
            (sender, e) =>
            {
                ShowStatus();
            };

        ToolStripMenuItem exitItem =
            new ToolStripMenuItem(
                "退出 wbcge");

        exitItem.Click +=
            (sender, e) =>
            {
                Running =
                    false;

                if (TrayIcon != null)
                {
                    TrayIcon.Visible =
                        false;

                    TrayIcon.Dispose();
                }

                Application.Exit();
            };

        menu.Items.Add(
            statusItem);

        menu.Items.Add(
            new ToolStripSeparator());

        menu.Items.Add(
            exitItem);

        TrayIcon.ContextMenuStrip =
            menu;

        TrayIcon.DoubleClick +=
            (sender, e) =>
            {
                ShowStatus();
            };

        TrayIcon.BalloonTipTitle =
            "wbcge";

        TrayIcon.BalloonTipText =
            "Wallpaper Engine 虚拟桌面助手已启动";

        TrayIcon.BalloonTipIcon =
            ToolTipIcon.Info;

        TrayIcon.ShowBalloonTip(
            2000);
    }


    // =========================================================
    // 启动成功提示
    // =========================================================

    static void ShowStartupSuccess(
        int desktop)
    {
        string wallpaper;

        if (DesktopWallpapers.TryGetValue(
                desktop,
                out string? savedWallpaper) &&
            !string.IsNullOrWhiteSpace(savedWallpaper))
        {
            wallpaper =
                "已恢复该桌面的壁纸记忆。";
        }
        else
        {
            wallpaper =
                "这是一个新的桌面，当前壁纸将在切换时自动记住。";
        }

        MessageBox.Show(
            "wbcge 启动成功！\n\n" +
            $"当前虚拟桌面：桌面 {desktop + 1}\n" +
            wallpaper +
            "\n\n" +
            $"已保存桌面数：{DesktopWallpapers.Count}\n\n" +
            "Wallpaper Engine 已自动找到：\n" +
            WallpaperEngine,
            "wbcge",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }


    // =========================================================
    // Wallpaper Engine 找不到
    // =========================================================

    static void ShowWallpaperEngineError()
    {
        MessageBox.Show(
            "wbcge 启动失败！\n\n" +
            "没有自动找到 Wallpaper Engine。\n\n" +
            "请确认 Wallpaper Engine 已经通过 Steam 安装。\n\n" +
            "详细寻找过程请查看：\n" +
            LogFile,
            "wbcge - 找不到 Wallpaper Engine",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }


    // =========================================================
    // 配置错误
    // =========================================================

    static void ShowConfigError()
    {
        MessageBox.Show(
            "wbcge 启动失败！\n\n" +
            "无法读取 wbcge.json。\n\n" +
            "请检查配置文件是否存在、JSON 格式是否正确。\n\n" +
            $"配置文件位置：\n{ConfigFile}\n\n" +
            "详细错误信息请查看：\n" +
            LogFile,
            "wbcge - 启动失败",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }


    // =========================================================
    // 主壁纸循环
    // =========================================================

    static void WallpaperLoop()
    {
        try
        {
            // -----------------------------------------------------
            // 第一步：
            // 自动寻找 Wallpaper Engine
            // -----------------------------------------------------

            if (!FindWallpaperEngine())
            {
                ShowWallpaperEngineError();

                Running =
                    false;

                Application.Exit();

                return;
            }


            // -----------------------------------------------------
            // 第二步：
            // 读取 wbcge.json
            // -----------------------------------------------------

            if (!LoadConfig())
            {
                ShowConfigError();

                Running =
                    false;

                Application.Exit();

                return;
            }


            // -----------------------------------------------------
            // 第三步：
            // 获取当前虚拟桌面
            // -----------------------------------------------------

            LastDesktop =
                GetCurrentDesktop();

            Log(
                $"启动时所在桌面: 桌面 {LastDesktop + 1}");


            // -----------------------------------------------------
            // 第四步：
            // 启动时保存当前桌面的实际壁纸
            //
            // 不主动修改壁纸
            // -----------------------------------------------------

            SaveCurrentDesktopWallpaper(
                LastDesktop);


            // -----------------------------------------------------
            // 第五步：
            // 启动成功提示
            // -----------------------------------------------------

            ShowStartupSuccess(
                LastDesktop);


            // -----------------------------------------------------
            // 第六步：
            // 开始监控虚拟桌面
            // -----------------------------------------------------

            while (Running)
            {
                try
                {
                    int currentDesktop =
                        GetCurrentDesktop();

                    if (currentDesktop != LastDesktop)
                    {
                        Log(
                            $"桌面 {LastDesktop + 1} -> 桌面 {currentDesktop + 1}");


                        // -------------------------------------------------
                        // 第一步：
                        // 保存离开的桌面的当前壁纸
                        // -------------------------------------------------

                        SaveCurrentDesktopWallpaper(
                            LastDesktop);


                        // -------------------------------------------------
                        // 第二步：
                        // 恢复即将进入的桌面的壁纸
                        // -------------------------------------------------

                        if (DesktopWallpapers.TryGetValue(
                                currentDesktop,
                                out string? wallpaper) &&
                            !string.IsNullOrWhiteSpace(wallpaper))
                        {
                            Log(
                                $"恢复桌面 {currentDesktop + 1} 壁纸: {wallpaper}");

                            SetWallpaper(
                                wallpaper);
                        }
                        else
                        {
                            Log(
                                $"桌面 {currentDesktop + 1} 没有保存的壁纸");

                            // 第一次进入该桌面：
                            // 不改变当前壁纸。
                            //
                            // 用户可以正常在 Wallpaper Engine
                            // 中选择自己喜欢的壁纸。
                        }


                        LastDesktop =
                            currentDesktop;
                    }

                    Thread.Sleep(200);
                }
                catch (Exception ex)
                {
                    Log(
                        $"主循环发生错误: {ex.Message}");

                    Thread.Sleep(1000);
                }
            }
        }
        catch (Exception ex)
        {
            Log(
                $"WallpaperLoop 致命错误: {ex.Message}");

            try
            {
                MessageBox.Show(
                    "wbcge 发生致命错误！\n\n" +
                    ex.Message +
                    "\n\n详细信息请查看：\n" +
                    LogFile,
                    "wbcge - 错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
            }
        }
    }


    // =========================================================
    // 程序入口
    // =========================================================

    [STAThread]
    static void Main()
    {
        Log("");

        Log(
            "========================================");

        Log(
            "wbcge 启动");

        Log(
            "========================================");

        Application.EnableVisualStyles();

        Application.SetCompatibleTextRenderingDefault(
            false);


        CreateTrayIcon();


        Thread wallpaperThread =
            new Thread(
                WallpaperLoop);

        wallpaperThread.IsBackground =
            true;

        wallpaperThread.Start();


        Application.Run();


        Running =
            false;

        try
        {
            if (wallpaperThread.IsAlive)
            {
                wallpaperThread.Join(1000);
            }
        }
        catch
        {
        }


        Log(
            "wbcge 已退出");


        if (TrayIcon != null)
        {
            TrayIcon.Visible =
                false;

            TrayIcon.Dispose();
        }
    }
}
