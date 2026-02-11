## Overview
ExcelChat is a desktop application that allows you to edit Excel spreadsheets through natural language conversation with AI. Powered by DeepSeek's language model, it bridges the gap between intent and execution in spreadsheet management.

## How It Works
- **AI Backend**: Python scripts handle Excel operations using the DeepSeek API
- **Web Capture**: Chrome browser integration for AI conversation interface
- **Desktop UI**: Godot engine provides the application interface
- **Communication**: Python backend and Godot UI communicate via socket on port 12111

## Key Features
- Natural language to Excel operations (formulas, formatting, data cleaning)
- Real-time preview of changes before application
- Conversation history and edit tracking
- Support for complex Excel functions through simple commands

## System Requirements
- **Operating System**: Windows 10/11 only
- **Required Software**: 
  - Microsoft Excel (Office 2016 or newer)
  - Google Chrome (latest version)
- **Network**: Internet connection for AI processing

## Installation & Usage
1. Download the latest release from [Releases Page]
2. Install the application (ensure Excel and Chrome are already installed)
3. Launch ExcelChat and connect to your open Excel workbook
4. Start chatting with AI to modify your spreadsheet

## Architecture Note
This application uses a dual-process architecture where the Godot UI (port 12111) communicates with Python backend via local socket connection for optimal performance and stability.

---

## 项目概述
ExcelChat 是一款桌面应用程序，让您能够通过与AI的自然语言对话来编辑Excel电子表格。基于DeepSeek语言模型，无需API。

## 工作原理
- **AI后端**：Python脚本使用DeepSeek API处理Excel操作
- **网页捕获**：Chrome浏览器集成提供AI对话界面
- **桌面UI**：基于Godot引擎开发的应用程序界面
- **通信方式**：Python后端和Godot UI通过端口12111的socket进行通信

## 核心功能
- 自然语言转Excel操作（公式、格式化、数据清洗）
- 应用更改前的实时预览
- 对话历史和编辑跟踪
- 通过简单命令支持复杂的Excel函数

## 系统要求
- **操作系统**：仅限Windows 10/11
- **必备软件**：
  - Microsoft Excel（Office 2016或更新版本）
  - Google Chrome（最新版本）
- **网络**：需要互联网连接进行AI处理

## 安装与使用
1. 从[发布页面]下载最新版本
2. 安装应用程序（请确保已安装Excel和Chrome）
3. 启动ExcelChat并连接到您已打开的Excel工作簿
4. 开始与AI对话来修改您的电子表格

## 架构说明
本应用程序采用双进程架构，Godot UI（端口12111）通过本地socket连接与Python后端通信。

---
