import re
import json
import sys

xaml_file = r'd:\Users\ta5mg\Desktop\pdftools\PdfTool\MainWindow.xaml'
with open(xaml_file, 'r', encoding='utf-8') as f:
    content = f.read()

pattern = re.compile(r'(Text|Content|Header|Title|ToolTip)="([^"{}]*?[a-zA-ZçğıöşüÇĞİÖŞÜ][^"{}]*?)"')
matches = pattern.findall(content)

strings = set([m[1] for m in matches if m[1].strip()])

out = {}
for i, s in enumerate(strings):
    out[s] = f"Str_{i}"

with open(r'd:\Users\ta5mg\Desktop\pdftools\PdfTool\strings.json', 'w', encoding='utf-8') as f:
    json.dump(out, f, ensure_ascii=False, indent=2)

print("Saved to strings.json")
