using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DtsxModifier
{
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

            Console.WriteLine("\nRozpoczynam modyfikację i zapisywanie plików...");

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
                // Usunięto ^\s* aby pominąć ewentualną nagłówkową linię <?xml ...?>
                Match rootTagMatch = Regex.Match(newContent, @"(?s)<DTS:Executable\b[^>]*>");
                if (rootTagMatch.Success)
                {
                    string oldRootTag = rootTagMatch.Value;
                    string newPackageName = Path.GetFileNameWithoutExtension(newFileName); 

                    // Zamiana wartości atrybutu DTS:ObjectName wewnątrz głównego tagu
                    string newRootTag = Regex.Replace(oldRootTag, @"\bDTS:ObjectName=""[^""]*""", $"DTS:ObjectName=\"{newPackageName}\"");

                    // Podmiana zmodyfikowanego tagu korzenia w treści dokumentu
                    newContent = newContent.Remove(rootTagMatch.Index, rootTagMatch.Length).Insert(rootTagMatch.Index, newRootTag);
                }

                // 3) Zapisz plik
                File.WriteAllText(newFilePath, newContent);
                Console.WriteLine($"Utworzono: {newFileName} | Nazwa pakietu: {Path.GetFileNameWithoutExtension(newFileName)}");
            }

            Console.WriteLine("\nZakończono pomyślnie. Naciśnij Enter, aby zamknąć.");
            Console.ReadLine();
        }
    }
}
