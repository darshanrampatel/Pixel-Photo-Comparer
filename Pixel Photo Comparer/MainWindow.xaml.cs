using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Jpeg;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Directory = System.IO.Directory;

namespace Pixel_Photo_Comparer
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;
            MaxWidth = SystemParameters.MaximizedPrimaryScreenWidth;
            RoutedCommand lhCommand = new RoutedCommand();
            RoutedCommand rhCommand = new RoutedCommand();
            KeyBinding lhBinding = new KeyBinding()
            {
                Command = lhCommand,
                Key = Key.A
            };
            KeyBinding rhBinding = new KeyBinding()
            {
                Command = rhCommand,
                Key = Key.L
            };
            InputBindings.Add(lhBinding);
            InputBindings.Add(rhBinding);
            CommandBindings.Add(new CommandBinding(lhCommand, SelectLHPhoto));
            CommandBindings.Add(new CommandBinding(rhCommand, SelectRHPhoto));
            MoveDuplicatePhotos();
            ProcessDuplicatePhotos();
        }

        void SelectLHPhoto(object sender, ExecutedRoutedEventArgs e) => SelectPhoto(true);
        void SelectRHPhoto(object sender, ExecutedRoutedEventArgs e) => SelectPhoto(false);

        void SelectPhoto(bool lh)
        {
            if (DuplicatesListView.SelectedItem is GroupedPhotos selectedGroup && !selectedGroup.Processed)
            {
                string photoToKeep;
                string photoToReject;
                if (lh)
                {
                    photoToKeep = selectedGroup.Duplicates[0];
                    photoToReject = selectedGroup.Duplicates[1];
                }
                else
                {
                    photoToKeep = selectedGroup.Duplicates[1];
                    photoToReject = selectedGroup.Duplicates[0];
                }
                var photoToKeepNewFilePath = Path.Combine(folderPath, Path.GetFileName(photoToKeep));
                var photoToRejectNewFilePath = Path.Combine(duplicateRejectedFolderPath, Path.GetFileName(photoToKeep));
                Debug.WriteLine($"Kept photo, moving {photoToKeep} => {photoToKeepNewFilePath}");
                Debug.WriteLine($"Rejected photo, moving {photoToReject} => {photoToRejectNewFilePath}");
                //File.Move(photoToKeep, photoToKeepNewFilePath);
                //File.Move(photoToReject, photoToRejectNewFilePath);
                selectedGroup.Processed = true;
                DuplicatesListView.ItemsSource = DuplicatesListView.ItemsSource.Cast<GroupedPhotos>().Where(g => g != selectedGroup);
                DuplicatesListView.SelectedIndex++;
            }
        }

        readonly static string folderPath = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), @"OneDrive\Pictures\Camera Roll");
        readonly static string duplicateFolderPath = Path.Combine(folderPath, @"Duplicates");
        readonly static string duplicateRejectedFolderPath = Path.Combine(duplicateFolderPath, @"Rejected");
        const string burst = "BURST";

        void MoveDuplicatePhotos()
        {
            var files = Directory.EnumerateFiles(folderPath, "*.jpg");

            var groupedPictures = files
                .Where(f => f.Contains($"_{burst}"))
                .GroupBy(f => Path.GetFileNameWithoutExtension(f).Split("_")?.FirstOrDefault(s => s.StartsWith(burst))?.Substring(burst.Length))
                .Select(g => new KeyValuePair<string, HashSet<string>>(g.Key, g.ToHashSet()))
                .ToList();

            Debug.WriteLine($"Found {groupedPictures.Count} grouped files");

            groupedPictures = groupedPictures.Where(g => g.Value.Count > 1).ToList();
            Debug.WriteLine($"Found {groupedPictures.Count} grouped files with duplicates");

            if (groupedPictures.Count > 0 && !Directory.Exists(duplicateFolderPath))
            {
                Directory.CreateDirectory(duplicateFolderPath);
            }
            if (!Directory.Exists(duplicateRejectedFolderPath))
            {
                Directory.CreateDirectory(duplicateRejectedFolderPath);
            }
            foreach (var groupedFile in groupedPictures)
            {
                var f = groupedFile.Value;
                foreach (var file in f)
                {
                    var fileName = Path.GetFileName(file);
                    var oldFilePath = Path.Combine(folderPath, fileName);
                    var newFilePath = Path.Combine(duplicateFolderPath, fileName);
                    File.Move(oldFilePath, newFilePath);
                    Debug.WriteLine($"Moved {oldFilePath} > {newFilePath}");
                }
            }

            Debug.WriteLine($"All files moved");
        }

        void ProcessDuplicatePhotos()
        {
            var duplicateFiles = Directory.EnumerateFiles(duplicateFolderPath, "*.jpg");
            var groupedPictures = duplicateFiles
                .Where(f => f.Contains($"_{burst}"))
                .GroupBy(f => Path.GetFileNameWithoutExtension(f).Split("_")?.FirstOrDefault(s => s.StartsWith(burst))?.Substring(burst.Length))
                .Where(g => g.Count() == 2)
                .Select((g, idx) => new GroupedPhotos { Index = idx + 1, Key = g.Key, Duplicates = g.ToArray() })
                .ToList();

            Debug.WriteLine($"Found {groupedPictures.Count} grouped files");

            DuplicatesListView.ItemsSource = groupedPictures;
            DuplicatesListView.SelectedItem = groupedPictures[0];
            DuplicatesListView.Focus();
        }

        class GroupedPhotos
        {
            public int Index { get; set; }
            public string Key { get; set; }
            public string KeyDisplay => DateTime.ParseExact(Key, "yyyyMMddHHmmssfff", CultureInfo.InvariantCulture).ToString("yyyy-MM-ddTHH:mm:ss.fff"); // seems like the filenames refer to local time
            public string[] Duplicates { get; set; } = new string[2];
            public bool Processed { get; set; }
        }

        private void DuplicatesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var selectedDuplicate = e.AddedItems[0] as GroupedPhotos;
                DisposeFileStreams();
                var (lhBitmap, lhRotation) = LoadAndRotateImage(lhFileStream, selectedDuplicate.Duplicates[0]);
                var (rhBitmap, rhRotation) = LoadAndRotateImage(rhFileStream, selectedDuplicate.Duplicates[1]);
                Application.Current.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new System.Threading.ThreadStart(delegate
                {
                    //Update UI here
                    LHImage.LayoutTransform = lhRotation;
                    LHImage.Source = lhBitmap;
                    RHImage.LayoutTransform = rhRotation;
                    RHImage.Source = rhBitmap;                    
                }));
            }
        }

        FileStream lhFileStream;
        FileStream rhFileStream;

        void DisposeFileStreams()
        {
            if (lhFileStream != null)
            {
                lhFileStream.Close();
                lhFileStream.Dispose();
                lhFileStream = null;
            }
            if (rhFileStream != null)
            {
                rhFileStream.Close();
                rhFileStream.Dispose();
                rhFileStream = null;
            }
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        }

#pragma warning disable IDE0060 // Remove unused parameter
        private (BitmapImage, RotateTransform) LoadAndRotateImage(FileStream stream, string filePath)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            var bitmap = new BitmapImage();
            stream = File.OpenRead(filePath);
            var rotation = GetRotation(stream);
            stream.Position = 0;
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.None;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            return (bitmap, new RotateTransform(rotation));
        }

        private double GetRotation(FileStream fileStream) // from https://stackoverflow.com/a/39840498/5024969
        {
            var directories = JpegMetadataReader.ReadMetadata(fileStream, new[] { new ExifReader() });
            var subIfdDirectory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            var orientation = subIfdDirectory?.GetInt16(ExifDirectoryBase.TagOrientation);
            return orientation switch
            {
                6 => 90D,
                3 => 180D,
                8 => 270D,
                _ => 0D,
            };
        }
    }
}
