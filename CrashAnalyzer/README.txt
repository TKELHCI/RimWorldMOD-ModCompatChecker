RimWorld 崩溃分析器 (CrashAnalyzer)
====================================
由 Codex (OpenAI) 生成

用途：
  当 RimWorld 因 MOD 冲突崩溃、无法进入游戏时，使用此工具直接分析 Player.log 文件。

使用方法：
  1. 双击 CrashAnalyzer.exe
  2. 点击「查找 Player.log」（或自动检测）
  3. 在「API 设置」中输入 DeepSeek 或 OpenAI 的 API Key
  4. 点击「开始 AI 分析」
  5. 等待分析结果

系统要求：
  - Windows 10/11 64位
  - .NET 6.0 桌面运行时
    下载: https://dotnet.microsoft.com/en-us/download/dotnet/6.0

注意：
  - 此工具仅分析日志，不会修改任何游戏文件
  - API Key 保存在本地 CrashAnalyzer.json 文件中
  - 支持 DeepSeek / OpenAI 兼容 API