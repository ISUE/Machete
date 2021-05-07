using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace Machete
{
    public class SampleUtils
    {
        /**
         * Load sample from file.
         */
        static public List<Vector> LoadSample(
            string fname,
            out List<float> _timeS,
            bool remove_duplicates = false,
            DeviceType deviceType = default,
            bool isSession = false)
        {
            RecordingData rd =
                RecordingData.Load(
                    fname,
                    remove_duplicates,
                    deviceType: deviceType,
                    isSession: isSession);

            _timeS = rd.timestamps;

            return rd.trajectory;
        }

        /**
        *  Writes the provided trajectory to the binary stream.
        */
        public static void WriteTrajectory(BinaryWriter bstream, List<Vector> trajectory)
        {
            // Write header.
            bstream.Write((int) trajectory.Count);
            bstream.Write((int) trajectory[0].Size);

            // Write points.
            for (int ii = 0;
                ii < trajectory.Count;
                ii++)
            {
                // Write components of point.
                for (int jj = 0;
                    jj < trajectory[ii].Size;
                    jj++)
                {
                    bstream.Write((float) trajectory[ii][jj]);
                }
            }
        }

        /**
         * This call affects only VIVE_POSITION and VIVE_QUATERNION devices
         * and is safe to call on trajectories of other samples and sessions
         */
        public static Vector PostProcessPoint(
            Vector pt,
            DeviceType deviceType,
            int subjectId,
            bool session)
        {
            Vector returnPt = null;

            switch (deviceType)
            {
                case DeviceType.KINECT:
                case DeviceType.MOUSE:
                    return pt;

                // Vive needs some additional processing
                case DeviceType.VIVE_POSITION:
                case DeviceType.VIVE_QUATERNION:
                    if (session && subjectId == 7)
                    {
                        // Swap 7th session left and right controllers
                        double[] tmp = new double[7];

                        tmp[0] = pt[0];
                        tmp[1] = pt[1];
                        tmp[2] = pt[2];

                        tmp[3] = pt[3];
                        tmp[4] = pt[4];
                        tmp[5] = pt[5];
                        tmp[6] = pt[6];

                        // c0
                        pt[0] = pt[7];
                        pt[1] = pt[8];
                        pt[2] = pt[9];

                        pt[3] = pt[10];
                        pt[4] = pt[11];
                        pt[5] = pt[12];
                        pt[6] = pt[13];

                        // cq
                        pt[7] = tmp[0];
                        pt[8] = tmp[1];
                        pt[9] = tmp[2];

                        pt[10] = tmp[3];
                        pt[11] = tmp[4];
                        pt[12] = tmp[5];
                        pt[13] = tmp[6];
                    }

                    if (deviceType == DeviceType.VIVE_POSITION)
                    {
                        pt[3] = pt[7];
                        pt[4] = pt[8];
                        pt[5] = pt[9];
                    }

                    if (deviceType == DeviceType.VIVE_QUATERNION)
                    {
                        pt[0] = pt[3];
                        pt[1] = pt[4];
                        pt[2] = pt[5];
                        pt[3] = pt[6];

                        pt[4] = pt[10];
                        pt[5] = pt[11];
                        pt[6] = pt[12];
                        pt[7] = pt[13];
                    }

                    break;
                default:
                    return pt;
            }

            // making new ones to overwrite memory
            if (deviceType == DeviceType.VIVE_QUATERNION)
            {
                returnPt = new Vector(8);
                returnPt[0] = pt[0];
                returnPt[1] = pt[1];
                returnPt[2] = pt[2];
                returnPt[3] = pt[3];
                returnPt[4] = pt[4];
                returnPt[5] = pt[5];
                returnPt[6] = pt[6];
                returnPt[7] = pt[7];
            }

            if (deviceType == DeviceType.VIVE_POSITION)
            {
                returnPt = new Vector(6);
                returnPt[0] = pt[0];
                returnPt[1] = pt[1];
                returnPt[2] = pt[2];
                returnPt[3] = pt[7];
                returnPt[4] = pt[8];
                returnPt[5] = pt[9];
            }

            return returnPt;
        }
        
        /**
         * Fix Discontinuity for Quaternion
         * Given a current and a previous quaternion set,
         * negate components when it results in smaller jumps
         */
        public static List<Vector> PostProcessQuaternionTrajectory(
            List<Vector> inputTrajectory,
            DeviceType deviceType,
            int subjectId,
            bool isSession = false)
        {
            Vector pt = inputTrajectory[0];
            for (int i = 1; i < inputTrajectory.Count; i++)
            {
                for (int c = 0; c < 2; c++)
                {
                    double d = pt[4 * c + 0] * inputTrajectory[i][4 * c + 0]
                               + pt[4 * c + 1] * inputTrajectory[i][4 * c + 1]
                               + pt[4 * c + 2] * inputTrajectory[i][4 * c + 2]
                               + pt[4 * c + 3] * inputTrajectory[i][4 * c + 3];
                    if (d < -d)
                    {
                        inputTrajectory[i][c * 4 + 0] = -inputTrajectory[i][c * 4 + 0];
                        inputTrajectory[i][c * 4 + 1] = -inputTrajectory[i][c * 4 + 1];
                        inputTrajectory[i][c * 4 + 2] = -inputTrajectory[i][c * 4 + 2];
                        inputTrajectory[i][c * 4 + 3] = -inputTrajectory[i][c * 4 + 3];
                    }
                }

                pt = inputTrajectory[i];
            }
            return inputTrajectory;
        }

        public static List<Vector> ReadTrajectory(
            BinaryReader bstream,
            bool removeDuplicates,
            DeviceType deviceType,
            bool isSession,
            HashSet<int> removedIndices = null,
            int external_PID = -1)
        {
            List<Vector> ret = new List<Vector>();

            // Load header.
            int ptCnt = bstream.ReadInt32();
            int componentCnt = bstream.ReadInt32();

            // Load points.
            for (int ii = 0; ii < ptCnt; ii++)
            {
                List<double> temp = new List<double>();

                // Load components of point.
                for (int jj = 0; jj < componentCnt; jj++)
                    temp.Add(bstream.ReadSingle());

                Vector pt = new Vector(temp);

                if ((ret.Count > 0) && (removeDuplicates == true))
                    if (pt == ret[ret.Count - 1])
                    {
                        if (removedIndices != null)
                            removedIndices.Add(ii);

                        continue;
                    }

                if (pt.isZero())
                {
                    if (removedIndices != null)
                        removedIndices.Add(ii);

                    continue;
                }

                pt = PostProcessPoint(pt, deviceType, subjectId: external_PID, true);
                ret.Add(pt);
            }


            // Further Post Process Sample Trajectory
            if (deviceType == DeviceType.VIVE_QUATERNION)
                ret = PostProcessQuaternionTrajectory(
                    ret,
                    deviceType,
                    external_PID,
                    isSession);

            return ret;
        }
    }
}