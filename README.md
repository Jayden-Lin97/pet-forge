# PetForge · 宠物工坊

**照片 / 视频 + 你自己的 API Key → 可离线运行的 Windows 桌宠。**

本地工作台，负责读取参考素材、制作角色和动作、检查质量、打包交付。生成服务的 Key 留在制作端，拿到成品的人不需要 Python 或 API Key。

## 开始使用

需要 Python 3.11+。完整 Windows 成品请在 Windows 10/11 64位、已安装 .NET Framework 4.8 的电脑上制作；不需要 Visual Studio。

```powershell
git clone https://github.com/Jayden-Lin97/pet-forge.git
cd pet-forge
python -m venv .venv
.venv\Scripts\python.exe -m pip install -e .
.venv\Scripts\python.exe -m petforge
```

打开 `http://127.0.0.1:8108/`：

1. 填写宠物名字，放入照片或视频。支持视频单独上传。
2. 填写自己的 OpenAI API Key，或从 `OPENAI_API_KEY` 环境变量读取。
3. 确认有权使用素材并同意发送参考帧，点击「开始制作」。
4. 全部检查通过后下载 ZIP，完整解压，双击 `PetForge.exe`。

也可运行 `start.ps1`；PowerShell 限制脚本时使用上面的逐条命令，不需要修改系统执行策略。

### 输入与输出

| 项目 | 支持范围 |
| --- | --- |
| 参考素材 | JPG、PNG、WebP、MP4、MOV、WebM、MKV；合计最多30个 |
| 容量 | 单图20MB，单视频80MB，总量200MB |
| 视频处理 | 每段取0、4、8秒关键帧，不上传原始视频；全部素材中最多选12张参考图 |
| 默认动作 | 单次舔毛、哈欠、打盹；可选增加坐起与站立 |
| 自动交付 | Windows程序、角色、透明动作图集、使用说明及SHA256 |
| 客户端 | 鼠标分区抚摸、玩具、自动活动、设置、相处记录、可选开机启动 |

当前动作集包括舔毛、哈欠、打盹，以及可选的坐起和站立。形象校准优先面向正面趴姿的猫咪；生成效果取决于参考素材和所选模型。

## 生成与质量检查

工作流使用 [GPT-Image-2.6 的参考图与 GIF 输出](https://developers.openai.com/api/docs/guides/image-generation#generate-animated-gif-output)，并通过视觉模型复核角色和动作。

```text
照片 / 视频
    ↓ 本地解码、去除图片元数据、抽取视频关键帧
参考图 → 透明基础角色 → 逐项生成短动作 GIF
    ↓ 裁切 / 体积 / 悬空 / 跳切 / 起止姿态检查
    ↓ 视觉模型检查同一角色、肢体和动作
原生帧图集 → 实际 Windows 素材加载器 → 桌宠 ZIP
```

每个形象或动作最多尝试2次，通过检查后进入打包。动作图集保留原生帧顺序；GIF编码合并的重复帧按原时长恢复，单次播放一个完整动作。

自动检查覆盖画面裁切、地面接触、体积变化、相邻帧变化和起止姿态，并由视觉模型检查角色一致性。建议下载后预览实际动作；自动检查不能替代对生成效果的判断。

## Key、费用与恢复

- Key 可在本地页面中输入，只用于这次任务，**不写入工作文件、日志、浏览器存储或成品包**。
- 图片与选取的视频帧会发送到配置的模型服务。原始文件、生成素材和任务保存在本地 `.work/`，不会自动删除；不需要时关闭程序后自行删除相应任务目录。
- 基础形象和每个动作均包含一次图像生成与一次视觉检查，失败可能增加生成和检查次数；模型服务按自身规则收费。本项目不保证固定生成费用。
- 默认图像模型 `gpt-image-2.6-sunburst`，视觉模型 `gpt-4.1-mini`。可在页面中改名，需确认账号有相应权限和 GIF 支持；模型可用性随服务端变化。
- 中断后重新提供 Key 可恢复，已完成素材按哈希复用，不重发已完成的生成请求。**请求超时或返回不明不会自动重试**；先检查提供商用量，再明确允许重新提交，可能额外计费。
- 鉴权或网络失败可以恢复。两轮质量失败建议调整参考素材并新建任务；保留旧文件便于查原因。
- 如需使用你信任的兼容服务，在启动前设置 `PETFORGE_API_BASE`。此设置会改变 Key 和参考帧的接收方，不能填入不信任的地址。
- 工作台仅绑定 `127.0.0.1`，包含 Origin、Host 和 CSRF 检查；**不是可直接暴露公网的多租户服务**。

请使用有相应模型权限和可用额度的 API Key。鉴权或权限不足时，页面会提示处理方式。

## 开发与验证

```powershell
.venv\Scripts\python.exe -m unittest discover -s tests -v
powershell -NoProfile -File tools/build-runtime.ps1
```

自动化测试使用本地测试素材和模拟 API，不产生模型调用费用。Windows 集成测试会构建桌宠程序并校验动作包，Linux 检查覆盖制作流程和本地服务。

```text
petforge/       上传工作台、API适配、视频读取、质量检查、任务和打包
runtime/src/    离线 Windows WPF 桌宠源码
tools/          Windows 构建、素材校验、发布前检查
tests/          不依赖真实账号的集成与回归测试
.github/        Linux / Windows 持续集成
```

`PETFORGE_RUNTIME` 可指向自行构建的可信Windows程序，方便将生成服务运行在其他系统上；设置这个变量不会把程序变成跨平台客户端。

## 许可

项目采用 [MIT License](LICENSE)，设计参考与依赖见 [THIRD_PARTY.md](THIRD_PARTY.md)。请使用有权使用的输入素材，并遵守所选模型服务的使用条款。

---

**English:** A local BYOK workshop for turning pet photos and videos into an offline Windows desktop pet. It extracts reference frames, generates transparent characters and short GIF motions, runs technical and vision quality checks, and packages the result. API keys and original references stay out of the delivery ZIP. Requires Python 3.11+ and Windows 10/11 x64 for the desktop build. MIT licensed.
