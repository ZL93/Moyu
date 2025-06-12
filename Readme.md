# Moyu 小说阅读器

Moyu 是一个基于 C# (.NET Framework 4.8) 的本地小说阅读器，支持 TXT 和 EPUB 文件，具备书架管理、进度保存、章节跳转、自动阅读等功能，适合在 Windows 控制台环境下使用。

## 主要特性

- 支持 TXT、EPUB 格式小说导入与阅读
- 书架分页浏览，支持键盘快捷操作
- 阅读进度自动保存，支持章节跳转
- 自动阅读模式（可调节速度，随时切换/暂停）
- Boss 键（隐藏界面）
- 支持环境变量设置（便于命令行快速启动）
- 简洁的控制台界面，适配中文显示宽度
- 操作说明可随时开关

## 快速开始

### 环境要求

- Windows 系统
- .NET Framework 4.8
- Visual Studio 2022 或以上

### 编译与运行

1. 使用 Visual Studio 打开本项目解决方案。
2. 还原依赖并编译项目。
3. 运行 `Moyu` 控制台程序。

### 添加小说

- 启动后按 `O`，输入 TXT 或 EPUB 文件/文件夹完整路径，即可导入小说。
- 支持批量导入文件夹内所有 TXT/EPUB 文件。

## 主要操作

- `↑/W` `↓/S`：选择书籍
- `Enter`：打开选中书籍进行阅读
- `←/A` `→/D`：分页浏览书架或翻页阅读
- `O`：添加小说
- `Delete`：删除选中小说
- `P`：设置环境变量
- `H`：开启/关闭操作说明
- `T`：阅读时选择章节
- `空格`：阅读时切换自动阅读模式（再次空格退出自动阅读）
- `+/-`：自动阅读时调节速度
- `ESC`：返回或退出
- `·` 或 `` ` ``：Boss 键隐藏界面

## 目录结构

- `Program.cs`：主程序入口
- `UI/ConsoleUI.cs`：控制台界面与交互逻辑
- `Services/BookService.cs`、`Services/TxtBookService.cs`、`Services/EpubBookService.cs`：书籍管理与格式适配
- `Models/BookInfo.cs`、`Models/ChapterInfo.cs`、`Models/BookFormatEnum.cs`：数据模型
- `Utils/ConsoleHelper.cs`、`Utils/TextFileReader.cs`、`Utils/SysEnvironment.cs`：工具类
- `Config.cs`：配置管理

## 进阶说明

- 支持中文、全角字符显示宽度适配
- 阅读进度、书架信息自动持久化
- Boss 键可快速隐藏界面，适合办公环境
- 自动阅读可随时切换，速度可调
- **设置环境变量后，可以方便的在VS终端中使用**

## 许可

本项目仅供学习与交流使用。