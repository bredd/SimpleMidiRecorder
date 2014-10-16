using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace SimpleMidiRecorder
{
    class MidiIn
    {
        public void Beep()
        {
            IntPtr h;
            uint r;
            r = MidiExtern.midiOutOpen(out h, 0, null, 0, 0);
            r = MidiExtern.midiOutShortMsg(h, 0x00403C90);             
        }

        static MidiExtern.MidiInProc thruProc;
        static IntPtr hThru;

        static void InProc(IntPtr handle, UInt16 msg, UInt32 instance, UInt32 param1, UInt32 param2)
        {
            Debug.WriteLine("InMsg {0}", msg);
            switch (msg)
            {
                case 963: // MIM_Data
                    MidiExtern.midiOutShortMsg(hThru, param1);
                    break;
            }
        }

        static void OutProc(IntPtr handle, UInt16 msg, UInt32 instance, UInt32 param1, UInt32 param2)
        {
            Debug.WriteLine("OutMsg {0}", msg);
        }

        public void Connect()
        {
            int numIn = MidiExtern.InGetNumDevs();
            for (int i=0; i<numIn; ++i)
            {
                MidiExtern.midiInCaps caps = MidiExtern.InGetDevCaps(i);
                Debug.WriteLine("{0}: {1}", i, caps.name);
            }

            int numOut = MidiExtern.OutGetNumDevs();
            for (int i = 0; i < numOut; ++i)
            {
                MidiExtern.midiOutCaps caps = MidiExtern.OutGetDevCaps(i);
                Debug.WriteLine("{0}: {1}", i, caps.name);
            }

            thruProc = InProc;
            IntPtr hIn = MidiExtern.InOpen(0, thruProc, 0);
            MidiExtern.InStart(hIn);

            IntPtr hOut = MidiExtern.OutOpen(0, OutProc, 0);
            MidiExtern.midiOutShortMsg(hOut, 0x00403C90);

            hThru = hOut; 

            //MidiExtern.Connect(hIn, hOut);

        }
    }

    /*
     * Next Steps:
     * 1. Marshal inbound events to another thread.
     * 2. Seed input with buffers.
     * 3. Trace event types
     */ 
}
