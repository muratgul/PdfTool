using System;
using System.Collections.Generic;
using System.IO;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Security;
using PdfSharp.Drawing;

namespace PdfTool.Services
{
    public static class PdfServiceExtensions
    {
        // 1. Compress
        public static void CompressPdf(string sourceFile, string outputFile)
        {
            using (PdfDocument document = PdfReader.Open(sourceFile, PdfDocumentOpenMode.Modify))
            {
                document.Options.CompressContentStreams = true;
                document.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Always;
                document.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
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
        public static void AddPageNumbers(string sourceFile, string outputFile, string format = "Sayfa {0} / {1}")
        {
            using (PdfDocument document = PdfReader.Open(sourceFile, PdfDocumentOpenMode.Modify))
            {
                XFont font = new XFont("Arial", 10, XFontStyleEx.Regular);
                XBrush brush = XBrushes.Black;
                int pageCount = document.PageCount;

                for (int i = 0; i < pageCount; i++)
                {
                    PdfPage page = document.Pages[i];
                    using (XGraphics gfx = XGraphics.FromPdfPage(page))
                    {
                        string text = string.Format(format, i + 1, pageCount);
                        XSize size = gfx.MeasureString(text, font);
                        
                        // Draw at the bottom center
                        double x = (page.Width - size.Width) / 2;
                        double y = page.Height - 20;

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
    }
}
