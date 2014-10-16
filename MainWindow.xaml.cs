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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;

namespace SimpleMidiRecorder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MidiRecorder mRecorder;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (mRecorder != null) return;

            EnterTracks dlg = new EnterTracks();
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                Debug.WriteLine("Folder: " + dlg.Folder);
                Debug.WriteLine("Album: " + dlg.Album);
                Debug.WriteLine("Artist: " + dlg.Artist);
                Debug.WriteLine("Genre: " + dlg.Genre);
                Debug.WriteLine("Tracks: " + string.Join(" | ", dlg.TrackNames));

                mRecorder = new MidiRecorder();
                mRecorder.Monitor = Monitor.IsChecked == true;
                mRecorder.StatusUpdated += mRecorder_StatusUpdated;

                mRecorder.Folder = dlg.Folder;
                mRecorder.Album = dlg.Album;
                mRecorder.Artist = dlg.Artist;
                mRecorder.Genre = dlg.Genre;
                mRecorder.TrackNames = dlg.TrackNames;

                RecordingToTextBox.Text = System.IO.Path.Combine(dlg.Folder, dlg.Album);
                
                mRecorder.BeginRecording();

                RecordButton.IsEnabled = false;
                StopButton.IsEnabled = true;

            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            EndRecording();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            EndRecording();
            base.OnClosing(e);
        }

        private void BeginRecording()
        {
            if (mRecorder == null)
            {
                mRecorder = new MidiRecorder();
                mRecorder.Monitor = Monitor.IsChecked == true;
                mRecorder.StatusUpdated += mRecorder_StatusUpdated;
                mRecorder.BeginRecording();
                RecordButton.IsEnabled = false;
                StopButton.IsEnabled = true;
            }
        }

        // Tolerates if no recording is happening
        private void EndRecording()
        {
            if (mRecorder != null)
            {
                mRecorder.EndRecording();
                mRecorder.StatusUpdated -= mRecorder_StatusUpdated;
                mRecorder.Dispose();
                mRecorder = null;
            }
            RecordButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }

        private void Monitor_Changed(object sender, RoutedEventArgs e)
        {
            if (mRecorder != null) mRecorder.Monitor = Monitor.IsChecked == true;
        }

        private MidiRecorder.StatusUpdateEventHandler InnerStatusUpdatedDelegate;

        private void mRecorder_StatusUpdated(bool inTrack, int track, int notes, int milliseconds, string note)
        {
            if (InnerStatusUpdatedDelegate == null) InnerStatusUpdatedDelegate = new MidiRecorder.StatusUpdateEventHandler(InnerStatusUpdated);

            // May arrive on any thread. Marshal to the UI thread
            Dispatcher.BeginInvoke(InnerStatusUpdatedDelegate, inTrack, track, notes, milliseconds, note);
        }

        private void InnerStatusUpdated(bool inTrack, int track, int notes, int ms, string note)
        {
            StatusBox.Text = string.Format("{0}: Track: {1} Notes: {2}, Elapsed: {3:d2}:{4:d2}:{5:d2}.{6:d3}\r\n{7}",
                inTrack ? "Recording" : "Waiting", track, notes,
                ms / (60*60*1000), (ms / (60*1000)) % 60, (ms / 1000) % 60, ms % 1000,
                note);
        }

    }

    public class Wpf32Window : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle { get; private set; }

        public Wpf32Window(Window wpfWindow)
        {
            Handle = new System.Windows.Interop.WindowInteropHelper(wpfWindow).Handle;
        }
    }
}
