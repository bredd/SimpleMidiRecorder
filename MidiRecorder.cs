using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using System.Diagnostics;
using System.IO;

namespace SimpleMidiRecorder
{

    /// <summary>
    /// Writes a MIDI sequence to a MIDI Type 0 file
    /// </summary>
    class MidiWriter
    {
        const int cMemStreamInitialCapacity = 0x00010000;   // 64KB

        public static readonly Encoding EncodingLATIN1 = Encoding.GetEncoding("ISO-8859-1");

        public enum Message : uint
        {
            Channel0ProgramAcousticGrand = 0x000000c0,
            Channel0AllNotesOff = 0x00007bb0,
            Channel0DamperOff = 0x000040b0
        }

        public enum TextType
        {
            GenericText = 1,
            Copyright = 2,
            TrackName = 3,
            InstrumentName = 4,
            Lyric = 5,
            Marker = 6,
            CuePoint = 7
        }

        // Member variables
        MemoryStream mCaptureStream;    // Stream used for capturing the sequence before writing out to a file
        UInt32 mLastEventTime;             // In Milliseconds

        /// <summary>
        /// Creates a MidiWriter and prepares it to begin capturing events
        /// </summary>
        /// <param name="startTimeMs">The starting event time in milliseconds.</param>
        public MidiWriter(UInt32 startTimeMs)
        {
            mCaptureStream = new MemoryStream(cMemStreamInitialCapacity);
            mLastEventTime = startTimeMs;
        }

        /// <summary>
        /// Writes a midi message
        /// </summary>
        /// <param name="midiMessage">A MIDI message in Microsoft MIDI API format</param>
        /// <param name="eventTimeMs">The time of the event in milliseconds</param>
        /// <remarks>MIDI messages may be one, two, three bytes long depending on the event/status type.</remarks>
        /// <returns>True if the event was written. False if it's invalid or unrecognized.</returns>
        public bool WriteMidiEvent(UInt32 eventTimeMs, UInt32 midiMessage)
        {
            // Taken from http://www.midi.org/techspecs/midimessages.php
            byte status = (byte)midiMessage;
            byte data1 = (byte)(midiMessage >> 8);
            byte data2 = (byte)(midiMessage >> 16);
            byte cmd = (byte)((midiMessage >> 4) & 0x0000000F);
            byte channel = (byte)(midiMessage & 0x0000000F);

            int bytecount = 0;
            switch(cmd)
            {
                case 0x08:  // Note off
                case 0x09:  // Note on
                case 0x0a:  // Note aftertouch
                case 0x0b:  // Control change
                case 0x0e:  // Pitch bend
                    bytecount = 3;
                    break;

                case 0x0c:  // Program change
                case 0x0d:  // Channel aftertouch
                    bytecount = 2;
                    break;

                case 0x0f:  // System common messages (channel indicates the message)
                    switch (channel)
                    {
                        case 0x00:  // System exclusive message
                            bytecount = 0;  // Not supported
                            break;

                        case 0x08:  // Timing clock
                        case 0x0a:  // Start
                        case 0x0b:  // Continue
                        case 0x0c:  // Stop
                        case 0x0e:  // Active sensing
                            bytecount = 1;
                            break;

                        case 0x01:  // Time code quarter frame
                        case 0x03:  // Song select
                            bytecount = 2;
                            break;

                        case 0x02:  // Song position pointer
                            bytecount = 3;
                            break;

                        case 0x0f:  // Reset
                            bytecount = 0;  // Should be suppressed, only valid in the stream, not the file.
                            break;

                        default:
                            bytecount = 0;  // Unsupported/Undefined/Unknown/Error
                            break;
                    }
                    break;

                default:
                    bytecount = 0;  // Unsupported/Undefined/Unknown/Error
                    break;
            }

            if (bytecount == 0) return false;

            // Write the time delta
            WriteEventTime(eventTimeMs);

            if (bytecount > 0)
            {
                mCaptureStream.WriteByte(status);
            }
            if (bytecount > 1)
            {
                mCaptureStream.WriteByte(data1);
            }
            if (bytecount > 2)
            {
                mCaptureStream.WriteByte(data2);
            }
            return true;         
        }

        public bool WriteMidiEvent(UInt32 eventTimeMs, Message midiMessage)
        {
            return WriteMidiEvent(eventTimeMs, (UInt32)midiMessage);
        }

