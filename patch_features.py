import re
import os

xaml_path = r'V:\Repos\Pdftool\MainWindow.xaml'
cs_path = r'V:\Repos\Pdftool\MainWindow.xaml.cs'

with open(xaml_path, 'r', encoding='utf-8') as f:
    xaml_content = f.read()

# 1. Add Navigation Buttons
nav_injection = '''
                        <RadioButton x:Name=\"BtnNavCompress\" Content=\"PDF Sıkıştır\" Style=\"{StaticResource SidebarButtonStyle}\" Checked=\"BtnNav_Checked\"/>
                        <RadioButton x:Name=\"BtnNavProtect\" Content=\"Şifrele/Çöz\" Style=\"{StaticResource SidebarButtonStyle}\" Checked=\"BtnNav_Checked\"/>
                        <RadioButton x:Name=\"BtnNavPageNumber\" Content=\"Sayfa Numarası\" Style=\"{StaticResource SidebarButtonStyle}\" Checked=\"BtnNav_Checked\"/>
                        <RadioButton x:Name=\"BtnNavReorder\" Content=\"Sayfa Sırala\" Style=\"{StaticResource SidebarButtonStyle}\" Checked=\"BtnNav_Checked\"/>
                        <RadioButton x:Name=\"BtnNavExtractText\" Content=\"Metin Çıkar\" Style=\"{StaticResource SidebarButtonStyle}\" Checked=\"BtnNav_Checked\"/>
'''
if 'BtnNavCompress' not in xaml_content:
    xaml_content = xaml_content.replace(
        '<RadioButton x:Name=\"BtnNavToPdf\" Content=\"Görselden PDF\'e\" Style=\"{StaticResource SidebarButtonStyle}\" Checked=\"BtnNav_Checked\"/>',
        '<RadioButton x:Name=\"BtnNavToPdf\" Content=\"Görselden PDF\'e\" Style=\"{StaticResource SidebarButtonStyle}\" Checked=\"BtnNav_Checked\"/>\n' + nav_injection
    )

