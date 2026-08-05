using System;
using System.Collections.Generic;
using System.IO;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Security;
using PdfSharp.Drawing;
using PdfSharp.Pdf.Advanced;

namespace PdfTool.Services
{
    public static class PdfServiceExtensions
    {
        // 1. Compress
        public static void CompressPdf(string sourceFile, string outputFile, int compressionLevel = 1, bool compressImages = true, bool compressContent = true)
        {
            using (PdfDocument document = PdfReader.Open(sourceFile, PdfDocumentOpenMode.Modify))
            {
                document.Options.CompressContentStreams = compressContent;
                
                if (compressImages)
                    document.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Always;
                else
                    document.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Never;

                switch (compressionLevel)
                {
                    case 0: document.Options.FlateEncodeMode = PdfFlateEncodeMode.BestSpeed; break;
                    case 1: document.Options.FlateEncodeMode = PdfFlateEncodeMode.Default; break;
                    case 2: document.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression; break;
                    default: document.Options.FlateEncodeMode = PdfFlateEncodeMode.Default; break;
                }
                
                document.Save(outputFile);
            }
        }

        // 2. Protect
        public static void ProtectPdf(string sourceFile, string outputFile, string userPassword, string ownerPassword)
        {
            using (PdfDocument document = PdfReader.Open(sourceFile, PdfDocumentOpenMode.Modify))
            {
                PdfSecuritySettings securitySettings = document.SecuritySettings;
                securitySettings.UserPassword = userPassword;
                securitySettings.OwnerPassword = ownerPassword;
                
                // Restrict some rights
                securitySettings.PermitPrint = false;
                securitySettings.PermitModifyDocument = false;
                securitySettings.PermitExtractContent = false;

                document.Save(outputFile);
            }
        }

        public static void UnlockPdf(string sourceFile, string outputFile, string password)
        {
            using (PdfDocument document = PdfReader.Open(sourceFile, password, PdfDocumentOpenMode.Modify))
            {
                // Remove password protection by setting empty passwords
                document.SecuritySettings.UserPassword = "";
                document.SecuritySettings.OwnerPassword = "";
                document.Save(outputFile);
            }
        }

        // 3. Add Page Numbers
        public static void AddPageNumbers(string sourceFile, string outputFile, string format = "Sayfa {0} / {1}", int position = 1, int fontSize = 10)
        {
            using (PdfDocument document = PdfReader.Open(sourceFile, PdfDocumentOpenMode.Modify))
            {
                XFont font = new XFont("Arial", fontSize, XFontStyleEx.Regular);
                XBrush brush = XBrushes.Black;
                int pageCount = document.PageCount;

                for (int i = 0; i < pageCount; i++)
                {
                    PdfPage page = document.Pages[i];
                    using (XGraphics gfx = XGraphics.FromPdfPage(page))
                    {
                        string text = string.Format(format, i + 1, pageCount);
                        XSize size = gfx.MeasureString(text, font);
                        
                        double x = 0;
                        double y = 0;
                        double margin = 20;

                        // Position mapping:
                        // 0: Bottom Left, 1: Bottom Center, 2: Bottom Right
                        // 3: Top Left, 4: Top Center, 5: Top Right
                        switch (position)
                        {
                            case 0: x = margin; y = page.Height - margin; break;
                            case 1: x = (page.Width - size.Width) / 2; y = page.Height - margin; break;
                            case 2: x = page.Width - size.Width - margin; y = page.Height - margin; break;
                            case 3: x = margin; y = margin + size.Height; break;
                            case 4: x = (page.Width - size.Width) / 2; y = margin + size.Height; break;
                            case 5: x = page.Width - size.Width - margin; y = margin + size.Height; break;
                            default: x = (page.Width - size.Width) / 2; y = page.Height - margin; break;
                        }

                        gfx.DrawString(text, font, brush, new XPoint(x, y));
                    }
                }
                document.Save(outputFile);
            }
        }

        // 4. Reorder Pages
        public static void ReorderPages(string sourceFile, string outputFile, List<int> newOrder)
        {
            using (PdfDocument inputDocument = PdfReader.Open(sourceFile, PdfDocumentOpenMode.Import))
            using (PdfDocument outputDocument = new PdfDocument())
            {
                foreach (int index in newOrder)
                {
                    if (index >= 0 && index < inputDocument.PageCount)
                    {
                        outputDocument.AddPage(inputDocument.Pages[index]);
                    }
                }
                outputDocument.Save(outputFile);
            }
        }
        
        // 5. Extract Text
        public static void ExtractText(string sourceFile, string outputTxtFile)
        {
            using (var docReader = Docnet.Core.DocLib.Instance.GetDocReader(sourceFile, new Docnet.Core.Models.PageDimensions(1.0)))
            {
                int pageCount = docReader.GetPageCount();
                using (StreamWriter writer = new StreamWriter(outputTxtFile))
                {
                    for (int i = 0; i < pageCount; i++)
                    {
                        using (var pageReader = docReader.GetPageReader(i))
                        {
                            string text = pageReader.GetText();
                            writer.WriteLine($"--- Sayfa {i + 1} ---");
                            writer.WriteLine(text);
                            writer.WriteLine();
                        }
                    }
                }
            }
        }

        // 6. Extract Images
        public static void ExtractImages(string sourceFile, string outputFolder)
        {
            using (PdfDocument document = PdfReader.Open(sourceFile, PdfDocumentOpenMode.Import))
            {
                int imageCount = 0;
                foreach (PdfPage page in document.Pages)
                {
                    PdfDictionary resources = page.Elements.GetDictionary("/Resources");
                    if (resources != null)
                    {
                        PdfDictionary xObjects = resources.Elements.GetDictionary("/XObject");
                        if (xObjects != null)
                        {
                            var items = xObjects.Elements.Values;
                            foreach (PdfItem item in items)
                            {
                                PdfReference reference = item as PdfReference;
                                if (reference != null)
                                {
                                    PdfDictionary xObject = reference.Value as PdfDictionary;
                                    if (xObject != null && xObject.Elements.GetString("/Subtype") == "/Image")
                                    {
                                        string filter = xObject.Elements.GetName("/Filter");
                                        if (filter == "/DCTDecode")
                                        {
                                            byte[] stream = xObject.Stream.Value;
                                            File.WriteAllBytes(Path.Combine(outputFolder, $"image_{++imageCount}.jpg"), stream);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                if (imageCount == 0)
                {
                    throw new Exception("PDF içerisinde dışa aktarılabilecek (JPEG formatında) bir görsel bulunamadı.");
                }
            }
        }
    }
}
