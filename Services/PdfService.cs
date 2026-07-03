using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Drawing;
using Docnet.Core;
using Docnet.Core.Models;

namespace PdfTool.Services
{
    public static class PdfService
    {
        /// <summary>
        /// Merges multiple PDF files into one.
        /// </summary>
        public static void MergePdf(List<string> sourceFiles, string outputFile)
        {
            if (sourceFiles == null || sourceFiles.Count == 0)
                throw new ArgumentException("No source files specified.");

            using (PdfDocument outputDocument = new PdfDocument())
            {
                OptimizeDocumentOptions(outputDocument);
                foreach (string file in sourceFiles)
                {
                    if (!File.Exists(file)) continue;

                    using (PdfDocument inputDocument = PdfReader.Open(file, PdfDocumentOpenMode.Import))
                    {
                        int count = inputDocument.PageCount;
                        for (int idx = 0; idx < count; idx++)
                        {
                            PdfPage page = inputDocument.Pages[idx];
                            outputDocument.AddPage(page);
                        }
                    }
                }
                outputDocument.Save(outputFile);
            }
        }

        /// <summary>
        /// Rotates specified pages of a PDF by 90, 180, or 270 degrees.
        /// </summary>
        public static void RotatePdfPages(string sourceFile, string outputFile, List<int> pageIndices, int rotationAngle)
        {
            if (!File.Exists(sourceFile))
                throw new FileNotFoundException("Source file not found.", sourceFile);

            using (PdfDocument document = PdfReader.Open(sourceFile, PdfDocumentOpenMode.Modify))
            {
                OptimizeDocumentOptions(document);
                foreach (int index in pageIndices)
                {
                    if (index >= 0 && index < document.PageCount)
                    {
                        PdfPage page = document.Pages[index];
                        page.Rotate = (page.Rotate + rotationAngle) % 360;
                    }
                }
                document.Save(outputFile);
            }
        }

        /// <summary>
        /// Deletes specified pages from a PDF.
        /// </summary>
        public static void DeletePdfPages(string sourceFile, string outputFile, List<int> pageIndicesToDelete)
        {
            if (!File.Exists(sourceFile))
                throw new FileNotFoundException("Source file not found.", sourceFile);

            // Sort indices in descending order to avoid shift issues during deletion
            var sortedIndices = new List<int>(pageIndicesToDelete);
            sortedIndices.Sort((a, b) => b.CompareTo(a));

            using (PdfDocument document = PdfReader.Open(sourceFile, PdfDocumentOpenMode.Modify))
            {
                OptimizeDocumentOptions(document);
                foreach (int index in sortedIndices)
                {
                    if (index >= 0 && index < document.PageCount)
                    {
                        document.Pages.RemoveAt(index);
                    }
                }
                
                if (document.PageCount == 0)
                {
                    throw new InvalidOperationException("You cannot delete all pages from a PDF.");
                }

                document.Save(outputFile);
            }
        }

        /// <summary>
        /// Adds a text watermark to all pages of a PDF.
        /// </summary>
        public static void AddTextWatermark(
            string sourceFile,
            string outputFile,
            string watermarkText,
            double opacity,
            double rotationAngle,
            double fontSize,
            System.Windows.Media.Color textColor,
            string fontName)
        {
            if (!File.Exists(sourceFile))
                throw new FileNotFoundException("Source file not found.", sourceFile);

            using (PdfDocument document = PdfReader.Open(sourceFile, PdfDocumentOpenMode.Modify))
            {
                OptimizeDocumentOptions(document);
                // Create font. FontResolver will handle this.
                XFont font = new XFont(fontName, fontSize, XFontStyleEx.Bold);

                int alpha = (int)(opacity * 255);
                XColor xColor = XColor.FromArgb(alpha, textColor.R, textColor.G, textColor.B);
                XBrush brush = new XSolidBrush(xColor);

                foreach (PdfPage page in document.Pages)
                {
                    using (XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append))
                    {
                        // Calculate middle point
                        double w = page.Width.Point;
                        double h = page.Height.Point;

                        // Center transform
                        gfx.TranslateTransform(w / 2, h / 2);
                        gfx.RotateTransform(rotationAngle);

                        XSize size = gfx.MeasureString(watermarkText, font);
                        
                        // Draw string centered
                        gfx.DrawString(watermarkText, font, brush, new XPoint(-size.Width / 2, size.Height / 4), XStringFormats.Default);
                    }
                }
                document.Save(outputFile);
            }
        }

