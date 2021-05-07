using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Machete
{
    /// <summary>
    /// Defines the data stored when a full session is recorded.
    /// </summary>
    public class RecordedSession
    {
        /// <summary>
        /// The data of the leader
        /// </summary>
        public RecordingData LeaderData { get; set; }

        /// <summary>
        /// The data of the player.
        /// </summary>
        public RecordingData PlayerData { get; set; }

        /// <summary>
        /// The command that was displayed on the screen (repeat, gid).
        /// </summary>
        public List<Tuple<int, string>> Commands { get; set; }

        /// <summary>
        /// The name of the session
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The speed of the recording
        /// </summary>
        public int Speed { get; set; }

        private RecordedSession(
            string name, 
            RecordingData leaderData, 
            RecordingData playerData, 
            List<Tuple<int, string>> commands, 
            int speed)
        {
            this.Name = name;
            this.LeaderData = leaderData;
            this.PlayerData = playerData;
            this.Commands = commands;
            this.Speed = speed;
        }

        public RecordedSession(
            string name, 
            int speed) : 
            this(
                name, 
                new RecordingData(
                    new List<Vector>(), 
                    new List<float>(),
                    1), /* Leader data does not need speed */
                new RecordingData(
                    new List<Vector>(), 
                    new List<float>(), 
                    speed),
                new List<Tuple<int, string>>(), 
                speed) 
        {
            // nothing to do
        }

        public void AddCommand(
            int reps, 
            string command)
        {
            Commands.Add(new Tuple<int, string>(reps, command));
        }

        /// <summary>
        /// Saves this session to a file.
        /// </summary>
        /// <param name="fname">The output file.</param>
        public void Save(string fname)
        {
            /**
             * Save format:
             *    - Session name
             *    - Speed
             *    - Leader trajectory
             *    - Participant trajectory
             *    - Commands
             */

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

            // Write name
            bstream.Write(Name);

            // Write speed
            bstream.Write(Speed);

            // Write leader data
            LeaderData.Save(bstream);

            // Write player data
            PlayerData.Save(bstream);

            // Write gid's now...
            // Write header.
            bstream.Write((int)Commands.Count);

            // Write each gid
            for (int ii = 0; ii < Commands.Count; ii++)
            {
                bstream.Write(Commands[ii].First);
                bstream.Write(Commands[ii].Second);
            }

            // Clean up.
            bstream.Close();
        }

        public static RecordedSession Load(
            string fname, 
            bool removeDuplicates = false,
            DeviceType deviceType = default, 
            bool isSession = false,
            int external_PID = -1)
        {
            // Read the sample.
            BinaryReader bstream;

            // Open up the binary stream.
            bstream = new BinaryReader(
                new FileStream(
                    fname,
                    FileMode.Open));

            if (bstream == null)
            {
                Debug.Log("Loading the recorded session failed!");
                return null;
            }

            // Read session name
            string name = bstream.ReadString();

            // Read speed
            int speed = bstream.ReadInt32();

            // Read leader data
            RecordingData leader = RecordingData.Load(
                bstream,
                removeDuplicates,
                deviceType: deviceType,
                external_PID: external_PID,
                isSession: isSession);
            
            // Read player data and account for removed indices
            HashSet<int> removedIndices = new HashSet<int>();

            RecordingData player = RecordingData.Load(
                bstream: bstream, 
                removeDuplicates, 
                removedIndices,
                deviceType: deviceType,
                external_PID: external_PID,
                isSession: isSession);

            // Read gid's
            int frameCount = bstream.ReadInt32();
            List<Tuple<int, string>> commands = new List<Tuple<int, string>>();

            for (int i = 0; i < frameCount; i++)
                if (!removedIndices.Contains(i))
                    commands.Add(
                        new Tuple<int, string>(
                            bstream.ReadInt32(), 
                            bstream.ReadString()));

            // Clean up.
            bstream.Close();

            return new RecordedSession(
                name, 
                leader, 
                player, 
                commands, 
                speed);
        }
    }
}