        public void WriteMidiText(UInt32 eventTimeMs, TextType textType, string text)
        {
            if (!Enum.IsDefined(typeof(TextType), textType)) throw new ArgumentException("Invalid TextType");
            byte[] byteText = EncodingLATIN1.GetBytes(text);
            if (byteText.Length == 0) return;

            WriteEventTime(eventTimeMs);
            mCaptureStream.WriteByte((byte)0xFF);
            mCaptureStream.WriteByte((byte)textType);
            WriteMidiVarLen(mCaptureStream, (uint)byteText.Length);
            mCaptureStream.Write(byteText, 0, byteText.Length);
        }

        public void WriteMidiTempo(UInt32 eventTimeMs, UInt32 microsecsPerBeat)
        {
            WriteEventTime(eventTimeMs);
            mCaptureStream.WriteByte((byte)0xFF);
            mCaptureStream.WriteByte((byte)0x51);   // Set Tempo
            mCaptureStream.WriteByte((byte)0x03);   // Tempo is 3 bytes long
            mCaptureStream.WriteByte((byte)(microsecsPerBeat >> 16));
            mCaptureStream.WriteByte((byte)(microsecsPerBeat >> 8));
            mCaptureStream.WriteByte((byte)microsecsPerBeat);
        }

        public void WriteMidiEndTrack(UInt32 eventTimeMs)
        {
            WriteEventTime(eventTimeMs);
            mCaptureStream.WriteByte((byte)0xFF);
            mCaptureStream.WriteByte((byte)0x2F);   // End of Track
            mCaptureStream.WriteByte((byte)0x00);   // No additional data
        }

        // Standard MIDI file header with format type 0, 1 track and 500 ticks per beat
        static readonly byte[] sMidiHeader = { (byte)'M', (byte)'T', (byte)'h', (byte)'d', 0, 0, 0, 6, 0, 0, 0, 1, 0x01, 0xF4 };
        static readonly byte[] sTrackHeaderId = { (byte)'M', (byte)'T', (byte)'r', (byte)'k' };

        public void SaveTo(string filename)
        {
            mCaptureStream.Flush();
            using (FileStream stream = new FileStream(filename, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                // Write MIDI header
                stream.Write(sMidiHeader, 0, sMidiHeader.Length);
                
                // Write Track header
                stream.Write(sTrackHeaderId, 0, sTrackHeaderId.Length);
                UInt32 trackLen = (UInt32)mCaptureStream.Length;
                stream.WriteByte((byte)(trackLen >> 24));
                stream.WriteByte((byte)(trackLen >> 16));
                stream.WriteByte((byte)(trackLen >> 8));
                stream.WriteByte((byte)trackLen);

                // Write the track
                mCaptureStream.Position = 0;
                mCaptureStream.WriteTo(stream);
            }
            mCaptureStream.Dispose();
            mCaptureStream = null;
        }

        private void WriteEventTime(UInt32 eventTimeMs)
        {
            // If event time is zero, use delta time zero
            if (eventTimeMs == 0)
            {
                mCaptureStream.WriteByte(0);
                return;
            }

            UInt32 deltaTime;
            if (eventTimeMs < mLastEventTime)
            {
                Debug.WriteLine("Warning, negative delta time!!");
                deltaTime = 0;
            }
            {
                deltaTime = eventTimeMs - mLastEventTime;
                mLastEventTime = eventTimeMs;
            }
            WriteMidiVarLen(mCaptureStream, deltaTime);
        }

        /// <summary>
        /// Writes a MIDI variable length integer -- mostly used for delta time
        /// </summary>
        /// <param name="stream">The stream to which the value is being written</param>
        /// <param name="value">The integer which will be written as a variable length value</param>
        /// <returns>The number of bytes written</returns>
        private int WriteMidiVarLen(Stream stream, UInt32 value)
        {
            int bytecount = 1;
            while (bytecount < 5 && 1 << (bytecount * 7) <= value) ++bytecount;
            int n = bytecount;
            while (n > 1)
            {
                --n;
                stream.WriteByte((byte)((value >> (n * 7)) | 0x00000080));
            }
            stream.WriteByte((byte)(value & 0x0000007F));
            return bytecount;
        }


    }

    class MidiRecorder : IDisposable
    {
        const UInt32 cStatusInterval = 100; // In milliseconds

        delegate void MsgDataDelegate(UInt32 param1, UInt32 param2);
        public delegate void StatusUpdateEventHandler(bool inTrack, int track, int notes, int milliseconds, string note);

