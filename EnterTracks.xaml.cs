using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace SimpleMidiRecorder
{
    public class Track
    {
        public Track(int num, string name)
        {
            Num = num;
            Name = name;
        }

        public int Num { get; private set; }
        public string Name { get; set; }
    }


    /// <summary>
    /// Interaction logic for EnterTracks.xaml
    /// </summary>
    public partial class EnterTracks : Window
    {
        const int maxTracks = 30;
        private List<Track> Tracks = new List<Track>();

        public EnterTracks()
        {
            InitializeComponent();

            FolderTextBox.Text = DefaultMidiFolder;

            for (int i = 0; i < maxTracks; ++i )
            {
                Tracks.Add(new Track(i+1, String.Empty));
            }
            TrackGrid.ItemsSource = Tracks;
        }

        private void ChangeFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            dlg.SelectedPath = FolderTextBox.Text;
            if (dlg.ShowDialog(new Wpf32Window(this)) == System.Windows.Forms.DialogResult.OK)
            {
                FolderTextBox.Text = dlg.SelectedPath;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate and transfer results
            Folder = FolderTextBox.Text.Trim();
            Album = AlbumTextBox.Text.Trim();
            if (string.IsNullOrEmpty(Album))
            {
                MessageBox.Show("Album name must not be empty.");
                return;
            }
            if (Album.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show("Album name must not use any invalid filename characters:\r\n \"<>|:*?\\/");
                return;
            }
            Artist = ArtistTextBox.Text.Trim();
            Genre = GenreTextBox.Text.Trim();

            // Transfer the track names
            {
                List<String> names = new List<string>();
                foreach(Track track in Tracks)
                {
                    if (String.IsNullOrEmpty(track.Name)) break;
                    names.Add(track.Name);
                }
                TrackNames = names.ToArray();
            }

            // Save the default folder
            DefaultMidiFolder = Folder;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // Results
        public string Folder { get; private set; }
        public string Album { get; private set; }
        public string Artist { get; private set; }
        public string Genre { get; private set; }
        public string[] TrackNames { get; private set; }
        
        static readonly string RegKey = @"Software\BCR\SimpleMidiRecorder";
        static readonly string RegMidiFolder = "MidiFolder";
        private static string DefaultMidiFolder
        {
            get
            {
                string value = null;
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\BCR\SimpleMidiRecorder", false))
                {
                    if (key != null)
                    {
                        value = key.GetValue(RegMidiFolder) as string;
                    }
                }
                if (string.IsNullOrEmpty(value))
                {
                    value = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                }
                if (string.IsNullOrEmpty(value))
                {
                    value = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
                return value;
            }

            set
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\BCR\SimpleMidiRecorder"))
                {
                    key.SetValue(RegMidiFolder, value);
                }
            }
        }
    }
}
