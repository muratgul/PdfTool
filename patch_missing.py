import os

missing_strings = {
    "Dosya seçilmedi": "No file selected",
    "Değiştir": "Change",
    "Dosya": "File",
    "Bilgi": "Info",
    "Numaralı PDF Seçin": "Select Numbered PDF",
    "Şifreli PDF Seçin": "Select Protected PDF",
    "Metin Çıkarılacak PDF": "PDF to Extract Text",
    "Sıkıştırılacak PDF": "PDF to Compress",
}

tr_path = r'd:\Users\ta5mg\Desktop\pdftools\PdfTool\Languages\tr.xaml'
en_path = r'd:\Users\ta5mg\Desktop\pdftools\PdfTool\Languages\en.xaml'

with open(tr_path, 'r', encoding='utf-8') as f:
    tr_content = f.read()

with open(en_path, 'r', encoding='utf-8') as f:
    en_content = f.read()

# Add missing strings
next_id = 168
for tr, en in missing_strings.items():
    key = f"Str_{next_id}"
    tr_item = f'    <system:String x:Key="{key}">{tr}</system:String>\n</ResourceDictionary>'
    en_item = f'    <system:String x:Key="{key}">{en}</system:String>\n</ResourceDictionary>'
    
    if tr not in tr_content:
        tr_content = tr_content.replace('</ResourceDictionary>', tr_item)
        en_content = en_content.replace('</ResourceDictionary>', en_item)
        next_id += 1

with open(tr_path, 'w', encoding='utf-8') as f:
    f.write(tr_content)

with open(en_path, 'w', encoding='utf-8') as f:
    f.write(en_content)

# Now fix MainWindow.xaml and FilePreviewControl.xaml
files_to_patch = [
    r'd:\Users\ta5mg\Desktop\pdftools\PdfTool\MainWindow.xaml',
    r'd:\Users\ta5mg\Desktop\pdftools\PdfTool\Controls\FilePreviewControl.xaml'
]

# We need a dynamic dictionary to map turkish text to keys
import re

# parse tr_content to build dict
key_map = {}
for match in re.finditer(r'<system:String x:Key="(Str_\d+)">(.*?)</system:String>', tr_content):
    key_map[match.group(2)] = match.group(1)

for fp in files_to_patch:
    with open(fp, 'r', encoding='utf-8') as f:
        content = f.read()
    
    for tr, key in key_map.items():
        pattern = r'(Text|Content|Header|Title|ToolTip)="' + re.escape(tr) + r'"'
        replacement = r'\1="{DynamicResource ' + key + '}"'
        content = re.sub(pattern, replacement, content)
        
    with open(fp, 'w', encoding='utf-8') as f:
        f.write(content)

print("Patch successful!")
