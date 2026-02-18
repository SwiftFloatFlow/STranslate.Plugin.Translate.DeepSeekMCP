# MCP API Key 格式说明

## 支持的格式

在设置 MCP 服务器的 API Key 时，支持以下 **4 种格式**：

### 格式 1：Authorization=Bearer token（推荐）
与 UI 提示保持一致：
```
Authorization=Bearer eyJhbGciOiJIUzI1NiIs...
```

### 格式 2：Authorization=token
如果不包含 Bearer 前缀，会自动添加：
```
Authorization=eyJhbGciOiJIUzI1NiIs...
```
实际发送：`Authorization: Bearer eyJhbGciOiJIUzI1NiIs...`

### 格式 3：Bearer token
直接输入 Bearer 格式的 token：
```
Bearer eyJhbGciOiJIUzI1NiIs...
```

### 格式 4：纯 token
只输入 token，自动生成 Bearer：
```
eyJhbGciOiJIUzI1NiIs...
```
实际发送：`Authorization: Bearer eyJhbGciOiJIUzI1NiIs...`

### 格式 5：自定义认证头（高级）
使用冒号分隔的格式设置其他请求头：
```
X-API-Key: your-api-key-here
```

## 日志输出对照

在详细日志级别下，可以看到实际使用的格式：

- `[MCP] 已配置Bearer认证 (Authorization=Bearer xxx 格式)` - 格式 1
- `[MCP] 已配置Bearer认证 (Authorization=xxx 格式，自动添加Bearer)` - 格式 2
- `[MCP] 已配置Bearer认证 (Bearer xxx 格式)` - 格式 3
- `[MCP] 已配置Bearer认证 (纯token，自动生成Bearer)` - 格式 4
- `[MCP] 已配置自定义认证头: X-API-Key` - 格式 5

## 注意事项

1. **大小写敏感**：`Authorization` 和 `Bearer` 的大小写可能会被某些服务器敏感对待，建议使用标准大小写

2. **空格处理**：系统会自动 trim 前后的空格，但请确保 `Bearer` 和 token 之间只有一个空格

3. **等号 vs 冒号**：
   - `Authorization=Bearer xxx` - 使用等号（UI 提示格式）
   - `Authorization: Bearer xxx` - 使用冒号（标准 HTTP 头格式）
   - 两者都支持

4. **无认证**：如果不需要认证，请将 API Key 留空

## 故障排除

如果连接失败，请检查：

1. **日志中的认证格式**是否与预期一致
2. **Token 是否过期**（某些服务器需要定期刷新）
3. **服务器端配置**是否匹配（Bearer Token、API Key、无认证等）
4. **网络连接**是否正常（防火墙、代理等）

## 测试步骤

1. 在 MCP 服务器设置中输入 API Key
2. 设置日志级别为"详细"
3. 点击"启用并测试连接"
4. 查看日志中的认证格式输出
5. 确认服务器端日志显示认证成功
