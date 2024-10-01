# WinDesktopTodoList

这是一个使用WPF开发的桌面待办事项应用程序。
只因在网上没有找到一个自己满意的待办事项应用程序，唯一一个感觉还行的完整功能居然要40RNB（包含了软件其他功能），所以一气之下（bushi）打算自己写一个。
因为零基础，所以借助了`GPT`和`Github Copilot`的帮助，代码可能写的比较乱，也没考虑分文件啥的，先能用就行。

## 功能
- 待办事项的增删
- 嵌入桌面（不会覆盖其他应用）
- 多种主题（黑、白、透明、高斯模糊透明、自定义）
- 数据保存到本地
- 配置文件修改属性

### 按钮功能
四个按钮的功能从左到右分别是：
1. 从本地文件重新加载数据
2. 切换主题
3. 清空已办事项
4. 项目介绍

### 如何安装
1. 在release中下载压缩包，将所有内容解压到你想要的目录即可。
2. 自行拉取仓库并用VS生成解决方案

### 如何自定义主题
1. 打开`src/theme.json`文件
2. 修改`Background`和`Foreground`的值，四位数字分别为`A`、`R`、`G`、`B`，范围为`0-255`，例如`255,255,255,255`表示完全不透明的白色
3. 保存文件，点击按钮切换应用程序主题即可

### 嵌入桌面
由于嵌入桌面用到了`user32.dll`中的`SetParent`函数，原理是将应用程序的窗口句柄设置为桌面窗口`Progman`的子窗口，所以在不同的设备上不能保证一定工作，如果不工作请尝试重启windows资源管理器，如果仍然不行请自行尝试修改代码解决。未来可能会增加是否嵌入桌面的选项。
本人测试环境：Windows 11 Professional 23H2

### 如何退出
程序默认在任务栏隐藏，只在Windows托盘中显示，右键点击托盘图标，选择退出即可。

### 如何设置开机自启动
1. 将程序复制到一个固定的位置，例如`D:\WinDesktopTodoList\`
2. 创建一个快捷方式，将快捷方式放到`C:\Users\你的用户名\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup`文件夹中
3. 完成

### 配置文件说明
- `debug`: 是否开启调试
- `scale_factor`: Windows系统缩放倍数
- `width`: 宽度
- `height`: 高度
- `default_theme`: 默认主题（0-4分别为黑、白、透明、高斯模糊透明、自定义）
- `embed_to_desktop`: 是否嵌入桌面

## 目前已知的问题
- 因为零基础，所以不太清楚怎么优化C#和xmal，内存占用有点多
- 因为本人Windows缩放设置为125%，目前在配置文件中默认值的125%，如果你的Windows缩放设置不是125%那么高斯模糊透明会显示不正常，请自行在配置文件中修改为正确的值
- 无法保证在所有设备上都能正常嵌入桌面
- 程序拖动过程中高斯模糊透明不会更新，只有在停止拖动后才会更新
- 透明主题实际上不是真正的透明，而是有1/255的不透明度，因为完全透明会导致无法拖动窗口而且无法点击文字输入等控件
- 在`theme.json`中将`Background`设置为完全透明会导致上一条描述的问题
