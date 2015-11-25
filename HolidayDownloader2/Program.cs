using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Net;
using System.Text.RegularExpressions;

namespace HolidayDownloader2
{
    class Program
    {
        static void Main()
        {
            HDDownloader hdd = new HDDownloader();
            hdd.Download();
        }
    }

    internal class LocaleInfo
    {
        public readonly string locale;
        public readonly string entry, directory;

        public LocaleInfo(string locale, string entry, string directory)
        {
            this.locale = locale;
            this.entry = entry;
            this.directory = directory;
        }
    }

    internal class HDDownloader
    {
        //TODO: SETTINGS!
        /*
         * Jazyky - "de", "fr", "it", "en " 
         */
        private Dictionary<string, LocaleInfo> locales;

        /*
         * Seznam polozek ke stazeni pro dany jazyk
         */
        private Dictionary<string, string[]> downloadItems;
        private Dictionary<string, string[]> downloadLocales;

        /*
         * Roky
         */
        private int minYear = 2010;
        private int maxYear = 2020;

        /*
         * Pro kazde lokale bude maping id a seznam nazvu
         */
        private Dictionary<string, string[]> geoid2countryMapping = new Dictionary<string, string[]>();

        /*
         * Trida svatku
         */
        private enum HolidayClasses { LEGALLY = 3, LEGGALYandNOTRECOGNIZED = 4, HOLIDAYSandEVENTS = 5 };

        /*
         * Korenova stranka, ze ktere se budou zjistovat geo kody zemi, popr stranky pro geo kody zemi
         */
        private string rootPage = @"http://" + "www.feiertagskalender.ch/index.php?hl={0}";

        /*
         * 
         */
        private string recordPage = @"http://" + "www.feiertagskalender.ch/index.php?geo={0}&jahr={1}&klasse={2}&hl={3}";

        /*
         * Vystupni adressar
         */
        private string outputDirectory = "Holidays";

        public HDDownloader()
        {
            locales = new Dictionary<string, LocaleInfo>();
            locales.Add("de", new LocaleInfo("de", "Eintrag", "Verzeichnis"));
            locales.Add("fr", new LocaleInfo("fr", "Entr&eacute;e", "R&eacute;pertoire"));
            locales.Add("it", new LocaleInfo("it", "Registrazione", "Directory"));
            locales.Add("en", new LocaleInfo("en", "entry", "directory"));

            downloadItems = new Dictionary<string, string[]>();
            downloadLocales = new Dictionary<string, string[]>();

            downloadItems.Add("en", new string[] {"Australia", "Ireland", "Malta", "United Kingdom", "United States", "Czech Republic", "Slovakia" });
            downloadLocales.Add("en", new string[] {"en_AU", "en_IE", "en_MT", "en_GB", "en_US", "cs_CZ", "sk_SK" });

            downloadItems.Add("it", new string[] { "Italia", "Svizzera" });
            downloadLocales.Add("it", new string[] { "it_IT", "it_CH"});

            downloadItems.Add("fr", new string[] { "France", "Belgique", "Luxembourg", "Suisse" });
            downloadLocales.Add("fr", new string[] { "fr_FR", "fr_BE", "fr_LU", "fr_CH"});

            downloadItems.Add("de", new string[] { "Deutschland", "Oesterreich", "Luxembourg", "Schweiz" });
            downloadLocales.Add("de", new string[] { "de_DE", "de_AT", "de_LU", "de_CH"});
        }

        private void PrepareOutputDirectory()
        {
            //TODO: mozna odstranit..
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }
        }

        public void Download()
        {
            PrepareOutputDirectory();

            Thread[] threads = new Thread[locales.Values.Count];

            for (int i = 0, e = locales.Values.Count; i < e; i++)
            {
                LocaleInfo locale = locales.Values.ToArray()[i];

                ParameterizedThreadStart pts = new ParameterizedThreadStart(Run);
                Thread t = threads[i] = new Thread(pts);
                t.Name = locale + "Thread";
                t.IsBackground = true;
                t.Start(locale);
            }

            foreach (Thread t in threads)
            {
                t.Join();
            }
        }

