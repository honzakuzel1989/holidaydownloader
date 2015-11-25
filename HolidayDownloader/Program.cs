using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;

namespace HolydayDownloader
{
    class Program
    {
        static void Main(string[] args)
        {

            HDDownloader hdd = new HDDownloader();
            hdd.Download();
            ////Vytvoreni adresare, pokud neexistuje
            //if(!Directory.Exists(outputDirectory))
            //{
            //    Directory.CreateDirectory(outputDirectory);
            //}

            ////Pres vsecky locale
            //foreach (string locale in locales)
            //{
            //    WebClient wc = new WebClient();
            //    wc.DownloadFile(inputFilePattern, )
            //}
        }
    }

    internal class LocaleInfo
    {
        public readonly string prefix, locale, locale2;
        public readonly string entry, directory;
        public readonly string page;

        public LocaleInfo(string prefix, string locale, string locale2, string entry, string directory, string page, params object[] parms)
        {
            this.locale = locale;
            this.locale2 = locale2;
            this.entry = entry;
            this.directory = directory;
            this.page = string.Format(page, parms);
            this.prefix = prefix;
        }
    }

    internal class HDDownloader
    {
        //TODO: SETTINGS!
        /*
         * Jazyky - "de", "fr", "it", "en " 
         */
        private LocaleInfo[] localesInfo = new LocaleInfo[4];

        /*
         * Seznam polozek ke stazeni pro dany jazyk
         */
        private Dictionary<string, Dictionary<string, string>> downloadItems;

        /*
         * Roky
         */
        private int minYear = 2010;
        private int maxYear = 2020;

        /*
         * Trida svatku
         */
        private enum holidayClasses { LEGALLY = 3, LEGGALYandNOTRECOGNIZED = 4, HOLIDAYSandEVENTS = 5 };

        /*
         * Korenova stranka, ze ktere se budou zjistovat geo kody zemi, popr stranky pro geo kody zemi
         */
        private string rootPage = @"http://" + "www.feiertagskalender.ch/index.php?hl={0}";

        /**
         * Stranka, ze ktere se bude tahat info o slozkach - vice regionu v zemi
         */
        private string directoryPage = @"http://" + "www.feiertagskalender.ch/index.php?hl={0}&geo={1}";

        /*
         * 
         */
        private string recordPage = @"http://" + "www.feiertagskalender.ch/index.php?geo={0}&jahr={1}&klasse={2}&hl={3}";

        /*
         * Vystupni adressar
         */
        private string outputDirectory = "Holidays";

        /*
         * Trida svatku
         */
        private enum HolidayClasses { LEGALLY = 3, LEGGALYandNOTRECOGNIZED = 4, HOLIDAYSandEVENTS = 5 };

        public HDDownloader()
        {
            //locales.Add("de", new string[] { "Eintrag", "Verzeichnis" });
            //locales.Add("fr", new string[] { "Entr&eacute;e", "R&eacute;pertoire" });
            //locales.Add("it", new string[] { "Registrazione", "Directory" });
            //locales.Add("en", new string[] { "entry", "directory", string.Format(rootPage, "en") });

            //TODO: Extrahovat konstanty
            localesInfo[0] = new LocaleInfo(string.Empty, "en", string.Empty, "entry", "directory", rootPage, "en");
            localesInfo[1] = new LocaleInfo(string.Empty, "it", string.Empty, "Registrazione", "Directory", rootPage, "it");

            //Itemy ke stazeni
            downloadItems = new Dictionary<string, Dictionary<string, string>>();
            downloadItems.Add("en", new Dictionary<string, string>
            { 
               {"Australia", "en_AU"},
               {"Ireland", "en_IE"}, 
               {"Malta","en_MT"}, 
                {"United Kingdom","en_GB"}, 
                {"United States", "en_US"}, 
                {"Slovakia", "en_SK"},
                {"Czech Republic", "en_CZ"},
            });
            downloadItems.Add("it", new Dictionary<string, string>
            { 
               {"Italia", "it_IT"},
               {"Svizzera", "it_CH"}, 
            });
            //downloadItems.Add("it", new string[] { "Italia" });
            //downloadItems.Add("fr", new string[] { "France" });
            //downloadItems.Add("de", new string[] { "Deutschland" });
        }

        private void PrepareOutputDirectory()
        {
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }
        }

        private void PrepareOutputSubDirectory(string subDirName)
        {
            string path = Path.Combine(outputDirectory, subDirName);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public void Download()
        {
            PrepareOutputDirectory();

            Thread[] threads = new Thread[localesInfo.Length];

            for (int i = 0, e = localesInfo.Length; i < e; i++)
            {
                LocaleInfo locale = localesInfo[i];

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
            string page = wc.DownloadString(data.page);

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

                    bool flag = false;

                    foreach (var s in downloadItems[data.locale])
                    {
                        string name = s.Key;

                        if (name == country || data.prefix.Contains(data.locale))
                        {
                            flag = true;
                            break;
                        }
                    }

                    //Slozka zanzamu
                    if (directory)
                    {
                        //Chceme jen ty slozky, ke kterym mame preklad
                        if (flag)
                        {
                            LocaleInfo info = new LocaleInfo(data.prefix == string.Empty ? downloadItems[data.locale][country] : data.prefix + "_" + country, data.locale, data.locale2 == string.Empty ? downloadItems[data.locale][country] : data.locale2, data.entry, data.directory, directoryPage, data.locale, geo);
                            Run(info);
                        }
                    }
                    //Zaznam
                    
                    {

                        //Vyber jen danych statu k danemu locale
                        if (flag)
                        {
                            //Zaznam
                            string name = WebUtility.HtmlDecode(data.locale2 == string.Empty ? downloadItems[data.locale][country] : data.prefix + "_" + country);
                            Console.WriteLine(name);

                            string path = Path.Combine(outputDirectory, name);

                            if (!File.Exists(path))
                            {
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
                                            //Regex week = new Regex("<td>\nCW (?<week>[0-9]{2})</td>", RegexOptions.Compiled);
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
                                                        sw.WriteLine(WebUtility.HtmlDecode(match.Groups[p.Key].Value.Trim()));
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
    }
}
