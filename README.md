# RimWorld ModCompatChecker

MOD 兼容性检查器 + 独立崩溃分析工具

## 结构

```
├── ModCompatChecker/     MOD 源代码（Steam Workshop 发布）
│   ├── Source/            C# 源码
│   ├── Languages/         8 种语言翻译
│   ├── Assemblies/        模型配置 + Mono.Cecil 许可
│   └── Textures/          图标
│
└── CrashAnalyzer/        独立崩溃分析工具（GitHub Releases 发布）
    └── CrashAnalyzer.exe  游戏打不开时直接分析 Player.log
```

## MOD 功能

自动扫描 Harmony 补丁冲突、Def 覆盖、依赖缺失和加载顺序问题。可选接入 AI API 进行诊断。

## 编译

见 `ModCompatChecker/Source/` 目录下的 .csproj

## 作者

秋羽雪绪 · 代码框架由 Codex（OpenAI）生成

## 许可

源代码仅供学习参考。Mono.Cecil 使用 MIT 许可证。