        Thread mWriterThread;
        Dispatcher mWriterDispatcher;
        MidiExtern.MidiInProc mInProcDelegate;
        MsgDataDelegate mMsgDataDelegate;
        IntPtr mHMidiIn;
        IntPtr mHMidiThru;

        bool mInTrack = false;
        MidiWriter mTrackWriter = null;
        UInt32 mLastStatus; // in milliseconds
        UInt32 mTrackStart; // in milliseconds
        int mTrack;
        int mNotes;
        string mStatusMessage;

        public event StatusUpdateEventHandler StatusUpdated;

        public MidiRecorder()
		{
			Debug.WriteLine("Starting writer dispatcher.");
            mWriterThread = new Thread(new ThreadStart(WriterThreadMain));
			mWriterThread.Start();
            mMsgDataDelegate = MsgData;
        }

		void WriterThreadMain()
		{
            mWriterDispatcher = Dispatcher.CurrentDispatcher;
			Debug.WriteLine("Writer dispatcher started.");
			Dispatcher.Run();
			Debug.WriteLine("Writer dispatcher ended.");
		}

        private bool mMonitor;
        public bool Monitor
        {
            get { return mMonitor; }
            set
            {
                mMonitor = value;
                if (!mMonitor && mHMidiThru != IntPtr.Zero)
                {
                    MidiExtern.midiOutShortMsg(mHMidiThru, 0x000040b0); // Damper pedal off
                    MidiExtern.midiOutShortMsg(mHMidiThru, 0x00007bb0); // All notes off
                }
            }
        }

        public String Folder { get; set; }
        public String Album { get; set; }
        public String Artist { get; set; }
        public String Genre { get; set; }
        public String[] TrackNames { get; set; }

        public void BeginRecording()
        {
            mHMidiThru = MidiExtern.OutOpen(0, null, 0);

            mInProcDelegate = InProc; // have to do this because MidiExtern is invisible to the GC
            mHMidiIn = MidiExtern.InOpen(0, mInProcDelegate, 0);
            MidiExtern.InStart(mHMidiIn);
            UpdateStatus(0);
        }

        public void EndRecording()
        {
            if (mTrackWriter != null)
            {
                mTrackWriter = null;
            }
            mInTrack = false;

            UpdateStatus(0, "Recording Ended");

            if (mHMidiIn != IntPtr.Zero)
            {
                MidiExtern.InStop(mHMidiIn);
                MidiExtern.InReset(mHMidiIn);
                MidiExtern.InClose(mHMidiIn);
                mHMidiIn = IntPtr.Zero;
            }
            if (mHMidiThru != IntPtr.Zero)
            {
                MidiExtern.OutClose(mHMidiThru);
            }
        }

        private void InProc(IntPtr handle, UInt16 msg, UInt32 instance, UInt32 param1, UInt32 param2)
        {
            MidiExtern.Msg mmsg = (MidiExtern.Msg)msg;
            switch (mmsg)
            {
                case MidiExtern.Msg.MIM_DATA: // MIM_DATA
                    if (mMonitor) MidiExtern.midiOutShortMsg(mHMidiThru, param1);

                    mWriterDispatcher.BeginInvoke(mMsgDataDelegate, param1, param2);
                    break;

                default:
                    Debug.WriteLine("InMsg msg={0} param1={1:x8} param2={2:x8}", mmsg, param1, param2);
                    break;

            }
        }

        enum MessageType
        {
            Unknown = 0,
            Clear = 1,      // Between songs clearning
            Note = 2,
            Control = 3,    // Control during music
            Neutral = 4,    // Write if recording, skip if not
            Suppress = 5,   // Suppress output to avoid causing problems
            Unexpected = 6
        }

