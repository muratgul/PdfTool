using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;
using PdfTool.Services;
using Docnet.Core;
using Docnet.Core.Models;

// Resolve namespace conflicts between WPF and Windows Forms/Drawing
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;
using BitmapSource = System.Windows.Media.Imaging.BitmapSource;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using ListBox = System.Windows.Controls.ListBox;
using DataObject = System.Windows.DataObject;

namespace PdfTool
{
    public partial class MainWindow : Window
    {
        // Lists for ListBoxes
        private readonly ObservableCollection<FileItem> _mergeFiles = new();
        private readonly ObservableCollection<FileItem> _toPdfFiles = new();

        // Loaded PDF files for single-file operations
        private string? _loadedRotateFile;
        private string? _loadedDeleteFile;
        private string? _loadedSplitFile;
        private string? _loadedWatermarkFile;
        private string? _loadedToImageFile;

        // Preview page indices
        private int _rotatePreviewPageIndex = 0;
        private int _deletePreviewPageIndex = 0;
        private int _splitPreviewPageIndex = 0;

        // Path for watermark image
        private string? _watermarkImagePath;

        // Toast Notification Timer
        private DispatcherTimer? _toastTimer;

        // Drag reordering tracking
        private Point _dragStartPoint;
        private FileItem? _draggedItem;
        private ListBox? _activeDragSource;

        public MainWindow()
        {
            InitializeComponent();

            // Bind ListBoxes
            LstMergeFiles.ItemsSource = _mergeFiles;
            LstToPdfFiles.ItemsSource = _toPdfFiles;

            // Wire up collections changes to update status texts
            _mergeFiles.CollectionChanged += (s, e) => UpdateMergeStatus();
            _toPdfFiles.CollectionChanged += (s, e) => UpdateToPdfStatus();

            UpdateMergeStatus();
            UpdateToPdfStatus();
        }

        #region Navigation and UI Styling Helpers

        private void BtnNav_Checked(object sender, RoutedEventArgs e)
        {
            if (MainTabControl == null) return;

            if (sender == BtnNavHome) MainTabControl.SelectedIndex = 0;
            else if (sender == BtnNavMerge) MainTabControl.SelectedIndex = 1;
            else if (sender == BtnNavRotate) MainTabControl.SelectedIndex = 2;
            else if (sender == BtnNavDelete) MainTabControl.SelectedIndex = 3;
            else if (sender == BtnNavSplit) MainTabControl.SelectedIndex = 4;
            else if (sender == BtnNavWatermark) MainTabControl.SelectedIndex = 5;
            else if (sender == BtnNavToImage) MainTabControl.SelectedIndex = 6;
            else if (sender == BtnNavToPdf) MainTabControl.SelectedIndex = 7;
            else if (sender == BtnNavCompress) MainTabControl.SelectedIndex = 8;
            else if (sender == BtnNavProtect) MainTabControl.SelectedIndex = 9;
            else if (sender == BtnNavPageNumber) MainTabControl.SelectedIndex = 10;
            else if (sender == BtnNavExtractText) MainTabControl.SelectedIndex = 11;
            else if (sender == BtnNavExtractImage) MainTabControl.SelectedIndex = 12;
        }

        private void CardMerge_Click(object sender, RoutedEventArgs e) => BtnNavMerge.IsChecked = true;
        private void CardRotate_Click(object sender, RoutedEventArgs e) => BtnNavRotate.IsChecked = true;
        private void CardDelete_Click(object sender, RoutedEventArgs e) => BtnNavDelete.IsChecked = true;
        private void CardSplit_Click(object sender, RoutedEventArgs e) => BtnNavSplit.IsChecked = true;
        private void CardWatermark_Click(object sender, RoutedEventArgs e) => BtnNavWatermark.IsChecked = true;
        private void CardToImage_Click(object sender, RoutedEventArgs e) => BtnNavToImage.IsChecked = true;
        private void CardToPdf_Click(object sender, RoutedEventArgs e) => BtnNavToPdf.IsChecked = true;

        #endregion

        #region Toast Notification System

        private void ShowToast(string message, bool isSuccess = true)
        {
            TxtToastMessage.Text = message;
            if (isSuccess)
            {
                BorderToast.Background = (SolidColorBrush)FindResource("SuccessBrush");
                BorderToast.BorderBrush = new SolidColorBrush(Color.FromRgb(6, 95, 70));
            }
            else
            {
                BorderToast.Background = (SolidColorBrush)FindResource("ErrorBrush");
                BorderToast.BorderBrush = new SolidColorBrush(Color.FromRgb(153, 27, 27));
            }
            BorderToast.Visibility = Visibility.Visible;

            _toastTimer?.Stop();
            _toastTimer = new DispatcherTimer();
            _toastTimer.Interval = TimeSpan.FromSeconds(5);
            _toastTimer.Tick += (s, ev) =>
            {
                BorderToast.Visibility = Visibility.Collapsed;
                _toastTimer.Stop();
            };
            _toastTimer.Start();
        }

        private void BtnCloseToast_Click(object sender, RoutedEventArgs e)
        {
            BorderToast.Visibility = Visibility.Collapsed;
            _toastTimer?.Stop();
        }

        #endregion

        #region Global Drag and Drop Support

        private void HomeDragDrop_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void HomeDragDrop_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[]? files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
                if (files == null || files.Length == 0) return;

                var pdfFiles = new List<string>();
                var imageFiles = new List<string>();

                foreach (var file in files)
                {
                    var ext = Path.GetExtension(file).ToLower();
                    if (ext == ".pdf")
                    {
                        pdfFiles.Add(file);
                    }
                    else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
                    {
                        imageFiles.Add(file);
                    }
                }

