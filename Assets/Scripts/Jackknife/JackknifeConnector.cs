using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JackknifeConnector
{
    /**
     * This might just be the worst piece of code that 
     * I will ever write in my life
     */
    public static List<Jackknife.Sample> GetJKTrainSet(List<Machete.Sample> in_samples)
    {
        List<Jackknife.Sample> samples = new List<Jackknife.Sample>();
            
        foreach (Machete.Sample y_s in in_samples)
        {
            // Copy sample ids
            Jackknife.Sample s = new Jackknife.Sample(
                    y_s.SubjectId,
                    y_s.GestureId,
                    0);

            // Copy sample trajectory
            List<Jackknife.Vector> newTrajectory = new List<Jackknife.Vector>();

            // Go through each vector
            foreach (Machete.Vector yeahSampleTrajectoryV in y_s.FilteredTrajectory) 
            {
                List<double> JKVectorData = new List<double>();

                // Go through all data in vector
                foreach (double data in yeahSampleTrajectoryV.Data)
                {
                    JKVectorData.Add(data);
                }
                Jackknife.Vector v = new Jackknife.Vector(JKVectorData);

                newTrajectory.Add(v);
            }

            s.AddTrajectory(newTrajectory);

            samples.Add(s);
        }

        return samples;
    }

    public static List<Jackknife.Vector> GetJKBufferFromVideo(
        List<Machete.Vector> video,
        int startFrameNo,
        int endFrameNo
        )
    {
        List<Jackknife.Vector> buffer = new List<Jackknife.Vector>();
        for (int ii = startFrameNo; ii <= endFrameNo; ii++)
        {
            Machete.Vector v = video[ii];

            // Go through all data in vector
            List<double> JKVectorData = new List<double>();
            foreach (double data in v.Data)
            {
                JKVectorData.Add(data);
            }
            Jackknife.Vector jv = new Jackknife.Vector(JKVectorData);
            buffer.Add(jv);
        }

        return buffer;
    }

    public static List<Jackknife.Vector> GetJKBuffer(List<Machete.Vector> buffer)
    {
        List<Jackknife.Vector> retpts = new List<Jackknife.Vector>();

        foreach (Machete.Vector v in buffer)
        {
            List<double> JKVectorData = new List<double>();

            // Go through all data in vector
            foreach (double data in v.Data)
            {
                JKVectorData.Add(data);
            }
            Jackknife.Vector jv = new Jackknife.Vector(JKVectorData);
            retpts.Add(jv);
        }

        return retpts;
    }

    public static Jackknife.Vector ToJKVector(Machete.Vector v)
    {
        List<double> JKVectorData = new List<double>();
        // Go through all data in vector
        foreach (double data in v.Data)
        {
            JKVectorData.Add(data);
        }
        Jackknife.Vector jkv = new Jackknife.Vector(JKVectorData);

        return jkv;
    }
}