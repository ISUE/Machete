using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Machete
{
    public struct configuartion_parameters_t
    {
        /**
         * The input device sampling rate.
         */
        public int fps;

        /**
         * The maximum amount of data (in seconds) that is collected
         * and passed to the recognizer. Since the buffer is cleared
         * when a gesture is recognized, the buffer may be shorter.
         */
        public double sliding_window_s;

        /**
         * Sliding window converted into frames based on FPS.
         */
        public int sliding_window_frame_cnt;

        /**
         * The recognizer is called once per this many frames.
         */
        public int update_interval;

        /**
         * A gesture has to have the best Jackknife score this many
         * times before being officially recognized by this application.
         */
        public int repeat_cnt;

        /**
         *
         */
        public configuartion_parameters_t(DeviceType device)
        {
            sliding_window_s = -1;
            fps = -1;
            update_interval = -1;
            sliding_window_frame_cnt = -1;
            repeat_cnt = -1;

            if (device == DeviceType.KINECT)
            {
                fps = 30;
                sliding_window_s = 2.0;
                update_interval = 5;
                repeat_cnt = 3;
            }

            sliding_window_frame_cnt = (int)((double)fps * sliding_window_s);
        }
    };

    public struct Frame
    {
        /**
         * The expected gesture: the gesture that the
         * participant should execute.
         */
        public int gid;

        public Vector pt;

        public float timestamp_s;

        public int cmd_id;

        public int attempt;

        public string gname;

        public bool valid;

        public Frame(
            Vector _pt, 
            float _timestamp,
            string _gname,
            int _gid,
            int _attempt)
        {
            this.pt = _pt.Clone() as Vector;
            this.timestamp_s = _timestamp;
            this.gname = _gname;
            this.gid = _gid; 
            this.attempt = _attempt;

            // FIXME Should cmd id be what i set gid to now?
            this.cmd_id = -1; //
            this.valid = false;
        }
    };
}
 