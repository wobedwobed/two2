using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DtsxModifier
{
    // Klasa reprezentująca pojedynczy wpis w tabeli JSON
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
            // Podaj właściwą ścieżkę do katalogu z plikami SSIS
            string directoryPath = @"C:\Sciezka\Do\Twojego\Katalogu";

            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine("Podany katalog nie istnieje!");
                Console.ReadLine();
                return;
            }

            // 1. Wyszukaj pliki .dtsx, pomijając kończące się na "__" oraz "example.dtsx"
            string[] files = Directory.GetFiles(directoryPath, "*.dtsx")
                .Where(f => !Path.GetFileNameWithoutExtension(f).EndsWith("__") 
                         && !Path.GetFileName(f).Equals("example.dtsx", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            Console.WriteLine($"Znaleziono plików do przetworzenia: {files.Length}");

            // 2. Pobierz wartość zmiennej "SQLTableName" z każdego pliku
            Dictionary<string, string> filesAndVarValues = new Dictionary<string, string>();
            
            string readPattern = @"(?s)(<DTS:Variable[^>]*DTS:ObjectName=""SQLTableName""[^>]*>.*?<DTS:VariableValue[^>]*>)(.*?)(</DTS:VariableValue>)";

            foreach (string file in files)
            {
                string content = File.ReadAllText(file);
                Match match = Regex.Match(content, readPattern);

                if (match.Success)
                {
                    string varValue = match.Groups[2].Value;
                    filesAndVarValues.Add(file, varValue);
                    Console.WriteLine($"Plik: {Path.GetFileName(file)} | SQLTableName = {varValue}");
                }
                else
                {
                    Console.WriteLine($"Plik: {Path.GetFileName(file)} | NIE ZNALEZIONO ZMIENNEJ SQLTableName");
                }
            }

            // 3. Sprawdź obecność pliku example.dtsx
            string exampleFilePath = Path.Combine(directoryPath, "example.dtsx");
            if (!File.Exists(exampleFilePath))
            {
                Console.WriteLine("\nBrak pliku example.dtsx w podanym katalogu.");
                Console.ReadLine();
                return;
            }

            string exampleContent = File.ReadAllText(exampleFilePath);
            string replaceVarPattern = @"(?s)(<DTS:Variable[^>]*DTS:ObjectName=""pkg_TargetTable""[^>]*>.*?<DTS:VariableValue[^>]*>)(.*?)(</DTS:VariableValue>)";

            List<DtsxResultItem> resultsList = new List<DtsxResultItem>();

            Console.WriteLine("\nRozpoczynam modyfikację, zapisywanie plików i przygotowanie danych JSON...");

            // 4. Pętla przetwarzająca każdy rekord z filesAndVarValues
            foreach (var record in filesAndVarValues)
            {
                string originalFilePath = record.Key;
                string newTableValue = record.Value;

                string originalFileNameWithoutExt = Path.GetFileNameWithoutExtension(originalFilePath);
                string newFileName = $"{originalFileNameWithoutExt}__.dtsx";
                string newFilePath = Path.Combine(directoryPath, newFileName);

                // 1) Podmień wartość zmiennej pkg_TargetTable
                string newContent = Regex.Replace(exampleContent, replaceVarPattern, "${1}" + newTableValue + "${3}");

                // 2) Podmień nazwę pakietu SSIS (DTS:ObjectName w PIERWSZYM tagu <DTS:Executable>)
                Match rootTagMatch = Regex.Match(newContent, @"(?s)<DTS:Executable\b[^>]*>");
                if (rootTagMatch.Success)
                {
                    string oldRootTag = rootTagMatch.Value;
                    string newPackageName = Path.GetFileNameWithoutExtension(newFileName); 

                    string newRootTag = Regex.Replace(oldRootTag, @"\bDTS:ObjectName=""[^""]*""", $"DTS:ObjectName=\"{newPackageName}\"");
                    newContent = newContent.Remove(rootTagMatch.Index, rootTagMatch.Length).Insert(rootTagMatch.Index, newRootTag);
                }

                // 3) Zapisz plik dtsx
                File.WriteAllText(newFilePath, newContent);
                Console.WriteLine($"Utworzono: {newFileName}");

                // 4) Oczyszczenie nazwy pliku (wyodrębnienie tekstu między "SEQC DDS_GIEK " a "__")
                string cleanedName = ExtractCleanedName(newFileName);

                // 5) Dodaj dane do listy wyników JSON
                resultsList.Add(new DtsxResultItem
                {
                    NewFileName = newFileName,
                    SqlTableName = newTableValue,
                    CleanedFileName = cleanedName
                });
            }

            // 5. Zapisanie danych do pliku JSON
            string jsonFilePath = Path.Combine(directoryPath, "wyniki.json");
            SaveAsJson(resultsList, jsonFilePath);

            Console.WriteLine($"\nZapisano podsumowanie w pliku JSON: {jsonFilePath}");
            Console.WriteLine("Zakończono pomyślnie. Naciśnij Enter, aby zamknąć.");
            Console.ReadLine();
        }

        // Metoda wyodrębniająca tekst pomiędzy "SEQC DDS_GIEK " a "__"
        private static string ExtractCleanedName(string fileName)
        {
            Match match = Regex.Match(fileName, @"SEQC DDS_GIEK (.*?)__");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            // Obsługa awaryjna: jeśli przedrostek "SEQC DDS_GIEK " nie wystąpi w nazwie pliku,
            // zwraca nazwę bez rozszerzenia i bez końcowego "__"
            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            return nameWithoutExt.EndsWith("__") 
                ? nameWithoutExt.Substring(0, nameWithoutExt.Length - 2) 
                : nameWithoutExt;
        }

        // Metoda budująca i zapisująca strukturyzowany plik JSON
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

        // Pomocnicza metoda escapująca znaki specjalne dla JSON
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
