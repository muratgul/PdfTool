import json
import re

with open(r'd:\Users\ta5mg\Desktop\pdftools\PdfTool\strings.json', 'r', encoding='utf-8') as f:
    strings = json.load(f)

with open(r'd:\Users\ta5mg\Desktop\pdftools\PdfTool\MainWindow.xaml', 'r', encoding='utf-8') as f:
    xaml_content = f.read()

# Replace strings with DynamicResource
for tr, key in strings.items():
    # we need to be careful. In XAML, it's (Text|Content|Header|Title|ToolTip)="tr"
    # We can replace them via regex to ensure we only replace attribute values
    pattern = r'(Text|Content|Header|Title|ToolTip)="' + re.escape(tr) + r'"'
    replacement = r'\1="{DynamicResource ' + key + '}"'
    xaml_content = re.sub(pattern, replacement, xaml_content)

# Add the Language Toggle Button next to Theme Toggle Button
theme_button = '<Button Click="BtnToggleTheme_Click" Background="Transparent" BorderThickness="0" Cursor="Hand" HorizontalAlignment="Right" VerticalAlignment="Center" ToolTip="Açık/Koyu Tema">'
lang_button = '<Button Click="BtnToggleLanguage_Click" Background="Transparent" BorderThickness="0" Cursor="Hand" HorizontalAlignment="Right" VerticalAlignment="Center" ToolTip="{DynamicResource Str_X}" Margin="0,0,10,0">\n                        <TextBlock Text="🌐" FontSize="18" Foreground="{DynamicResource TextGray}"/>\n                    </Button>\n                    ' + theme_button

if "BtnToggleLanguage_Click" not in xaml_content:
    xaml_content = xaml_content.replace(theme_button, lang_button)

with open(r'd:\Users\ta5mg\Desktop\pdftools\PdfTool\MainWindow.xaml', 'w', encoding='utf-8') as f:
    f.write(xaml_content)

print("MainWindow.xaml patched.")
