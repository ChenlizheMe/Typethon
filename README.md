# Typethon - Unity Python Integration Tool

[中文](#中文说明) | [English](#english)

---

## 中文说明

### 项目简介

Typethon 是一个Unity与Python集成的工具，允许您在Unity项目中直接编写和执行Python代码。它提供了实时代码执行、逐步调试、状态指示器等功能，是开发和原型制作的强大工具。

### 主要功能

- **实时Python代码执行**：在Unity中直接运行Python代码
- **多种执行模式**：
  - 普通执行（Ctrl+R）
  - 延迟执行（Ctrl+D）：每行间添加延迟
  - 逐步执行（Ctrl+E）：需要手动按下箭头键继续执行
- **状态指示器**：显示当前执行状态（编辑/运行/错误）
- **自动Agent服务**：在打包的exe中自动运行后台Python服务
- **行跟踪功能**：实时显示当前执行的代码行
- **输出管理**：实时显示Python执行结果和错误信息

### 安装教程

#### 步骤1：创建Unity项目
1. 打开Unity Hub
2. 创建一个新的3D项目
3. 等待项目初始化完成

#### 步骤2：替换Assets文件夹
1. 从GitHub下载或克隆本仓库：
   ```bash
   git clone https://github.com/ChenlizheMe/Typethon.git
   ```
2. 关闭Unity编辑器
3. 删除您Unity项目中的 `Assets` 文件夹
4. 将本仓库中的 `Assets` 文件夹复制到您的Unity项目根目录
5. 重新打开Unity项目

#### 步骤3：配置Python环境
1. 确保您的系统已安装Python 3.7+
2. 在项目中找到 `ShortcutManager` 组件
3. 根据需要配置Python可执行文件路径

### 使用方法

#### 快捷键操作
- **Ctrl+R**：执行当前代码
- **Ctrl+C**：终止执行
- **Ctrl+D**：执行代码（每行间有延迟）
- **Ctrl+E**：逐步执行（需按下箭头键继续）
- **↓（下箭头）**：在逐步模式下继续执行下一行

#### 状态指示器
- **黄色**：编辑模式
- **绿色**：代码执行中
- **红色**：执行出错

#### 代码编辑
在代码输入框中编写Python代码，支持多行代码和复杂逻辑。

### 打包部署

当您将项目打包为exe文件时：
1. 在exe同目录下放置 `agent_service.py` 文件（如需要）
2. 创建 `.pyenv` 文件夹并安装所需的Python环境
3. 系统会自动启动后台Python服务

### 系统要求

- Unity 2020.3 LTS 或更高版本
- Python 3.7+
- Windows 操作系统（主要支持）

---

## English

### Project Overview

Typethon is a Unity-Python integration tool that allows you to write and execute Python code directly within Unity projects. It provides real-time code execution, step-by-step debugging, status indicators, and other powerful features for development and prototyping.

### Key Features

- **Real-time Python Code Execution**: Run Python code directly in Unity
- **Multiple Execution Modes**:
  - Normal execution (Ctrl+R)
  - Delayed execution (Ctrl+D): Adds delay between lines
  - Step-by-step execution (Ctrl+E): Manual progression with arrow keys
- **Status Indicator**: Shows current execution state (Edit/Run/Error)
- **Automatic Agent Service**: Auto-runs background Python service in packaged exe
- **Line Tracking**: Real-time display of currently executing code line
- **Output Management**: Real-time display of Python execution results and errors

### Installation Guide

#### Step 1: Create Unity Project
1. Open Unity Hub
2. Create a new 3D project
3. Wait for project initialization to complete

#### Step 2: Replace Assets Folder
1. Download or clone this repository from GitHub:
   ```bash
   git clone https://github.com/ChenlizheMe/Typethon.git
   ```
2. Close Unity Editor
3. Delete the `Assets` folder in your Unity project
4. Copy the `Assets` folder from this repository to your Unity project root directory
5. Reopen Unity project

#### Step 3: Configure Python Environment
1. Ensure Python 3.7+ is installed on your system
2. Find the `ShortcutManager` component in the project
3. Configure the Python executable path as needed

### Usage

#### Keyboard Shortcuts
- **Ctrl+R**: Execute current code
- **Ctrl+C**: Terminate execution
- **Ctrl+D**: Execute code with delay between lines
- **Ctrl+E**: Step-by-step execution (requires arrow key to continue)
- **↓ (Down Arrow)**: Continue to next line in step-by-step mode

#### Status Indicator
- **Yellow**: Edit mode
- **Green**: Code executing
- **Red**: Execution error

#### Code Editing
Write Python code in the input field, supporting multi-line code and complex logic.

### Build and Deployment

When building your project as an exe file:
1. Place `agent_service.py` file in the same directory as the exe (if needed)
2. Create `.pyenv` folder and install required Python environment
3. The system will automatically start the background Python service

### System Requirements

- Unity 2020.3 LTS or higher
- Python 3.7+
- Windows Operating System (primary support)

### Contributing

Feel free to submit issues and pull requests to help improve this project.

### License

This project is open source. Please refer to the LICENSE file for details.

---

## Support

If you encounter any issues or have questions, please:
1. Check the Unity Console for error messages
2. Ensure Python is properly installed and configured
3. Submit an issue on GitHub with detailed information

**Happy Coding with Typethon! 🐍🎮**