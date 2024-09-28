using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;


namespace WinDesktopTodoList
{
    public class ViewModel : INotifyPropertyChanged
    {
        private SolidColorBrush _foregroundColor = System.Windows.Media.Brushes.White;

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

        protected virtual void OnPropertyChanged(string propertyName)
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

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, uint fuFlage, uint timeout, IntPtr result);

        //查找窗口的委托 查找逻辑
        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string className, string winName);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern IntPtr SetParent(IntPtr hwnd, IntPtr parentHwnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        // https://msdn.microsoft.com/en-us/library/windows/desktop/dd162758(v=vs.85).aspx
        [DllImport("user32.dll", EntryPoint = "PaintDesktop")]
        public static extern int PaintDesktop(IntPtr hdc);
        // https://msdn.microsoft.com/en-us/library/windows/desktop/ms633504(v=vs.85).aspx
        [DllImport("user32.dll", EntryPoint = "GetDesktopWindow", SetLastError = true)]
        public static extern IntPtr GetDesktopWindow();
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
    }


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 向桌面发送消息
        /// </summary>
        /// 定义programHandle 
        public IntPtr programHandle;
        public IntPtr mainwindowParentHandle;
        private static bool debug = true;

        private const long WS_EX_TRANSPARENT = 0x00000020L;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int LWA_ALPHA = 0x2;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        public void SendMsgToProgman()
        {
            // 桌面窗口句柄，在外部定义，用于后面将我们自己的窗口作为子窗口放入
            programHandle = Win32Func.FindWindow("Progman", null);

            IntPtr result = IntPtr.Zero;
            // 向 Program Manager 窗口发送消息 0x52c 的一个消息，超时设置为2秒
            Win32Func.SendMessageTimeout(programHandle, 0x52c, IntPtr.Zero, IntPtr.Zero, 0, 2, result);

            // 遍历顶级窗口
            Win32Func.EnumWindows((hwnd, lParam) =>
            {
                // 找到第一个 WorkerW 窗口，此窗口中有子窗口 SHELLDLL_DefView，所以先找子窗口
                if (Win32Func.FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                {
                    // 找到当前第一个 WorkerW 窗口的，后一个窗口，即第二个 WorkerW 窗口。
                    IntPtr tempHwnd = Win32Func.FindWindowEx(IntPtr.Zero, hwnd, "WorkerW", null);
                    // 隐藏第二个 WorkerW 窗口
                    Win32Func.ShowWindow(tempHwnd, 0);
                    // 记录第一个 WorkerW 窗口的句柄
                    mainwindowParentHandle = hwnd;
                }
                return true;
            }, IntPtr.Zero);

        }

        private const int WM_ACTIVATE = 0x0006;
        private const int WA_INACTIVE = 0;
        private const int WM_NCACTIVATE = 0x0086;

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

        private int currentId = 0;
        private enum Theme
        {
            Dark,
            Light,
            Transparent,
            GaussianBlur,
            Custom
        };
        private Theme currentTheme;
        private ViewModel viewModel;

        public MainWindow()
        {
            InitializeComponent();
            this.viewModel = new ViewModel();
            this.DataContext = this.viewModel;
            this.currentTheme = Theme.GaussianBlur;

            this.Left = 1300; // 设置窗口左边距
            this.Top = 10;  // 设置窗口上边距
            setWindowBackGround();

            loadFromFile();

            //向桌面发送消息
            //SendMsgToProgman(); 

            // 添加窗口消息处理程序
            Loaded += (s, e) =>
            {
                var hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                hwndSource.AddHook(WndProc);

                attachWindowToDesktop();
            };
        }

        private const int WM_EXITSIZEMOVE = 0x0232;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_EXITSIZEMOVE && this.currentTheme == Theme.GaussianBlur)
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
            int realLeft = (int)(this.Left * 1.25);
            int realTop = (int)(this.Top * 1.25);
            // 获取window宽度和高度
            int realwidth = (int)(this.Width * 1.25);
            int realheight = (int)(this.Height * 1.25);
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
            int realLeft = (int)(this.Left * 1.25);
            int realTop = (int)(this.Top * 1.25);
            // 获取window宽度和高度
            int realwidth = (int)(this.Width * 1.25);
            int realheight = (int)(this.Height * 1.25);
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

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public const UInt32 SWP_NOSIZE = 0x0001;
        public const UInt32 SWP_NOMOVE = 0x0002;
        public const UInt32 SWP_NOACTIVATE = 0x0010;
        public const UInt32 SWP_NOZORDER = 0x0004;
        public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnActivated(e);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
        }

        private bool hasSentToBack = false;

        private void attachWindowToDesktop()
        {
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
                // 获取桌面窗口的设备上下文
                progman = Win32Func.FindWindow("Progman", "Program Manager");
                shellView = Win32Func.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", "");
                sysListView = Win32Func.FindWindowEx(shellView, IntPtr.Zero, "SysListView32", "FolderView");

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
                    appBackground.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0, 255, 255, 255));
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

        private void reloadFromFile(object sender, RoutedEventArgs e)
        {
            loadFromFile();
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

