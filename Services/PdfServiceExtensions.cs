using System;
using System.Collections.Generic;
using System.IO;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Security;
using PdfSharp.Drawing;
using PdfSharp.Pdf.Advanced;
using System.Windows.Media.Imaging;

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

                int jpegQuality = 50;
                switch (compressionLevel)
                {
                    case 0: 
                        document.Options.FlateEncodeMode = PdfFlateEncodeMode.BestSpeed; 
                        jpegQuality = 75; // Low compression, high quality
                        break;
                    case 1: 
                        document.Options.FlateEncodeMode = PdfFlateEncodeMode.Default; 
                        jpegQuality = 50; // Medium compression
                        break;
                    case 2: 
                        document.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression; 
                        jpegQuality = 25; // High compression, lower quality
                        break;
                    default: 
                        document.Options.FlateEncodeMode = PdfFlateEncodeMode.Default; 
                        break;
                }

                if (compressImages)
                {
                    document.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Never;

                    // Actually re-compress JPEG images inside the PDF
                    foreach (PdfPage page in document.Pages)
                    {
                        PdfDictionary resources = page.Elements.GetDictionary("/Resources");
                        if (resources != null)
                        {
                            PdfDictionary xObjects = resources.Elements.GetDictionary("/XObject");
                            if (xObjects != null)
                            {
                                foreach (PdfItem item in xObjects.Elements.Values)
                                {
                                    if (item is PdfReference reference && reference.Value is PdfDictionary xObject)
                                    {
                                        if (xObject.Elements.GetString("/Subtype") == "/Image" && xObject.Elements.GetName("/Filter") == "/DCTDecode")
                                        {
                                            try
                                            {
                                                byte[] originalBytes = xObject.Stream.Value;
                                                using (var msOriginal = new MemoryStream(originalBytes))
                                                {
                                                    var decoder = new JpegBitmapDecoder(msOriginal, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                                                    var encoder = new JpegBitmapEncoder { QualityLevel = jpegQuality };
                                                    encoder.Frames.Add(decoder.Frames[0]);
                                                    
                                                    using (var msCompressed = new MemoryStream())
                                                    {
                                                        encoder.Save(msCompressed);
                                                        byte[] compressedBytes = msCompressed.ToArray();
                                                        
                                                        // Update the stream only if the compressed size is smaller
                                                        if (compressedBytes.Length < originalBytes.Length)
                                                        {
                                                            xObject.Stream.Value = compressedBytes;
                                                            xObject.Elements.SetInteger("/Length", compressedBytes.Length);
                                                        }
                                                    }
                                                }
                                            }
                                            catch { /* Ignore invalid image streams */ }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    document.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Never;
                }
                
                document.Options.NoCompression = false;
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
        public static void AddPageNumbers(string sourceFile, string outputFile, string format = "Sayfa {0} / {1}", int position = 1, int fontSize = 10, int marginX = 20, int marginY = 20, string fontFamily = "Arial")
        {
            using (PdfDocument document = PdfReader.Open(sourceFile, PdfDocumentOpenMode.Modify))
            {
                // JPEGs should never be touched to preserve original image quality
                document.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Never;
                // Enable lossless compression for text/layout to avoid huge file sizes
                document.Options.CompressContentStreams = true;
                document.Options.NoCompression = false;

                XFont font = new XFont(fontFamily, fontSize, XFontStyleEx.Regular);
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

                        // Position mapping:
                        // 0: Bottom Left, 1: Bottom Center, 2: Bottom Right
                        // 3: Top Left, 4: Top Center, 5: Top Right
                        switch (position)
                        {
                            case 0: x = marginX; y = page.Height - marginY; break;
                            case 1: x = (page.Width - size.Width) / 2; y = page.Height - marginY; break;
                            case 2: x = page.Width - size.Width - marginX; y = page.Height - marginY; break;
                            case 3: x = marginX; y = marginY + size.Height; break;
                            case 4: x = (page.Width - size.Width) / 2; y = marginY + size.Height; break;
                            case 5: x = page.Width - size.Width - marginX; y = marginY + size.Height; break;
                            default: x = (page.Width - size.Width) / 2; y = page.Height - marginY; break;
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
