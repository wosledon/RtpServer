
# RtpServer

一个用 C# (.NET 8) 编写的轻量级 RTP/RTCP 处理与转换示例项目。

该仓库包含一个简单的 RTP 服务器实现、RTCP 构建/解析器、以及与 FLV 流转换相关的工具和测试用例。

## 主要特性

- RTP 包解析与构建
- RTCP 包解析与构建
- RTP -> FLV 的转换辅助（用于简单流媒体处理示例）
- 单元测试覆盖核心逻辑

## 目录结构

- `src/RtpServer/` - 项目源代码
	- `Program.cs` - 程序入口（示例/运行）
	- `RtpServer.cs` - RTP 服务主要逻辑
	- `RtpPacket.cs` / `RtcpPacket.cs` - RTP/RTCP 包模型与解析
	- `RtcpBuilder.cs` - RTCP 构建器
	- `RtpExtensions.cs` - 附加的扩展方法与配置
	- `RtpToFlvConverter.cs` - （FLV 转换器）RTP -> FLV 辅助
- `tests/RtpServer.Tests/` - 单元测试项目

（开发过程中可能产生的 `bin/` 和 `obj/` 目录已被忽略或加入到仓库的构建输出）

## 先决条件

- .NET SDK 8.x
- 推荐在 Windows 下使用 PowerShell（但也支持 macOS / Linux 的 bash / zsh）

## 快速开始（PowerShell）

1. 克隆仓库并进入目录：

```powershell
git clone https://github.com/wosledon/RtpServer.git
cd RtpServer
```

2. 构建项目：

```powershell
dotnet build RtpServer.sln -c Debug
```

3. 运行（示例）：

```powershell
dotnet run --project src/RtpServer -c Debug
```

4. 运行测试：

```powershell
dotnet test tests/RtpServer.Tests -c Debug
```

## 代码说明（简要）

- Rtp/RTCP 相关的解析与构建都放在 `src/RtpServer` 下对应的文件中，测试在 `tests/RtpServer.Tests` 下覆盖关键逻辑。若要理解实现细节，可从 `RtpPacket.cs` 与 `RtcpBuilder.cs` 开始阅读。
- `RtpToFlvConverter.cs` 提供了将接收到的 RTP 数据转换为 FLV 片段的辅助逻辑（适用于演示/实验目的）。

## 测试

项目包含一组单元测试。建议在修改核心逻辑后运行测试来保证行为不回归：

```powershell
dotnet test
```

## 贡献

欢迎 issue、PR 与建议。贡献流程：

1. 提交 issue 描述你要修复的 bug 或新增的功能。
2. Fork 仓库并新建分支（feature/xxx 或 fix/xxx）。
3. 提交 PR，并在 PR 描述中说明变更与测试覆盖情况。

请尽量保持提交信息清晰、单一变更，并包含必要的单元测试。

## 许可证

本项目使用 MIT 许可证（如果需要，请在仓库中添加 `LICENSE` 文件并在此处替换许可证类型）。

## 联系

如果有疑问或需要协助，请在 GitHub 上创建 issue。仓库拥有者：wosledon。

---

（此 README 由自动化助手生成，欢迎根据项目细节补充/修改）

