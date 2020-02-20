using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace Pixel_Photo_Comparer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            GetPhotos();
        }       

        void GetPhotos()
        {
            var folderPath = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), @"OneDrive\Pictures\Camera Roll");
            var newFolderPath = Path.Combine(folderPath, @"Duplicates");
            var files = Directory.EnumerateFiles(folderPath, "*.jpg");
            const string burst = "BURST";

            var groupedPictures = files
                .Where(f => f.Contains($"_{burst}"))
                .GroupBy(f => Path.GetFileNameWithoutExtension(f).Split("_")?.FirstOrDefault(s => s.StartsWith(burst))?.Substring(burst.Length))
                .Select(g => new KeyValuePair<string, HashSet<string>>(g.Key, g.ToHashSet()))
                .ToList();

            Debug.WriteLine($"Found {groupedPictures.Count} grouped files");

            groupedPictures = groupedPictures.Where(g => g.Value.Count > 1).ToList();
            Debug.WriteLine($"Found {groupedPictures.Count} grouped files with duplicates");

            if (groupedPictures.Count > 0 && !Directory.Exists(newFolderPath))
            {
                Directory.CreateDirectory(newFolderPath);
            }
            foreach (var groupedFile in groupedPictures)
            {
                var f = groupedFile.Value;
                foreach (var file in f)
                {
                    var fileName = Path.GetFileName(file);
                    var oldFilePath = Path.Combine(folderPath, fileName);
                    var newFilePath = Path.Combine(newFolderPath, fileName);
                    File.Move(oldFilePath, newFilePath);
                    Debug.WriteLine($"Moved {oldFilePath} > {newFilePath}");
                }
            }

            Debug.WriteLine($"All files moved");
        }
    }
}
