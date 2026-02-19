# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- 全局提示词集成 - 支持从主软件加载全局提示词
- 全局提示词实时同步 - 名称和prompt内容变更即时同步
- 全局提示词★前缀标识 - 直观区分全局和局部提示词
- 固定宽度下拉框 - 120px宽度，超长名称自动截断
- ID绑定策略 - 使用提示词ID绑定MCP策略，重命名不影响策略绑定

### Changed
- 优化全局提示词同步机制 - 同时同步名称和prompt内容
- 改进UI布局 - 下拉框固定宽度，不影响其他组件位置

### Technical
- 实现全局提示词变更回调机制
- 添加UpdatePromptItems方法用于同步prompt内容

### Added
- Initial release of DeepSeekMCP translation plugin
- DeepSeek API integration for high-quality translation
- MCP (Model Context Protocol) server tool integration
- 5 tool invocation strategies (Disabled/Blank/Hybrid/ToolFirst/ToolForced)
- Multi-server MCP management with dynamic tool discovery
- Visual configuration interface with server management
- Command system for quick strategy switching (/now, /switch, /status, /chain, /result, /mcp, /help)
- Two-layer priority architecture for MCP control
- Tool chain display with enable/disable toggle
- Multiple result display modes (Disabled/Coarse/Mixed/Detailed)
- Multi-language support (English, Simplified Chinese, Traditional Chinese)
- MIT License

### Features
- Support for DeepSeek V3 and R1 models
- Non-streaming API requests for translation
- JSON-RPC 2.0 MCP protocol implementation
- Auto-save settings with debouncing (300ms)
- Connection testing for MCP servers
- Tool enable/disable management per server

### Technical
- Built with .NET 8.0
- WPF user interface
- CommunityToolkit.Mvvm for MVVM pattern
- iNKORE.UI.WPF.Modern for modern UI components
- Newtonsoft.Json for JSON serialization

## [1.0.0] - 2026-02-18

### Added
- First stable release
- Complete translation plugin for STranslate
- Full MCP tool calling support
- Comprehensive documentation in `docs/` directory