# 2. Add Tabs
tabs_injection = '''
                <!-- ================= NEW FEATURE TABS ================= -->
                <!-- Compress Tab -->
                <TabItem Header=\"Compress\">
                    <Grid>
                        <Grid.RowDefinitions>
                            <RowDefinition Height=\"Auto\"/>
                            <RowDefinition Height=\"*\"/>
                        </Grid.RowDefinitions>
                        <StackPanel Grid.Row=\"0\" Margin=\"0,0,0,16\">
                            <TextBlock Text=\"PDF Sıkıştırma\" FontSize=\"22\" FontWeight=\"Bold\" Foreground=\"{StaticResource TextLight}\"/>
                            <TextBlock Text=\"PDF dosyanızın boyutunu optimize edin.\" FontSize=\"13\" Foreground=\"{StaticResource TextGray}\" Margin=\"0,2,0,0\"/>
                        </StackPanel>
                        <Grid Grid.Row=\"1\">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width=\"*\"/>
                                <ColumnDefinition Width=\"320\"/>
                            </Grid.ColumnDefinitions>
                            <Border Background=\"{StaticResource BackgroundCard}\" CornerRadius=\"8\" BorderThickness=\"1\" BorderBrush=\"{StaticResource BorderDark}\" Margin=\"0,0,16,0\" Padding=\"20\" AllowDrop=\"True\" DragOver=\"SinglePdfDragOver\" Drop=\"CompressDrop\">
                                <StackPanel VerticalAlignment=\"Center\">
                                    <Button Content=\"PDF Seçin\" Style=\"{StaticResource ModernButtonStyle}\" Click=\"BtnSelectCompressFile_Click\" HorizontalAlignment=\"Center\"/>
                                    <TextBlock x:Name=\"TxtCompressFileName\" Text=\"Dosya seçilmedi\" Foreground=\"{StaticResource TextGray}\" Margin=\"0,10,0,0\" HorizontalAlignment=\"Center\"/>
                                </StackPanel>
                            </Border>
                            <Border Grid.Column=\"1\" Background=\"{StaticResource BackgroundCard}\" CornerRadius=\"8\" BorderThickness=\"1\" BorderBrush=\"{StaticResource BorderDark}\" Padding=\"20\">
                                <StackPanel>
                                    <TextBlock Text=\"Ayarlar\" FontSize=\"16\" FontWeight=\"Bold\" Foreground=\"{StaticResource TextLight}\" Margin=\"0,0,0,16\"/>
                                    <Button x:Name=\"BtnCompressAction\" Content=\"Sıkıştır ve Kaydet\" Style=\"{StaticResource ModernButtonStyle}\" Click=\"BtnCompressAction_Click\" IsEnabled=\"False\"/>
                                </StackPanel>
                            </Border>
                        </Grid>
                    </Grid>
                </TabItem>

                <!-- Protect Tab -->
                <TabItem Header=\"Protect\">
                    <Grid>
                        <Grid.RowDefinitions><RowDefinition Height=\"Auto\"/><RowDefinition Height=\"*\"/></Grid.RowDefinitions>
                        <StackPanel Grid.Row=\"0\" Margin=\"0,0,0,16\">
                            <TextBlock Text=\"Şifrele / Çöz\" FontSize=\"22\" FontWeight=\"Bold\" Foreground=\"{StaticResource TextLight}\"/>
                            <TextBlock Text=\"PDF dosyasına parola koyun veya kaldırın.\" FontSize=\"13\" Foreground=\"{StaticResource TextGray}\" Margin=\"0,2,0,0\"/>
                        </StackPanel>
                        <Grid Grid.Row=\"1\">
                            <Grid.ColumnDefinitions><ColumnDefinition Width=\"*\"/><ColumnDefinition Width=\"320\"/></Grid.ColumnDefinitions>
                            <Border Background=\"{StaticResource BackgroundCard}\" CornerRadius=\"8\" BorderThickness=\"1\" BorderBrush=\"{StaticResource BorderDark}\" Margin=\"0,0,16,0\" Padding=\"20\" AllowDrop=\"True\" DragOver=\"SinglePdfDragOver\" Drop=\"ProtectDrop\">
                                <StackPanel VerticalAlignment=\"Center\">
                                    <Button Content=\"PDF Seçin\" Style=\"{StaticResource ModernButtonStyle}\" Click=\"BtnSelectProtectFile_Click\" HorizontalAlignment=\"Center\"/>
                                    <TextBlock x:Name=\"TxtProtectFileName\" Text=\"Dosya seçilmedi\" Foreground=\"{StaticResource TextGray}\" Margin=\"0,10,0,0\" HorizontalAlignment=\"Center\"/>
                                </StackPanel>
                            </Border>
                            <Border Grid.Column=\"1\" Background=\"{StaticResource BackgroundCard}\" CornerRadius=\"8\" BorderThickness=\"1\" BorderBrush=\"{StaticResource BorderDark}\" Padding=\"20\">
                                <StackPanel>
                                    <TextBlock Text=\"İşlem Türü\" FontSize=\"13\" FontWeight=\"SemiBold\" Margin=\"0,0,0,6\"/>
                                    <ComboBox x:Name=\"CmbProtectAction\" SelectedIndex=\"0\" Margin=\"0,0,0,12\">
                                        <ComboBoxItem Content=\"Şifre Ekle\"/>
                                        <ComboBoxItem Content=\"Şifre Kaldır\"/>
                                    </ComboBox>
                                    <TextBlock Text=\"Şifre (Açılış/Sahip)\" FontSize=\"13\" FontWeight=\"SemiBold\" Margin=\"0,0,0,6\"/>
                                    <TextBox x:Name=\"TxtProtectPassword\" Style=\"{StaticResource ModernTextBoxStyle}\" Margin=\"0,0,0,24\"/>
                                    <Button x:Name=\"BtnProtectAction\" Content=\"Uygula ve Kaydet\" Style=\"{StaticResource ModernButtonStyle}\" Click=\"BtnProtectAction_Click\" IsEnabled=\"False\"/>
                                </StackPanel>
                            </Border>
                        </Grid>
                    </Grid>
                </TabItem>

                <!-- Page Number Tab -->
                <TabItem Header=\"PageNumber\">
                    <Grid>
                        <Grid.RowDefinitions><RowDefinition Height=\"Auto\"/><RowDefinition Height=\"*\"/></Grid.RowDefinitions>
                        <StackPanel Grid.Row=\"0\" Margin=\"0,0,0,16\">
                            <TextBlock Text=\"Sayfa Numarası Ekle\" FontSize=\"22\" FontWeight=\"Bold\" Foreground=\"{StaticResource TextLight}\"/>
                            <TextBlock Text=\"PDF sayfalarının alt kısmına sayfa numarası ekleyin.\" FontSize=\"13\" Foreground=\"{StaticResource TextGray}\" Margin=\"0,2,0,0\"/>
                        </StackPanel>
                        <Grid Grid.Row=\"1\">
                            <Grid.ColumnDefinitions><ColumnDefinition Width=\"*\"/><ColumnDefinition Width=\"320\"/></Grid.ColumnDefinitions>
                            <Border Background=\"{StaticResource BackgroundCard}\" CornerRadius=\"8\" BorderThickness=\"1\" BorderBrush=\"{StaticResource BorderDark}\" Margin=\"0,0,16,0\" Padding=\"20\" AllowDrop=\"True\" DragOver=\"SinglePdfDragOver\" Drop=\"PageNumberDrop\">
                                <StackPanel VerticalAlignment=\"Center\">
                                    <Button Content=\"PDF Seçin\" Style=\"{StaticResource ModernButtonStyle}\" Click=\"BtnSelectPageNumberFile_Click\" HorizontalAlignment=\"Center\"/>
                                    <TextBlock x:Name=\"TxtPageNumberFileName\" Text=\"Dosya seçilmedi\" Foreground=\"{StaticResource TextGray}\" Margin=\"0,10,0,0\" HorizontalAlignment=\"Center\"/>
                                </StackPanel>
                            </Border>
                            <Border Grid.Column=\"1\" Background=\"{StaticResource BackgroundCard}\" CornerRadius=\"8\" BorderThickness=\"1\" BorderBrush=\"{StaticResource BorderDark}\" Padding=\"20\">
                                <StackPanel>
                                    <TextBlock Text=\"Format\" FontSize=\"13\" FontWeight=\"SemiBold\" Margin=\"0,0,0,6\"/>
                                    <TextBox x:Name=\"TxtPageNumberFormat\" Style=\"{StaticResource ModernTextBoxStyle}\" Text=\"Sayfa {0} / {1}\" Margin=\"0,0,0,24\"/>
                                    <Button x:Name=\"BtnPageNumberAction\" Content=\"Numara Ekle ve Kaydet\" Style=\"{StaticResource ModernButtonStyle}\" Click=\"BtnPageNumberAction_Click\" IsEnabled=\"False\"/>
                                </StackPanel>
                            </Border>
                        </Grid>
                    </Grid>
                </TabItem>

                <!-- Extract Text Tab -->
                <TabItem Header=\"ExtractText\">
                    <Grid>
                        <Grid.RowDefinitions><RowDefinition Height=\"Auto\"/><RowDefinition Height=\"*\"/></Grid.RowDefinitions>
                        <StackPanel Grid.Row=\"0\" Margin=\"0,0,0,16\">
                            <TextBlock Text=\"Metin Çıkarma\" FontSize=\"22\" FontWeight=\"Bold\" Foreground=\"{StaticResource TextLight}\"/>
                            <TextBlock Text=\"PDF içerisindeki tüm metni bir TXT dosyasına aktarın.\" FontSize=\"13\" Foreground=\"{StaticResource TextGray}\" Margin=\"0,2,0,0\"/>
                        </StackPanel>
                        <Grid Grid.Row=\"1\">
                            <Grid.ColumnDefinitions><ColumnDefinition Width=\"*\"/><ColumnDefinition Width=\"320\"/></Grid.ColumnDefinitions>
                            <Border Background=\"{StaticResource BackgroundCard}\" CornerRadius=\"8\" BorderThickness=\"1\" BorderBrush=\"{StaticResource BorderDark}\" Margin=\"0,0,16,0\" Padding=\"20\" AllowDrop=\"True\" DragOver=\"SinglePdfDragOver\" Drop=\"ExtractTextDrop\">
                                <StackPanel VerticalAlignment=\"Center\">
                                    <Button Content=\"PDF Seçin\" Style=\"{StaticResource ModernButtonStyle}\" Click=\"BtnSelectExtractTextFile_Click\" HorizontalAlignment=\"Center\"/>
                                    <TextBlock x:Name=\"TxtExtractTextFileName\" Text=\"Dosya seçilmedi\" Foreground=\"{StaticResource TextGray}\" Margin=\"0,10,0,0\" HorizontalAlignment=\"Center\"/>
                                </StackPanel>
                            </Border>
                            <Border Grid.Column=\"1\" Background=\"{StaticResource BackgroundCard}\" CornerRadius=\"8\" BorderThickness=\"1\" BorderBrush=\"{StaticResource BorderDark}\" Padding=\"20\">
                                <StackPanel>
                                    <Button x:Name=\"BtnExtractTextAction\" Content=\"Metni Çıkar (.txt)\" Style=\"{StaticResource ModernButtonStyle}\" Click=\"BtnExtractTextAction_Click\" IsEnabled=\"False\"/>
                                </StackPanel>
                            </Border>
                        </Grid>
                    </Grid>
                </TabItem>
'''
if 'Header=\"Compress\"' not in xaml_content:
    xaml_content = xaml_content.replace('</TabControl>', tabs_injection + '\n            </TabControl>')

