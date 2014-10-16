using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace SimpleMidiRecorder
{
    class MidiExBuf : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        protected struct MIDIHDR
        {
            /// <summary>
            /// Pointer to MIDI data.
            /// </summary>
            public IntPtr data;

            /// <summary>
            /// Size of the buffer.
            /// </summary>
            public UInt32 bufferLength; 

            /// <summary>
            /// Actual amount of data in the buffer. This value should be less than 
            /// or equal to the value given in the dwBufferLength member.
            /// </summary>
            public UInt32 bytesRecorded; 

            /// <summary>
            /// Custom user data.
            /// </summary>
            public IntPtr user; 

            /// <summary>
            /// Flags giving information about the buffer.
            /// </summary>
            public UInt32 flags; 

            /// <summary>
            /// Reserved; do not use.
            /// </summary>
            public IntPtr next; 

            /// <summary>
            /// Reserved; do not use.
            /// </summary>
            public IntPtr reserved; 

            /// <summary>
            /// Offset into the buffer when a callback is performed. (This 
            /// callback is generated because the MEVT_F_CALLBACK flag is 
            /// set in the dwEvent member of the MidiEventArgs structure.) 
            /// This offset enables an application to determine which 
            /// event caused the callback. 
            /// </summary>
            public UInt32 offset; 

            /// <summary>
            /// Reserved; do not use.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst=4)]
            public IntPtr[] reservedArray; 
        }

        static readonly int sSizeOfMIDIHDR = Marshal.SizeOf(typeof(MIDIHDR));

        const int DefaultDataBufSize = 256;

        bool mMidiIn;       // True if input buffer, false if output buffer
        IntPtr mHMidi;      // Handle to the corresponding midi channel
        IntPtr mBufHdr;     // Buffer for MIDIHDR
        IntPtr mBufData;    // Buffer for data
        int mBufDataSize;   // Data Buffer size
        bool mHeaderPrepped;
        GCHandle mGch;      // Allows this to be found from unmanaged code

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private MidiExBuf(bool midiIn, IntPtr hMidi, int dataBufSize)
        {
            mMidiIn = midiIn;
            mHMidi = hMidi;
            mBufHdr = Marshal.AllocHGlobal(sSizeOfMIDIHDR);
            mBufData = Marshal.AllocHGlobal(dataBufSize);
            mBufDataSize = dataBufSize;
            mGch = GCHandle.Alloc(this, GCHandleType.Weak);

            MIDIHDR hdr = new MIDIHDR();
            hdr.data = mBufData;
            hdr.bufferLength = (UInt32)dataBufSize;
            hdr.flags = 0;
            hdr.user = GCHandle.ToIntPtr(mGch);
            Marshal.StructureToPtr(hdr, mBufHdr, false);

            if (mMidiIn)
            {
                MidiExtern.MidiThrowOnError(MidiExtern.midiInPrepareHeader(mHMidi, mBufHdr, (uint)sSizeOfMIDIHDR));
            }
            else
            {
                MidiExtern.MidiThrowOnError(MidiExtern.midiOutPrepareHeader(mHMidi, mBufHdr, (uint)sSizeOfMIDIHDR));
            }
            mHeaderPrepped = true;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (mHeaderPrepped)
            {
                if (mMidiIn)
                {
                    MidiExtern.midiInUnprepareHeader(mHMidi, mBufHdr, (uint)sSizeOfMIDIHDR);
                }
                else
                {
                    MidiExtern.midiOutUnprepareHeader(mHMidi, mBufHdr, (uint)sSizeOfMIDIHDR);
                }
                mHeaderPrepped = false;
            }
            if (mBufHdr != IntPtr.Zero)
            {
                try
                {
                    Marshal.FreeHGlobal(mBufHdr);
                }
                catch (Exception err)
                {
                    Debug.WriteLine(err.ToString());
                }
                mBufHdr = IntPtr.Zero;
            }
            if (mBufData != IntPtr.Zero)
            {
                try
                {
                    Marshal.FreeHGlobal(mBufData);
                }
                catch (Exception err)
                {
                    Debug.WriteLine(err.ToString());
                }
                mBufData = IntPtr.Zero;
                mBufDataSize = 0;
            }
            if (mGch.IsAllocated)
            {
                mGch.Free();
            }
        }

        ~MidiExBuf()
        {
            Debug.WriteLine("Failed to dispose Midi header");
            Dispose(false);
        }

        public void Reset()
        {
            // bytesRecorded is at offset 8
            Marshal.WriteInt32(mBufHdr, 8, 0);
            // flags are at offset 16
            Marshal.WriteInt32(mBufHdr, 9, 0);
        }

        // Adds the buffer to the input so that it's ready to use
        public void EnqueueBuffer()
        {
            if (!mMidiIn) throw new InvalidOperationException();
            Reset();
            MidiExtern.MidiThrowOnError(MidiExtern.midiInAddBuffer(mHMidi, mBufHdr, (uint)sSizeOfMIDIHDR));
        }

        public byte[] GetData()
        {
            // BytesRecorded is at offset 8
            int len = Marshal.ReadInt32(mBufHdr, 8);
            byte[] data = new byte[len];
            Marshal.Copy(mBufData, data, 0, len);
            return data;
        }

        public static MidiExBuf BuildInBuffer(IntPtr hMidiIn)
        {
            return new MidiExBuf(true, hMidiIn, DefaultDataBufSize);
        }

        public static MidiExBuf BuildOutBuffer(IntPtr hMidiOut, byte[] data)
        {
            MidiExBuf buf = new MidiExBuf(false, hMidiOut, data.Length);
            Marshal.Copy(data, 0, buf.mBufData, data.Length);
            return buf;
        }

        public static MidiExBuf FromHdrPtr(IntPtr midiHdrPtr)
        {
            // User variable is at offset 12 within MIDIHDR
            return (MidiExBuf)GCHandle.FromIntPtr(Marshal.ReadIntPtr(midiHdrPtr, 12)).Target;
        }
    }

    static class MidiExtern
    {
        public const UInt32 CALLBACK_NULL = 0;
        public const UInt32 CALLBACK_FUNCTION = 0x00030000;

        public enum Msg : ushort
        {
            MIM_OPEN        = 0x3C1,
            MIM_CLOSE       = 0x3C2,
            MIM_DATA        = 0x3C3,
            MIM_LONGDATA    = 0x3C4,
            MIM_ERROR       = 0x3C5,
            MIM_LONGERROR   = 0x3C6,
            MIM_MOREDATA    = 0x3CC
        }

        public const UInt32 midiInCapsSize = 44;

        [StructLayout(LayoutKind.Sequential)]
        public struct midiInCaps
        {
            /// <summary>
            /// Manufacturer identifier of the device driver for the Midi output 
            /// device. 
            /// </summary>
            public UInt16 mid;

            /// <summary>
            /// Product identifier of the Midi output device. 
            /// </summary>
            public UInt16 pid;

            /// <summary>
            /// Version number of the device driver for the Midi output device. The 
            /// high-order byte is the major version number, and the low-order byte 
            /// is the minor version number. 
            /// </summary>
            public UInt32 driverVersion;

            /// <summary>
            /// Product name.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string name;

            /// <summary>
            /// Optional functionality supported by the device. 
            /// </summary>
            public UInt32 support;
        }

        public const UInt32 midiOutCapsSize = 52;

        [StructLayout(LayoutKind.Sequential)]
        public struct midiOutCaps
        {
            /// <summary>
            /// Manufacturer identifier of the device driver for the Midi output 
            /// device. 
            /// </summary>
            public UInt16 mid;

            /// <summary>
            /// Product identifier of the Midi output device. 
            /// </summary>
            public UInt16 pid;

            /// <summary>
            /// Version number of the device driver for the Midi output device. The 
            /// high-order byte is the major version number, and the low-order byte 
            /// is the minor version number. 
            /// </summary>
            public UInt32 driverVersion;

            /// <summary>
            /// Product name.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string name;

            public UInt16 technology;
            public UInt16 voices;
            public UInt16 notes;
            public UInt16 channelMask;

            /// <summary>
            /// Optional functionality supported by the device. 
            /// </summary>
            public UInt32 support;
        }

        public delegate void MidiInProc(IntPtr handle, UInt16 msg, UInt32 instance, UInt32 param1, UInt32 param2);

        public delegate void MidiOutProc(IntPtr handle, UInt16 msg, UInt32 instance, UInt32 param1, UInt32 param2);

        [DllImport("winmm.dll")]
        public static extern UInt32 midiInGetNumDevs();

        [DllImport("winmm.dll")]
        public static extern UInt32 midiOutGetNumDevs();

        [DllImport("winmm.dll")]
        public static extern UInt32 midiInGetDevCaps(UInt32 deviceID, ref midiInCaps caps, UInt32 cbMidiInCaps);

        [DllImport("winmm.dll")]
        public static extern UInt32 midiOutGetDevCaps(UInt32 deviceID, ref midiOutCaps caps, UInt32 cbMidiInCaps);

        [DllImport("winmm.dll")]
        public static extern UInt32 midiInOpen(out IntPtr handle, UInt32 deviceId, MidiInProc callback, UInt32 instance, UInt32 flags);

        [DllImport("winmm.dll")]
        public static extern UInt32 midiOutOpen(out IntPtr handle, UInt32 deviceId, MidiOutProc callback, UInt32 instance, UInt32 flags);

        [DllImport("winmm.dll")]
        public static extern UInt32 midiInClose(IntPtr handle);

        [DllImport("winmm.dll")]
        public static extern UInt32 midiOutClose(IntPtr handle);

        [DllImport("winmm.dll")]
        public static extern UInt32 midiInPrepareHeader(IntPtr hMidiIn, IntPtr lpMidiInHdr, UInt32 cbMidiInHdr);

        [DllImport("winmm.dll")]
        public static extern UInt32 midiOutPrepareHeader(IntPtr hMidiOut, IntPtr lpMidiOutHdr, UInt32 cbMidiOutHdr);

        [DllImport("winmm.dll")]
        public static extern UInt32 midiInUnprepareHeader(IntPtr hMidiIn, IntPtr lpMidiInHdr, UInt32 cbMidiInHdr);

        [DllImport("winmm.dll")]
        public static extern UInt32 midiOutUnprepareHeader(IntPtr hMidiOut, IntPtr lpMidiOutHdr, UInt32 cbMidiOutHdr);

        [DllImport("winmm.dll")]
        public static extern UInt32 midiInAddBuffer(IntPtr hMidiIn, IntPtr lpMidiInHdr, UInt32 cbMidiInHdr);

        [DllImport("winmm.dll")]
        public static extern UInt32 midiOutShortMsg(IntPtr handle, UInt32 message);

        [DllImport("winmm.dll")]
        public static extern UInt32 midiInStart(IntPtr handle);

        [DllImport("winmm.dll")]
        public static extern UInt32 midiInStop(IntPtr handle);

        [DllImport("winmm.dll")]
        public static extern UInt32 midiInReset(IntPtr handle);

        [DllImport("winmm.dll")]
        public static extern UInt32 midiConnect(IntPtr handleIn, IntPtr handleOut, IntPtr reserved);

        public class MidiException : Exception
        {
            public MidiException(UInt32 err)
                : base(string.Format("MIDI Error 0x{0:x8}", err))
            {
            }
        }

        public static void MidiThrowOnError(UInt32 err)
        {
            if (err != 0)
            {
                MidiException e = new MidiException(err);
                Debug.WriteLine(e.ToString());
                throw e;
            }
        }

        public static int InGetNumDevs()
        {
            return (int)midiInGetNumDevs();
        }

        public static int OutGetNumDevs()
        {
            return (int)midiOutGetNumDevs();
        }

        public static midiInCaps InGetDevCaps(int deviceID)
        {
            midiInCaps caps = new midiInCaps();
            MidiThrowOnError(midiInGetDevCaps((UInt32)deviceID, ref caps, midiInCapsSize));
            return caps;
        }

        public static midiOutCaps OutGetDevCaps(int deviceID)
        {
            midiOutCaps caps = new midiOutCaps();
            MidiThrowOnError(midiOutGetDevCaps((UInt32)deviceID, ref caps, midiOutCapsSize));
            return caps;
        }

        public static IntPtr InOpen(int deviceId, MidiInProc callback, UInt32 instance)
        {
            IntPtr h;
            MidiThrowOnError(midiInOpen(out h, (UInt32)deviceId, callback, instance, (callback == null) ? CALLBACK_NULL : CALLBACK_FUNCTION));
            return h;
        }

        public static IntPtr OutOpen(int deviceId, MidiOutProc callback, UInt32 instance)
        {
            IntPtr h;
            MidiThrowOnError(midiOutOpen(out h, (UInt32)deviceId, callback, instance, (callback == null) ? CALLBACK_NULL : CALLBACK_FUNCTION));
            return h;
        }

        public static void InReset(IntPtr hIn)
        {
            UInt32 e = midiInReset(hIn);
            if (e != 0)
            {
                Debug.WriteLine("midiInReset error 0x{0:x8}", e);
            }
        }

        public static void InClose(IntPtr hIn)
        {
            UInt32 e = midiInClose(hIn);
            if (e != 0)
            {
                Debug.WriteLine("midiInClose error 0x{0:x8}", e);
            }
        }

        public static void OutClose(IntPtr hOut)
        {
            UInt32 e = midiOutClose(hOut);
            if (e != 0)
            {
                Debug.WriteLine("midiOutClose error 0x{0:x8}", e);
            }
        }

        public static void InStart(IntPtr hIn)
        {
            MidiThrowOnError(midiInStart(hIn));
        }

        public static void InStop(IntPtr hIn)
        {
            MidiThrowOnError(midiInStop(hIn));
        }

        public static void Connect(IntPtr hIn, IntPtr hOut)
        {
            MidiThrowOnError(midiConnect(hIn, hOut, (IntPtr)0));
        }
    }
}
