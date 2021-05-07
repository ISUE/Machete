using System.Collections.Generic;

namespace Machete
{
    public class GestureCommand
    {
        public int gid;
        public int start;
        public int end;
        public bool detected = false;

        public void Reset()
        {
            detected = false;
        }

        public bool Hit(ContinuousResult detection)
        {
            if (gid != detection.gid)
                return false;

            if (start > detection.endFrameNo)
                return false;

            if (end < detection.startFrameNo)
                return false;

            return true;
        }

        public bool Hit(RecognitionResult result)
        {
            if (gid != result.gid)
                return false;

            if (start > result.end)
                return false;

            if (end < result.start)
                return false;

            return true;
        }

        public static bool IsBadCommand(
            List<Frame> frames,
            ContinuousResult detection)
        {
            int hits = 0;

            for (int frame_no = detection.startFrameNo;
                frame_no < detection.endFrameNo;
                frame_no++)
            {
                if (frame_no < detection.startFrameNo)
                    continue;

                if (frame_no > detection.endFrameNo)
                    continue;

                if (frames[frame_no].gid != detection.gid)
                    continue;

                if (frames[frame_no].attempt != -1)
                    continue;

                hits++;
            }

            return hits > 0;
        }

        public static bool IsBadCommand(
            List<Frame> frames,
            RecognitionResult result)
        {
            int hits = 0;

            for (int frame_no = result.start;
                frame_no < result.end;
                frame_no++)
            {
                if (frame_no < result.start)
                    continue;

                if (frame_no > result.end)
                    continue;

                if (frames[frame_no].gid != result.gid)
                    continue;

                if (frames[frame_no].attempt != -1)
                    continue;

                hits++;
            }

            return hits > 0;
        }

        public static void GetAllCommands(
            List<GestureCommand> commands,
            Dataset dataset,
            DeviceType deviceType,
            int pid
        )
        {
            int deviceToUse = (int) deviceType;
            // Both Vives should be mapped to device 2
            if (deviceType == DeviceType.VIVE_POSITION || deviceType == DeviceType.VIVE_QUATERNION)
                deviceToUse = 2;

            commands.Clear();

            for (int ii = 0; ii < Truth.truth.Count; ii++)
            {
                if (deviceToUse != Truth.truth[ii].device)
                {
                    continue;
                }

                if (pid != Truth.truth[ii].pid)
                {
                    continue;
                }

                GestureCommand cmd = new GestureCommand();
                cmd.gid = dataset.GestureNameToId(Truth.truth[ii].gname);
                cmd.start = Truth.truth[ii].start;
                cmd.end = Truth.truth[ii].end;
                cmd.detected = false;

                commands.Add(cmd);
            }
        }
    }
}