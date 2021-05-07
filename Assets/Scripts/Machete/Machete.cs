using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Machete {

    public class Machete
    {
        public List<MacheteTemplate> templates;

        public double bestScore;

        public MacheteTemplate bestTemplate;

        public int lastFrameNo;

        public DeviceType device_type;

        public double device_fps;

        /**
         * Store buffer of data stream
         */
        public CircularBuffer<Vector> buffer;

        public Vector last_pt;

        public ContinuousResultOptions cr_options;

        protected List<Sample> trainingSet;

        public Machete(
            DeviceType _device_type,
            ContinuousResultOptions _cr_options
            )
        {
            device_type = _device_type;
            cr_options = _cr_options;
            buffer = new CircularBuffer<Vector>();
            templates = new List<MacheteTemplate>();
            trainingSet = new List<Sample>();
            lastFrameNo = -1;
            device_fps = -1;
        }

        public List<Sample> GetTrainingSet()
        {
            return trainingSet;
        }

        public ContinuousResultOptions GetCROptions()
        {
            return cr_options;
        }

        public void Clear()
        {
            for (int tt = 0; tt < templates.Count; tt++)
            {

            }

            templates.Clear();
            lastFrameNo = -1;
        }

        public void AddSample(Sample sample, bool filtered)
        {
            // Make sure buffer is sufficiently large to
            // store a very slow version of this sample.
            int size = sample.Trajectory.Count * 5;
            if (size > buffer.Size())
                buffer.Resize(size);

            templates.Add(
                new MacheteTemplate(
                    device_type,
                    cr_options,
                    sample,
                    filtered)
                    );

            trainingSet.Add(sample);
            Reset();
        }

        public void Reset()
        {
            for (int ii = 0; ii < templates.Count; ii++)
            {
                templates[ii].Reset();
            }

            buffer.Clear();
        }

        public void Segmentation(
            double score,
            int head,
            int tail
            )
        {
            score = bestScore;

            head = -1;
            tail = -1;

            if (bestTemplate.trigger.check == true)
            {
                head = bestTemplate.trigger.start;
                tail = bestTemplate.trigger.end;
            }
        }

        // Originally overloaded operator ()
        public void ProcessFrame(
            Vector pt,
            int frame_no,
            List<ContinuousResult> results
            )
        {
            if (lastFrameNo == -1)
            {
                last_pt = pt;
            }

            // Update Circular Buffer
            while (lastFrameNo < frame_no)
            {
                buffer.Insert(pt);
                lastFrameNo++;
            }

            Vector vec = pt - last_pt;
            double segment_length = vec.L2Norm();

            if (device_type == DeviceType.MOUSE && segment_length < 10.0)
            {
                return;
            }

            last_pt = pt;

            if (segment_length <= double.Epsilon)
            {
                return;
            }

            vec /= segment_length;

            for (int ii = 0; ii < templates.Count; ii++)
            {
                templates[ii].Update(
                    buffer,
                    pt,
                    vec,
                    frame_no,
                    segment_length
                    );

                results.Add(templates[ii].result);
            }
        }
    }
}