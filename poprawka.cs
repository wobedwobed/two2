using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DtsxModifier
{
    class DtsxResultItem
    {
        public string NewFileName { get; set; }
        public string SqlTableName { get; set; }
        public string CleanedFileName { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            string directoryPath = @"C:\Sciezka\Do\Twojego\Katalogu";

            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine("Podany katalog nie istnieje!");
                Console.ReadLine();
                return;
            }

            // 1. Wyszukaj pliki .dtsx
            string[] files = Directory.GetFiles(directoryPath, "*.dtsx")
                .Where(f => !Path.GetFileNameWithoutExtension(f).EndsWith("__") 
                         && !Path.GetFileName(f).Equals("example.dtsx", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            Console.WriteLine($"Znaleziono plików do przetworzenia: {files.Length}");

            // 2. Pobierz wartość zmiennej SQLTableName
            Dictionary<string, string> filesAndVarValues = new Dictionary<string, string>();
            
            // Ultra-elastyczny regex dla XML (odporny na spacje i nowe linie)
            string readPattern = @"(?s)<DTS:Variable\b[^>]*?\bDTS:ObjectName\s*=\s*""SQLTableName""[^>]*?>\s*<DTS:VariableValue[^>]*?>(.*?)</DTS:VariableValue>";

            foreach (string file in files)
            {
                string content = File.ReadAllText(file);
                Match match = Regex.Match(content, readPattern, RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    string varValue = match.Groups[1].Value.Trim();
                    filesAndVarValues.Add(file, varValue);
                    Console.WriteLine($"Plik: {Path.GetFileName(file)} | SQLTableName = {varValue}");
                }
                else
                {
                    Console.WriteLine($"Plik: {Path.GetFileName(file)} | NIE ZNALEZIONO ZMIENNEJ SQLTableName");
                }
            }

            // 3. Sprawdź example.dtsx
            string exampleFilePath = Path.Combine(directoryPath, "example.dtsx");
            if (!File.Exists(exampleFilePath))
            {
                Console.WriteLine("\nBrak pliku example.dtsx w podanym katalogu.");
                Console.ReadLine();
                return;
            }

            string exampleContent = File.ReadAllText(exampleFilePath);
            
            // Pattern do podmiany wartości pkg_TargetTable
            string replaceVarPattern = @"(?s)(<DTS:Variable\b[^>]*?\bDTS:ObjectName\s*=\s*""pkg_TargetTable""[^>]*?>\s*<DTS:VariableValue[^>]*?>)(.*?)(</DTS:VariableValue>)";

            List<DtsxResultItem> resultsList = new List<DtsxResultItem>();

            Console.WriteLine("\nRozpoczynam przetwarzanie...");

            // 4. Modyfikacja plików
            foreach (var record in filesAndVarValues)
            {
                string originalFilePath = record.Key;
                string newTableValue = record.Value;

                string originalFileNameWithoutExt = Path.GetFileNameWithoutExtension(originalFilePath);
                string newFileName = $"{originalFileNameWithoutExt}__.dtsx";
                string newFilePath = Path.Combine(directoryPath, newFileName);

                // Podmiana pkg_TargetTable
                string newContent = Regex.Replace(exampleContent, replaceVarPattern, "${1}" + newTableValue + "${3}", RegexOptions.IgnoreCase);

                // Podmiana DTS:ObjectName w głównym tagu pakietu
                string newPackageName = Path.GetFileNameWithoutExtension(newFileName);
                string packageTitlePattern = @"(?s)(<DTS:Executable\b[^>]*?\bDTS:ObjectName\s*=\s*")([^"]*)(")";
                newContent = Regex.Replace(newContent, packageTitlePattern, "${1}" + newPackageName + "${3}", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

                // Zapis pliku .dtsx
                File.WriteAllText(newFilePath, newContent);

                // Oczyszczenie nazwy pliku
                string cleanedName = ExtractCleanedName(newFileName);

                resultsList.Add(new DtsxResultItem
                {
                    NewFileName = newFileName,
                    SqlTableName = newTableValue,
                    CleanedFileName = cleanedName
                });

                Console.WriteLine($"Utworzono: {newFileName} | Oczyszczona nazwa: {cleanedName}");
            }

            // 5. Zapis do JSON
            string jsonFilePath = Path.Combine(directoryPath, "wyniki.json");
            SaveAsJson(resultsList, jsonFilePath);

            Console.WriteLine($"\nZapisano podsumowanie JSON: {jsonFilePath}");
            Console.WriteLine("Gotowe. Naciśnij Enter.");
            Console.ReadLine();
        }

        private static string ExtractCleanedName(string fileName)
        {
            // Odporny regex: dopasowuje SEQC DDS_GIEK (ze spacjami lub podłogami) aż do __
            Match match = Regex.Match(fileName, @"SEQC[\s_]+DDS_GIEK[\s_]+(.*?)__", RegexOptions.IgnoreCase);
            
            if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
            {
                return match.Groups[1].Value.Trim();
            }

            // Fallback: jeśli wzorzec nie pasuje, obcina rozszerzenie i końcowe __
            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            if (nameWithoutExt.EndsWith("__"))
            {
                return nameWithoutExt.Substring(0, nameWithoutExt.Length - 2);
            }
            return nameWithoutExt;
        }

        private static void SaveAsJson(List<DtsxResultItem> items, string filePath)
        {
            var jsonEntries = items.Select(item => string.Format(
                "  {{\n    \"NazwaPliku\": \"{0}\",\n    \"SQLTableName\": \"{1}\",\n    \"OczyszczonaNazwa\": \"{2}\"\n  }}",
                EscapeJson(item.NewFileName),
                EscapeJson(item.SqlTableName),
                EscapeJson(item.CleanedFileName)
            ));

            string jsonContent = "[\n" + string.Join(",\n", jsonEntries) + "\n]";
            File.WriteAllText(filePath, jsonContent);
        }

        private static string EscapeJson(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}
