using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Docnet.Core;
using Docnet.Core.Models;
using PdfTool.Services;

namespace PdfTool.Controls
{
    public partial class FilePreviewControl : System.Windows.Controls.UserControl
    {
        public event RoutedEventHandler FileSelectRequested;

        public FilePreviewControl()
        {
            InitializeComponent();
        }

        private void BtnSelect_Click(object sender, RoutedEventArgs e)
        {
            FileSelectRequested?.Invoke(this, e);
        }

        public async void LoadFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                PanelEmpty.Visibility = Visibility.Visible;
                PanelLoaded.Visibility = Visibility.Collapsed;
                return;
            }

            PanelEmpty.Visibility = Visibility.Collapsed;
            PanelLoaded.Visibility = Visibility.Visible;

            TxtFileName.Text = Path.GetFileName(filePath);
            
            long fileBytes = new FileInfo(filePath).Length;
            string sizeStr = fileBytes > 1048576 ? (fileBytes / 1048576.0).ToString("0.00") + " MB" : (fileBytes / 1024.0).ToString("0") + " KB";
            
            int pages = PdfService.GetPdfPageCount(filePath);
            TxtFileInfo.Text = $"{pages} Sayfa • {sizeStr}";

            var docData = await Task.Run(() => GetThumbnailData(filePath));
            if (docData != null && docData.Item1 != null)
            {
                var rawBytes = docData.Item1;
                int width = docData.Item2;
                int height = docData.Item3;

                var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                bitmap.WritePixels(new Int32Rect(0, 0, width, height), rawBytes, width * 4, 0);
                
                double scale = 100.0 / Math.Max(width, height);
                var scaledBitmap = new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
                scaledBitmap.Freeze();

                ImgThumbnail.Source = scaledBitmap;
                ImgThumbnail.Visibility = Visibility.Visible;
                IconFallback.Visibility = Visibility.Hidden;
            }
            else
            {
                ImgThumbnail.Visibility = Visibility.Hidden;
                IconFallback.Visibility = Visibility.Visible;
            }
        }

        private Tuple<byte[], int, int> GetThumbnailData(string filePath)
        {
            try
            {
                using (var docReader = DocLib.Instance.GetDocReader(filePath, new PageDimensions(1.0)))
                {
                    using (var pageReader = docReader.GetPageReader(0))
                    {
                        var rawBytes = pageReader.GetImage();
                        int width = pageReader.GetPageWidth();
                        int height = pageReader.GetPageHeight();

                        return Tuple.Create(rawBytes, width, height);
                    }
                }
            }
            catch
            {
                return Tuple.Create((byte[])null, 0, 0);
            }
        }
    }
}