        private void Run(object locale)
        {
            /*
             * Test validity dat
             */
            LocaleInfo data = locale as LocaleInfo;
            if (data == null)
            {
                return;
            }

            /**
             * Zpracovani dat
             */

            //Stazeni vychozi stranky pro dane lokale
            WebClient wc = new WebClient();
            string page = wc.DownloadString(string.Format(rootPage, data.locale));

            //Rozdeleni na radky
            string[] lines = page.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            //Tvorba regexu
            Regex re = new Regex("[?]geo=(?<geo>[0-9]+)[&]amp;.*\"[>](?<country>.*)[<][/]a[>][<]br [/][>]$", RegexOptions.Compiled);

            //Pruchod radky a odstarnovani nepotrebnych
            foreach (string line in lines)
            {
                //Zaznam
                bool directory = false;
                if (line.Contains(data.entry) || (directory = line.Contains(data.directory)))
                {
                    //Melo by se diky contains v ifu vzdy naplnit
                    Match m = re.Match(line);
                    string country = m.Groups["country"].Value;
                    string geo = m.Groups["geo"].Value;

                    //Vyber jen danych statu k danemu locale
                    if (downloadItems[data.locale].Contains(country))
                    {
                        //Ziskani cisla
                        int position = 0;
                        foreach (string item in downloadItems[data.locale])
                        {
                            if (item == country)
                            {
                                break;
                            }

                            position++;
                        }

                        //Vypis
                        Console.WriteLine(downloadLocales[data.locale][position]);

                        //Zaznam
                        string path = Path.Combine(outputDirectory, downloadLocales[data.locale][position]);
                        using (FileStream fs = File.Create(path))
                        {
                            using (StreamWriter sw = new StreamWriter(fs, Encoding.UTF8))
                            {
                                //Pro vsechny roky
                                for (int jahr = minYear; jahr <= maxYear; jahr++)
                                {
                                    //Stazeni stranky s vlastnimi zaznamy
                                    page = wc.DownloadString(string.Format(recordPage, geo, jahr, (int)HolidayClasses.LEGALLY, data.locale));
                                    lines = page.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                                    //Regex pro odchyceni datumu
                                    Regex datum = new Regex("<td class=\"datum\">(?<datum>.+)</td>", RegexOptions.Compiled);
                                    //Regex day = new Regex("<td>(?<day>.{2})</td>", RegexOptions.Compiled);
                                    //Regex week = new Regex("CW (?<week>[0-9]{2})</td>", RegexOptions.Compiled);
                                    Regex title = new Regex("\">(?<title>.+)</a>(</i>)?</td>", RegexOptions.Compiled);
                                    //Regex type = new Regex("\">(?<type>.{1})</abbr></td></tr>", RegexOptions.Compiled);

                                    Dictionary<string, Regex> regexs = new Dictionary<string, Regex>();
                                    regexs.Add("datum", datum);
                                    //regexs.Add("day", day);
                                    //regexs.Add("week", week);
                                    regexs.Add("title", title);
                                    //regexs.Add("type", type);


                                    //Pruchod pres retezce stranky
                                    foreach (string l in lines)
                                    {
                                        foreach (var p in regexs)
                                        {
                                            if (p.Value.IsMatch(l))
                                            {
                                                Match match = p.Value.Match(l);
                                                //Posledni regex
                                                if (p.Value.Equals(title))
                                                {
                                                    sw.Write(WebUtility.HtmlDecode(match.Groups[p.Key].Value.Trim()) + "\n");
                                                }
                                                else
                                                {
                                                    sw.Write(WebUtility.HtmlDecode(match.Groups[p.Key].Value.Trim()) + ";");
                                                }
                                            }
                                        }
                                    }
                                }
                                sw.Close();
                            }
                            fs.Close();
                        }
                    }
                }
            }
        }
    }
}
