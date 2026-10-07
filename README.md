# LGT-LXL · 六级单词对战

> 给两个人用的六级背单词 App：内置百词斩 CET6 词库（**3991 词**），两个用户各自记进度并可在两台手机之间**同步进度**；背单词要过「英译中 + 中译英」两关才算背过；还有**复习**和 **50 / 100 词的单词对战**（本机轮流 或 两台手机 Wi-Fi 直连联机）。全程离线可用。

用 **.NET MAUI（.NET 10 / `net10.0-android`）** 写的安卓应用。解锁密码 `lgt520lxl`，应用名 **LGT-LXL**。

当前版本 **v1.5** ｜ 包名 `com.lgtlxl.vocab` ｜ Android 7.0+（minSdk 24，arm64）

## ✨ 功能

| 功能 | 说明 |
| --- | --- |
| 内置六级词库 | 百词斩 CET6 三本词书合并去重 **3991 词**，含音标 / 中文释义 / 例句，打包在 APK 内 |
| 背单词（两关制） | 第 1 关「英语 → 中文」、第 2 关「中文 → 英语」；**两关都答对**才真正算背过并进入复习计划 |
| 答错重来 | 任意一关答错，这个词会重新排回队列，**直到答对为止**（答错时展示音标、释义、例句） |
| 复习 | 首页独立入口，显示「今日 N 个词到期」；按 1/2/4/7/15/30 天排期，等级 ≥3 的词只考「中 → 英」 |
| 单词对战 | 从两人已学单词抽 **50 / 100 词**，每题 1 分，分高者胜，结果写入历史战绩 |
| 本机轮流对战 | 一台手机，两个人先后答同一套题，自动比分 |
| 联机对战 | 两台手机连同一个 Wi-Fi / 热点，TCP 直连 + UDP 自动搜索房主，无需服务器和流量 |
| 同步进度 | 两台手机互相合并「单词进度 / 每日统计 / 对战记录」，同步后两边都能看到两个人的数据 |
| 双人独立记录 | LGT、LXL 各自的已学、掌握、连续天数、对战战绩互不影响 |
| 完全离线 | 词库在包内、进度存本地 JSON；只有「联机 / 同步」需要两台手机在同一局域网 |

## 📲 安装

1. 到 [Releases](../../releases) 下载最新的 `LGT-LXL-vX.Y.apk`。
2. 传到手机上安装，第一次要允许「未知来源应用」；覆盖安装不会丢进度。
3. 打开后输入密码 `lgt520lxl`（可勾选「在这台手机上记住密码」）。

## 🎮 怎么用

### 背单词（两关制）

首页 → **📖 背单词（学新词）**

- 第 1 关：**英语 → 中文**；答对后隔 2 题考第 2 关：**中文 → 英语**。
- **两关都答对**才算真正背过，才计入「已学」并进入复习计划（答对升级，答错回到第 1 级）。
- 任意一关答错 → 该词重新排回队列，**直到答对为止**。

### 复习

首页 → **🔁 复习**：优先复习到期词；没到期的就随机抽已学单词。

### 单词对战

首页 → **⚔️ 单词对战** → 选 50 / 100 词 → 选方式：

- **本机轮流**：一台手机两人先后答同一套题。
- **联机对战**：房主点「创建房间」，另一方点「🔍 自动搜索附近的房主」（或手动输入房主屏幕上列出的地址），点一下即可开局。
- 两人已学单词不足 5 个时，可以直接选「🎲 用词库随机词开一局」先把联机跑通。

### 同步进度

首页 → **🔄 同步进度（两台手机）**：一台「📡 我来发起同步」，另一台「🔍 搜索」或输地址。
合并规则：同一个词以**复习时间更近**的为准；每日统计取两边较大值；对战记录去重合并。

### 连不上怎么办（按顺序排查）

1. 两台手机必须在**同一个** Wi-Fi / 热点下；
2. 最稳的方式是**一台手机开热点、另一台连热点**（不经过路由器）；
3. 房主先点「创建房间 / 发起同步」，屏幕出现地址后另一方再搜索/输入；
4. 校园网、公共 Wi-Fi、部分路由器会开「**设备隔离 / AP 隔离**」，这种情况下任何两台设备都互相连不上，必须改用热点；
5. 房主可能同时有多个地址（`192.168.1.5（Wi-Fi）`、`192.168.43.1（热点）`），加入方挑一个试，或直接写 `IP:端口`。

## 🔨 构建

### Visual Studio

1. 安装 Visual Studio 2022 / 2026，勾选 **.NET MAUI** 工作负载。
2. 打开 `LgtLxlVocab/LgtLxlVocab.csproj`，配置选 `Release`、目标框架选 `net10.0-android`，「生成 → 发布」。

### 命令行

