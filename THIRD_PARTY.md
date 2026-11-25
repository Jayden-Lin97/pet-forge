# 来源与依赖

PetForge 的行为调度与桌面交互设计参考了以下开源项目。本仓库采用独立实现，不包含这些项目的代码或角色素材：

- [RJ1107/desktop-pet](https://github.com/RJ1107/desktop-pet)：温和的需求调度、行为冷却、共享图集缓存和存档备份；参考提交 `c2a0e8ed9fe08445a58fdf6e19e7e59885432eee`。
- [LorisYounger/VPet](https://github.com/LorisYounger/VPet)：动作进入、驻留、退出和可打断调度；参考提交 `2dc1fadd65748d1f725ce25b6f97dd727453d795`。
- [DalekCraft2/Shimeji-Desktop](https://github.com/DalekCraft2/Shimeji-Desktop)：声明式行为和窗口边陪伴；参考提交 `ed737036ee3002d88fc8556a025592c3c7d86cc9`。

Python依赖由安装工具获取，不在源码仓库内捆绑：

| 依赖 | 用途 | 上游许可 |
| --- | --- | --- |
| Flask / Werkzeug | 本地HTTP工作台与上传 | BSD-3-Clause |
| Requests | 模型服务HTTP请求 | Apache-2.0 |
| Pillow | 本地图像/GIF解码与图集 | HPND |
| NumPy | 图像一致性数值检查 | BSD-3-Clause |
| imageio-ffmpeg | FFmpeg可执行文件定位与视频解码 | BSD-2-Clause（Python封装）；FFmpeg构建另附自身许可 |

运行时使用 Windows / .NET Framework 4.8 / WPF 系统组件。没有把 .NET Framework 安装包放进成品。FFmpeg在制作端使用，不放进桌宠交付ZIP。

模型服务不属于开源代码的一部分。OpenAI及其他可选提供商按自身许可、价格和使用政策提供服务。本项目的MIT许可不授予第三方照片、宠物肖像或模型服务的额外权利。

`tests/fixtures.py` 使用独立绘制的角色，用于自动化测试。
