# LGT-LXL · 六级单词对战

> 给两个人用的六级背单词 App：内置百词斩 CET6 词库（**3991 词**），两个用户各自记进度，还能从两人已学单词里抽 **50 / 100 词**打对战——本机轮流，或者两台手机联机（Wi-Fi 直连，不耗流量、不需要服务器）。全程离线可用。

用 **.NET MAUI（.NET 10 / `net10.0-android`）** 写的安卓应用。解锁密码 `lgt520lxl`，应用名就是 **LGT-LXL**。

## ✨ 功能

| 功能 | 说明 |
| --- | --- |
| 内置六级词库 | 3991 个单词，每个词都有音标、中文释义、例句，全部打包进 APK |
| 双人各自进度 | LGT / LXL 的已学词数、掌握词数、连续天数、对战战绩完全独立，随时切换身份 |
| 背单词 | 先复习到期词、再补新词；答对升级、答错回到第 1 级，按 1 / 2 / 4 / 7 / 15 / 30 天间隔安排复习 |
| 单词对战 | 从两人已学单词中抽 50 词或 100 词，每题 1 分，分高者胜，结果写进历史战绩 |
| 本机轮流对战 | 一部手机，两个人先后答同一套题，自动对比得分 |
| 联机对战 | 两台手机连同一个 Wi-Fi / 热点，TCP 直连（端口 47615），不用服务器也不耗流量 |
| 完全离线 | 词库在包内、进度存本地 JSON，飞行模式也能背单词、记进度、看战绩 |

## 📲 安装

1. 到 [Releases](https://github.com/) 下载 `LGT-LXL-*.apk`（或者自己构建，见下）。
2. 传到手机上安装，第一次会提示「未知来源应用」，允许即可。
3. 打开后输入密码 `lgt520lxl`，可勾选「在这台手机上记住密码」。

要求：Android 7.0+（minSdk 24），arm64 设备。

## 🔨 构建

### Visual Studio

1. 安装 Visual Studio 2022 / 2026，勾选 **.NET MAUI** 工作负载（含 Android SDK）。
2. 打开 `LgtLxlVocab/LgtLxlVocab.csproj`。
3. 配置选 `Release`，目标框架选 `net10.0-android`，右键「发布」即可得到 APK。

### 命令行

```bash
dotnet workload install maui-android
dotnet publish LgtLxlVocab/LgtLxlVocab.csproj \
  -f net10.0-android -c Release -p:AndroidPackageFormats=apk
# 产物：LgtLxlVocab/bin/Release/net10.0-android/publish/*-Signed.apk
```

想让 GitHub 每次推送都自动打包 APK：把仓库里的 `ci/build-apk.yml` 复制到 `.github/workflows/build-apk.yml` 即可。
（GitHub 规定通过令牌写入 `.github/workflows/` 需要额外勾选 `workflow` 权限，所以在网页上创建/粘贴一次最省事。）

### 签名

仓库里带了签名文件 `LgtLxlVocab/LgtLxl.keystore`（别名 `lgtlxl`，密码 `lgt520lxl`），所以任何电脑打包出来的 APK 都能互相覆盖安装，不用卸载重装。

## 🗂 项目结构

```
LgtLxlVocab/
├── App.xaml / App.xaml.cs        程序入口，启动到密码锁屏
├── MauiProgram.cs                 MAUI 启动配置
├── Models.cs                      WordEntry / WordProgress / UserProfile / BattleRecord / Question
├── AppState.cs                    词库加载、两个用户的进度存档、出题与复习算法
├── LanBattle.cs                   联机对战的 TCP 通信（监听、连接、收发 JSON 消息）
├── Ui.cs                          配色与控件工厂（页面全部用 C# 写，没有 XAML 页面）
├── Pages1.cs                      密码锁屏 / 选择用户 / 首页
├── Pages2.cs                      背单词 / 学习进度 / 单词列表
├── Pages3.cs                      对战设置 / 联机房间
├── Pages4.cs                      答题页 / 对战结果 / 历史战绩
├── Platforms/Android/             Android 清单与入口 Activity
└── Resources/Raw/cet6_words.json  内置词库（3991 条）
```

界面代码是纯 C# 构建的（不写 XAML 页面），改起来比较直接。

## 🌐 联机对战怎么用

1. 两台手机连到同一个 Wi-Fi；或者 A 手机开热点、B 手机连这个热点。
2. A：单词对战 → 选词数 → 联机对战 → **创建房间**，屏幕会显示本机 IP。
3. B：单词对战 → 选词数 → 联机对战 → 输入那个 IP → **加入**。
4. 两边同时答题，答完自动对比分数并写入战绩。

连不上通常是：两台手机不在同一网络，或热点开了「设备隔离」。换网络重试即可。

## 📚 词库

- 来源：百词斩 CET6 词书（核心词汇 / 真题词汇等三本）的离线数据，合并去重后 3991 词，经 [kajweb/dict](https://github.com/kajweb/dict) 整理的版本提取。
- 字段：`word` / `phonetic` / `translation` / `example` / `exampleCn`。
- 想换词表（四级、考研、雅思…）：替换 `Resources/Raw/cet6_words.json` 即可，代码不用改。
- 词库数据仅用于个人学习交流，请勿商用。

## 🔐 数据与隐私

所有数据只存在手机本地：`lgtlxl/profiles.json`（两个用户的进度、每日统计、对战记录）和 `lgtlxl/words.json`（词库缓存）。App 不联网、不上传任何数据；联机对战也只在两台手机之间直连。

## 🛠 小改法

| 想改什么 | 改哪里 |
| --- | --- |
| 解锁密码 | `AppState.cs` 里的 `Password` 常量 |
| 两个用户的名字 / 头像 | `AppState.cs` 里的 `UserNames` 和 `LoadAsync()` |
| 每日目标词数 | `UserProfile.DailyGoal` |
| 复习间隔 | `AppState.ReviewDays` |
| 对战词数选项 | `Pages3.cs` 的 `BattleSelect` 部分 |
| 联机端口 | `LanBattle.Port` |

## 📄 License

[MIT](LICENSE)
