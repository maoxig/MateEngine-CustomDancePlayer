using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace CustomDancePlayer
{
    public static class DanceLocale
    {
        private static readonly Dictionary<string, Dictionary<string,string>> tables = new Dictionary<string, Dictionary<string,string>>(StringComparer.OrdinalIgnoreCase);
        public static string Language { get; private set; } = "en";
        public static event Action Changed;
        public static string[] AvailableLanguages => tables.Keys.OrderBy(code=>code,StringComparer.OrdinalIgnoreCase).ToArray();
        public static string LanguageName(string code) => tables.TryGetValue(code,out var table)&&table.TryGetValue("language.name",out var name)&&!string.IsNullOrWhiteSpace(name)?name:code;
        public static void Set(string choice)
        {
            tables.Clear();
            string directory=Path.Combine(Path.GetDirectoryName(typeof(DanceLocale).Assembly.Location),"Locales");
            if(Directory.Exists(directory))foreach(string path in Directory.GetFiles(directory,"*.json"))
            {
                try
                {
                    var table=JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(path));
                    if(table!=null)tables[Path.GetFileNameWithoutExtension(path)]=table;
                }
                catch(Exception error){Debug.LogWarning("[CustomDancePlayer] Locale "+Path.GetFileName(path)+": "+error.Message);}
            }
            string requested=choice;
            if(string.IsNullOrEmpty(choice)||choice=="auto")
            {
                requested=CultureInfo.CurrentUICulture.Name;
                if(Application.systemLanguage==SystemLanguage.ChineseSimplified)requested="zh-CN";
                else if(Application.systemLanguage==SystemLanguage.ChineseTraditional)requested="zh-TW";
            }
            Language=tables.Keys.FirstOrDefault(code=>string.Equals(code,requested,StringComparison.OrdinalIgnoreCase))
                ?? tables.Keys.OrderBy(code=>code,StringComparer.OrdinalIgnoreCase).FirstOrDefault(code=>code.Split('-')[0].Equals((requested??"en").Split('-')[0],StringComparison.OrdinalIgnoreCase)) ?? "en";
            Changed?.Invoke();
        }
        public static string T(string key, params object[] values)
        {
            string fallback=tables.TryGetValue("en",out var english)&&english.TryGetValue(key,out var value)&&value!=null?value:key;
            string result=tables.TryGetValue(Language,out var local)&&local.TryGetValue(key,out var translated)&&translated!=null?translated:fallback;
            if(values.Length==0)return result;
            try{return string.Format(result,values);}
            catch(FormatException){try{return string.Format(fallback,values);}catch(FormatException){return fallback;}}
        }
    }
}