        private void MsgData(UInt32 message, UInt32 milliseconds)
        {
            byte status = (byte)message;
            byte data1 = (byte)(message >> 8);
            byte data2 = (byte)(message >> 16);
            byte cmd = (byte)((message >> 4) & 0x0000000F);
            byte channel = (byte)(message & 0x0000000F);

            bool forceStatus = false;
            MessageType mtype = MessageType.Unknown;
            switch (cmd)
            {
                case 0x08: // Note off
                    mtype = MessageType.Note;
                    break;

                case 0x09: // Note on
                    mtype = MessageType.Note;
                    ++mNotes;
                    break;

                case 0x0a: // Note Aftertouch
                    mtype = MessageType.Note;
                    break;

                case 0x0b:
                    switch (data1)
                    {
                        case 0x01:  // Modulation
                            // Type depends on context
                            if (data2 == 0 && !mInTrack)
                            {
                                mtype = MessageType.Clear;
                            }
                            else
                            {
                                mtype = MessageType.Control;
                            }
                            break;

                        case 0x07:  // Channel Volume
                            mtype = MessageType.Suppress;   // Leave volume resetting to the host controller
                            break;

                        case 0x40:  // Damper pedal
                        case 0x42:  // Sostenuto
                        case 0x43:  // Soft pedal
                            // Type depends on context
                            if (data2 < 64 && !mInTrack)
                            {
                                // If not in a track and this is a pedal release, it's a clear
                                mtype = MessageType.Clear;
                            }
                            else
                            {
                                mtype = MessageType.Control;
                            }
                            break;

                        case 0x79:  // Reset all controllers
                            mtype = MessageType.Suppress;   // Leave that to the host controller
                            break;

                        case 0x7B:  // All notes off
                            mtype = MessageType.Clear;
                            break;

                        default:
                            mtype = MessageType.Neutral;
                            break;
                    }
                    break;

                case 0x0c: // Program change
                    mtype = MessageType.Control;
                    break;

                case 0x0d: // Channel Aftertouch
                    mtype = MessageType.Control;
                    break;

                case 0x0e: // Pitch bend
                    // Type depends on context
                    if (data1 == 0 && data2 == 0x40 && !mInTrack)
                    {
                        mtype = MessageType.Clear;
                    }
                    else
                    {
                        mtype = MessageType.Control;
                    }
                    break;

                case 0x0f: // System controls
                    mtype = MessageType.Suppress;
                    break;                   
            }

            switch (mtype)
            {
                case MessageType.Suppress:
                    break;  // DO nothing

                case MessageType.Note:
                case MessageType.Control:
                    if (!mInTrack)
                    {
                        StartTrackRecording(milliseconds);
                        forceStatus = true;
                    }
                    mTrackWriter.WriteMidiEvent(milliseconds, message);
                    if (mtype == MessageType.Note)
                    {
                        ++mNotes;
                    }
                    break;

                case MessageType.Clear:
                    if (mInTrack)
                    {
                        SaveTrackRecording(milliseconds);

                        mInTrack = false;
                        forceStatus = true;
                        UInt32 elapsed = milliseconds - mTrackStart;
                        Debug.WriteLine("Track {0} Ended. {1} notes. {2:d2}:{3:d2}", mTrack, mNotes, elapsed / (60*1000), (elapsed/1000)%60);
                        ++mTrack;
                    }
                    break;

                case MessageType.Neutral:
                    if (mTrackWriter != null) mTrackWriter.WriteMidiEvent(milliseconds, message);
                    break;

                default:
                    Debug.WriteLine("{0}: cmd={1:x2} data1={2:x2} data2={3:x2} ms={4}", mtype, cmd, data1, data2, milliseconds);
                    break;
            }

            if (forceStatus || milliseconds < mLastStatus || milliseconds - mLastStatus > cStatusInterval)
            {
                UpdateStatus((int)milliseconds);
                mLastStatus = milliseconds;
            }
        }

        void UpdateStatus(int milliseconds, string message = null)
        {
            if (message != null)
            {
                mStatusMessage = message;
            }
            if (StatusUpdated != null)
            {
                StatusUpdated(mInTrack, mTrack + 1, mNotes, milliseconds, mStatusMessage);
            }
        }

