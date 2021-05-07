using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Debug = UnityEngine.Debug;

namespace Machete
{
    public class Global
    {
        public static string App_datapath;

        public static string DeviceString(DeviceType deviceType)
        {
            if (deviceType == DeviceType.KINECT) return "kinect";
            if (deviceType == DeviceType.MOUSE) return "mouse";
            if (deviceType == DeviceType.VIVE_POSITION) return "vive";
            if (deviceType == DeviceType.VIVE_QUATERNION) return "vive";
            return null;
        }
        
        /*
         * Get string where the dataset is stored
         */
        public static string GetDevicePath(DeviceType deviceType, bool forTraining)
        {
            string returnPath = $"/../datasets/{DeviceString(deviceType)}/";

            if (forTraining)
                returnPath += "training/Sub_u";
            else
                returnPath += "sessions/Sub_u";

            return returnPath;
        }

        /**
         * Get list of participants for specific device
         */
        public static List<int> GetParticipantList(DeviceType deviceType)
        {
            switch (deviceType)
            {
                case DeviceType.KINECT:
                    return new List<int> {15, 16, 17, 18, 19, 20, 21, 22, 23, 24};
                case DeviceType.MOUSE:
                    return new List<int> {30, 31, 33, 36, 42, 43, 44, 46, 48, 49};
                case DeviceType.VIVE_POSITION:
                    return new List<int> {1, 2, 4, 5, 7, 8, 9, 10, 11, 12};
                case DeviceType.VIVE_QUATERNION:
                    return new List<int> {1, 2, 4, 5, 7, 8, 9, /**/ 11, 12};
                default:
                    Debug.Log("Device Participant list not available");
                    break;
            }

            return null;
        }
        
        
        /*
         * Get the found known best thresholds for known datasets
         */
        public static double GetDatasetThreshold(DeviceType deviceType)
        {
            switch (deviceType)
            {
                case DeviceType.KINECT:
                    return 7.0;
                case DeviceType.MOUSE:
                    return 7.0;
                case DeviceType.VIVE_POSITION:
                    return 7.0;
                case DeviceType.VIVE_QUATERNION:
                    return 5.5;
                default:
                    return 10;
            }
        }
        

        /**
         * Load train_count number of Samples of each class 
         */
        public static List<Sample> GetTrainSet(
            Dataset dataset,
            int trainCount)
        {
            List<Sample> trainSet = new List<Sample>();

            for (int ii = 0; ii < dataset.Gestures.Count; ii++)
            {
                trainSet.AddRange(dataset.SamplesByGesture[ii].OrderBy(x => Guid.NewGuid()).Take(trainCount)
                    .ToList());
            }

            return trainSet;
        }

        public static Dataset load_subject_dataset(
            DeviceType deviceType,
            int subjectID)
        {
            string path = App_datapath;
            path += GetDevicePath(deviceType, forTraining: true);
            path += $"{subjectID:D3}";

            Dataset ds = new Dataset();
            ds = Dataset.LoadSubjectDataset(
                ds,
                path,
                deviceType: deviceType,
                isSession: false);
            ds = CentralMovingAverage.FilterDataset(deviceType, ds);

            return ds;
        }

        public static void load_session(
            DeviceType deviceType,
            int external_PID,
            List<Frame> frames,
            Dataset ds)
        {
            string sessionPath = App_datapath;
            sessionPath += GetDevicePath(deviceType, forTraining: false);
            sessionPath += $"{external_PID:D3}";
            sessionPath += "/session_1";

            RecordedSession rs = RecordedSession.Load(
                sessionPath, 
                deviceType: deviceType,
                isSession: true,
                external_PID: external_PID);

            List<Tuple<int, string>> command = rs.Commands;
            List<Vector> trajectory = rs.PlayerData.trajectory;
            List<float> timestamps = rs.PlayerData.timestamps;

            for (int i = 0; i < trajectory.Count; i++)
            {
                Frame frame = new Frame(
                    trajectory[i],
                    timestamps[i],
                    command[i].Second,
                    ds.GestureNameToId(command[i].Second),
                    command[i].First);

                frames.Add(frame);
            }
        }

        public static double EstimateFPS(List<Sample> samples)
        {
            List<double> fps = new List<double>();
            for (int sid = 0; sid < samples.Count; sid++)
            {
                List<float> times_s = samples[sid].TimeS;

                for (int ii = 1; ii < times_s.Count; ii++)
                {
                    double period = times_s[ii] - times_s[ii - 1];
                    fps.Add(1.0f / period);
                }
            }

            return fps.Average();
        }

        public static double GetFPS(DeviceType device, List<Sample> samples)
        {
            double fps = EstimateFPS(samples);

            if (device == DeviceType.MOUSE)
            {
                fps = 75;
            }
            else if (88.0 < fps && fps < 92.0)
            {
                fps = 90;
            }
            else if (28.0 < fps && fps < 32.0)
            {
                fps = 30;
            }
            else
            {
                Debug.LogWarning("Something's wrong with fps");
            }

            return fps;
        }

        public static int GetResampleCnt(DeviceType deviceType)
        {
            switch (deviceType)
            {
                case DeviceType.KINECT:
                    return 20;
                case DeviceType.VIVE_POSITION:
                    return 32;
                case DeviceType.VIVE_QUATERNION:
                    return 16;
                case DeviceType.MOUSE:
                    return 96;
                default:
                    return 20;
            }
        }
    }
}