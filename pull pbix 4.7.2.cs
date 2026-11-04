using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Data.SqlClient; // Wbudowane w .NET Framework

namespace ReportDownloader
{
    class Program
    {
        class ReportInfo
        {
            public int Index { get; set; }
            public string Path { get; set; }
            public string Name { get; set; }
            public byte[] Content { get; set; }
        }

        static void Main(string[] args)
        {
            // Konfiguracja połączenia z bazą ReportServer
            string connectionString = "Server=NAZWA_SERWERA;Database=ReportServer;Trusted_Connection=True;";
            
            // Folder docelowy w katalogu uruchomieniowym aplikacji (.exe)
            string outputBaseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PobraneRaporty");

            var reports = new List<ReportInfo>();

            // Zapytanie SQL sortujące po Path, a następnie po Name
            string query = @"
                SELECT Path, Name, Content 
                FROM dbo.Catalog 
                WHERE Type = 13 
                ORDER BY Path, Name";

            try
            {
                Console.WriteLine("Łączenie z bazą danych i pobieranie listy raportów...");
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    SqlCommand command = new SqlCommand(query, connection);
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        int index = 1;
                        while (reader.Read())
                        {
                            reports.Add(new ReportInfo
                            {
                                Index = index++,
                                Path = reader["Path"] != DBNull.Value ? reader["Path"].ToString() : string.Empty,
                                Name = reader["Name"] != DBNull.Value ? reader["Name"].ToString() : string.Empty,
                                Content = reader["Content"] != DBNull.Value ? (byte[])reader["Content"] : null
                            });
                        }
                    }
                }

                if (reports.Count == 0)
                {
                    Console.WriteLine("Nie znaleziono żadnych raportów Power BI (Type = 13) w bazie.");
                    Console.WriteLine("Naciśnij dowolny klawisz, aby zakończyć...");
                    Console.ReadKey();
                    return;
                }

                // Wyświetlenie listy raportów
                Console.WriteLine("\nZnalezione raporty na serwerze:");
                Console.WriteLine(new string('-', 90));
                foreach (var r in reports)
                {
                    Console.WriteLine(string.Format("[{0,3}] Ścieżka: {1} | Nazwa: {2}", r.Index, r.Path, r.Name));
                }
                Console.WriteLine(new string('-', 90));

                // Prompt dla użytkownika
                Console.Write("\nPodaj numery raportów do pobrania (np. 1,3,5-8) lub wpisz 'all' dla wszystkich: ");
                string input = Console.ReadLine();

                var selectedIndices = ParseSelection(input, reports.Count);

                if (selectedIndices.Count == 0)
                {
                    Console.WriteLine("Nie wybrano żadnych poprawnych raportów. Zakończono działanie.");
                    Console.WriteLine("Naciśnij dowolny klawisz, aby zakończyć...");
                    Console.ReadKey();
                    return;
                }

                // Tworzenie głównego folderu wynikowego
                Directory.CreateDirectory(outputBaseDir);
                Console.WriteLine(string.Format("\nRozpoczynanie pobierania do folderu: {0}\n", outputBaseDir));

                int successCount = 0;
                foreach (var idx in selectedIndices)
                {
                    var report = reports.First(r => r.Index == idx);
                    
                    if (report.Content == null)
                    {
                        Console.WriteLine(string.Format("[POMINIĘTO] [{0}] {1} - zawartość binarna jest pusta.", report.Index, report.Name));
                        continue;
                    }

                    // Wyciąganie struktury folderów z właściwości Path
                    string reportPath = report.Path;
                    string folderPart = "";
                    int lastSlash = reportPath.LastIndexOf('/');
                    if (lastSlash > 0)
                    {
                        folderPart = reportPath.Substring(0, lastSlash);
                    }

                    // Przygotowanie lokalnej ścieżki katalogu
                    string relativeDir = folderPart.TrimStart('/');
                    string targetDir = Path.Combine(outputBaseDir, relativeDir.Replace('/', Path.DirectorySeparatorChar));
                    
                    Directory.CreateDirectory(targetDir);

                    // Zapis pliku .pbix
                    string filePath = Path.Combine(targetDir, report.Name + ".pbix");
                    File.WriteAllBytes(filePath, report.Content);
                    
                    Console.WriteLine(string.Format("[POBRANO] [{0}] {1} -> {2}", report.Index, report.Path, targetDir));
                    successCount++;
                }

                Console.WriteLine(string.Format("\nZakończono! Pomyślnie pobrano {0} raportów.", successCount));
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("\nWystąpił krytyczny błąd: {0}", ex.Message));
            }

            Console.WriteLine("\nNaciśnij dowolny klawisz, aby wyjść...");
            Console.ReadKey();
        }

        // Metoda pomocnicza do parsowania wpisów użytkownika
        static HashSet<int> ParseSelection(string input, int maxIndex)
        {
            var result = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(input)) return result;

            if (input.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 1; i <= maxIndex; i++) result.Add(i);
                return result;
            }

            string[] parts = input.Split(',');
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.Contains('-'))
                {
                    string[] rangeParts = trimmed.Split('-');
                    int start, end;
                    if (rangeParts.Length == 2 && 
                        int.TryParse(rangeParts[0], out start) && 
                        int.TryParse(rangeParts[1], out end))
                    {
                        if (start > end) 
                        { 
                            int temp = start; 
                            start = end; 
                            end = temp; 
                        } // Zamiana miejscami jeśli zakres podano wspak
                        
                        for (int i = start; i <= end; i++)
                        {
                            if (i >= 1 && i <= maxIndex) result.Add(i);
                        }
                    }
                }
                else
                {
                    int num;
                    if (int.TryParse(trimmed, out num))
                    {
                        if (num >= 1 && num <= maxIndex) result.Add(num);
                    }
                }
            }
            return result;
        }
    }
}