        private void StartTrackRecording(UInt32 milliseconds)
        {
            Debug.Assert(mTrackWriter == null);
            mTrackWriter = new MidiWriter(milliseconds-100);    // Give a 100 millisecond gap between the metadata and the first note

            // Generate track name
            string trackName = (mTrack < TrackNames.Length) ? TrackNames[mTrack] : null;
            if (string.IsNullOrEmpty(trackName))
            {
                if (!string.IsNullOrEmpty(Album))
                {
                    trackName = string.Format("{0} - Track {1:d2}", Album, mTrack+1);
                }
                else if (!string.IsNullOrEmpty(Artist))
                {
                    trackName = string.Format("{0} - Track {1:d2}", Artist, mTrack+1);
                }
                else
                {
                    trackName = string.Format("Track {0:d2}", mTrack+1);
                }
            }

            // Write metadata
            mTrackWriter.WriteMidiText(0, MidiWriter.TextType.TrackName, trackName);
            if (!String.IsNullOrEmpty(Artist))
            {
                mTrackWriter.WriteMidiText(0, MidiWriter.TextType.GenericText, Artist); // Convention seems to use the plain "Text" field for the artist name
            }
            else if (!String.IsNullOrEmpty(Album))
            {
                mTrackWriter.WriteMidiText(0, MidiWriter.TextType.GenericText, Album);
            }
            mTrackWriter.WriteMidiText(0, MidiWriter.TextType.Lyric, string.Format("{{#Title={0}}}", trackName));
            if (!String.IsNullOrEmpty(Artist)) mTrackWriter.WriteMidiText(0, MidiWriter.TextType.Lyric, string.Format("{{#Artist={0}}}", Artist));
            if (!String.IsNullOrEmpty(Album)) mTrackWriter.WriteMidiText(0, MidiWriter.TextType.Lyric, string.Format("{{#Album={0}}}", Album));
            mTrackWriter.WriteMidiText(0, MidiWriter.TextType.Lyric, string.Format("{{#Track={0:d3}}}", mTrack+1));
            if (!String.IsNullOrEmpty(Genre)) mTrackWriter.WriteMidiText(0, MidiWriter.TextType.Lyric, string.Format("{{#Genre={0}}}", Genre));
            mTrackWriter.WriteMidiText(0, MidiWriter.TextType.Lyric, "{#}"); // End of metadata

            // Set tempo
            mTrackWriter.WriteMidiTempo(0, 500000);  // Combined with 500 ticks per beat this comes out at 1 millisecond per tick

            // Clear channel 0
            mTrackWriter.WriteMidiEvent(0, MidiWriter.Message.Channel0ProgramAcousticGrand);
            mTrackWriter.WriteMidiEvent(0, MidiWriter.Message.Channel0AllNotesOff);
            mTrackWriter.WriteMidiEvent(0, MidiWriter.Message.Channel0DamperOff);

            mInTrack = true;
            mTrackStart = milliseconds;
            mNotes = 0;
            Debug.WriteLine("Track {0} Started", mTrack+1);
            UpdateStatus((int)milliseconds, "Track Name: " + trackName);
        }

        static string MakeValidFilename(string name)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            for (;;)
            {
                int i = name.IndexOfAny(invalidChars);
                if (i < 0) break;
                name = string.Concat(name.Substring(0, i), "-", name.Substring(i + 1));
            }
            return name;
        }

        private void SaveTrackRecording(UInt32 milliseconds)
        {
            try
            {
                // Clear channel 0
                mTrackWriter.WriteMidiEvent(0, MidiWriter.Message.Channel0DamperOff);
                mTrackWriter.WriteMidiEvent(0, MidiWriter.Message.Channel0AllNotesOff);

                // Create the directory if it doesn't exist
                string directoryName = Path.Combine(Folder, Album);
                if (!Directory.Exists(directoryName)) Directory.CreateDirectory(directoryName);

                // Generate filename core from track name
                string fileCoreName = (mTrack < TrackNames.Length) ? TrackNames[mTrack] : null;
                if (string.IsNullOrEmpty(fileCoreName))
                {
                    fileCoreName = "Track";
                }

                // Generate a full, unique filename
                string filePath = Path.Combine(directoryName, string.Format("{0:d2}-{1}", mTrack+1, MakeValidFilename(fileCoreName)));
                if (File.Exists(filePath + ".mid"))
                {
                    int i=1;
                    string newPath;
                    do
                    {
                        newPath = string.Format("{0} ({1})", filePath, i);
                        ++i;
                    } while (File.Exists(newPath + ".mid"));
                    filePath = newPath;
                }
                filePath = filePath + ".mid";

                // Save the file
                mTrackWriter.WriteMidiEndTrack(milliseconds);
                mTrackWriter.SaveTo(filePath);

                UpdateStatus((int)milliseconds, "Written To: " + filePath);
            }
            catch (Exception err)
            {
                UpdateStatus((int)milliseconds, "Error saving track: " + err.Message);
                Debug.WriteLine(err.ToString());
            }

            mTrackWriter = null;
        }

        private void Dispose(bool disposing)
        {
            EndRecording(); // Does nothing if not recording
            mInProcDelegate = null;
            if (mWriterThread != null)
            {
			    Debug.WriteLine("Shutting down writer dispatcher.");
                if (mWriterDispatcher != null)
			    {
                    mWriterDispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
                    mWriterDispatcher = null;
			    }
                mWriterThread = null;
		    }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MidiRecorder()
        {
            Debug.Fail("MidiRecorder not disposed.");
            Dispose(false);
        }
    }
}
