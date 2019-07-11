using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

// Для выбранной папки/диска приложение ищет файлы, в которых есть совпадения
// с введеным регулярным выражением

namespace WpfWordsFinder
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {        
        CancellationTokenSource _cancelTokenSearch;
        CancellationToken _token;
        ObservableCollection<MyFileInfo> _myFilesInfo;
        bool _isSearch;

        System.Windows.Controls.Image _imgSearch = new System.Windows.Controls.Image() { Width = 16, Height = 16 };
        System.Windows.Controls.Image _imgCancel = new System.Windows.Controls.Image() { Width = 16, Height = 16 };
        System.Windows.Controls.Image _imgAtension = new System.Windows.Controls.Image() { Width = 16, Height = 16 };

        public MainWindow()
        {
            InitializeComponent();
            BtnSearch.IsEnabled = false;
            LblPlaceFind.Content = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            _imgSearch.Source = new BitmapImage(new Uri(@"/Images/Search-icon.png", UriKind.RelativeOrAbsolute));
            _imgCancel.Source = new BitmapImage(new Uri(@"/Images/Cancel-icon.png", UriKind.RelativeOrAbsolute));
            _imgAtension.Source = new BitmapImage(new Uri(@"/Images/Atention-icon.png", UriKind.RelativeOrAbsolute));
        }

        private void BtnPlaceFind_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog
            {
                ShowNewFolderButton = false,
                RootFolder = Environment.SpecialFolder.Desktop,
                Description = "Выберите диск или папку для поиска"
            };
            if(fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                LblPlaceFind.Content = fbd.SelectedPath;
            }
        }

        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            if (!_isSearch)
            {
                _isSearch = !_isSearch;    
                BtnSearch.Content = _imgCancel;
                _myFilesInfo = new ObservableCollection<MyFileInfo>();
                ListBox1.ItemsSource = _myFilesInfo;

                _cancelTokenSearch = new CancellationTokenSource();
                _token = _cancelTokenSearch.Token;

                EnableControls(false);

                string path = LblPlaceFind.Content.ToString();

                await Task.Factory.StartNew(() => Find(new DirectoryInfo(path), _token));

                EnableControls(true);
                BtnSearch.Content = _imgSearch;

            }
            else 
            {
                _cancelTokenSearch.Cancel(); // установка токена отмены
                _isSearch = !_isSearch;
                EnableControls(true);
                BtnSearch.Content = _imgSearch;
            }     
            
        }

        private void EnableControls(bool isEnabled)
        {          
            BtnPlaceFind.IsEnabled = isEnabled;
            TbSearch.IsEnabled = isEnabled;
            ProgressBar1.IsIndeterminate = !isEnabled;
        }

        public ImageSource ConvertIconToImageSource(Icon icon)
        {            
            ImageSource imageSource = new BitmapImage(new Uri(@"/WpfWordsFinder;component/Images/Filetype-default-icon.png", UriKind.RelativeOrAbsolute));

            try
            {
                imageSource = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            }
            catch
            { }

            return imageSource;
        }

        private void Find(DirectoryInfo dir, CancellationToken token)
        {
            string search = null;            
            try
            {              

                FileInfo[] fileInfo = dir.GetFiles();
                Parallel.ForEach(fileInfo, new ParallelOptions { CancellationToken = token }, fi =>
                {
                    try
                    {
                        using (StreamReader sr = new StreamReader(fi.OpenRead(), Encoding.Default))
                        {
                            string content = sr.ReadToEnd();
                            if (!Dispatcher.CheckAccess())
                            {
                                Dispatcher.Invoke(() => search = TbSearch.Text);
                            }
                            if (Regex.IsMatch(content, search))
                            {
                                if (!Dispatcher.CheckAccess())
                                {                                   
                                    Dispatcher.Invoke(() =>
                                    {
                                        _myFilesInfo.Add(new MyFileInfo
                                        {
                                            FullPath = fi.FullName,
                                            ImageSource = ConvertIconToImageSource(System.Drawing.Icon.ExtractAssociatedIcon(fi.FullName))                                            
                                        });
                                        //ListBox1.SelectedIndex = ListBox1.Items.Count - 1;
                                        //ListBox1.ScrollIntoView(ListBox1.SelectedItem);
                                    });                                                                       
                                }
                            }
                        }
                    }
                 
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => _myFilesInfo.Add(new MyFileInfo { FullPath = ex.Message.ToUpper(), ImageSource = _imgAtension.Source }));
                    }
                });

                Parallel.ForEach(dir.GetDirectories(),
                    new ParallelOptions { CancellationToken = token },
                    d => Find(d, token));               
            }

            catch (OperationCanceledException ex)
            {
                Dispatcher.Invoke(() => _myFilesInfo.Add(new MyFileInfo { FullPath = ex.Message.ToUpper(), ImageSource = _imgAtension.Source }));
            }

            catch (Exception ex)
            {               
                Dispatcher.Invoke(() => _myFilesInfo.Add(new MyFileInfo { FullPath = ex.Message.ToUpper(), ImageSource = _imgAtension.Source }));
            }
          
        }

        private void ListBox1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if(ListBox1.SelectedIndex != -1)
            {
                try
                {
                    Process.Start(((MyFileInfo)ListBox1.SelectedItem).FullPath);
                }
                catch(Exception ex)
                {
                    Dispatcher.Invoke(() => _myFilesInfo.Add(new MyFileInfo { FullPath = ex.Message.ToUpper(), ImageSource = _imgAtension.Source }));
                }
            }
        }

        private void TbSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            BtnSearch.IsEnabled = TbSearch.Text.Length > 2 ?  true : false;           
        }
    }
}
