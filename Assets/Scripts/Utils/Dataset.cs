/**
 * Copyright 2017 
 * Eugene M. Taranta <etaranta@gmail.com>
 * Amirreza Samiei <samiei@knights.ucf.edu>
 * Mehran Maghoumi <mehran@cs.ucf.edu>
 * Pooya Khaloo <pooya@cs.ucf.edu>
 * Corey R. Pittman <cpittman@knights.ucf.edu>
 * Joseph J. LaViola Jr. <jjl@cs.ucf.edu>
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
 * copies of the Software, and to permit persons to whom the Software is 
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Machete
{

    public class Dataset
    {
        /**
         * Enumeration of gesture string names.
         */
        public List<string> Gestures { get; set; }

        /**
         * Enumeration of subject names.
         */
        public List<string> Subjects { get; set; }

        /**
         * List of all samples from the dataset.
         */
        public List<Sample> Samples { get; set; }

        /**
         * Use samples_by_gesture[gesture_id] to get all the samples
         * from a particular gesture class.
         */
        public List<List<Sample>> SamplesByGesture { get; set; }

        /**
         *
         */
        public Dataset()
        {
            Gestures = new List<string>();
            Subjects = new List<string>();
            Samples = new List<Sample>();
            SamplesByGesture = new List<List<Sample>>();
        }

        /**
         * Add gesture name to database if it does not
         * already exist and returns its enumerated id.
         */
        public int AddGesture(string gname)
        {
            int i;

            for (i = 0; i < Gestures.Count; i++)
            {
                if (Gestures[i] == gname)
                    return i;
            }

            // push gesture name into list and make room
            // for sample list in samples_by_gesture, which
            // will be populated later add sample is called
            Gestures.Add(gname);
            SamplesByGesture.Add(new List<Sample>());
            return i;
        }

        /**
         * Add subject to database if it does not already exist
         * and returns their enumerated id.
         */
        public int AddSubject(string sname)
        {
            int i;

            for (i = 0; i < Subjects.Count; i++)
                if (Subjects[i] == sname)
                    return i;

            Subjects.Add(sname);
            return i;
        }

        /**
         * Add a new sample to the dataset. The gesture name
         * and subject must have already been added.
         */
        public void AddSample(Sample sample, int subject_id, int gesture_id)
        {
            Samples.Add(sample);

            if (gesture_id >= SamplesByGesture.Count)
                throw new Exception("gesture_id >= SamplesByGesture.Count");

            SamplesByGesture[gesture_id].Add(sample);
        }

        /**
         *
         */
        public int GestureNameToId(string gname)
        {
            for (int i = 0; i < Gestures.Count; i++)
            {
                if (Gestures[i] == gname)
                    return i;
            }

            return -1;
        }

        /**
         *
         */
        public void DumpCatalog()
        {
            Debug.Log("Subject Count: " + Subjects.Count);
            Debug.Log("Sample Count: " + Samples.Count);
            Debug.Log("Gesture Count: " + SamplesByGesture.Count);

            for (int i = 0; i < SamplesByGesture.Count; i++)
            {
                string gname = Gestures[i];
                Debug.Log(gname + ": " + SamplesByGesture[i].Count);
            }
        }

        /**
         *
         */
        public static Sample LoadSampleFile(
            string path,
            int subject_id, 
            int gesture_id,
            DeviceType deviceType = default,
            bool isSession = false)
        {
            Sample ret = new Sample(subject_id, gesture_id, 0);
            
            List<float> timeS;
            List<Vector> trajectory = SampleUtils.LoadSample(
                path, 
                out timeS,
                deviceType: deviceType,
                isSession: isSession);
            
            ret.AddTrajectory(trajectory);
            ret.AddTimeStamps(timeS);

            return ret;
        }

        /**
         * Recurse through a *subject* directory and load up all of the samples:
         * <path>/sub_* /gesture name/ex_*
         *
         * Also, all subject and gesture names are recorded and enumerated.
         *
         * This function can be called multiple times with to build a single data set.
         * First call with dataset=NULL to allocate a new Dataset and then pass in
         * on subsequent calls.
         */
        public static Dataset LoadSubjectDataset(
            Dataset dataset, 
            string subject_path,
            DeviceType deviceType = default,
            bool isSession = false)
        {
            // the path and gesture name
            List<Tuple<string, string>> gestures = new List<Tuple<string, string>>();
            Dataset ret = dataset; // alias

            //
            // Create a new data set if required.
            //
            if (ret == null)
            {
                ret = new Dataset();
            }

            //
            // Get subject name from path string ".../Sub_<subject_name>".
            //
            int idx;
            idx = subject_path.IndexOf("Sub_");

            if (idx == -1)
            {
                Console.WriteLine("exit 0\n");
                return ret;
            }

            idx += 4;

            //
            // Add subject to database, get sub id.
            //
            string tmp = subject_path.Substring(idx);
            string subject_name = tmp;
            int subject_id = ret.AddSubject(subject_name);

            //
            // Find all gesture directories.
            //
            if (!Directory.Exists(subject_path))
                throw new Exception("Directory does not exist");

            var dirs = Directory.GetDirectories(subject_path);

            foreach (var dir in dirs)
            {
                string gname = new DirectoryInfo(dir).Name;

                gestures.Add(new Tuple<string, string>(dir, gname));
            }

            //
            // Load each gesture directory.
            //
            for (int ii = 0; ii < gestures.Count; ii++)
            {
                List<string> sample_paths = new List<string>();

                // Get a gesture ID.
                int gesture_id = ret.AddGesture(gestures[ii].Second);

                //
                // Now find all examples.
                //
                var path = gestures[ii].First;
                var files = Directory.GetFiles(path);
                foreach (var file in files)
                {
                    string sname = Path.GetFileNameWithoutExtension(file);
                    
                    if (!sname.StartsWith("ex"))
                        continue;

                    if (Path.GetFileName(file).Contains(".meta"))
                        continue;

                    sample_paths.Add(file);
                }

                //
                // Again, we prefer to have samples sorted.
                //
                sample_paths.Sort();

                for (int sample_no = 0; sample_no < sample_paths.Count; sample_no++)
                {
                    

                    //
                    // finally! load a sample file
                    //
                    Sample sample;
                    sample = LoadSampleFile(
                        sample_paths[sample_no],
                        subject_id, 
                        gesture_id,
                        deviceType: deviceType,
                        isSession: isSession);

                    if (sample == null)
                        continue;

                    sample.GestureName = gestures[ii].Second;

                    ret.AddSample(
                        sample,
                        subject_id,
                        gesture_id);
                }
            }

            return ret;
        }

        
        public static Dataset LoadDataset(string path)
        {
            List<string> subject_paths = new List<string>();
            Dataset ret = new Dataset();

            //
            // Find all subject directories.
            //
            if (!Directory.Exists(path))
                throw new Exception("Path does not exist: " + path);

            var dirs = Directory.GetDirectories(path);

            foreach (var dir in dirs)
            {
                string folderName = new DirectoryInfo(dir).Name;
                if (!folderName.StartsWith("Sub_"))
                    continue;
                subject_paths.Add(dir);
            }

            subject_paths.Sort();

            //
            // Load the subject directories.
            //
            for (int ii = 0; ii < subject_paths.Count; ii++)
            {
                ret = LoadSubjectDataset(ret, subject_paths[ii]);
            }

            return ret;
        }
    }

    /**
    * Helper tuple class.
    */
    public class Tuple<T1, T2>
    {
        public T1 First { get; set; }

        public T2 Second { get; set; }

        internal Tuple(T1 first, T2 second)
        {
            First = first;
            Second = second;
        }
    }
}