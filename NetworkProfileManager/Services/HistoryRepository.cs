using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using NetworkProfileManager.Helpers;
using NetworkProfileManager.Models;

namespace NetworkProfileManager.Services
{
    public class HistoryRepository
    {
        private const int MaxEntries = 100;
        private string DataDir => AppPaths.DataDirectory;

        public List<HistoryEntry> Load(string adapterId)
        {
            var file = FilePath(adapterId);
            if (!File.Exists(file)) return new List<HistoryEntry>();

            try
            {
                var json = File.ReadAllText(file);
                return JsonConvert.DeserializeObject<List<HistoryEntry>>(json) ?? new List<HistoryEntry>();
            }
            catch
            {
                BackupAndReset(file);
                return new List<HistoryEntry>();
            }
        }

        public void Append(string adapterId, HistoryEntry entry)
        {
            var list = Load(adapterId);
            list.Insert(0, entry);

            if (list.Count > MaxEntries)
                list.RemoveRange(MaxEntries, list.Count - MaxEntries);

            var json = JsonConvert.SerializeObject(list, Formatting.Indented);
            File.WriteAllText(FilePath(adapterId), json);
        }

        public void Clear(string adapterId)
        {
            try
            {
                var file = FilePath(adapterId);
                if (File.Exists(file)) File.Delete(file);
            }
            catch { }
        }

        private string FilePath(string adapterId)
            => Path.Combine(DataDir, $"history_{Sanitize(adapterId)}.json");

        private static string Sanitize(string id)
            => Regex.Replace(id, @"[^\w\-]", "_");

        private static void BackupAndReset(string file)
        {
            try { File.Copy(file, file + ".bak", overwrite: true); } catch { }
            try { File.Delete(file); } catch { }
        }
    }
}
