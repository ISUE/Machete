using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Machete
{
    public class Sample
    {
        public int SubjectId { get; set; }

        public int GestureId { get; set; }

        public string GestureName { get; set; }

        public int InstanceId { get; set; }

        public int SampleId { get; set; }

        public List<Vector> Trajectory { get; set; }

        public List<Vector> FilteredTrajectory { get; set; }

        public List<float> TimeS { get; set; }

        public Sample(int subjectId, int gestureId, int instanceId)
        {
            SubjectId = subjectId;
            GestureId = gestureId;
            InstanceId = instanceId;

            Trajectory = new List<Vector>();
            TimeS = new List<float>();
            FilteredTrajectory = new List<Vector>();
        }

        public void AddTrajectory(List<Vector> trajectory)
        {
            for (int i = 0; i < trajectory.Count; i++)
            {
                Trajectory.Add(trajectory[i].Clone() as Vector);
            }
        }

        public void AddTimeStamps(List<float> _timeS)
        {
            for (int i = 0; i < _timeS.Count; i++)
            {
                TimeS.Add(_timeS[i]);
            }
        }

        public void AddFilteredTrajectory(List<Vector> _filtered)
        {
            for (int i = 0; i < _filtered.Count; i++)
            {
                FilteredTrajectory.Add(_filtered[i].Clone() as Vector);
            }
        }

        public Sample Clone()
        {
            Sample ret = new Sample(
                this.SubjectId,
                this.GestureId,
                this.InstanceId
                );

            ret.AddTrajectory(this.Trajectory);
            ret.AddTimeStamps(this.TimeS);
            ret.AddFilteredTrajectory(this.FilteredTrajectory);

            return ret;
        }
    }
}
