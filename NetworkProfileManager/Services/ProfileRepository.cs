using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using NetworkProfileManager.Helpers;
using NetworkProfileManager.Models;

namespace NetworkProfileManager.Services
{
    public class ProfileRepository
    {
        private string DataDir => AppPaths.DataDirectory;

        public List<IpProfile> Load(string adapterId)
        {
            var file = FilePath(adapterId);
            if (!File.Exists(file)) return new List<IpProfile>();

            try
            {
                var json = File.ReadAllText(file);
                return JsonConvert.DeserializeObject<List<IpProfile>>(json) ?? new List<IpProfile>();
            }
            catch
            {
                BackupAndReset(file);
                return new List<IpProfile>();
            }
        }

        public void Save(string adapterId, List<IpProfile> profiles)
        {
            if (profiles.Count > 5)
                throw new InvalidOperationException(
                    Loc.Get("Error.MaxProfiles", "Maksimum 5 profil kaydedilebilir."));

            var json = JsonConvert.SerializeObject(profiles, Formatting.Indented);
            File.WriteAllText(FilePath(adapterId), json);
        }

        private string FilePath(string adapterId)
            => Path.Combine(DataDir, $"profiles_{Sanitize(adapterId)}.json");

        private static string Sanitize(string id)
            => Regex.Replace(id, @"[^\w\-]", "_");

        private static void BackupAndReset(string file)
        {
            try { File.Copy(file, file + ".bak", overwrite: true); } catch { }
            try { File.Delete(file); } catch { }
        }
    }
}
