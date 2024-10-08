﻿using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;


namespace WinDesktopTodoList
{
    // ViewModel类，用于绑定前景色
    public class ViewModel : INotifyPropertyChanged
    {
        private SolidColorBrush _foregroundColor = System.Windows.Media.Brushes.White;  // 默认前景色为白色

        public SolidColorBrush ForegroundColor
        {
            get { return _foregroundColor; }
            set
            {
                if (_foregroundColor != value)
                {
                    _foregroundColor = value;
                    OnPropertyChanged(nameof(ForegroundColor));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)  // 属性改变事件
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
    //Win32方法
    public static class Win32Func
    {
        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);
        public const int SM_CXSCREEN = 0;
        public const int SM_CYSCREEN = 1;

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string className, string winName);

        //查找窗口的委托 查找逻辑
        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string className, string winName);

        [DllImport("user32.dll")]
        public static extern IntPtr SetParent(IntPtr hwnd, IntPtr parentHwnd);

        // http://msdn.microsoft.com/en-us/library/dd144871(VS.85).aspx
        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hwnd);
        // http://msdn.microsoft.com/en-us/library/dd162920(VS.85).aspx
        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        #region gdi32
        // http://msdn.microsoft.com/en-us/library/dd183370(VS.85).aspx
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool BitBlt(IntPtr hDestDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, Int32 dwRop);
        // http://msdn.microsoft.com/en-us/library/dd183488(VS.85).aspx
        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);
        // http://msdn.microsoft.com/en-us/library/dd183489(VS.85).aspx
        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        // http://msdn.microsoft.com/en-us/library/dd162957(VS.85).aspx
        [DllImport("gdi32.dll", ExactSpelling = true, PreserveSig = true, SetLastError = true)]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        // http://msdn.microsoft.com/en-us/library/dd183539(VS.85).aspx
        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        public const int SRCCOPY = 0xCC0020;
        #endregion

        public const int WM_EXITSIZEMOVE = 0x0232;
    }

    public partial class MainWindow : Window
    {
        private static bool debug = true;
        private double scale_factor = 1.0;
        private int width = 300;
        private int height = 360;
        private Theme currentTheme;
        private bool embed_to_desktop = true;

        private int currentId = 0;
        private enum Theme
        {
            Dark,
            Light,
            Transparent,
            GaussianBlur,
            Custom
        };
        private ViewModel viewModel;

        public static BitmapSource ApplyGaussianBlur(BitmapSource bitmapSource, double radius)
        {
            // 创建一个 WriteableBitmap 以便应用效果
            WriteableBitmap writeableBitmap = new WriteableBitmap(bitmapSource);

            // 创建一个 BlurEffect 并设置其半径
            BlurEffect blurEffect = new BlurEffect
            {
                Radius = radius
            };

            // 创建一个 DrawingVisual 并在其上应用模糊效果
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawImage(writeableBitmap, new Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
            }
            drawingVisual.Effect = blurEffect;
            // 将 writeableBitmap 转换为 Pbgra32 格式
            FormatConvertedBitmap formattedBitmap = new FormatConvertedBitmap();
            formattedBitmap.BeginInit();
            formattedBitmap.Source = writeableBitmap;
            formattedBitmap.DestinationFormat = PixelFormats.Pbgra32;
            formattedBitmap.EndInit();

            // 渲染模糊效果到 RenderTargetBitmap
            RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(formattedBitmap.PixelWidth, formattedBitmap.PixelHeight, formattedBitmap.DpiX, formattedBitmap.DpiY, formattedBitmap.Format);
            renderTargetBitmap.Render(drawingVisual);

            return renderTargetBitmap;
        }

        public MainWindow()
        {
            if (!loadConfig())
            {
                // 加载配置失败，退出程序
                Application.Current.Shutdown();
            }
            InitializeComponent();
            getSystemWindowHandlers();
            this.viewModel = new ViewModel();
            this.DataContext = this.viewModel;

            this.Left = 1300; // 设置窗口左边距
            this.Top = 10;  // 设置窗口上边距
            // 设置窗口大小
            this.Width = this.width;
            this.Height = this.height;
            setWindowBackGround();

            loadFromFile();

            // 添加窗口消息处理程序
            Loaded += (s, e) =>
            {
                var hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                hwndSource.AddHook(WndProc);

                if (this.embed_to_desktop)
                {
                    attachWindowToDesktop(); 
                }
            };
        }

        private void getSystemWindowHandlers()
        {
            // 获取桌面窗口的设备上下文
            progman = Win32Func.FindWindow("Progman", "Program Manager");
            shellView = Win32Func.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", "");
            sysListView = Win32Func.FindWindowEx(shellView, IntPtr.Zero, "SysListView32", "FolderView");
        }

        private class Config
        {
            public bool debug { get; set; }
            public double scale_factor { get; set; }
            public int width { get; set; }
            public int height { get; set; }
            public int default_theme { get; set; }
            public bool embed_to_desktop { get; set; }
        }

        private bool loadConfig()
        {
            // 从src/config.json中加载配置
            if (!File.Exists("src/config.json"))
            {
                if (debug)
                {
                    MessageBox.Show("配置文件不存在");
                }
                return false;
            }
            string content = File.ReadAllText("src/config.json");
            if (content != null)
            {
                try
                {
                    Config config = JsonSerializer.Deserialize<Config>(content);
                    debug = config.debug;
                    scale_factor = config.scale_factor;
                    width = config.width;
                    height = config.height;
                    switch (config.default_theme)
                    {
                        case 0:
                            this.currentTheme = Theme.Dark;
                            break;
                        case 1:
                            this.currentTheme = Theme.Light;
                            break;
                        case 2:
                            this.currentTheme = Theme.Transparent;
                            break;
                        case 3:
                            this.currentTheme = Theme.GaussianBlur;
                            break;
                        case 4:
                            this.currentTheme = Theme.Custom;
                            break;
                        default:
                            this.currentTheme = Theme.GaussianBlur;
                            break;
                    }
                    embed_to_desktop = config.embed_to_desktop;
                    return true;
                }
                catch (JsonException e)
                {
                    if (debug)
                    {
                        MessageBox.Show(e.Message);
                    }
                    return false;
                }
            }
            else
            {
                if (debug)
                {
                    MessageBox.Show("读取文件失败，读取内容为空");
                }
                return false;
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Win32Func.WM_EXITSIZEMOVE && this.currentTheme == Theme.GaussianBlur)
            {
                resetWindowBackGround();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private BitmapSource backgroundPicture = null;

        private void resetWindowBackGround()
        {
            ImageBrush imageBrush = new ImageBrush();
            if (backgroundPicture == null)
            {
                return;
            }
            int realLeft = (int)(this.Left * this.scale_factor);
            int realTop = (int)(this.Top * this.scale_factor);
            // 获取window宽度和高度
            int realwidth = (int)(this.Width * this.scale_factor);
            int realheight = (int)(this.Height * this.scale_factor);
            // 获取 bitmapSource 的宽度和高度
            int bitmapWidth = backgroundPicture.PixelWidth;
            int bitmapHeight = backgroundPicture.PixelHeight;

            // 检查是否超出桌面边界
            if (realLeft < 0 || realTop < 0 || realLeft + realwidth >= bitmapWidth || realTop + realheight >= bitmapHeight)
            {
                return;
            }
            //将图片设置为（realLeft，realTop）开始的（realwidth，realheight）大小
            CroppedBitmap croppedBitmap = new CroppedBitmap(backgroundPicture, new Int32Rect(realLeft, realTop, realwidth, realheight));
            imageBrush.ImageSource = croppedBitmap;

            // 将 ImageBrush 设置为 appBackground 的背景
            appBackground.Background = imageBrush;
        }

        private void setWindowBackGround()
        {
            switch (this.currentTheme)
            {
                case Theme.Dark:
                    this.currentTheme = Theme.Dark;
                    appBackground.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 0, 0));
                    //this.foreground = "White";
                    this.viewModel.ForegroundColor = System.Windows.Media.Brushes.White;
                    setCheckBoxBorderBrush(System.Windows.Media.Brushes.White);
                    return;
                case Theme.Light:
                    this.currentTheme = Theme.Light;
                    appBackground.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255));
                    //this.foreground = "Black";
                    this.viewModel.ForegroundColor = System.Windows.Media.Brushes.Black;
                    setCheckBoxBorderBrush(System.Windows.Media.Brushes.Black);
                    return;
                case Theme.Transparent:
                    this.currentTheme = Theme.Transparent;
                    appBackground.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 255, 255, 255));
                    //this.foreground = "White";
                    this.viewModel.ForegroundColor = System.Windows.Media.Brushes.White;
                    setCheckBoxBorderBrush(System.Windows.Media.Brushes.White);
                    outerBorder.BorderThickness = new Thickness(0);
                    return;
                case Theme.GaussianBlur:
                    break;
                case Theme.Custom:
                    if (loadThemeFromFile())
                    {
                        this.currentTheme = Theme.Custom;
                    }
                    else
                    {
                        // message box提示用户失败
                        MessageBox.Show("加载自定义主题失败。自动切换至Dark主题");
                        this.currentTheme = Theme.Dark;
                        appBackground.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 0, 0));
                        //this.foreground = "White";
                        this.viewModel.ForegroundColor = System.Windows.Media.Brushes.White;
                        setCheckBoxBorderBrush(System.Windows.Media.Brushes.White);
                    }
                    return;
            }

            // 创建一个 ImageBrush 并设置图片源
            ImageBrush imageBrush = new ImageBrush();
            BitmapSource bitmapSource = CaptureWallpaper();
            if (bitmapSource == null)
            {
                if (debug)
                {
                    MessageBox.Show("Failed to capture wallpaper.");
                }
                return;
            }
            int realLeft = (int)(this.Left * this.scale_factor);
            int realTop = (int)(this.Top * this.scale_factor);
            // 获取window宽度和高度
            int realwidth = (int)(this.Width * this.scale_factor);
            int realheight = (int)(this.Height * this.scale_factor);
            // 获取 bitmapSource 的宽度和高度
            int bitmapWidth = bitmapSource.PixelWidth;
            int bitmapHeight = bitmapSource.PixelHeight;

            // 提高图片rgb通道各加15，使图片变亮，超过255则设置为255
            byte[] pixels = new byte[bitmapSource.PixelWidth * bitmapSource.PixelHeight * 4];
            bitmapSource.CopyPixels(pixels, bitmapSource.PixelWidth * 4, 0);
            for (int i = 0; i < pixels.Length; i++)
            {
                if (i % 4 == 3)
                {
                    continue;
                }
                if (pixels[i] + 15 > 255)
                {
                    pixels[i] = 255;
                }
                else
                {
                    pixels[i] += 15;
                }
            }
            bitmapSource = BitmapSource.Create(bitmapSource.PixelWidth, bitmapSource.PixelHeight, bitmapSource.DpiX, bitmapSource.DpiY, PixelFormats.Bgra32, null, pixels, bitmapSource.PixelWidth * 4);

            // 对bitmapSource应用高斯模糊
            bitmapSource = ApplyGaussianBlur(bitmapSource, 35);

            backgroundPicture = bitmapSource;

            // 检查是否超出桌面边界
            if (realLeft < 0 || realTop < 0 || realLeft + realwidth >= bitmapWidth || realTop + realheight >= bitmapHeight)
            {
                return;
            }

            //将图片设置为（realLeft，realTop）开始的（realwidth，realheight）大小
            CroppedBitmap croppedBitmap = new CroppedBitmap(bitmapSource, new Int32Rect(realLeft, realTop, realwidth, realheight));
            bitmapSource = croppedBitmap;
            imageBrush.ImageSource = bitmapSource;

            // 将 ImageBrush 设置为 appBackground 的背景
            appBackground.Background = imageBrush;
        }

        private bool hasSentToBack = false;

        private void attachWindowToDesktop()
        {
            // 将窗口嵌入到桌面
            if (!hasSentToBack)
            {
                IntPtr hWnd = new WindowInteropHelper(this).Handle;
                Win32Func.SetParent(hWnd, progman);
                hasSentToBack = true;
            }
        }

        public class TodoItem
        {
            public int Id { get; set; }
            public string Text { get; set; }

        }

        private void txtNewItem_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (txtNewItem.Text.Trim() == "")
                {
                    txtNewItem.Clear();
                    return;
                }
                lstItems.Items.Add(new TodoItem { Id = currentId++, Text = txtNewItem.Text });
                // 保存到文件，附加模式
                using (StreamWriter sw = File.AppendText("todo.txt"))
                {
                    sw.WriteLine(txtNewItem.Text);
                }
                txtNewItem.Clear();
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            // 获取 CheckBox 的 DataContext, 即绑定的 TodoItem 对象的id，然后删除对应id的TodoItem
            var item = lstItems.Items.OfType<TodoItem>().FirstOrDefault(i => i.Id == (int)(checkBox.DataContext)) as TodoItem;
            lstItems.Items.Remove(item);
            finishedItems.Items.Add(item);
            // 从文件中删除
            string[] lines = File.ReadAllLines("todo.txt");
            File.WriteAllLines("todo.txt", lines.Where(line => !line.Contains(item.Text)));
            // 添加到finished.txt
            using (StreamWriter sw = File.AppendText("finished.txt"))
            {
                sw.WriteLine(item.Text);
            }
        }

        private IntPtr progman;
        private IntPtr shellView;
        private IntPtr sysListView;

        public BitmapSource CaptureWallpaper()
        {
            BitmapSource bitmapSource = null;
            IntPtr sourceDC = IntPtr.Zero;
            IntPtr targetDC = IntPtr.Zero;
            IntPtr compatibleBitmapHandle = IntPtr.Zero;
            try
            {
                if (sysListView == IntPtr.Zero)
                {
                    Console.WriteLine("Failed to find SysListView32 window.");
                    return null;
                }

                sourceDC = Win32Func.GetDC(sysListView);
                if (sourceDC == IntPtr.Zero)
                {
                    Console.WriteLine("Failed to get source DC.");
                    return null;
                }

                // 获取屏幕的实际分辨率
                int screenWidth = Win32Func.GetSystemMetrics(Win32Func.SM_CXSCREEN);
                int screenHeight = Win32Func.GetSystemMetrics(Win32Func.SM_CYSCREEN);

                // 创建兼容的内存设备上下文
                targetDC = Win32Func.CreateCompatibleDC(sourceDC);
                if (targetDC == IntPtr.Zero)
                {
                    Console.WriteLine("Failed to create compatible DC.");
                    return null;
                }

                // 创建兼容的位图
                compatibleBitmapHandle = Win32Func.CreateCompatibleBitmap(sourceDC, screenWidth, screenHeight);
                if (compatibleBitmapHandle == IntPtr.Zero)
                {
                    Console.WriteLine("Failed to create compatible bitmap.");
                    return null;
                }

                // 将位图选择到目标设备上下文中
                IntPtr oldBitmap = Win32Func.SelectObject(targetDC, compatibleBitmapHandle);
                if (oldBitmap == IntPtr.Zero)
                {
                    Console.WriteLine("Failed to select bitmap into DC.");
                    return null;
                }

                // 将桌面图像复制到目标设备上下文中
                if (!Win32Func.BitBlt(targetDC, 0, 0, screenWidth, screenHeight, sourceDC, 0, 0, Win32Func.SRCCOPY))
                {
                    Console.WriteLine("BitBlt failed.");
                    return null;
                }

                // 将位图转换为 BitmapSource
                bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    compatibleBitmapHandle, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
            finally
            {
                if (compatibleBitmapHandle != IntPtr.Zero)
                    Win32Func.DeleteObject(compatibleBitmapHandle);
                if (sourceDC != IntPtr.Zero)
                    Win32Func.ReleaseDC(IntPtr.Zero, sourceDC);
                if (targetDC != IntPtr.Zero)
                    Win32Func.ReleaseDC(IntPtr.Zero, targetDC);
            }

            if (debug)
            {
                // 保存图片到本地
                if (bitmapSource != null)
                {
                    using (var fileStream = new FileStream("E:/desktop/capturedWallpaper.png", FileMode.Create))
                    {
                        BitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                        encoder.Save(fileStream);
                    }
                }
            }

            return bitmapSource;
        }

        private void loadFromFile()
        {
            // 从文件中加载待办事项
            finishedItems.Items.Clear();
            lstItems.Items.Clear();
            if (File.Exists("finished.txt"))
            {
                using (StreamReader sr = File.OpenText("finished.txt"))
                {
                    string s = "";
                    while ((s = sr.ReadLine()) != null)
                    {
                        finishedItems.Items.Add(new TodoItem { Id = -1, Text = s });
                    }
                }
            }
            if (!File.Exists("todo.txt"))
            {
                return;
            }
            using (StreamReader sr = File.OpenText("todo.txt"))
            {
                string s = "";
                while ((s = sr.ReadLine()) != null)
                {
                    lstItems.Items.Add(new TodoItem { Id = currentId++, Text = s });
                }
            }
        }

        private void changeTheme(object sender, RoutedEventArgs e)
        {
            // 轮换主题
            switch (this.currentTheme)
            {
                case Theme.Dark:
                    this.currentTheme = Theme.Light;
                    appBackground.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 255, 255));
                    //this.foreground = "Black";
                    this.viewModel.ForegroundColor = System.Windows.Media.Brushes.Black;
                    setCheckBoxBorderBrush(System.Windows.Media.Brushes.Black);
                    break;
                case Theme.Light:
                    this.currentTheme = Theme.Transparent;
                    appBackground.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 255, 255, 255));
                    //this.foreground = "White";
                    this.viewModel.ForegroundColor = System.Windows.Media.Brushes.White;
                    setCheckBoxBorderBrush(System.Windows.Media.Brushes.White);
                    outerBorder.BorderThickness = new Thickness(0);
                    break;
                case Theme.Transparent:
                    this.currentTheme = Theme.GaussianBlur;
                    this.resetWindowBackGround();
                    //this.foreground = "White";
                    this.viewModel.ForegroundColor = System.Windows.Media.Brushes.White;
                    outerBorder.BorderThickness = new Thickness(1);
                    break;
                case Theme.GaussianBlur:
                    if (loadThemeFromFile())
                    {
                        this.currentTheme = Theme.Custom;
                    }
                    else
                    {
                        // message box提示用户失败
                        MessageBox.Show("加载自定义主题失败。自动切换至Dark主题");
                        this.currentTheme = Theme.Dark;
                        appBackground.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 0, 0));
                        //this.foreground = "White";
                        this.viewModel.ForegroundColor = System.Windows.Media.Brushes.White;
                        setCheckBoxBorderBrush(System.Windows.Media.Brushes.White);
                    }
                    break;
                case Theme.Custom:
                    this.currentTheme = Theme.Dark;
                    appBackground.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 0, 0));
                    //this.foreground = "White";
                    this.viewModel.ForegroundColor = System.Windows.Media.Brushes.White;
                    setCheckBoxBorderBrush(System.Windows.Media.Brushes.White);
                    break;
            }
        }

        private void setCheckBoxBorderBrush(SolidColorBrush color)
        {
            // 设置所有CheckBox的边框颜色
            foreach (var item in lstItems.Items)
            {
                var listBoxItem = (ListBoxItem)(lstItems.ItemContainerGenerator.ContainerFromItem(item));
                if (listBoxItem != null)
                {
                    var checkBoxes = FindVisualChildren<CheckBox>(listBoxItem);
                    foreach (CheckBox checkBox in checkBoxes)
                    {
                        checkBox.BorderBrush = color;
                    }
                }
            }

        }

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            // 递归查找所有子元素
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    var child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }


        private class MyTheme
        {
            public string Background { get; set; }
            public string Foreground { get; set; }
        }

        private bool loadThemeFromFile()
        {
            // 从src/theme.json中加载主题
            if (!File.Exists("src/theme.json"))
            {
                if (debug)
                {
                    MessageBox.Show("主题文件不存在");
                }
                return false;
            }
            string content = File.ReadAllText("src/theme.json");
            if (content != null)
            {
                MyTheme theme = null;
                try
                {
                    theme = JsonSerializer.Deserialize<MyTheme>(content);
                }
                catch (JsonException e)
                {
                    if (debug)
                    {
                        MessageBox.Show(e.Message);
                    }
                    return false;
                }
                // 解析颜色字符串并应用到前景和背景
                try
                {
                    appBackground.Background = new SolidColorBrush(ParseColorString(theme.Background));
                    //this.foreground = ParseColorString(theme.Foreground).ToString();
                    this.viewModel.ForegroundColor = new SolidColorBrush(ParseColorString(theme.Foreground));
                    setCheckBoxBorderBrush(new SolidColorBrush(ParseColorString(theme.Foreground)));
                }
                catch (FormatException e)
                {
                    if (debug)
                    {
                        MessageBox.Show(e.Message);
                    }
                    return false;
                }
                return true;
            }
            else
            {
                if (debug)
                {
                    MessageBox.Show("读取文件失败，读取内容为空");
                }
                return false;
            }
        }

        private System.Windows.Media.Color ParseColorString(string colorString)
        {
            // 去掉括号并分割字符串
            colorString = colorString.Trim('(', ')');
            string[] parts = colorString.Split(',');

            if (parts.Length == 4)
            {
                // 解析颜色分量
                byte a = byte.Parse(parts[0].Trim());
                byte r = byte.Parse(parts[1].Trim());
                byte g = byte.Parse(parts[2].Trim());
                byte b = byte.Parse(parts[3].Trim());

                if (a >= 0 && a <= 255 && r >= 0 && r <= 255 && g >= 0 && g <= 255 && b >= 0 && b <= 255)
                {
                    // 返回 ARGB 颜色字符串
                    return System.Windows.Media.Color.FromArgb(a, r, g, b);
                }
                else
                {
                    if (debug)
                    {
                        MessageBox.Show("颜色分量不在合法范围内");
                    }
                    throw new FormatException("颜色分量不在合法范围内");
                }
            }
            if (debug)
            {
                MessageBox.Show("颜色字符串格式不正确");
            }
            throw new FormatException("颜色字符串格式不正确");
        }

        private void  refresh(object sender, RoutedEventArgs e)
        {
            loadConfig();
            this.Width = this.width;
            this.Height = this.height;
            loadFromFile();
            setWindowBackGround();
        }

        private void showInfo(object sender, RoutedEventArgs e)
        {
            // 介绍项目和开发者
            MessageBox.Show("WinDesktopTodoList\n"
                + "一个简单的桌面待办事项列表\n\n"
                + "开发者：TheColdSummer\n"
                + "项目地址：https://github.com/TheColdSummer/WinDesktopTodoList");
        }

        private void clearFinished(object sender, RoutedEventArgs e)
        {
            // 清空finished.txt
            File.WriteAllText("finished.txt", "");
            finishedItems.Items.Clear();
        }

        private void sourceInitialized(object sender, EventArgs e)
        {

            setCheckBoxBorderBrush(this.viewModel.ForegroundColor);
        }

        private void checkBoxInitialized(object sender, EventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox != null)
            {
                checkBox.BorderBrush = this.viewModel.ForegroundColor;
            }
        }
    }
}

