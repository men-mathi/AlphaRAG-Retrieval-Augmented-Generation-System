using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using UglyToad.PdfPig;
using ExcelDataReader;

namespace RagService.Infrastructure.Services
{
    public static class DocumentParser
    {
        static DocumentParser()
        {
            // Register encoding provider required by ExcelDataReader
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        public static async Task<string> ExtractTextAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return string.Empty;

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            try
            {
                switch (ext)
                {
                    case ".md":
                    case ".txt":
                        return await File.ReadAllTextAsync(filePath);

                    case ".pdf":
                        return ExtractFromPdf(filePath);

                    case ".docx":
                        return ExtractFromDocx(filePath);

                    case ".pptx":
                        return ExtractFromPptx(filePath);

                    case ".xlsx":
                    case ".csv":
                        return ExtractFromExcelOrCsv(filePath);

                    case ".html":
                    case ".htm":
                        return await ExtractFromHtmlAsync(filePath);

                    default:
                        // Unsupported files
                        Console.WriteLine($"[Warning] Unsupported file extension {ext} for {Path.GetFileName(filePath)}. Skipping content.");
                        return string.Empty;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to extract text from {Path.GetFileName(filePath)}: {ex.Message}");
                return string.Empty;
            }
        }

        // 1. PDF Parser (using PdfPig)
        private static string ExtractFromPdf(string path)
        {
            using (var pdf = PdfDocument.Open(path))
            {
                var sb = new StringBuilder();
                foreach (var page in pdf.GetPages())
                {
                    var text = page.Text;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text);
                    }
                }
                return sb.ToString();
            }
        }

        // 2. Word Parser (using Decompression and OpenXML XML parsing)
        private static string ExtractFromDocx(string path)
        {
            using (var archive = ZipFile.OpenRead(path))
            {
                var entry = archive.GetEntry("word/document.xml");
                if (entry == null) 
                    return string.Empty;

                using (var stream = entry.Open())
                {
                    var doc = XDocument.Load(stream);
                    XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                    
                    // Extract text from text elements (w:t)
                    var texts = doc.Descendants(w + "t").Select(t => t.Value);
                    return string.Join(" ", texts);
                }
            }
        }

        // 3. PowerPoint Parser (using Decompression and DrawingML XML parsing)
        private static string ExtractFromPptx(string path)
        {
            using (var archive = ZipFile.OpenRead(path))
            {
                var sb = new StringBuilder();
                
                // Get all slides in order (slide1.xml, slide2.xml...)
                var slideEntries = archive.Entries
                    .Where(e => e.FullName.StartsWith("ppt/slides/slide") && e.FullName.EndsWith(".xml"))
                    .OrderBy(e => e.FullName);

                XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";

                foreach (var entry in slideEntries)
                {
                    using (var stream = entry.Open())
                    {
                        var doc = XDocument.Load(stream);
                        // Extract text from text elements (a:t)
                        var texts = doc.Descendants(a + "t").Select(t => t.Value);
                        var slideText = string.Join(" ", texts);
                        
                        if (!string.IsNullOrWhiteSpace(slideText))
                        {
                            sb.AppendLine(slideText);
                        }
                    }
                }
                return sb.ToString();
            }
        }

        // 4. Excel & CSV Parser (using ExcelDataReader)
        private static string ExtractFromExcelOrCsv(string path)
        {
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                using (var reader = ext == ".csv" ? ExcelReaderFactory.CreateCsvReader(stream) : ExcelReaderFactory.CreateReader(stream))
                {
                    var sb = new StringBuilder();
                    do
                    {
                        while (reader.Read())
                        {
                            var rowValues = new List<string>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var val = reader.GetValue(i)?.ToString()?.Trim();
                                if (!string.IsNullOrEmpty(val))
                                {
                                    rowValues.Add(val);
                                }
                            }
                            if (rowValues.Count > 0)
                            {
                                sb.AppendLine(string.Join(" | ", rowValues));
                            }
                        }
                    } while (reader.NextResult()); // Moves to next sheet if any

                    return sb.ToString();
                }
            }
        }

        // 5. HTML Parser (using Regex to strip script/style and markup tags)
        private static async Task<string> ExtractFromHtmlAsync(string path)
        {
            var html = await File.ReadAllTextAsync(path);
            
            // Strip scripts and styles
            html = Regex.Replace(html, @"<(script|style)\b[^>]*>([\s\S]*?)</\1>", "", RegexOptions.IgnoreCase);
            
            // Strip all HTML tag elements
            var plainText = Regex.Replace(html, @"<[^>]*>", " ");
            
            // Decode HTML special entities (e.g. &nbsp; to space, &amp; to &)
            return System.Net.WebUtility.HtmlDecode(plainText);
        }
    }
}
