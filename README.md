SimpleMidiRecorder
==================
This is a MIDI Recorder for Windows that I created to capture and archive recordings from my PianoDisc PDS-128Plus player piano system.

Our PDS-128 is equipped with the record option and we have a number of recordings of family members and friends. Unfortunately, the device records to 3.5" floppy disks in a proprietary format that cannot be read on a PC without special software most of which is incompatible with recent versions of Windows (Vista, Windows 7, Windows 8, etc.). The floppies are aging and I fear losing priceless family memories.

My solution to this is to link the PDS-128 controller to a laptop using an inexpensive MIDI cable. Then use this recording program to capture the MIDI information as a disk is played on the PDS-128. One floppy disk typically contains multiple recordings. Conveniently, the PDS-128 controller sends out some MIDI control commands between tracks. Accordingly, this recording program is able to automatically detect breaks between pieces and place each track in a separate Standard MIDI File (.mid extension).

Since the PDS-128 is capable of playing commercially produced disks from other brands such as Yamaha Disclavier this also is a universal solution for archiving all of the disks we have.

## How To Use
1. Purchase an inexpensive MIDI to USB cable like [this one](http://www.amazon.com/gp/product/B00ACGMOA6/ref=as_li_qf_sp_asin_il_tl?ie=UTF8&camp=1789&creative=9325&creativeASIN=B00ACGMOA6&linkCode=as2&tag=ofthacom-20&linkId=TOIU6WFT72WX4PCQ).
2. Connect the "In" plug on the cable to the "MIDI Out" socket on the back of the PDS-128.
3. Turn on the PDS-128
4. Insert the disk to be transferred into the PDS-128.
5. Press "Mode" twice, then press 1 for "MIDI", then press 3 for "RDIR", then press 3 to "Redirect PianoOut to Midi Out". (After making recordings, remember to clear this setting for normal piano operation.)
6. On your PC, start "SimpleMidiRecorder"
7. Click the "Begin Recording" button.
8. Select the MIDI folder where all of your recordings will land. SimpleMidi recorder will create a subfolder within the specified folder for each full disk that you transfer. Within that folder will be one .mid file for each track on the disk.
9. Optionally enter Album, Artist, Genre, and Names for each of the tracks.
10. Click "OK"
11. Press "Play" on the PDS-128 and let it run to the end of the disk. All tracks will be recorded.

## Notes
* If you want to use the PDS-128 away from the piano, you'll need a 9-volt power supply with the right connector. I use [this universal supply](http://www.amazon.com/gp/product/B000Z31G3M/ref=as_li_qf_sp_asin_il_tl?ie=UTF8&camp=1789&creative=9325&creativeASIN=B000Z31G3M&linkCode=as2&tag=ofthacom-20&linkId=QE5HOEAUFPEXSRIU). One of the included tips fits properly. Make sure you set it up with the tip positive and the ring negative and set the supply to 9 volts.

