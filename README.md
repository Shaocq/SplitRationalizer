# 拆分合理化（SplitRationalizer）

[![游戏](https://img.shields.io/badge/游戏-Escape%20from%20Duckov-blue)](https://store.steampowered.com/app/3167020/)
[![协议](https://img.shields.io/badge/协议-MIT-yellow)](LICENSE)
[![版本](https://img.shields.io/badge/版本-1.0.0-brightgreen)](CHANGELOG.md)

> 在物品拆分界面加一个「物品防吞」按钮：一键把拆分数量设为该物品的**原版堆叠上限**。

| 项目 | 信息 |
| --- | --- |
| 游戏 | 逃离鸭科夫（Escape from Duckov，AppID `3167020`） |
| 框架 | `Duckov.Modding.ModBehaviour` |
| 前置 | HarmonyLib（工坊 `3589088839`），必须置于本 mod **之前**加载 |
| 当前版本 | 1.0.0 |
| 开源协议 | [MIT](LICENSE) |

## 为什么需要它

原版拆分界面里，数量滑块的最大值取决于**当前堆叠里有多少**，想把整组拆到接近上限时只能一下一下点。装了堆叠类 mod 之后更是容易被「吞物品」的边界情况坑到。

本 mod 的做法是：**直接从游戏本体的物品数据里读原版堆叠上限**，不去猜、也不受其它堆叠 mod 的运行时改动影响，把这个值一键填进拆分数量。

## 功能特性

- **「物品防吞」按钮**：注入到原版拆分界面，点击后把拆分数量设为该物品的原版叠加上限；
- **不受其它 mod 影响**：上限来自游戏本体的 `ItemMetaData`，而不是当前堆叠或其它 mod 的运行时改写；
- **叠加上限导出**：启动时把本体所有物品的叠加上限导出为 `vanilla_stack_limits.csv`，方便排查与做兼容；
- 复用原版按钮的外观与按压动画，不破坏原版布局。

## 安装

1. 订阅前置 **HarmonyLib**（工坊 `3589088839`），并确保它排在本 mod **之前**加载；
2. 把 `SplitRationalizer` 文件夹放入 `Escape from Duckov\Duckov_Data\Mods\`。

## 输出文件

| 文件 | 说明 |
| --- | --- |
| `vanilla_stack_limits.csv` | 启动时导出，记录本体所有物品的 TypeID 与原版叠加上限，**运行时生成**，不属于发布包 |

## 从源码构建

```bash
dotnet build SplitRationalizer.csproj -c Release
```

游戏不在默认路径时加 `-p:DuckovPath="你的游戏目录"`。

> `lib/0Harmony.dll` 不在仓库内。编译前请从创意工坊前置 HarmonyLib 目录复制一份到 `lib/`。

## 更新日志

完整版本记录见 [CHANGELOG.md](CHANGELOG.md)。

## 许可证

本项目以 [MIT 协议](LICENSE) 开源，可自由使用、修改与再发布，保留版权声明即可。

## 致谢

- 游戏与 mod 框架：TeamSoda《逃离鸭科夫》
- Harmony 补丁框架：[Harmony](https://github.com/pardeike/Harmony)（MIT）