```bash
dotnet workload install maui-android
dotnet publish LgtLxlVocab/LgtLxlVocab.csproj \
  -f net10.0-android -c Release -p:AndroidPackageFormats=apk
# 产物：LgtLxlVocab/bin/Release/net10.0-android/publish/*-Signed.apk
```

### 自动打包（可选）

把 `ci/build-apk.yml` 复制到 `.github/workflows/build-apk.yml`，之后每次推送都会自动构建 APK 并作为 artifact 上传。
（GitHub 规定：用令牌写入 `.github/workflows/` 需要令牌额外带 `workflow` 权限，所以在网页上创建一次最省事。）

### ⚠️ 一个坑（很重要）

**不要**把 csproj 里的这几项改成 `None` / `false`：

```
AndroidLinkMode / PublishTrimmed / RunAOTCompilation / AndroidEnableProfiledAot
```

官方模板 Release 的默认组合是 `SdkOnly + true + true + true`，全关掉会出现「**启动图之后白屏闪退**」（.NET Android 不支持的组合）。

### 签名

仓库里带了 `LgtLxlVocab/LgtLxl.keystore`（别名 `lgtlxl`，密码 `lgt520lxl`），任何电脑打出来的 APK 都能互相覆盖安装，不用卸载重装。

## 🗂 项目结构

```
LgtLxlVocab/
├── App.xaml / App.xaml.cs        程序入口，启动到密码锁屏
├── MauiProgram.cs                MAUI 启动配置
├── Models.cs                     WordEntry / WordProgress / UserProfile / BattleRecord / Question / QuizMode
├── AppState.cs                   词库加载、双人进度存档、出题算法、复习排期、进度同步合并
├── CrashReporter.cs              崩溃自诊断（屏幕显示 + 写日志文件）
├── LanBattle.cs                  局域网 TCP 通信（监听 / 连接 / 收发 JSON / 本机地址枚举）
├── LanDiscovery.cs               UDP 广播自动发现房主
├── SyncPage.cs                   两台手机同步学习进度
├── Ui.cs                         配色与控件工厂（页面全部用 C# 写，没有 XAML 页面）
├── Pages1.cs                     密码锁屏 / 选择用户 / 首页
├── Pages2.cs                     背单词（两关制）/ 复习 / 学习进度 / 单词列表
├── Pages3.cs                     对战设置 / 联机房间
├── Pages4.cs                     答题页 / 对战结果 / 历史战绩
├── Platforms/Android/            Android 清单与入口 Activity
└── Resources/Raw/cet6_words.json 内置词库（3991 条）
```

## 📚 词库

- 来源：百词斩 CET6 词书（核心 / 真题等三本）的离线数据，经 [kajweb/dict](https://github.com/kajweb/dict) 整理，合并去重 3991 词。
- 字段：`word` / `phonetic` / `translation` / `example` / `exampleCn`。
- 换词表只需替换 `Resources/Raw/cet6_words.json`，代码不用改。
- 词库数据仅用于个人学习交流，请勿商用。

## 🔐 数据与隐私

所有数据只存在手机本地：`lgtlxl/profiles.json`（双人进度、每日统计、对战记录）、`lgtlxl/words.json`（词库缓存）。
App 不联网、不上传任何数据；「联机 / 同步」只在两台手机之间直连。
注意：**卸载 App 会清空本机进度**（重装后可以用「同步进度」把对方手机上的进度合并回来）。

## 🛠 常见改造点

| 想改什么 | 改哪里 |
| --- | --- |
| 解锁密码 | `AppState.cs` 的 `Password` |
| 两个用户的名字 / 头像 | `AppState.cs` 的 `UserNames`、`LoadAsync()` |
| 每组背多少词 | `Pages2.cs` 里 `StudyPage.SessionSize` |
| 第 2 关隔几题出现 / 答错后隔几题重考 | `Pages2.cs` 里 `Enqueue(..., 2)` 与 `Enqueue(..., 3)` |
| 复习间隔 | `AppState.ReviewDays` |
| 每日目标 | `UserProfile.DailyGoal` |
| 联机端口 | `LanBattle.Port`（占用时自动往后试） |
| UDP 发现端口 | `LanDiscovery.UdpPort` |

## 📝 版本记录

- **v1.5**：修复「对战入口跳到背单词页」；联机 / 同步大改（枚举全部本机地址并标注 Wi-Fi/热点、子网广播搜索、端口自动切换、连接超时提示、抽题范围修正）。
- **v1.4**：新增「同步进度」；修复 IP 输入框打不出小数点；新增「复习」。
- **v1.3**：修复 Release 配置错误导致的启动白屏闪退（恢复官方裁剪 + AOT）。
- **v1.1**：背单词改成两关制（英译中 + 中译英），答错重考直到答对。
- **v1.0**：首个版本。

## 📄 License

[MIT](LICENSE)