                if (pdfFiles.Count > 1)
                {
                    BtnNavMerge.IsChecked = true;
                    foreach (var pdf in pdfFiles)
                    {
                        AddFileToMergeList(pdf);
                    }
                    UpdateMergeStatus();
                    ShowToast($"{pdfFiles.Count} adet PDF birleştirme listesine eklendi.");
                }
                else if (pdfFiles.Count == 1)
                {
                    LoadSinglePdfFile(pdfFiles[0]);
                    BtnNavRotate.IsChecked = true;
                    ShowToast("PDF belgesi yüklendi. Yapmak istediğiniz işlemi seçin.");
                }
                else if (imageFiles.Count > 0)
                {
                    BtnNavToPdf.IsChecked = true;
                    int addedCount = 0;
                    foreach (var img in imageFiles)
                    {
                        if (AddFileToToPdfList(img))
                            addedCount++;
                    }
                    UpdateToPdfStatus();
                    if (addedCount > 0)
                        ShowToast($"{addedCount} adet görsel PDF dönüştürme listesine eklendi.");
                }
                else
                {
                    ShowToast("Desteklenmeyen dosya türü sürüklendi. Lütfen PDF veya Görsel dosyası bırakın.", false);
                }
            }
        }

        #endregion

        #region Helper: Single PDF Loader

        private void LoadSinglePdfFile(string filePath)
        {
            int pageCount = PdfService.GetPdfPageCount(filePath);
            string fileName = Path.GetFileName(filePath);

            // Populate Rotate panel
            _loadedRotateFile = filePath;
            TxtRotateFileName.Text = fileName;
            TxtRotateFilePages.Text = $"Sayfa Sayısı: {pageCount}";
            PanelRotateNoFile.Visibility = Visibility.Collapsed;
            PanelRotateFileLoaded.Visibility = Visibility.Visible;
            BtnRotateAction.IsEnabled = true;
            _rotatePreviewPageIndex = 0;
            UpdateRotatePreview();

            // Populate Delete panel
            _loadedDeleteFile = filePath;
            TxtDeleteFileName.Text = fileName;
            TxtDeleteFilePages.Text = $"Sayfa Sayısı: {pageCount}";
            PanelDeleteNoFile.Visibility = Visibility.Collapsed;
            PanelDeleteFileLoaded.Visibility = Visibility.Visible;
            BtnDeleteAction.IsEnabled = true;
            _deletePreviewPageIndex = 0;
            UpdateDeletePreview();

            // Populate Split panel
            _loadedSplitFile = filePath;
            TxtSplitFileName.Text = fileName;
            TxtSplitFilePages.Text = $"Sayfa Sayısı: {pageCount}";
            PanelSplitNoFile.Visibility = Visibility.Collapsed;
            PanelSplitFileLoaded.Visibility = Visibility.Visible;
            BtnSplitAction.IsEnabled = true;
            _splitPreviewPageIndex = 0;
            UpdateSplitPreview();

            // Populate Watermark panel
            _loadedWatermarkFile = filePath;
            TxtWatermarkFileName.Text = fileName;
            TxtWatermarkFilePages.Text = $"Sayfa Sayısı: {pageCount}";
            PanelWatermarkNoFile.Visibility = Visibility.Collapsed;
            PanelWatermarkFileLoaded.Visibility = Visibility.Visible;
            BtnWatermarkAction.IsEnabled = true;

            // Populate ToImage panel
            _loadedToImageFile = filePath;
            TxtToImageFileName.Text = fileName;
            TxtToImageFilePages.Text = $"Sayfa Sayısı: {pageCount}";
            PanelToImageNoFile.Visibility = Visibility.Collapsed;
            PanelToImageFileLoaded.Visibility = Visibility.Visible;
            UpdateToImageActionButtonState();
        }

        #endregion

        #region PDF Merge Tab Logic

        private void UpdateMergeStatus()
        {
            if (_mergeFiles.Count == 0)
            {
                TxtMergeStatus.Text = "Listeye en az 2 PDF ekleyin.";
            }
            else
            {
                TxtMergeStatus.Text = $"Listede {_mergeFiles.Count} adet PDF bulunuyor.";
            }
        }

        private bool AddFileToMergeList(string path)
        {
            // Avoid adding duplicates
            if (_mergeFiles.Any(f => f.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                return false;

            int pageCount = PdfService.GetPdfPageCount(path);
            _mergeFiles.Add(new FileItem
            {
                FileName = Path.GetFileName(path),
                FilePath = path,
                PageCount = pageCount,
                PageInfo = $"{pageCount} Sayfa"
            });
            return true;
        }

        private void BtnMergeAdd_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "PDF Belgeleri (*.pdf)|*.pdf",
                Multiselect = true,
                Title = "PDF Dosyalarını Seçin"
            };

            if (dialog.ShowDialog() == true)
            {
                int addedCount = 0;
                foreach (string file in dialog.FileNames)
                {
                    if (AddFileToMergeList(file))
                        addedCount++;
                }
                UpdateMergeStatus();
                if (addedCount > 0)
                {
                    ShowToast($"{addedCount} adet PDF eklendi.");
                }
            }
        }

        private void BtnMergeUp_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = LstMergeFiles.SelectedIndex;
            if (selectedIndex > 0)
            {
                _mergeFiles.Move(selectedIndex, selectedIndex - 1);
                LstMergeFiles.SelectedIndex = selectedIndex - 1;
            }
        }

        private void BtnMergeDown_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = LstMergeFiles.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < _mergeFiles.Count - 1)
            {
                _mergeFiles.Move(selectedIndex, selectedIndex + 1);
                LstMergeFiles.SelectedIndex = selectedIndex + 1;
            }
        }

        private void BtnMergeRemove_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = LstMergeFiles.SelectedIndex;
            if (selectedIndex >= 0)
            {
                _mergeFiles.RemoveAt(selectedIndex);
            }
        }

        private void BtnMergeClear_Click(object sender, RoutedEventArgs e)
        {
            _mergeFiles.Clear();
        }

        private void BtnMergeAction_Click(object sender, RoutedEventArgs e)
        {
            if (_mergeFiles.Count < 2)
            {
                ShowToast("Birleştirme işlemi için en az 2 PDF dosyası eklemelisiniz.", false);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "PDF Belgesi (*.pdf)|*.pdf",
                FileName = "birlesmis_belge.pdf",
                Title = "Birleştirilen Dosyayı Kaydet"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var filePaths = _mergeFiles.Select(f => f.FilePath).ToList();
                    PdfService.MergePdf(filePaths, dialog.FileName);
                    ShowToast("PDF birleştirme işlemi başarıyla tamamlandı.");
                    
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    ShowToast($"Hata oluştu: {ex.Message}", false);
                }
            }
        }

        #endregion

        #region PDF Rotate Tab Logic

        private void BtnSelectRotateFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "PDF Belgeleri (*.pdf)|*.pdf",
                Title = "Döndürülecek PDF Belgesini Seçin"
            };

            if (dialog.ShowDialog() == true)
            {
                LoadSinglePdfFile(dialog.FileName);
            }
        }

        private void BtnRotateAction_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_loadedRotateFile)) return;

            int maxPages = PdfService.GetPdfPageCount(_loadedRotateFile);
            string rangeInput = TxtRotatePages.Text;

            List<int> pagesToRotate;
            try
            {
                pagesToRotate = ParsePageRange(rangeInput, maxPages);
            }
            catch
            {
                ShowToast("Sayfa aralığı formatı geçersiz. Örn: Hepsi, 1, 3-5, 8", false);
                return;
            }

            if (pagesToRotate.Count == 0)
            {
                ShowToast("Döndürülecek sayfa bulunamadı. Lütfen geçerli bir sayfa aralığı yazın.", false);
                return;
            }

            int angle = 90;
            switch (CmbRotateAngle.SelectedIndex)
            {
                case 0: angle = 90; break;
                case 1: angle = 180; break;
                case 2: angle = 270; break;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "PDF Belgesi (*.pdf)|*.pdf",
                FileName = "[Dondurulmus] " + Path.GetFileName(_loadedRotateFile),
                Title = "Döndürülen PDF Belgesini Kaydet"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    PdfService.RotatePdfPages(_loadedRotateFile, dialog.FileName, pagesToRotate, angle);
                    ShowToast("Sayfa döndürme işlemi tamamlandı.");
                    LoadSinglePdfFile(dialog.FileName); // Refresh loaded file
                }
                catch (Exception ex)
                {
                    ShowToast($"Hata oluştu: {ex.Message}", false);
                }
            }
        }

        #endregion

        #region PDF Delete Tab Logic

        private void BtnSelectDeleteFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "PDF Belgeleri (*.pdf)|*.pdf",
                Title = "Sayfaları Silinecek PDF Belgesini Seçin"
            };

            if (dialog.ShowDialog() == true)
            {
                LoadSinglePdfFile(dialog.FileName);
            }
        }

        private void BtnDeleteAction_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_loadedDeleteFile)) return;

            int maxPages = PdfService.GetPdfPageCount(_loadedDeleteFile);
            string rangeInput = TxtDeletePages.Text;

            List<int> pagesToDelete;
            try
            {
                pagesToDelete = ParsePageRange(rangeInput, maxPages);
            }
            catch
            {
                ShowToast("Sayfa numarası formatı geçersiz. Örn: 2, 4-6", false);
                return;
            }

            if (pagesToDelete.Count == 0)
            {
                ShowToast("Silinecek sayfa belirtilmedi.", false);
                return;
            }

            if (pagesToDelete.Count >= maxPages)
            {
                ShowToast("Belgedeki tüm sayfaları silemezsiniz. En az 1 sayfa kalmalıdır.", false);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "PDF Belgesi (*.pdf)|*.pdf",
                FileName = "[Sayfa_Silinmis] " + Path.GetFileName(_loadedDeleteFile),
                Title = "Yeni PDF Belgesini Kaydet"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    PdfService.DeletePdfPages(_loadedDeleteFile, dialog.FileName, pagesToDelete);
                    ShowToast("Seçilen sayfalar başarıyla silindi.");
                    LoadSinglePdfFile(dialog.FileName); // Refresh loaded file
                }
                catch (Exception ex)
                {
                    ShowToast($"Hata oluştu: {ex.Message}", false);
                }
            }
        }

        #endregion

        #region PDF Watermark Tab Logic

        private void BtnSelectWatermarkFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "PDF Belgeleri (*.pdf)|*.pdf",
                Title = "Filigran Eklenecek PDF Belgesini Seçin"
            };

            if (dialog.ShowDialog() == true)
            {
                LoadSinglePdfFile(dialog.FileName);
            }
        }

        private void WatermarkType_Changed(object sender, RoutedEventArgs e)
        {
            if (PanelTextWatermarkSettings == null || PanelImageWatermarkSettings == null) return;

            if (RadioTextWatermark.IsChecked == true)
            {
                PanelTextWatermarkSettings.Visibility = Visibility.Visible;
                PanelImageWatermarkSettings.Visibility = Visibility.Collapsed;
            }
            else
            {
                PanelTextWatermarkSettings.Visibility = Visibility.Collapsed;
                PanelImageWatermarkSettings.Visibility = Visibility.Visible;
            }
        }

        private void BtnSelectWatermarkImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Görsel Dosyaları (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
                Title = "Filigran Görselini Seçin"
            };

            if (dialog.ShowDialog() == true)
            {
                _watermarkImagePath = dialog.FileName;
                TxtWatermarkImagePath.Text = Path.GetFileName(dialog.FileName);
            }
        }

        private void BtnWatermarkAction_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_loadedWatermarkFile)) return;

            double opacity = SliderWatermarkOpacity.Value;
            double rotation = SliderWatermarkAngle.Value;

            var dialog = new SaveFileDialog
            {
                Filter = "PDF Belgesi (*.pdf)|*.pdf",
                FileName = "[Filigranli] " + Path.GetFileName(_loadedWatermarkFile),
                Title = "Filigranlı PDF Belgesini Kaydet"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                if (RadioTextWatermark.IsChecked == true)
                {
                    string text = TxtWatermarkText.Text;
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        ShowToast("Lütfen filigran metnini yazın.", false);
                        return;
                    }

                    double fontSize = SliderWTextFontSize.Value;
                    Color color = Colors.Red;

                    if (CmbWatermarkColor.SelectedItem is ComboBoxItem item && item.Tag is string tag)
                    {
                        var convertedColor = ColorConverter.ConvertFromString(tag);
                        if (convertedColor is Color c) color = c;
                    }

                    PdfService.AddTextWatermark(
                        _loadedWatermarkFile,
                        dialog.FileName,
                        text,
                        opacity,
                        rotation,
                        fontSize,
                        color,
                        "Arial"
                    );
                }
                else
                {
                    if (string.IsNullOrEmpty(_watermarkImagePath) || !File.Exists(_watermarkImagePath))
                    {
                        ShowToast("Lütfen geçerli bir filigran resmi seçin.", false);
                        return;
                    }

                    double scale = SliderWImageScale.Value;
                    string position = "Center";

                    if (CmbWatermarkPosition.SelectedItem is ComboBoxItem item && item.Tag is string tag)
                    {
                        position = tag;
                    }

                    PdfService.AddImageWatermark(
                        _loadedWatermarkFile,
                        dialog.FileName,
                        _watermarkImagePath,
                        opacity,
                        rotation,
                        scale,
                        position
                    );
                }

                ShowToast("Filigran başarıyla eklendi.");
                LoadSinglePdfFile(dialog.FileName); // Refresh loaded file
            }
            catch (Exception ex)
            {
                ShowToast($"Hata oluştu: {ex.Message}", false);
            }
        }

        #endregion

        #region PDF To Image Tab Logic

        private void BtnSelectToImageFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "PDF Belgeleri (*.pdf)|*.pdf",
                Title = "Görsele Çevrilecek PDF Belgesini Seçin"
            };

            if (dialog.ShowDialog() == true)
            {
                LoadSinglePdfFile(dialog.FileName);
            }
        }

        private void BtnSelectToImageFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Görsellerin Kaydedileceği Klasörü Seçin";
                dialog.UseDescriptionForTitle = true;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    TxtToImageFolder.Text = dialog.SelectedPath;
                    UpdateToImageActionButtonState();
                }
            }
        }

        private void UpdateToImageActionButtonState()
        {
            if (BtnToImageAction == null) return;

            bool hasFile = !string.IsNullOrEmpty(_loadedToImageFile);
            bool hasFolder = !string.IsNullOrEmpty(TxtToImageFolder.Text) && TxtToImageFolder.Text != "Seçilmedi...";

            BtnToImageAction.IsEnabled = hasFile && hasFolder;
        }

        private void BtnToImageAction_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_loadedToImageFile) || string.IsNullOrEmpty(TxtToImageFolder.Text)) return;

            string folder = TxtToImageFolder.Text;
            int dpi = 150;

            if (CmbToImageDpi.SelectedItem is ComboBoxItem dpiItem && dpiItem.Tag is string dpiStr)
            {
                int.TryParse(dpiStr, out dpi);
            }

            string format = "PNG";
            if (CmbToImageFormat.SelectedItem is ComboBoxItem formatItem && formatItem.Tag is string formatStr)
            {
                format = formatStr;
            }

            try
            {
                PdfService.ConvertPdfToImages(_loadedToImageFile, folder, format, dpi);
                ShowToast("Sayfalar başarıyla görsel olarak kaydedildi.");
            }
            catch (Exception ex)
            {
                ShowToast($"Hata oluştu: {ex.Message}", false);
            }
        }

        #endregion

        #region Image To PDF Tab Logic

        private void UpdateToPdfStatus()
        {
            if (_toPdfFiles.Count == 0)
            {
                TxtToPdfStatus.Text = "Listeye en az 1 görsel ekleyin.";
            }
            else
            {
                TxtToPdfStatus.Text = $"Listede {_toPdfFiles.Count} adet görsel bulunuyor.";
            }
        }

        private bool AddFileToToPdfList(string path)
        {
            if (_toPdfFiles.Any(f => f.FilePath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                return false;

            _toPdfFiles.Add(new FileItem
            {
                FileName = Path.GetFileName(path),
                FilePath = path
            });
            return true;
        }

        private void BtnToPdfAdd_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Görsel Dosyaları (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
                Multiselect = true,
                Title = "Dönüştürülecek Görselleri Seçin"
            };

            if (dialog.ShowDialog() == true)
            {
                int addedCount = 0;
                foreach (string file in dialog.FileNames)
                {
                    if (AddFileToToPdfList(file))
                        addedCount++;
                }
                UpdateToPdfStatus();
                if (addedCount > 0)
                {
                    ShowToast($"{addedCount} adet görsel listeye eklendi.");
                }
            }
        }

        private void BtnToPdfUp_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = LstToPdfFiles.SelectedIndex;
            if (selectedIndex > 0)
            {
                _toPdfFiles.Move(selectedIndex, selectedIndex - 1);
                LstToPdfFiles.SelectedIndex = selectedIndex - 1;
            }
        }

        private void BtnToPdfDown_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = LstToPdfFiles.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < _toPdfFiles.Count - 1)
            {
                _toPdfFiles.Move(selectedIndex, selectedIndex + 1);
                LstToPdfFiles.SelectedIndex = selectedIndex + 1;
            }
        }

        private void BtnToPdfRemove_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = LstToPdfFiles.SelectedIndex;
            if (selectedIndex >= 0)
            {
                _toPdfFiles.RemoveAt(selectedIndex);
            }
        }

        private void BtnToPdfClear_Click(object sender, RoutedEventArgs e)
        {
            _toPdfFiles.Clear();
        }

        private void BtnToPdfAction_Click(object sender, RoutedEventArgs e)
        {
            if (_toPdfFiles.Count == 0)
            {
                ShowToast("Dönüştürmek için en az 1 görsel eklemelisiniz.", false);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "PDF Belgesi (*.pdf)|*.pdf",
                FileName = "gorsellerden_belge.pdf",
                Title = "Oluşturulacak PDF Belgesini Kaydet"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var filePaths = _toPdfFiles.Select(f => f.FilePath).ToList();
                    PdfService.ConvertImagesToPdf(filePaths, dialog.FileName);
                    ShowToast("Görseller başarıyla PDF dosyasına dönüştürüldü.");
                    
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    ShowToast($"Hata oluştu: {ex.Message}", false);
                }
            }
        }

        #endregion

        #region Helper: Page Range Parser

        /// <summary>
        /// Parses a string like "1,3,5-7" and returns list of 0-based page indices.
        /// </summary>
        private static List<int> ParsePageRange(string rangeStr, int maxPages)
        {
            var indices = new List<int>();
            if (string.IsNullOrWhiteSpace(rangeStr))
                return indices;

            if (rangeStr.Trim().Equals("hepsi", StringComparison.OrdinalIgnoreCase) || 
                rangeStr.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 0; i < maxPages; i++)
                    indices.Add(i);
                return indices;
            }

            var parts = rangeStr.Split(',', ';');
            foreach (var part in parts)
            {
                var cleanPart = part.Trim();
                if (cleanPart.Contains('-'))
                {
                    var rangeParts = cleanPart.Split('-');
                    if (rangeParts.Length == 2 && 
                        int.TryParse(rangeParts[0], out int start) && 
                        int.TryParse(rangeParts[1], out int end))
                    {
                        // Convert 1-based to 0-based
                        int startIdx = Math.Min(start, end) - 1;
                        int endIdx = Math.Max(start, end) - 1;

                        startIdx = Math.Max(0, startIdx);
                        endIdx = Math.Min(maxPages - 1, endIdx);

                        for (int i = startIdx; i <= endIdx; i++)
                        {
                            if (!indices.Contains(i))
                                indices.Add(i);
                        }
                    }
                }
                else
                {
                    if (int.TryParse(cleanPart, out int val))
                    {
                        int idx = val - 1; // 1-based to 0-based
                        if (idx >= 0 && idx < maxPages)
                        {
                            if (!indices.Contains(idx))
                                indices.Add(idx);
                        }
                    }
                }
            }
            indices.Sort();
            return indices;
        }

        #endregion

        #region PDF Previews and Drag Drop Extra Handlers

        private void MergeList_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
                e.Handled = true;
            }
            else if (e.Data.GetDataPresent("FileItemDragData") && _activeDragSource == LstMergeFiles)
            {
                e.Effects = System.Windows.DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private void MergeList_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[]? files = (string[]?)e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files == null || files.Length == 0) return;

                int addedCount = 0;
                foreach (var file in files)
                {
                    if (Path.GetExtension(file).ToLower() == ".pdf")
                    {
                        if (AddFileToMergeList(file))
                        {
                            addedCount++;
                        }
                    }
                }
                UpdateMergeStatus();
                if (addedCount > 0)
                {
                    ShowToast($"{addedCount} adet PDF birleştirme listesine eklendi.");
                }
            }
            else if (e.Data.GetDataPresent("FileItemDragData") && _activeDragSource == LstMergeFiles)
            {
                var droppedItem = e.Data.GetData("FileItemDragData") as FileItem;
                if (droppedItem != null)
                {
                    var targetItem = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject)?.Content as FileItem;
                    int oldIndex = _mergeFiles.IndexOf(droppedItem);
                    if (oldIndex >= 0)
                    {
                        int newIndex = targetItem != null ? _mergeFiles.IndexOf(targetItem) : _mergeFiles.Count - 1;
                        if (newIndex >= 0 && oldIndex != newIndex)
                        {
                            _mergeFiles.Move(oldIndex, newIndex);
                        }
                    }
                }
            }
        }

        private void ToPdfList_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
                e.Handled = true;
            }
            else if (e.Data.GetDataPresent("FileItemDragData") && _activeDragSource == LstToPdfFiles)
            {
                e.Effects = System.Windows.DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private void ToPdfList_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[]? files = (string[]?)e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files == null || files.Length == 0) return;

                int addedCount = 0;
                foreach (var file in files)
                {
                    var ext = Path.GetExtension(file).ToLower();
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
                    {
                        if (AddFileToToPdfList(file))
                            addedCount++;
                    }
                }
                UpdateToPdfStatus();
                if (addedCount > 0)
                {
                    ShowToast($"{addedCount} adet görsel listeye eklendi.");
                }
            }
            else if (e.Data.GetDataPresent("FileItemDragData") && _activeDragSource == LstToPdfFiles)
            {
                var droppedItem = e.Data.GetData("FileItemDragData") as FileItem;
                if (droppedItem != null)
                {
                    var targetItem = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject)?.Content as FileItem;
                    int oldIndex = _toPdfFiles.IndexOf(droppedItem);
                    if (oldIndex >= 0)
                    {
                        int newIndex = targetItem != null ? _toPdfFiles.IndexOf(targetItem) : _toPdfFiles.Count - 1;
                        if (newIndex >= 0 && oldIndex != newIndex)
                        {
                            _toPdfFiles.Move(oldIndex, newIndex);
                        }
                    }
                }
            }
        }

        private void List_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                _dragStartPoint = e.GetPosition(null);
                _activeDragSource = listBox;
                _draggedItem = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject)?.Content as FileItem;
            }
        }

        private void List_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null && _activeDragSource == sender)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _dragStartPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var dragData = new DataObject("FileItemDragData", _draggedItem);
                    DragDrop.DoDragDrop(_activeDragSource, dragData, DragDropEffects.Move);

                    // Reset tracking
                    _draggedItem = null;
                    _activeDragSource = null;
                }
            }
        }

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            if (child == null) return null;
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindVisualParent<T>(parentObject);
        }

        private void SinglePdfDragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[]? files = (string[]?)e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files != null && files.Length == 1 && Path.GetExtension(files[0]).ToLower() == ".pdf")
                {
                    e.Effects = System.Windows.DragDropEffects.Copy;
                    e.Handled = true;
                }
            }
        }

        private void RotateDrop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[]? files = (string[]?)e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files != null && files.Length > 0 && Path.GetExtension(files[0]).ToLower() == ".pdf")
                {
                    LoadSinglePdfFile(files[0]);
                    ShowToast("PDF dosyası başarıyla yüklendi.");
                }
            }
        }

        private void DeleteDrop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[]? files = (string[]?)e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files != null && files.Length > 0 && Path.GetExtension(files[0]).ToLower() == ".pdf")
                {
                    LoadSinglePdfFile(files[0]);
                    ShowToast("PDF dosyası başarıyla yüklendi.");
                }
            }
        }

        private void BtnRotatePrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_rotatePreviewPageIndex > 0)
            {
                _rotatePreviewPageIndex--;
                UpdateRotatePreview();
            }
        }

        private void BtnRotateNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_loadedRotateFile)) return;
            int pageCount = PdfService.GetPdfPageCount(_loadedRotateFile);
            if (_rotatePreviewPageIndex < pageCount - 1)
            {
                _rotatePreviewPageIndex++;
                UpdateRotatePreview();
            }
        }

        private void BtnDeletePrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_deletePreviewPageIndex > 0)
            {
                _deletePreviewPageIndex--;
                UpdateDeletePreview();
            }
        }

        private void BtnDeleteNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_loadedDeleteFile)) return;
            int pageCount = PdfService.GetPdfPageCount(_loadedDeleteFile);
            if (_deletePreviewPageIndex < pageCount - 1)
            {
                _deletePreviewPageIndex++;
                UpdateDeletePreview();
            }
        }

        private void UpdateRotatePreview()
        {
            if (string.IsNullOrEmpty(_loadedRotateFile)) return;
            int pageCount = PdfService.GetPdfPageCount(_loadedRotateFile);
            if (pageCount == 0) return;

            if (_rotatePreviewPageIndex < 0) _rotatePreviewPageIndex = 0;
            if (_rotatePreviewPageIndex >= pageCount) _rotatePreviewPageIndex = pageCount - 1;

            TxtRotatePageIndicator.Text = $"Sayfa {_rotatePreviewPageIndex + 1} / {pageCount}";

            var bmp = RenderPdfPage(_loadedRotateFile, _rotatePreviewPageIndex);
            if (bmp != null)
            {
                ImgRotatePreview.Source = bmp;
            }
        }

        private void UpdateDeletePreview()
        {
            if (string.IsNullOrEmpty(_loadedDeleteFile)) return;
            int pageCount = PdfService.GetPdfPageCount(_loadedDeleteFile);
            if (pageCount == 0) return;

            if (_deletePreviewPageIndex < 0) _deletePreviewPageIndex = 0;
            if (_deletePreviewPageIndex >= pageCount) _deletePreviewPageIndex = pageCount - 1;

            TxtDeletePageIndicator.Text = $"Sayfa {_deletePreviewPageIndex + 1} / {pageCount}";

            var bmp = RenderPdfPage(_loadedDeleteFile, _deletePreviewPageIndex);
            if (bmp != null)
            {
                ImgDeletePreview.Source = bmp;
            }
        }

        private BitmapSource? RenderPdfPage(string pdfPath, int pageIndex)
        {
            if (!File.Exists(pdfPath)) return null;

            try
            {
                using (var docLib = DocLib.Instance)
                {
                    using (var docReader = docLib.GetDocReader(pdfPath, new PageDimensions(1.2)))
                    {
                        int pageCount = docReader.GetPageCount();
                        if (pageIndex < 0 || pageIndex >= pageCount) return null;

                        using (var pageReader = docReader.GetPageReader(pageIndex))
                        {
                            int width = pageReader.GetPageWidth();
                            int height = pageReader.GetPageHeight();
                            byte[] rawBytes = pageReader.GetImage(); // BGRA

                            int stride = width * 4;
                            var bitmap = BitmapSource.Create(
                                width,
                                height,
                                96,
                                96,
                                System.Windows.Media.PixelFormats.Bgra32,
                                null,
                                rawBytes,
                                stride
                            );
                            bitmap.Freeze();
                            return bitmap;
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private void BtnSelectSplitFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "PDF Belgeleri (*.pdf)|*.pdf",
                Title = "Bölünecek PDF Belgesini Seçin"
            };

            if (dialog.ShowDialog() == true)
            {
                LoadSinglePdfFile(dialog.FileName);
            }
        }

        private void SplitDrop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[]? files = (string[]?)e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files != null && files.Length > 0 && Path.GetExtension(files[0]).ToLower() == ".pdf")
                {
                    LoadSinglePdfFile(files[0]);
                    ShowToast("PDF dosyası başarıyla yüklendi.");
                }
            }
        }

        private void SplitMode_Changed(object sender, RoutedEventArgs e)
        {
            if (PanelSplitExtractSettings == null) return;

            if (RadioSplitExtract.IsChecked == true)
            {
                PanelSplitExtractSettings.Visibility = Visibility.Visible;
            }
            else
            {
                PanelSplitExtractSettings.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnSplitPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_splitPreviewPageIndex > 0)
            {
                _splitPreviewPageIndex--;
                UpdateSplitPreview();
            }
        }

        private void BtnSplitNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_loadedSplitFile)) return;
            int pageCount = PdfService.GetPdfPageCount(_loadedSplitFile);
            if (_splitPreviewPageIndex < pageCount - 1)
            {
                _splitPreviewPageIndex++;
                UpdateSplitPreview();
            }
        }

        private void UpdateSplitPreview()
        {
            if (string.IsNullOrEmpty(_loadedSplitFile)) return;
            int pageCount = PdfService.GetPdfPageCount(_loadedSplitFile);
            if (pageCount == 0) return;

            if (_splitPreviewPageIndex < 0) _splitPreviewPageIndex = 0;
            if (_splitPreviewPageIndex >= pageCount) _splitPreviewPageIndex = pageCount - 1;

            TxtSplitPageIndicator.Text = $"Sayfa {_splitPreviewPageIndex + 1} / {pageCount}";

            var bmp = RenderPdfPage(_loadedSplitFile, _splitPreviewPageIndex);
            if (bmp != null)
            {
                ImgSplitPreview.Source = bmp;
            }
        }

        private void BtnSplitAction_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_loadedSplitFile)) return;

            if (RadioSplitExtract.IsChecked == true)
            {
                int maxPages = PdfService.GetPdfPageCount(_loadedSplitFile);
                string rangeInput = TxtSplitPages.Text;

                List<int> pagesToKeep;
                try
                {
                    pagesToKeep = ParsePageRange(rangeInput, maxPages);
                }
                catch
                {
                    ShowToast("Sayfa numarası formatı geçersiz. Örn: 1-3, 5", false);
                    return;
                }

                if (pagesToKeep.Count == 0)
                {
                    ShowToast("Ayıklanacak sayfa belirtilmedi.", false);
                    return;
                }

                if (ChkSplitExtractIndividually.IsChecked == true)
                {
                    var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                    {
                        Description = "Seçilen sayfaların ayrı ayrı PDF olarak kaydedileceği klasörü seçin",
                        UseDescriptionForTitle = true
                    };

                    if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        try
                        {
                            PdfService.ExtractPdfPagesIndividually(_loadedSplitFile, folderDialog.SelectedPath, pagesToKeep);
                            ShowToast("Seçilen sayfalar ayrı ayrı başarıyla ayıklandı.");
                        }
                        catch (Exception ex)
                        {
                            ShowToast($"Hata oluştu: {ex.Message}", false);
                        }
                    }
                }
                else
                {
                    var dialog = new SaveFileDialog
                    {
                        Filter = "PDF Belgesi (*.pdf)|*.pdf",
                        FileName = "[Ayiklanmis] " + Path.GetFileName(_loadedSplitFile),
                        Title = "Ayıklanan PDF Belgesini Kaydet"
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        try
                        {
                            PdfService.ExtractPdfPages(_loadedSplitFile, dialog.FileName, pagesToKeep);
                            ShowToast("PDF ayıklama işlemi başarıyla tamamlandı.");
                        }
                        catch (Exception ex)
                        {
                            ShowToast($"Hata oluştu: {ex.Message}", false);
                        }
                    }
                }
            }
            else
            {
                // Split all pages into individual files
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Parçalanmış PDF sayfalarının kaydedileceği klasörü seçin",
                    UseDescriptionForTitle = true
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        PdfService.SplitPdfToIndividualPages(_loadedSplitFile, dialog.SelectedPath);
                        ShowToast("PDF tüm sayfalara başarıyla bölündü.");
                    }
                    catch (Exception ex)
                    {
                        ShowToast($"Hata oluştu: {ex.Message}", false);
                    }
                }
            }
        }

        #region New Features Logic

        // --- COMPRESS ---
        private string _loadedCompressFile;
        private void BtnSelectCompressFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "PDF Belgeleri (*.pdf)|*.pdf" };
            if (dlg.ShowDialog() == true) LoadCompressFile(dlg.FileName);
        }
        private void CompressDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0 && Path.GetExtension(files[0]).ToLower() == ".pdf")
                    LoadCompressFile(files[0]);
            }
        }
        private void LoadCompressFile(string path)
        {
            _loadedCompressFile = path;
            TxtCompressFileName.Text = Path.GetFileName(path);
            BtnCompressAction.IsEnabled = true;
        }
        private void BtnCompressAction_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = "sikistirilmis.pdf" };
            if (dlg.ShowDialog() == true)
            {
                try {
                    int level = CmbCompressLevel.SelectedIndex;
                    bool compressImages = ChkCompressImages.IsChecked == true;
                    bool compressStreams = ChkCompressStreams.IsChecked == true;
                    
                    PdfTool.Services.PdfServiceExtensions.CompressPdf(_loadedCompressFile, dlg.FileName, level, compressImages, compressStreams);
                    ShowToast("PDF başarıyla sıkıştırıldı.");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
                } catch (Exception ex) { ShowToast($"Hata: {ex.Message}", false); }
            }
        }

        // --- PROTECT ---
        private string _loadedProtectFile;
        private void BtnSelectProtectFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "PDF Belgeleri (*.pdf)|*.pdf" };
            if (dlg.ShowDialog() == true) LoadProtectFile(dlg.FileName);
        }
        private void ProtectDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0 && Path.GetExtension(files[0]).ToLower() == ".pdf")
                    LoadProtectFile(files[0]);
            }
        }
        private void LoadProtectFile(string path)
        {
            _loadedProtectFile = path;
            TxtProtectFileName.Text = Path.GetFileName(path);
            BtnProtectAction.IsEnabled = true;
        }
        private void BtnProtectAction_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = "islem_gormus.pdf" };
            if (dlg.ShowDialog() == true)
            {
                try {
                    if (CmbProtectAction.SelectedIndex == 0)
                        PdfTool.Services.PdfServiceExtensions.ProtectPdf(_loadedProtectFile, dlg.FileName, TxtProtectPassword.Text, TxtProtectPassword.Text);
                    else
                        PdfTool.Services.PdfServiceExtensions.UnlockPdf(_loadedProtectFile, dlg.FileName, TxtProtectPassword.Text);
                    ShowToast("PDF güvenlik işlemi başarıyla tamamlandı.");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
                } catch (Exception ex) { ShowToast($"Hata: {ex.Message}", false); }
            }
        }

        // --- PAGE NUMBER ---
        private string _loadedPageNumberFile;
        private void BtnSelectPageNumberFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "PDF Belgeleri (*.pdf)|*.pdf" };
            if (dlg.ShowDialog() == true) LoadPageNumberFile(dlg.FileName);
        }
        private void PageNumberDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0 && Path.GetExtension(files[0]).ToLower() == ".pdf")
                    LoadPageNumberFile(files[0]);
            }
        }
        private void LoadPageNumberFile(string path)
        {
            _loadedPageNumberFile = path;
            TxtPageNumberFileName.Text = Path.GetFileName(path);
            BtnPageNumberAction.IsEnabled = true;
        }
        private void BtnPageNumberAction_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = "numarali.pdf" };
            if (dlg.ShowDialog() == true)
            {
                try {
                    int position = CmbPageNumberPosition.SelectedIndex;
                    int fontSize = (int)SldPageNumberSize.Value;
                    PdfTool.Services.PdfServiceExtensions.AddPageNumbers(_loadedPageNumberFile, dlg.FileName, TxtPageNumberFormat.Text, position, fontSize);
                    ShowToast("PDF sayfa numaraları başarıyla eklendi.");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
                } catch (Exception ex) { ShowToast($"Hata: {ex.Message}", false); }
            }
        }

        // --- EXTRACT TEXT ---
        private string _loadedExtractTextFile;
        private void BtnSelectExtractTextFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "PDF Belgeleri (*.pdf)|*.pdf" };
            if (dlg.ShowDialog() == true) LoadExtractTextFile(dlg.FileName);
        }
        private void ExtractTextDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0 && Path.GetExtension(files[0]).ToLower() == ".pdf")
                    LoadExtractTextFile(files[0]);
            }
        }
        private void LoadExtractTextFile(string path)
        {
            _loadedExtractTextFile = path;
            TxtExtractTextFileName.Text = Path.GetFileName(path);
            BtnExtractTextAction.IsEnabled = true;
        }
        private void BtnExtractTextAction_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "Metin (*.txt)|*.txt", FileName = "metin.txt" };
            if (dlg.ShowDialog() == true)
            {
                try {
                    PdfTool.Services.PdfServiceExtensions.ExtractText(_loadedExtractTextFile, dlg.FileName);
                    ShowToast("Metin çıkarma işlemi başarıyla tamamlandı.");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
                } catch (Exception ex) { ShowToast($"Hata: {ex.Message}", false); }
            }
        }

        #endregion
        // --- EXTRACT IMAGE ---
        private string _loadedExtractImageFile;
        private void BtnSelectExtractImageFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "PDF Belgeleri (*.pdf)|*.pdf" };
            if (dlg.ShowDialog() == true) LoadExtractImageFile(dlg.FileName);
        }
        private void ExtractImageDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0 && Path.GetExtension(files[0]).ToLower() == ".pdf")
                    LoadExtractImageFile(files[0]);
            }
        }
        private void LoadExtractImageFile(string path)
        {
            _loadedExtractImageFile = path;
            TxtExtractImageFileName.Text = Path.GetFileName(path);
            BtnExtractImageAction.IsEnabled = true;
        }
        private void BtnExtractImageAction_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { 
                Title = "Klasör Seçmek İçin Dosya Adını Dokunmadan Kaydet'e Basın",
                Filter = "Klasör Yolu (*.klasor)|*.klasor", 
                FileName = "Gorselleri_Buraya_Cikar.klasor" 
            };
            if (dlg.ShowDialog() == true)
            {
                try {
                    string outFolder = Path.GetDirectoryName(dlg.FileName);
                    PdfTool.Services.PdfServiceExtensions.ExtractImages(_loadedExtractImageFile, outFolder);
                    ShowToast("Görseller başarıyla klasöre çıkarıldı.");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", outFolder) { UseShellExecute = true });
                } catch (Exception ex) { ShowToast($"Hata: {ex.Message}", false); }
            }
        }

        #endregion
    }

    /// <summary>
    /// File item view representation.
    /// </summary>
    public class FileItem
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string PageInfo { get; set; } = string.Empty;
        public int PageCount { get; set; }
    }

}