with open(xaml_path, 'w', encoding='utf-8') as f:
    f.write(xaml_content)


with open(cs_path, 'r', encoding='utf-8') as f:
    cs_content = f.read()

# Route Navigation Buttons
cs_nav_routes = '''
            else if (clickedButton == BtnNavCompress) MainTabControl.SelectedIndex = 8;
            else if (clickedButton == BtnNavProtect) MainTabControl.SelectedIndex = 9;
            else if (clickedButton == BtnNavPageNumber) MainTabControl.SelectedIndex = 10;
            // else if (clickedButton == BtnNavReorder) MainTabControl.SelectedIndex = 11; // Skiping reorder UI for now to save space
            else if (clickedButton == BtnNavExtractText) MainTabControl.SelectedIndex = 11;
'''
if 'BtnNavCompress' not in cs_content:
    cs_content = cs_content.replace(
        'else if (clickedButton == BtnNavToPdf) MainTabControl.SelectedIndex = 7;',
        'else if (clickedButton == BtnNavToPdf) MainTabControl.SelectedIndex = 7;\n' + cs_nav_routes
    )

# Add code behind logic
cs_logic = '''
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
                    PdfTool.Services.PdfServiceExtensions.CompressPdf(_loadedCompressFile, dlg.FileName);
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
                    PdfTool.Services.PdfServiceExtensions.AddPageNumbers(_loadedPageNumberFile, dlg.FileName, TxtPageNumberFormat.Text);
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
'''

# We want to replace the LAST closing brace with our new logic and a closing brace.
if 'BtnCompressAction_Click' not in cs_content:
    last_brace_index = cs_content.rfind('}')
    cs_content = cs_content[:last_brace_index] + cs_logic + '\n}'
    
with open(cs_path, 'w', encoding='utf-8') as f:
    f.write(cs_content)

print("Patch complete.")
