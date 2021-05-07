using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Machete
{
    /**
	 * 
	 */
    public class RecordingData
    {
        /**
	 	 * Place to store input when recording.
	 	 */
        public List<Vector> trajectory;

        /**
	 	 * Place to store timestamps when recording.
	 	 */
        public List<float> timestamps;

        /**
		 * Index into trajectory when replaying sample.
		 */
        public int replayIdx;

        public string Gid { get; set; }

        /**
	 	 * Place to store the speed of this sample when recording.
	 	 */
        public int Speed { get; private set; }

        public RecordingData(
            List<Vector> trajectory, 
            List<float> timestamps, 
            int speed, 
            string gid = "", 
            int replayIdx = 0)
        {
            this.trajectory = trajectory;
            this.timestamps = timestamps;
            this.Speed = speed;
            this.Gid = gid;
            this.replayIdx = replayIdx;
        }

        public void Save(string fname)
        {
            // Create the directories if necessary.
            string dname = System.IO.Path.GetDirectoryName(fname);
            System.IO.Directory.CreateDirectory(dname);

            // Write out the sample.
            BinaryWriter bstream;

            // Open up the binary stream.
            bstream = new BinaryWriter(
                new FileStream(
                    fname,
                    FileMode.Create));

            Debug.Assert(bstream != null);

            Save(bstream);

            // Clean up.
            bstream.Close();
        }

        public void Save(BinaryWriter bstream)
        {
            /*
             * Format:
             *    - gid
             *    - speed
             *    - trajectories
             *    - timestamps
             */

            // Write the gesture name
            bstream.Write(Gid);

            // Write the gesture speed
            bstream.Write(Speed);

            // Write the trajectory
            SampleUtils.WriteTrajectory(bstream, trajectory);

            // Write timestamps now...
            // Write header.
            bstream.Write((int)timestamps.Count);

            // Write each timestamp
            for (int ii = 0; ii < timestamps.Count; ii++)
                bstream.Write(timestamps[ii]);
        }

        public static RecordingData Load(
            string fname, 
            bool removeDuplicates = false,
            DeviceType deviceType = default,
            int subjectId = -1,
            bool isSession = false)
        {
            // Read the sample.
            BinaryReader bstream;

            try
            {
                // Open up the binary stream.
                bstream = new BinaryReader(
                                new FileStream(
                                    fname,
                                    FileMode.Open));
            }
            catch (Exception)
            {
                Debug.Log("Unable to load sample");
                return null;
            }

            var ret = Load(
                bstream, 
                removeDuplicates: removeDuplicates,
                deviceType: deviceType,
                external_PID: subjectId,
                isSession: isSession);

            // Clean up.
            bstream.Close();

            return ret;
        }

        public static RecordingData Load(
            BinaryReader bstream, 
            bool removeDuplicates, 
            HashSet<int> removedDataIndices = null,
            DeviceType deviceType = default,
            int external_PID = -1,
            bool isSession = false)
        {
            // Read gid
            string gid = bstream.ReadString();

            // Read speed
            int speed = bstream.ReadInt32();

            HashSet<int> removedIndices = new HashSet<int>();

            // Read the trajectory
            List<Vector> trajectory = SampleUtils.ReadTrajectory(
                bstream, 
                removeDuplicates,
                deviceType: deviceType,
                external_PID: external_PID,
                isSession: isSession,
                removedIndices: removedIndices);

            // Read the timestamps
            int tcnt = bstream.ReadInt32();
            List<float> timestamps = new List<float>();

            for (int i = 0; i < tcnt; i++)
            {
                float value = bstream.ReadSingle();

                // Discard it if the corresponding trajectory was also removed
                if (!removedIndices.Contains(i))
                    timestamps.Add(value);
            }

            // Adjust the frame time offsets
            timestamps = timestamps.Select(item => item - timestamps[0]).ToList();

            if (removedDataIndices != null)
                foreach (var index in removedIndices)
                    removedDataIndices.Add(index);

            return new RecordingData(
                trajectory,
                timestamps,
                speed,
                gid);
        }
    };
}
