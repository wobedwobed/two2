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
            // ZMIEŃ PONIŻSZĄ ŚCIEŻKĘ NA WŁAŚCIWY KATALOG
            string directoryPath = @"C:\Sciezka\Do\Twojego\Katalogu";

            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine("Podany katalog nie istnieje!");
                Console.ReadLine();
                return;
            }

            // 1. Wyszukaj wszystkie pliki z rozszerzeniem .dtsx
            string[] allDtsxFiles = Directory.GetFiles(directoryPath, "*.dtsx");

            // 2. Odfiltruj te, które mają na końcu nazwy "__" oraz odrzuć plik "example.dtsx"
            List<string> files = allDtsxFiles
                .Where(f => !Path.GetFileNameWithoutExtension(f).EndsWith("__") 
                         && !Path.GetFileName(f).Equals("example.dtsx", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Console.WriteLine($"Znaleziono plików do przetworzenia: {files.Count}");

            // 3. Utwórz strukturę filesAndVarValues (Słownik: Klucz = ścieżka pliku, Wartość = wartość zmiennej)
            Dictionary<string, string> filesAndVarValues = new Dictionary<string, string>();

            // Wzorzec Regex do znalezienia wartości zmiennej pkg_TargetTable.
            string pattern = @"(?s)(<DTS:Variable[^>]*DTS:ObjectName=""pkg_TargetTable""[^>]*>.*?<DTS:VariableValue[^>]*>)(.*?)(</DTS:VariableValue>)";

            foreach (string file in files)
            {
                string content = File.ReadAllText(file);
                Match match = Regex.Match(content, pattern);

                if (match.Success)
                {
                    string varValue = match.Groups[2].Value; // Pobranie wartości
                    filesAndVarValues.Add(file, varValue);
                    Console.WriteLine($"Plik: {Path.GetFileName(file)} | pkg_TargetTable = {varValue}");
                }
                else
                {
                    Console.WriteLine($"Plik: {Path.GetFileName(file)} | NIE ZNALEZIONO ZMIENNEJ pkg_TargetTable");
                }
            }

            // 4. Znajdź plik example.dtsx w podanym katalogu
            string exampleFilePath = Path.Combine(directoryPath, "example.dtsx");

            if (!File.Exists(exampleFilePath))
            {
                Console.WriteLine("\nBrak pliku example.dtsx w podanym katalogu. Kończenie pracy.");
                Console.ReadLine();
                return;
            }

            Console.WriteLine("\nRozpoczynam generowanie nowych plików na podstawie example.dtsx...");
            string exampleContent = File.ReadAllText(exampleFilePath);

            // 5. Pętla dla każdego rekordu z tablicy filesAndVarValues
            foreach (var record in filesAndVarValues)
            {
                string originalFilePath = record.Key;
                string replacementValue = record.Value;

                // Zastąpienie wartości w pliku example.dtsx
                string newContent = Regex.Replace(exampleContent, pattern, "${1}" + replacementValue + "${3}");

                // Zapis pliku ze zmienioną nazwą (dodanie sufixu "__")
                string originalFileNameWithoutExt = Path.GetFileNameWithoutExtension(originalFilePath);
                string newFileName = $"{originalFileNameWithoutExt}__.dtsx";
                string newFilePath = Path.Combine(directoryPath, newFileName);

                File.WriteAllText(newFilePath, newContent);
                Console.WriteLine($"Utworzono: {newFileName}");
            }

            Console.WriteLine("\nOperacja zakończona sukcesem. Naciśnij Enter, aby wyjść.");
            Console.ReadLine();
        }
    }
}