        /// <summary>
        /// Adds an image watermark to all pages of a PDF.
        /// </summary>
        public static void AddImageWatermark(
            string sourceFile,
            string outputFile,
            string imagePath,
            double opacity,
            double rotationAngle,
            double scalePercent,
            string position) // "Center", "TopLeft", "TopRight", "BottomLeft", "BottomRight"
        {
            if (!File.Exists(sourceFile))
                throw new FileNotFoundException("Source file not found.", sourceFile);
            if (!File.Exists(imagePath))
                throw new FileNotFoundException("Watermark image not found.", imagePath);

            using (PdfDocument document = PdfReader.Open(sourceFile, PdfDocumentOpenMode.Modify))
            {
                OptimizeDocumentOptions(document);
                // Apply transparency to image first
                using (MemoryStream ms = ApplyOpacityToImage(imagePath, opacity))
                {
                    using (XImage watermarkImage = XImage.FromStream(ms))
                    {
                        foreach (PdfPage page in document.Pages)
                        {
                            using (XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append))
                            {
                                double pw = page.Width.Point;
                                double ph = page.Height.Point;

                                double scale = scalePercent / 100.0;
                                double imgW = watermarkImage.PointWidth * scale;
                                double imgH = watermarkImage.PointHeight * scale;

                                double x = 0;
                                double y = 0;

                                switch (position)
                                {
                                    case "Center":
                                        x = (pw - imgW) / 2;
                                        y = (ph - imgH) / 2;
                                        break;
                                    case "TopLeft":
                                        x = 20;
                                        y = 20;
                                        break;
                                    case "TopRight":
                                        x = pw - imgW - 20;
                                        y = 20;
                                        break;
                                    case "BottomLeft":
                                        x = 20;
                                        y = ph - imgH - 20;
                                        break;
                                    case "BottomRight":
                                        x = pw - imgW - 20;
                                        y = ph - imgH - 20;
                                        break;
                                    default:
                                        x = (pw - imgW) / 2;
                                        y = (ph - imgH) / 2;
                                        break;
                                }

                                 if (rotationAngle != 0)
                                 {
                                     XGraphicsState state = gfx.Save();
                                     gfx.TranslateTransform(x + imgW / 2, y + imgH / 2);
                                     gfx.RotateTransform(rotationAngle);
                                     gfx.DrawImage(watermarkImage, -imgW / 2, -imgH / 2, imgW, imgH);
                                     gfx.Restore(state);
                                 }
                                else
                                {
                                    gfx.DrawImage(watermarkImage, x, y, imgW, imgH);
                                }
                            }
                        }
                    }
                }
                document.Save(outputFile);
            }
        }

        /// <summary>
        /// Converts PDF pages to PNG or JPEG images.
        /// </summary>
        public static void ConvertPdfToImages(string sourceFile, string outputFolder, string format, int dpi = 150)
        {
            if (!File.Exists(sourceFile))
                throw new FileNotFoundException("Source file not found.", sourceFile);

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            using (var docLib = DocLib.Instance)
            {
                double scale = dpi / 72.0;
                using (var docReader = docLib.GetDocReader(sourceFile, new PageDimensions(scale)))
                {
                    int pageCount = docReader.GetPageCount();

                    for (int i = 0; i < pageCount; i++)
                    {
                        using (var pageReader = docReader.GetPageReader(i))
                        {
                            int width = pageReader.GetPageWidth();
                            int height = pageReader.GetPageHeight();
                            byte[] rawBytes = pageReader.GetImage(); // Returns BGRA bytes

                            int stride = width * 4;
                            var bitmap = BitmapSource.Create(
                                width,
                                height,
                                dpi,
                                dpi,
                                PixelFormats.Bgra32,
                                null,
                                rawBytes,
                                stride
                            );

                            string fileExtension = format.ToLower() == "png" ? "png" : "jpg";
                            string outputFilePath = Path.Combine(outputFolder, $"page_{i + 1}.{fileExtension}");

                            using (var stream = new FileStream(outputFilePath, FileMode.Create))
                            {
                                BitmapEncoder encoder;
                                if (format.ToLower() == "png")
                                {
                                    encoder = new PngBitmapEncoder();
                                }
                                else
                                {
                                    encoder = new JpegBitmapEncoder { QualityLevel = 90 };
                                }
                                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                                encoder.Save(stream);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Converts a list of images into a single PDF document.
        /// </summary>
        public static void ConvertImagesToPdf(List<string> imageFiles, string outputFile)
        {
            if (imageFiles == null || imageFiles.Count == 0)
                throw new ArgumentException("No image files specified.");

            using (PdfDocument document = new PdfDocument())
            {
                OptimizeDocumentOptions(document);
                foreach (string imgFile in imageFiles)
                {
                    if (!File.Exists(imgFile)) continue;

                    PdfPage page = document.AddPage();
                    using (XImage image = XImage.FromFile(imgFile))
                    {
                        page.Width = XUnit.FromPoint(image.PointWidth);
                        page.Height = XUnit.FromPoint(image.PointHeight);

                        using (XGraphics gfx = XGraphics.FromPdfPage(page))
                        {
                            gfx.DrawImage(image, 0, 0, page.Width.Point, page.Height.Point);
                        }
                    }
                }
                document.Save(outputFile);
            }
        }

        /// <summary>
        /// Applies transparency to an image and returns it as a stream.
        /// </summary>
        private static MemoryStream ApplyOpacityToImage(string imagePath, double opacity)
        {
            var ms = new MemoryStream();
            if (opacity >= 0.99)
            {
                using (var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                {
                    fs.CopyTo(ms);
                }
                ms.Position = 0;
                return ms;
            }

            var uri = new Uri(imagePath);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();

            var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);

            int width = converted.PixelWidth;
            int height = converted.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            converted.CopyPixels(pixels, stride, 0);

            for (int i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = (byte)(pixels[i] * opacity);
            }

            var newBmp = BitmapSource.Create(width, height, converted.DpiX, converted.DpiY, PixelFormats.Bgra32, null, pixels, stride);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(newBmp));
            encoder.Save(ms);
            ms.Position = 0;
            return ms;
        }

        /// <summary>
        /// Extracts specified pages from a PDF and saves them to a new file.
        /// </summary>
        public static void ExtractPdfPages(string sourceFile, string outputFile, List<int> pageIndicesToKeep)
        {
            if (!File.Exists(sourceFile))
                throw new FileNotFoundException("Source file not found.", sourceFile);

            using (PdfDocument inputDocument = PdfReader.Open(sourceFile, PdfDocumentOpenMode.Import))
            {
                using (PdfDocument outputDocument = new PdfDocument())
                {
                    OptimizeDocumentOptions(outputDocument);
                    foreach (int index in pageIndicesToKeep)
                    {
                        if (index >= 0 && index < inputDocument.PageCount)
                        {
                            outputDocument.AddPage(inputDocument.Pages[index]);
                        }
                    }

                    if (outputDocument.PageCount == 0)
                    {
                        throw new InvalidOperationException("No valid pages were selected for extraction.");
                    }

                    outputDocument.Save(outputFile);
                }
            }
        }

        /// <summary>
        /// Extracts specified pages from a PDF and saves each page as a separate file in the target directory.
        /// </summary>
        public static void ExtractPdfPagesIndividually(string sourceFile, string outputFolder, List<int> pageIndices)
        {
            if (!File.Exists(sourceFile))
                throw new FileNotFoundException("Source file not found.", sourceFile);

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            using (PdfDocument inputDocument = PdfReader.Open(sourceFile, PdfDocumentOpenMode.Import))
            {
                string baseName = Path.GetFileNameWithoutExtension(sourceFile);
                foreach (int index in pageIndices)
                {
                    if (index >= 0 && index < inputDocument.PageCount)
                    {
                        using (PdfDocument outputDocument = new PdfDocument())
                        {
                            OptimizeDocumentOptions(outputDocument);
                            outputDocument.AddPage(inputDocument.Pages[index]);
                            string outputFile = Path.Combine(outputFolder, $"{baseName}_sayfa_{index + 1}.pdf");
                            outputDocument.Save(outputFile);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Splits a PDF file into individual page files in the target directory.
        /// </summary>
        public static void SplitPdfToIndividualPages(string sourceFile, string outputFolder)
        {
            if (!File.Exists(sourceFile))
                throw new FileNotFoundException("Source file not found.", sourceFile);

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            using (PdfDocument inputDocument = PdfReader.Open(sourceFile, PdfDocumentOpenMode.Import))
            {
                string baseName = Path.GetFileNameWithoutExtension(sourceFile);
                for (int i = 0; i < inputDocument.PageCount; i++)
                {
                    using (PdfDocument outputDocument = new PdfDocument())
                    {
                        OptimizeDocumentOptions(outputDocument);
                        outputDocument.AddPage(inputDocument.Pages[i]);
                        string outputFile = Path.Combine(outputFolder, $"{baseName}_sayfa_{i + 1}.pdf");
                        outputDocument.Save(outputFile);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the page count of a PDF file using Docnet.Core.
        /// </summary>
        public static int GetPdfPageCount(string pdfPath)
        {
            if (!File.Exists(pdfPath)) return 0;
            try
            {
                using (var docLib = DocLib.Instance)
                using (var docReader = docLib.GetDocReader(pdfPath, new PageDimensions(1.0)))
                {
                    return docReader.GetPageCount();
                }
            }
            catch
            {
                return 0;
            }
        }

        private static void OptimizeDocumentOptions(PdfDocument document)
        {
            document.Options.CompressContentStreams = true;
            document.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Always;
            document.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
        }
    }
}
