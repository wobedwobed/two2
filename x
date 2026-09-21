using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace SSISUpdateApp
{
    class Program
    {
        static void Main(string[] args)
        {
            // Podaj ścieżkę do folderu z projektem SSIS
            string folderPath = @"C:\TwojFolderZProjektemSSIS";
            
            if (!Directory.Exists(folderPath))
            {
                Console.WriteLine("Podany folder nie istnieje!");
                return;
            }

            // KROK 1 i 2: Definicja sufixu i prefixu oraz ich długości
            string sufix = "_v2"; // Zmień na swój właściwy sufix
            int sufixDlugosc = sufix.Length;

            string prefix = "PKG_"; // Zmień na swój właściwy prefix
            int prefixDlugosc = prefix.Length;

            // KROK 3: Znalezienie wszystkich plików .dtsx w folderze
            var wszystkieDtsxFiles = Directory.GetFiles(folderPath, "*.dtsx");
            List<string> wszystkieDtsx = wszystkieDtsxFiles
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .ToList();

            // KROK 4: Odczytanie pliku .dtproj i znalezienie użytych w projekcie paczek
            string dtprojFile = Directory.GetFiles(folderPath, "*.dtproj").FirstOrDefault();
            if (string.IsNullOrEmpty(dtprojFile))
            {
                Console.WriteLine("Nie znaleziono pliku .dtproj w podanym folderze.");
                return;
            }

            XDocument dtprojXml = XDocument.Load(dtprojFile);
            
            // Pobranie węzłów <SSIS:Package> lub <Package> z <SSIS:Packages>
            List<string> wProjekcieDtsx = dtprojXml.Descendants()
                .Where(e => e.Name.LocalName == "Package")
                .Select(e => e.Attributes().FirstOrDefault(a => a.Name.LocalName == "Name")?.Value)
                .Where(val => val != null && val.EndsWith(".dtsx", StringComparison.OrdinalIgnoreCase))
                .Select(val => val.Substring(0, val.Length - 5)) // usunięcie ".dtsx"
                .ToList();

            // KROK 5: Filtrowanie 'wszystkieDtsx' za pomocą 'wProjekcieDtsx'
            List<string> wProjekciePrzefiltrowaneDtsx = wszystkieDtsx
                .Intersect(wProjekcieDtsx)
                .ToList();

            // KROK 6: Wyodrębnienie plików "Starych" (bez sufixu na końcu, z prefixem na początku)
            List<string> wProjekciePrzefiltrowaneDtsxStare = wProjekciePrzefiltrowaneDtsx
                .Where(nazwa => nazwa.StartsWith(prefix) && 
                                !nazwa.EndsWith(sufix) && 
                                nazwa.Length >= prefixDlugosc)
                .ToList();

            // KROK 7: Wyodrębnienie plików "Nowych" (z sufixem na końcu, z prefixem na początku)
            List<string> wProjekciePrzefiltrowaneDtsxNowe = wProjekciePrzefiltrowaneDtsx
                .Where(nazwa => nazwa.StartsWith(prefix) && 
                                nazwa.EndsWith(sufix) && 
                                nazwa.Length >= (prefixDlugosc + sufixDlugosc))
                .ToList();

            // KROK 8 i 9: Dopasowanie par i usunięcie elementów bez odpowiedników
            // Interpretacja logiczna: element Stary + sufix == element Nowy
            Dictionary<string, string> paryStareNowe = new Dictionary<string, string>();

            foreach (var stare in wProjekciePrzefiltrowaneDtsxStare)
            {
                string poszukiwaneNowe = stare + sufix;
                if (wProjekciePrzefiltrowaneDtsxNowe.Contains(poszukiwaneNowe))
                {
                    paryStareNowe.Add(stare, poszukiwaneNowe);
                }
            }

            // KROK 10: Przetwarzanie plików .dtsx w ramach dopasowanych par
            foreach (var para in paryStareNowe)
            {
                string stareFilePath = Path.Combine(folderPath, para.Key + ".dtsx");
                string noweFilePath = Path.Combine(folderPath, para.Value + ".dtsx");

                // Odczytanie plików
                XDocument docStare = XDocument.Load(stareFilePath);
                XDocument docNowe = XDocument.Load(noweFilePath);

                // Szukanie głównego węzła Executable (zazwyczaj <DTS:Executable>)
                XElement rootStare = docStare.Elements().FirstOrDefault(e => e.Name.LocalName == "Executable");
                XElement rootNowe = docNowe.Elements().FirstOrDefault(e => e.Name.LocalName == "Executable");

                if (rootStare != null && rootNowe != null)
                {
                    // W pliku STARYM usuwamy wszystko oprócz PackageParameters 
                    // (Atrybuty w XElement i tak pozostaną nienaruszone, usuwamy tylko węzły potomne)
                    var wezlyDoUsunieciaStare = rootStare.Elements()
                        .Where(e => e.Name.LocalName != "PackageParameters")
                        .ToList();

                    foreach (var wezel in wezlyDoUsunieciaStare)
                    {
                        wezel.Remove();
                    }

                    // W pliku NOWYM bierzemy wszystkie węzły oprócz PackageParameters
                    var wezlyDoSkopiowaniaNowe = rootNowe.Elements()
                        .Where(e => e.Name.LocalName != "PackageParameters")
                        .ToList();

                    // Wklejamy je do STAREGO pliku
                    foreach (var wezel in wezlyDoSkopiowaniaNowe)
                    {
                        rootStare.Add(new XElement(wezel)); // deep copy (kopiowanie zawartości)
                    }

                    // Zapisujemy nadpisując stary plik
                    docStare.Save(stareFilePath);
                    Console.WriteLine($"Zaktualizowano plik: {para.Key}.dtsx");
                }
            }

            // KROK 11: Modyfikacja pliku .dtproj
            // Usuwamy wpisy o plikach, których nazwa kończy się sufixem 
            string pelnySufixDtsx = sufix + ".dtsx";

            var dtprojWezlyDoUsuniecia = dtprojXml.Descendants()
                .Where(e => e.Name.LocalName == "Package" || e.Name.LocalName == "PackageMetaData")
                .Where(e => 
                {
                    var nameAttr = e.Attributes().FirstOrDefault(a => a.Name.LocalName == "Name");
                    if (nameAttr != null && nameAttr.Value.EndsWith(pelnySufixDtsx, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    return false;
                })
                .ToList();

            foreach (var wezel in dtprojWezlyDoUsuniecia)
            {
                wezel.Remove();
            }

            // Zapisanie zmian w pliku .dtproj
            dtprojXml.Save(dtprojFile);
            Console.WriteLine($"\nZaktualizowano projekt: {Path.GetFileName(dtprojFile)}");
            Console.WriteLine("Proces zakończony powodzeniem.");
            Console.ReadLine();
        }
    }
}
